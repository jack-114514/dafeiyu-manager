param([switch]$Live)
$ErrorActionPreference='Stop'
$base=Split-Path -Parent $MyInvocation.MyCommand.Path
$tokens=$null;$errors=$null
[Management.Automation.Language.Parser]::ParseFile((Join-Path $base 'control.ps1'),[ref]$tokens,[ref]$errors)|Out-Null
if($errors.Count){throw ($errors|Out-String)}
Write-Output 'PASS: backend PowerShell syntax.'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /r:System.Drawing.dll /r:System.Windows.Forms.dll "/out:$base\dist\ui-audit.exe" "$base\ui-audit.cs"
if($LASTEXITCODE){throw 'UI audit compilation failed'}
& "$base\dist\ui-audit.exe" "$base\dist\大肥鱼管家.exe"
if($LASTEXITCODE){throw 'UI font size, button fit or contrast audit failed'}
Write-Output 'PASS: 1.2x main font, button fit and 4.5:1 text contrast in light/dark, hover, pressed and disabled states.'
if($Live){
 $exe=Join-Path $base 'dist/大肥鱼管家.exe'
 $proc=Start-Process -FilePath $exe -ArgumentList '--probe Status' -WindowStyle Hidden -PassThru -Wait
 if($proc.ExitCode){throw 'Status probe failed'}
 $result=Get-Content (Join-Path $base 'logs/probe.json') -Raw -Encoding UTF8|ConvertFrom-Json
 if(-not $result.ok -or $result.message -ne '检查完成'){throw 'Status or UTF-8 result failed'}
 if(-not $result.harnessRunning -or -not $result.napcatRunning -or -not $result.online){throw 'Expected running online services'}
 Write-Output 'PASS: GUI backend returned valid UTF-8 JSON and actual online services.'
}
