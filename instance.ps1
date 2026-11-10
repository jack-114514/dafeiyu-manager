function Read-DafeiyuInstance {
    $file = Join-Path $PSScriptRoot 'private\instance.json'
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw 'Missing private/instance.json. Copy instance.example.json and fill in the local settings.'
    }
    $config = Get-Content -LiteralPath $file -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($name in @('runtimeDir','harnessExe','harnessProfileDir','fishHome','workspaceDir')) {
        if ($config.$name -isnot [string] -or $config.$name -notmatch '^[A-Za-z]:[\\/]') {
            throw "$name must be an absolute Windows path in private/instance.json"
        }
        $config.$name = [IO.Path]::GetFullPath($config.$name)
    }
    foreach ($name in @('botQQ','ownerQQ')) {
        if ($config.$name -isnot [string] -or $config.$name -notmatch '^\d{5,12}$') {
            throw "$name must be a QQ number string in private/instance.json"
        }
    }
    return $config
}
