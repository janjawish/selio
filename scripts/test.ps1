$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'dotnet.ps1') run --project (Join-Path $projectRoot 'tests\Selio.Tests\Selio.Tests.csproj') -c Release
exit $LASTEXITCODE
