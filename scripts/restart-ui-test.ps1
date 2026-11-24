$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $projectRoot 'artifacts\self-contained\Selio.exe'
$quit = Start-Process -FilePath $exe -ArgumentList '--quit' -PassThru -WindowStyle Hidden
$quit.WaitForExit()
# Only close our synthetic document, never an unrelated process/window.
Get-Process -Name Selio -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $exe -and $_.MainWindowTitle -eq 'Sélio — Document de test OCR' } |
    ForEach-Object { $_.CloseMainWindow() | Out-Null }
Start-Sleep -Milliseconds 300
& (Join-Path $PSScriptRoot 'dotnet.ps1') publish (Join-Path $projectRoot 'src\Selio\Selio.csproj') -c Release -r win-x64 --self-contained true --no-restore -o (Join-Path $projectRoot 'artifacts\self-contained')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Start-Process -FilePath $exe -ArgumentList '--fixture' -WindowStyle Hidden
Start-Process -FilePath $exe -ArgumentList '--test-session --ui-test --show' -WindowStyle Hidden
