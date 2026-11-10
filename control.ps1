param([ValidateSet('Status','Start','BootStart','EnableAutoStart','DisableAutoStart','EnableAutoRepair','DisableAutoRepair','RepairWatch','Pause','Resume','Stop','OpenWeb','BiliLogin','LifePause','LifeResume','LifeRun','RepairQQ')][string]$Action='Status')
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
try{[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)}catch{}
$Manager=Split-Path -Parent $MyInvocation.MyCommand.Path
$Utf8=New-Object Text.UTF8Encoding($false)
$AutoStartTasks=@('DeepSeek-Dafeiyu-Boot','DeepSeek-Dafeiyu-LogonWindow')
$RepairTask='DeepSeek-Dafeiyu-AutoRepair'
$RepairArmFile=Join-Path $Manager 'logs\auto-repair-armed.txt'
. (Join-Path $Manager 'instance.ps1')
. (Join-Path $Manager 'repair-watch.ps1')
function Set-AutoStart([bool]$enabled) {
 $tasks=@(foreach($name in $AutoStartTasks){Get-ScheduledTask -TaskName $name -TaskPath '\'})
 $changed=@()
 try{
  foreach($task in $tasks){
   if($task.Settings.Enabled -eq $enabled){continue}
   if($enabled){$task | Enable-ScheduledTask | Out-Null}
   else{$task | Disable-ScheduledTask | Out-Null}
   $changed+=$task.TaskName
  }
 }catch{
  foreach($name in $changed){
   if($enabled){Disable-ScheduledTask -TaskName $name -TaskPath '\' -ErrorAction SilentlyContinue | Out-Null}
   else{Enable-ScheduledTask -TaskName $name -TaskPath '\' -ErrorAction SilentlyContinue | Out-Null}
  }
  throw
 }
}
function Services {
 $all=@(Get-CimInstance Win32_Process)
 return @{harness=@($all|Where-Object ExecutablePath -eq $Harness);napcat=@($all|Where-Object ExecutablePath -eq "$Runtime\napcat\node.exe")}
}
function Api($route,$body=@{}) {
 if(-not $script:Credential){
  $secret=Get-Content "$Runtime\connection-private.json" -Raw|ConvertFrom-Json
  $bytes=[Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($secret.webToken+'.napcat'))
  $hash=([BitConverter]::ToString($bytes)).Replace('-','').ToLowerInvariant()
  $auth=Invoke-RestMethod 'http://127.0.0.1:6100/api/auth/login' -Method Post -ContentType 'application/json' -Body (@{hash=$hash}|ConvertTo-Json) -TimeoutSec 3
  if($auth.code -ne 0){throw '管理页面令牌验证失败'}
  $script:Credential=$auth.data.Credential
 }
 return Invoke-RestMethod ('http://127.0.0.1:6100/api/'+$route) -Method Post -ContentType 'application/json' -Body ($body|ConvertTo-Json -Compress) -Headers @{Authorization='Bearer '+$script:Credential} -TimeoutSec 4
}
function Set-Paused([bool]$paused) {
 $old=[IO.File]::ReadAllText($Patch)
 $pattern='(?ms)^- id: dsh-onebot\r?\n.*?(?=^- |\z)'
 $blocks=[regex]::Matches($old,$pattern)
 if($blocks.Count -ne 1){throw 'QQ 插件配置不唯一，已停止修改'}
 $block=$blocks[0].Value
 if([regex]::Matches($block,'(?m)^    dmPolicy:.*$').Count -ne 1){throw '未找到私聊开关'}
 if([regex]::Matches($block,'(?m)^    groupPolicy: disabled\s*$').Count -ne 1){throw '群聊配置已改变，请先核对'}
 $policy=if($paused){'disabled'}else{'allowlist'}
 $next=[regex]::Replace($block,'(?m)^    dmPolicy:.*$',('    dmPolicy: '+$policy))
 if($next -ne $block){
  $backup=Join-Path $Manager 'private\config-backups'
  New-Item $backup -ItemType Directory -Force|Out-Null
  Copy-Item $Patch (Join-Path $backup ('cordis-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff')+'.yml'))
  [IO.File]::WriteAllText($Patch,$old.Replace($block,$next),$Utf8)
 }
}
function Snapshot {
 $s=Services
 $paused=([IO.File]::ReadAllText($Patch) -match '(?ms)^- id: dsh-onebot\r?\n(?:(?!^- ).)*?^    dmPolicy: disabled\s*$')
 $online=$false;$qq=$BotQQ;$actualQQ='';$accountMismatch=$false;$detail='QQ 后台未运行'
 if($s.napcat.Count -gt 0){
  try{
   $state=Api 'QQLogin/CheckLoginStatus'
   $online=[bool]$state.data.isLogin
   if($online){$info=Api 'QQLogin/GetQQLoginInfo';$actualQQ=[string]$info.data.uin;$accountMismatch=$actualQQ -ne $qq;if($accountMismatch){$online=$false}}
   if($online){$detail='QQ 已登录'}elseif($state.data.isOffline){$detail='QQ 已离线，请打开登录页'}else{$detail='等待 QQ 登录'}
   if($accountMismatch){$detail='登录了其他 QQ，请使用配置中的机器人账号'}
  }catch{$detail='QQ 后台存在，管理接口暂未就绪'}
 }
 $bridge=@(Get-NetTCPConnection -State Established -LocalPort 8643 -ErrorAction SilentlyContinue).Count -gt 0
 $life=$null
 if(Test-Path "$Runtime\life\status.json"){try{$life=Get-Content "$Runtime\life\status.json" -Raw -Encoding UTF8|ConvertFrom-Json}catch{}}
 $lifeEnabled=$false
 if(Test-Path "$Runtime\life\config.json"){$lifeEnabled=(Get-Content "$Runtime\life\config.json" -Raw|ConvertFrom-Json).enabled -eq $true}
 $health=$null
 if(Test-Path "$Runtime\health\status.json"){try{$health=Get-Content "$Runtime\health\status.json" -Raw -Encoding UTF8|ConvertFrom-Json}catch{}}
 $fresh=$health -and ([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()-[double]$health.updatedAt -lt 15000)
 $replyLevel=if($fresh){$health.level}else{'error'}
 $replySummary=if($fresh){$health.summary}else{'Harness 回复后台心跳缺失或过期，请重新检查'}
 if(-not $online){$replyLevel='unverified';$replySummary='QQ 尚未登录，请打开登录网页；保存的授权失效时需要重新扫码'}
 if($accountMismatch){$replyLevel='error';$replySummary=$detail}
 $qqSessionState=if($accountMismatch){'wrong_account'}elseif($fresh -and $health.sessionProbe.kind -eq 'needs_login'){'needs_login'}elseif($fresh -and $health.sessionProbe.kind -eq 'failed' -and $health.sessionProbe.failures -ge 2){'failed'}elseif($fresh -and $health.sessionProbe.kind -eq 'ok'){'ok'}else{'unknown'}
 $tasks=@(foreach($name in $AutoStartTasks){Get-ScheduledTask -TaskName $name -TaskPath '\' -ErrorAction SilentlyContinue})
 $repair=Get-ScheduledTask -TaskName $RepairTask -TaskPath '\' -ErrorAction SilentlyContinue
 return @{autoRepairAvailable=$null -ne $repair;autoRepairEnabled=[bool]$repair.Settings.Enabled;autoStartAvailable=$tasks.Count -eq $AutoStartTasks.Count;autoStartEnabled=@($tasks|Where-Object {$_.Settings.Enabled}).Count -eq $AutoStartTasks.Count;harnessRunning=$s.harness.Count -gt 0;napcatRunning=$s.napcat.Count -gt 0;online=$online;paused=$paused;bridgeConnected=$bridge;qq=$qq;detail=$detail;lifeEnabled=$lifeEnabled;biliLoggedIn=[bool]$life.loggedIn;lifeActive=[bool]$life.active;diaryCount=[int]$life.diaryCount;lifeInstalled=[bool]$life.installed;replyLevel=$replyLevel;replySummary=$replySummary;qqSessionState=$qqSessionState;lastInbound=[double]$health.lastInbound;lastOutbound=[double]$health.lastOutbound}
}
$serviceLock=$null
$lockTaken=$false
try {
 if($Action -in @('Start','BootStart','Stop','RepairQQ','RepairWatch','EnableAutoRepair','DisableAutoRepair')){
  $serviceLock=New-Object Threading.Mutex($false,'Global\DeepSeekDafeiyuServiceControl')
  $lockWait=if($Action -eq 'RepairWatch'){0}else{45000}
  try{$lockTaken=$serviceLock.WaitOne($lockWait)}catch [Threading.AbandonedMutexException]{$lockTaken=$true}
  if(-not $lockTaken){if($Action -eq 'RepairWatch'){return};throw 'Service operation already in progress'}
 }
 $instance=Read-DafeiyuInstance
 $Runtime=$instance.runtimeDir
 $Harness=$instance.harnessExe
 $Patch=Join-Path $instance.harnessProfileDir 'cordis.patch.yml'
 $BotQQ=$instance.botQQ
 $message='检查完成'
 switch($Action){
  'RepairWatch' {Invoke-AutoRepair;return}
  'EnableAutoRepair' {
   $running=Services
   if($running.harness.Count -or $running.napcat.Count){Set-RepairArmed}
   Enable-ScheduledTask -TaskName $RepairTask -TaskPath '\' | Out-Null
   $message='自动修复已开启'
  }
  'DisableAutoRepair' {Disable-ScheduledTask -TaskName $RepairTask -TaskPath '\' | Out-Null;$message='自动修复已关闭'}
  'RepairQQ' {
   Stop-ScheduledTask -TaskName 'DeepSeek-Dafeiyu-QQ' -ErrorAction SilentlyContinue
   foreach($proc in (Services).napcat){Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue}
   Start-Sleep -Seconds 1
   Start-ScheduledTask -TaskName 'DeepSeek-Dafeiyu-QQ'
   $script:Credential=$null;$ready=$false
   $waiting=[Diagnostics.Stopwatch]::StartNew();for($i=0;$i -lt 20 -and $waiting.Elapsed.TotalSeconds -lt 20;$i++){try{$null=Api 'QQLogin/CheckLoginStatus';$ready=$true;break}catch{Start-Sleep -Milliseconds 700}}
   if($ready){try{$login=Api 'QQLogin/SetQuickLogin' @{uin=$BotQQ};Start-Sleep -Seconds 2}catch{}}
   Start-Process 'http://127.0.0.1:6100/webui'|Out-Null
   $message='已重连 QQ 后台；若保存的授权失效，请在打开的网页重新扫码。收发需以新消息实测。'
  }
  {$_ -in @('Start','BootStart')} {
   $boot=$Action -eq 'BootStart'
   Set-RepairArmed
   $s=Services
   if($s.harness.Count -eq 0){
    if($boot){Start-Process -FilePath $Harness -WindowStyle Hidden | Out-Null}
    else{Start-Process -FilePath $Harness | Out-Null}
   }elseif(-not $boot -and (Get-Process -Id $PID).SessionId -gt 0 -and @($s.harness | Where-Object SessionId -ne 0).Count -eq 0){
    Start-ScheduledTask -TaskName 'DeepSeek-Dafeiyu-ReloadHarness'
   }
   if(-not $boot){Set-Paused $false}
   if($s.napcat.Count -eq 0){
    if($boot){
     Start-BackgroundNapCat
    }else{Start-ScheduledTask -TaskName 'DeepSeek-Dafeiyu-QQ'}
   }
   $ready=$false
   $waiting=[Diagnostics.Stopwatch]::StartNew();for($i=0;$i -lt 15 -and $waiting.Elapsed.TotalSeconds -lt 20;$i++){
    try{$null=Api 'QQLogin/CheckLoginStatus';$ready=$true;break}catch{Start-Sleep -Milliseconds 700}
   }
   if($ready){
    $current=Api 'QQLogin/CheckLoginStatus'
    if(-not $current.data.isLogin){
     try{
      $cache=Api 'QQLogin/GetQuickLoginList'
      for($j=0;$j -lt 6 -and @($cache.data) -notcontains $BotQQ;$j++){
       Start-Sleep -Milliseconds 800
       $cache=Api 'QQLogin/GetQuickLoginList'
      }
      if(@($cache.data) -contains $BotQQ){
       $login=Api 'QQLogin/SetQuickLogin' @{uin=$BotQQ}
       if($login.code -eq 0){Start-Sleep -Seconds 2}
      }
     }catch{ } # Expired saved authorization falls back to QR login.
    }
    if($boot){$message='开机启动已执行'}
    else{Start-Process 'http://127.0.0.1:6100/webui'|Out-Null;$message='已启动；复用现有进程，并打开 QQ 登录页'}
   }
   else{$message='已提交启动，QQ 页面暂未就绪；稍后点击“QQ 登录网页”'}
  }
  'EnableAutoStart' {Set-AutoStart $true;$message='开机自启已开启，后台随系统启动，管家在登录 Windows 后打开'}
  'DisableAutoStart' {Set-AutoStart $false;$message='开机自启已关闭，当前服务继续运行'}
  'Pause' {Set-Paused $true;$message='已暂停自动回复，QQ 保持登录'}
  'Resume' {Set-Paused $false;$message='已恢复自动回复'}
  'Stop' {
   if(Test-Path -LiteralPath $RepairArmFile){Remove-Item -LiteralPath $RepairArmFile}
   Set-Paused $true
   Stop-ScheduledTask -TaskName 'DeepSeek-Dafeiyu-QQ' -ErrorAction SilentlyContinue
   $s=Services
   $master=@($s.napcat|Where-Object {$_.ParentProcessId -notin @($s.napcat.ProcessId)})
   foreach($proc in $master){Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue}
   foreach($proc in (Services).napcat){Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue}
   foreach($proc in $s.harness){
    $p=Get-Process -Id $proc.ProcessId -ErrorAction SilentlyContinue
    if($p -and $p.MainWindowHandle -ne [IntPtr]::Zero){[void]$p.CloseMainWindow()}
   }
   for($i=0;$i -lt 10;$i++){if((Services).harness.Count -eq 0){break};Start-Sleep -Milliseconds 300}
   foreach($proc in (Services).harness){Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue}
   for($i=0;$i -lt 8;$i++){
    $remaining=Services
    if($remaining.harness.Count -eq 0 -and $remaining.napcat.Count -eq 0){break}
    Start-Sleep -Milliseconds 350
   }
   $remaining=Services
   if($remaining.harness.Count -gt 0 -or $remaining.napcat.Count -gt 0){throw 'Managed services did not exit'}
   $message='已关闭大肥鱼 QQ 后台与 DeepSeek Harness；管家继续保持打开'
  }
  'OpenWeb' {Start-Process 'http://127.0.0.1:6100/webui'|Out-Null;$message='已打开 QQ 登录网页'}
  'BiliLogin' {
   if((Services).harness.Count -eq 0){Start-Process -FilePath $Harness|Out-Null}
   $ready=$false
   for($i=0;$i -lt 20;$i++){if(Get-NetTCPConnection -State Listen -LocalPort 6101 -ErrorAction SilentlyContinue){$ready=$true;break};Start-Sleep -Milliseconds 600}
   if(-not $ready){throw 'Bilibili login page is not ready'}
   $access=Get-Content "$Manager\private\life-access.json" -Raw|ConvertFrom-Json
   Start-Process ('http://127.0.0.1:6101/?token='+$access.token)|Out-Null
   $message='已打开 B 站小号扫码登录与日记页面'
  }
  'LifePause' {
   $cfg=Get-Content "$Runtime\life\config.json" -Raw|ConvertFrom-Json;$cfg.enabled=$false
   [IO.File]::WriteAllText("$Runtime\life\config.json",($cfg|ConvertTo-Json),$Utf8)
   $message='已暂停自主活动，QQ 私聊回复不受影响'
  }
  'LifeResume' {
   $cfg=Get-Content "$Runtime\life\config.json" -Raw|ConvertFrom-Json;$cfg.enabled=$true
   [IO.File]::WriteAllText("$Runtime\life\config.json",($cfg|ConvertTo-Json),$Utf8)
   $message='已开启混合自主生活，主动消息每天最多五条'
  }
  'LifeRun' {
   $access=Get-Content "$Manager\private\life-access.json" -Raw|ConvertFrom-Json
   $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
   $session.Cookies.Add((New-Object System.Net.Cookie('fish_life',$access.token,'/','127.0.0.1')))
   $result=Invoke-RestMethod 'http://127.0.0.1:6101/api/run' -Method Post -ContentType 'application/json' -Body '{}' -WebSession $session -TimeoutSec 12
   $message=if($result.accepted){'已交给大肥鱼活动一次，完成后可查看日记'}else{'未开始活动：'+$result.reason}
  }
 }
 $result=Snapshot
 if($Action -eq 'BootStart'){
  $waiting=[Diagnostics.Stopwatch]::StartNew()
  while($waiting.Elapsed.TotalSeconds -lt 40 -and -not ($result.harnessRunning -and $result.bridgeConnected -and $result.qqSessionState -eq 'ok')){
   Start-Sleep -Seconds 2
   $result=Snapshot
  }
  $result.ok=$result.harnessRunning -and $result.napcatRunning -and $result.online -and $result.bridgeConnected -and $result.qqSessionState -eq 'ok'
  $result.message=$message
  $result.checkedAt=[DateTime]::UtcNow.ToString('o')
  $logs=Join-Path $Manager 'logs'
  New-Item -ItemType Directory -Path $logs -Force | Out-Null
  [IO.File]::WriteAllText((Join-Path $logs 'boot-status.json'),($result|ConvertTo-Json -Compress),$Utf8)
  if(-not $result.ok){exit 1}
 }
 $result.ok=$true;$result.message=$message
 $result|ConvertTo-Json -Compress
}catch{
 if(-not (Test-Path -LiteralPath (Join-Path $Manager 'private\instance.json'))){
  @{ok=$false;message='请先按 README 创建 private/instance.json'}|ConvertTo-Json -Compress
  exit 1
 }
 # Avoid including credentials or raw API payloads in UI/log output.
 @{ok=$false;message=('操作未完成：'+$_.Exception.GetType().Name+'。请检查后台服务或项目配置。')}|ConvertTo-Json -Compress
 exit 1
}finally{
 if($lockTaken){$serviceLock.ReleaseMutex()}
 if($serviceLock){$serviceLock.Dispose()}
}
