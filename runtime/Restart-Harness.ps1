$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'instance.ps1')
$instance = Read-DafeiyuInstance
$executable = $instance.harnessExe
$services = @(Get-CimInstance Win32_Process | Where-Object ExecutablePath -eq $executable)
foreach ($service in $services) {
    Stop-Process -Id $service.ProcessId -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 2
Start-Process -FilePath $executable | Out-Null
