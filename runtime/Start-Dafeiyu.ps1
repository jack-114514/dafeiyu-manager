$ErrorActionPreference='Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'instance.ps1')
$instance=Read-DafeiyuInstance
$root=$instance.runtimeDir
$env:NAPCAT_QQ_DATA_DIR="$root\qq-data"
$env:NAPCAT_WORKDIR="$root\napcat\napcat"
$env:PATH="$root\napcat;$root\napcat\win64;"+$env:PATH
$running=Get-CimInstance Win32_Process | Where-Object {$_.Name -eq 'node.exe' -and $_.ExecutablePath -eq "$root\napcat\node.exe"}
if($running){exit}
$p=Start-Process -FilePath "$root\napcat\node.exe" -ArgumentList ('index.js -q '+$instance.botQQ) -WorkingDirectory "$root\napcat" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$root\logs\napcat-out.log" -RedirectStandardError "$root\logs\napcat-err.log"
$p.WaitForExit()
exit $p.ExitCode
