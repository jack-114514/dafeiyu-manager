param([switch]$Render)
$ErrorActionPreference='Stop'
$base=Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$dist=Join-Path $base 'dist'
New-Item $dist -ItemType Directory -Force|Out-Null
& $compiler /nologo /target:exe /r:System.Drawing.dll "/out:$dist\IconBuilder.exe" "$base\IconBuilder.cs"
if($LASTEXITCODE){throw 'Icon builder compilation failed'}
& "$dist\IconBuilder.exe" "$dist\dafeiyu.ico"
if($LASTEXITCODE){throw 'Icon generation failed'}
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll "/win32icon:$dist\dafeiyu.ico" "/out:$dist\大肥鱼管家.exe" "$base\Manager.cs" "$base\VoiceSettings.cs" "$base\Theme.cs"
if($LASTEXITCODE){throw 'Manager compilation failed'}
if($Render){& "$dist\大肥鱼管家.exe" --render "$dist\preview.png"}
Write-Output "Built $dist\大肥鱼管家.exe"
