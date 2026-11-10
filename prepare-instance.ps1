$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'instance.ps1')
$instance = Read-DafeiyuInstance
$runtime = $instance.runtimeDir
$private = Join-Path $PSScriptRoot 'private'
$utf8 = [Text.UTF8Encoding]::new($false)

function Write-NewJson($file, $value) {
    if (-not (Test-Path -LiteralPath $file)) {
        [IO.File]::WriteAllText($file, ($value | ConvertTo-Json -Depth 8), $utf8)
    }
}

foreach ($dir in @($private, "$runtime\logs", "$runtime\qq-data", "$runtime\life\diary", "$runtime\bili-home\.bili-ai", $instance.workspaceDir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

$secretFile = Join-Path $runtime 'connection-private.json'
if (-not (Test-Path -LiteralPath $secretFile)) {
    $secret = Read-Host 'NapCat WebUI token' -AsSecureString
    $webToken = [Net.NetworkCredential]::new('', $secret).Password
    if ([string]::IsNullOrWhiteSpace($webToken)) { throw 'NapCat WebUI token is required' }
    Write-NewJson $secretFile @{webToken = $webToken}
}

$bytes = New-Object byte[] 32
$random = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $random.GetBytes($bytes) } finally { $random.Dispose() }
Write-NewJson (Join-Path $private 'life-access.json') @{token = [BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()}
Write-NewJson "$runtime\life\config.json" @{enabled = $false; readOnly = $true; fishBridge = $false; allowProactiveQQ = $false}
Write-NewJson "$runtime\life\bili-whale.config.json" @{
    readOnly = $true
    share = @{enabled = $false}
    listen = @{comment = $false; dm = $false; live = $false}
    notify = @{type = 'none'}
    diary = @{enabled = $true; push = $false}
}

$user = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$powerShell = Join-Path $PSHOME 'powershell.exe'
foreach ($entry in @(@{name='DeepSeek-Dafeiyu-QQ'; script='Start-Dafeiyu.ps1'}, @{name='DeepSeek-Dafeiyu-ReloadHarness'; script='Restart-Harness.ps1'})) {
    $runner = Join-Path $PSScriptRoot ('runtime\' + $entry.script)
    $arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $runner + '"'
    $action = New-ScheduledTaskAction -Execute $powerShell -Argument $arguments -WorkingDirectory $PSScriptRoot
    $settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit ([TimeSpan]::Zero)
    Register-ScheduledTask -TaskName $entry.name -Action $action -Principal $principal -Settings $settings -Force | Out-Null
}
Write-Output 'Local directories, credentials and interactive tasks are ready.'
