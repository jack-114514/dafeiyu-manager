function Get-RepairBootId {
 (Get-CimInstance Win32_OperatingSystem).LastBootUpTime.ToUniversalTime().Ticks.ToString()
}

function Set-RepairArmed {
 New-Item -ItemType Directory -Path (Split-Path -Parent $RepairArmFile) -Force | Out-Null
 [IO.File]::WriteAllText($RepairArmFile,(Get-RepairBootId),$Utf8)
}

function Test-RepairStopped($bootId) {
 (Test-Path -LiteralPath $RepairArmFile) -and [IO.File]::ReadAllText($RepairArmFile) -eq ('stopped:'+$bootId)
}

function Test-BootReady($snapshot) {
 if(-not ($snapshot.harnessRunning -and $snapshot.healthFresh -and $snapshot.napcatRunning -and $snapshot.online -and $snapshot.bridgeConnected)){return $false}
 try{
  $probe=Api 'Debug/call' @{action='get_stranger_info';params=@{user_id=$OwnerQQ;no_cache=$true}}
  if($probe.code -eq 0 -and $probe.data.status -eq 'ok' -and $probe.data.retcode -eq 0 -and [string]$probe.data.data.user_id -eq $OwnerQQ){
   $snapshot.qqSessionState='ok'
   return $true
  }
 }catch{}
 return $false
}

function Invoke-SavedQQLogin($cache) {
 if(@($cache.data) -notcontains $BotQQ){return}
 $attemptFile=Join-Path $Manager 'logs\auto-login-boot.txt'
 $bootId=Get-RepairBootId
 if((Test-Path -LiteralPath $attemptFile) -and [IO.File]::ReadAllText($attemptFile) -eq $bootId){return}
 [IO.File]::WriteAllText($attemptFile,$bootId,$Utf8)
 Api 'QQLogin/SetQuickLogin' @{uin=$BotQQ}
}

function Start-BackgroundNapCat {
 $runner=Join-Path $Manager 'runtime\Start-Dafeiyu.ps1'
 Start-Process -FilePath 'powershell.exe' -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File "'+$runner+'"') -WindowStyle Hidden | Out-Null
}

function Get-RepairIssue($snapshot) {
 if(-not $snapshot.harnessRunning -or -not $snapshot.napcatRunning){return 'process_missing'}
 if(-not $snapshot.healthFresh -or -not $snapshot.harnessListening){return 'harness_unresponsive'}
 if($snapshot.qqSessionState -eq 'wrong_account'){return 'wrong_account'}
 if(-not $snapshot.online -and $snapshot.detail -in @('QQ 已离线，请打开登录页','等待 QQ 登录')){return 'login_required'}
 if($snapshot.detail -eq 'QQ 后台存在，管理接口暂未就绪'){return 'qq_api_down'}
 if(-not $snapshot.bridgeConnected){return 'qq_bridge_down'}
 if($snapshot.online -and $snapshot.qqSessionState -in @('failed','needs_login')){return 'qq_session_failed'}
 return 'healthy_or_waiting'
}

function Invoke-AutoRepair {
 $task=Get-ScheduledTask -TaskName $RepairTask -TaskPath '\'
 if(-not $task.Settings.Enabled){return}
 $bootId=Get-RepairBootId
 $armed=if(Test-Path -LiteralPath $RepairArmFile){[IO.File]::ReadAllText($RepairArmFile)}else{''}
 if($armed -eq ('stopped:'+$bootId)){return}
 if($armed -ne $bootId){
  if(-not (Get-ScheduledTask -TaskName 'DeepSeek-Dafeiyu-Boot').Settings.Enabled){return}
  Set-RepairArmed
 }

 $statePath=Join-Path $Manager 'logs\auto-repair.json'
 $previous=$null
 if(Test-Path -LiteralPath $statePath){try{$previous=Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json}catch{}}
 if($previous.bootId -ne $bootId){$previous=$null}
 $snapshot=Snapshot
 if($snapshot.online -and $snapshot.qqSessionState -eq 'ok'){
  $attemptFile=Join-Path $Manager 'logs\auto-login-boot.txt'
  if(Test-Path -LiteralPath $attemptFile){Remove-Item -LiteralPath $attemptFile}
 }
 $issue=Get-RepairIssue $snapshot
 $now=[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
 if($previous.checkedAt -and ($now-[long]$previous.checkedAt) -lt 50000){return}
 $count=if($previous.issue -eq $issue){[int]$previous.count+1}else{1}
 $state=@{
  bootId=$bootId;issue=$issue;count=$count;checkedAt=$now
  lastAttemptAt=[long]$previous.lastAttemptAt;lastAction=$previous.lastAction
 }
 $canAct=$count -ge 2 -and ($now-$state.lastAttemptAt) -ge 300000 -and $snapshot.replyLevel -ne 'working'
 if($canAct){
  switch($issue){
   'process_missing' {$state.lastAction='start_missing_processes'}
   'harness_unresponsive' {$state.lastAction='restart_harness'}
   {$_ -in @('qq_api_down','qq_bridge_down','qq_session_failed')} {$state.lastAction='restart_napcat'}
   'login_required' {$state.lastAction='try_saved_login'}
   default {$canAct=$false}
  }
 }
 if($canAct){$state.lastAttemptAt=$now}
 [IO.File]::WriteAllText($statePath,($state|ConvertTo-Json -Compress),$Utf8)
 if(-not $canAct){return}
 switch($state.lastAction){
  'restart_harness' {
   foreach($process in (Services).harness){Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue}
   Start-Sleep -Seconds 2
   Start-Process -FilePath $Harness -WindowStyle Hidden | Out-Null
  }
  'start_missing_processes' {
   if(-not $snapshot.harnessRunning){Start-Process -FilePath $Harness -WindowStyle Hidden | Out-Null}
   if(-not $snapshot.napcatRunning){Start-BackgroundNapCat}
  }
  'restart_napcat' {
   Stop-ScheduledTask -TaskName 'DeepSeek-Dafeiyu-QQ' -ErrorAction SilentlyContinue
   foreach($process in (Services).napcat){Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue}
   Start-Sleep -Seconds 1
   Start-BackgroundNapCat
  }
  'try_saved_login' {
   $cache=Api 'QQLogin/GetQuickLoginList'
   $null=Invoke-SavedQQLogin $cache
  }
 }
}
