param([Parameter(Mandatory=$true)][pscredential]$Credential)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$user=$Credential.UserName
$powerShell=Join-Path $PSHOME 'powershell.exe'

$bootAction=New-ScheduledTaskAction -Execute $powerShell -WorkingDirectory $root `
 -Argument ('-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+(Join-Path $root 'control.ps1')+'" -Action BootStart')
$bootTrigger=New-ScheduledTaskTrigger -AtStartup
$bootTrigger.Delay='PT20S'
$bootSettings=New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 3)
Register-ScheduledTask -TaskPath '\' -TaskName 'DeepSeek-Dafeiyu-Boot' -Action $bootAction `
 -Trigger $bootTrigger -Settings $bootSettings -User $user -Password $Credential.GetNetworkCredential().Password `
 -Description 'Start Dafeiyu Harness and QQ after Windows boots' -RunLevel Limited -Force | Out-Null

$windowAction=New-ScheduledTaskAction -Execute (Join-Path $root 'dist\大肥鱼管家.exe') -WorkingDirectory $root
$windowTrigger=New-ScheduledTaskTrigger -AtLogOn -User $user
$windowPrincipal=New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$windowSettings=New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit ([TimeSpan]::Zero)
Register-ScheduledTask -TaskPath '\' -TaskName 'DeepSeek-Dafeiyu-LogonWindow' -Action $windowAction `
 -Trigger $windowTrigger -Principal $windowPrincipal -Settings $windowSettings `
 -Description 'Open the Dafeiyu manager when its Windows user logs on' -Force | Out-Null

$repairAction=New-ScheduledTaskAction -Execute $powerShell -WorkingDirectory $root `
 -Argument ('-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+(Join-Path $root 'control.ps1')+'" -Action RepairWatch')
$repairTrigger=New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(2) -RepetitionInterval (New-TimeSpan -Minutes 2)
$repairSettings=New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskPath '\' -TaskName 'DeepSeek-Dafeiyu-AutoRepair' -Action $repairAction `
 -Trigger $repairTrigger -Settings $repairSettings -User $user -Password $Credential.GetNetworkCredential().Password `
 -Description 'Recover Dafeiyu processes and the QQ connection after confirmed failures' -RunLevel Limited -Force | Out-Null
