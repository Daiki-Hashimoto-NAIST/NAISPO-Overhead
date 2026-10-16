$ErrorActionPreference = 'Stop'
try {
    $buildFolder = Join-Path $PSScriptRoot 'Builds\Windows'
    $runtimeFolder = Join-Path $env:USERPROFILE 'NaispoOverheadRuntime'
    $exe = Join-Path $runtimeFolder 'NAISPO-Overhead.exe'
    if (-not (Test-Path -LiteralPath (Join-Path $buildFolder 'NAISPO-Overhead.exe'))) {
        throw 'Build not found. In Unity, select Overhead / Build Windows.'
    }
    if ($runtimeFolder -match '[^\x00-\x7F]') { throw 'Please set runtimeFolder to an ASCII-only path.' }
    if (Get-Process -Name NAISPO-Overhead -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }) {
        throw 'NAISPO Overhead is already running. Close it before launching again.'
    }
    New-Item -ItemType Directory -Force -Path $runtimeFolder | Out-Null
    & robocopy.exe $buildFolder $runtimeFolder /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -ge 8) { throw "Runtime copy failed: $LASTEXITCODE" }
    Start-Process -FilePath $exe -WorkingDirectory $runtimeFolder -ArgumentList ('-screen-fullscreen 0 -screen-width 1440 -screen-height 900 -logFile "' + (Join-Path $runtimeFolder 'Player.log') + '"')
    exit 0
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
