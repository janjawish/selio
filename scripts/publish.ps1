param([switch]$FrameworkOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $projectRoot 'src\Selio\Selio.csproj'
$runner = Join-Path $PSScriptRoot 'dotnet.ps1'
& $runner publish $project -c Release -r win-x64 --self-contained false -o (Join-Path $projectRoot 'artifacts\framework-dependent')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (!$FrameworkOnly) {
    & $runner publish $project -c Release -r win-x64 --self-contained true -o (Join-Path $projectRoot 'artifacts\self-contained')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'artifacts') -Directory |
    Where-Object Name -in 'framework-dependent','self-contained' |
    ForEach-Object {
        $bytes = (Get-ChildItem -LiteralPath $_.FullName -File -Recurse | Measure-Object Length -Sum).Sum
        [PSCustomObject]@{ Publication = $_.Name; Bytes = $bytes; MiB = [Math]::Round($bytes / 1MB, 2); Executable = Join-Path $_.FullName 'Selio.exe' }
    } | Format-Table -AutoSize
