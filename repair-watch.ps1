function Get-RepairBootId {
 (Get-CimInstance Win32_OperatingSystem).LastBootUpTime.ToUniversalTime().Ticks.ToString()
}

function Set-RepairArmed {
 New-Item -ItemType Directory -Path (Split-Path -Parent $RepairArmFile) -Force | Out-Null
 [IO.File]::WriteAllText($RepairArmFile,(Get-RepairBootId),$Utf8)
}

function Start-BackgroundNapCat {
 $runner=Join-Path $Manager 'runtime\Start-Dafeiyu.ps1'
 Start-Process -FilePath 'powershell.exe' -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File "'+$runner+'"') -WindowStyle Hidden | Out-Null
}

function Get-RepairIssue($snapshot) {
 if(-not $snapshot.harnessRunning -or -not $snapshot.napcatRunning){return 'process_missing'}
 if($snapshot.qqSessionState -eq 'wrong_account'){return 'wrong_account'}
 if(-not $snapshot.online -and $snapshot.detail -in @('QQ 已离线，请打开登录页','等待 QQ 登录')){return 'login_required'}
 if($snapshot.detail -eq 'QQ 后台存在，管理接口暂未就绪'){return 'qq_api_down'}
 if(-not $snapshot.bridgeConnected -and (Get-NetTCPConnection -State Listen -LocalPort 8643 -ErrorAction SilentlyContinue)){return 'qq_bridge_down'}
 if($snapshot.online -and $snapshot.qqSessionState -in @('failed','needs_login')){return 'qq_session_failed'}
 return 'healthy_or_waiting'
}

function Invoke-AutoRepair {
 $task=Get-ScheduledTask -TaskName $RepairTask -TaskPath '\'
 if(-not $task.Settings.Enabled -or -not (Test-Path -LiteralPath $RepairArmFile)){return}
 $bootId=Get-RepairBootId
 if([IO.File]::ReadAllText($RepairArmFile) -ne $bootId){return}

 $statePath=Join-Path $Manager 'logs\auto-repair.json'
 $previous=$null
 if(Test-Path -LiteralPath $statePath){try{$previous=Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json}catch{}}
 if($previous.bootId -ne $bootId){$previous=$null}
 $snapshot=Snapshot
 $issue=Get-RepairIssue $snapshot
 $count=if($previous.issue -eq $issue){[int]$previous.count+1}else{1}
 $now=[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
 $state=@{
  bootId=$bootId;issue=$issue;count=$count;checkedAt=$now
  lastAttemptAt=[long]$previous.lastAttemptAt;lastAction=$previous.lastAction
  loginAttempted=[bool]$previous.loginAttempted
 }
 if($snapshot.online){$state.loginAttempted=$false}
 $canAct=$count -ge 2 -and ($now-$state.lastAttemptAt) -ge 300000 -and $snapshot.replyLevel -ne 'working'
 if($canAct){
  switch($issue){
   'process_missing' {$state.lastAction='start_missing_processes'}
   {$_ -in @('qq_api_down','qq_bridge_down','qq_session_failed')} {$state.lastAction='restart_napcat'}
   'login_required' {if(-not $state.loginAttempted){$state.lastAction='try_saved_login';$state.loginAttempted=$true}else{$canAct=$false}}
   default {$canAct=$false}
  }
 }
 if($canAct){$state.lastAttemptAt=$now}
 [IO.File]::WriteAllText($statePath,($state|ConvertTo-Json -Compress),$Utf8)
 if(-not $canAct){return}
 switch($state.lastAction){
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
   if(@($cache.data) -contains $BotQQ){$null=Api 'QQLogin/SetQuickLogin' @{uin=$BotQQ}}
  }
 }
}
