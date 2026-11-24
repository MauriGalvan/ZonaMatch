@echo off
setlocal
set "AMBA_BOUNDS_FILE=%~dp0photon\amba-bounds.json"

powershell.exe -NoProfile -NonInteractive -Command ^
    "$ErrorActionPreference = 'Stop';" ^
    "$bounds = Get-Content -Raw -LiteralPath $env:AMBA_BOUNDS_FILE | ConvertFrom-Json;" ^
    "$coords = @($bounds.MinLongitude, $bounds.MinLatitude, $bounds.MaxLongitude, $bounds.MaxLatitude);" ^
    "if ($coords -contains $null) { throw 'The shared AMBA bounds are incomplete.' };" ^
    "$west, $south, $east, $north = [double[]]$coords;" ^
    "if (-not ($west -ge -180 -and $west -lt $east -and $east -le 180 -and $south -ge -90 -and $south -lt $north -and $north -le 90)) { throw 'The shared AMBA bounds are invalid.' };" ^
    "[string]::Format([cultureinfo]::InvariantCulture, '{0},{1},{2},{3}', [object[]]@($west, $south, $east, $north))"

exit /b %errorlevel%
