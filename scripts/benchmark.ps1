param(
    [string]$Exe = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\self-contained\Selio.exe'),
    [string]$Output = (Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts\benchmarks\' + (Get-Date -Format 'yyyyMMdd-HHmmss')))
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $Exe)) { throw "Exécutable introuvable : $Exe. Exécutez scripts/publish.ps1." }
if (Get-Process -Name Selio -ErrorAction SilentlyContinue) { throw 'Quittez Sélio avant le benchmark pour isoler les mesures.' }
$Exe = [IO.Path]::GetFullPath($Exe)
$Output = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$arguments = '--test-session --benchmark "' + $Output + '"'
$process = Start-Process -FilePath $Exe -ArgumentList $arguments -PassThru -WindowStyle Hidden
$samples = [Collections.Generic.List[object]]::new()
$logicalProcessors = [Environment]::ProcessorCount
$watch = [Diagnostics.Stopwatch]::StartNew()
$lastTime = 0.0
$lastCpu = 0.0
$first = $true
$phase = 'demarrage'
Write-Host 'Benchmark : 15 s de stabilisation, 5 min au repos, 20 + 30 cycles, deux repos de 30 s.'
Write-Host 'Une mire OCR apparaîtra pendant les cycles. Laissez le bureau déverrouillé et libre pendant cette phase.'
try {
    while (!$process.HasExited) {
        $process.Refresh()
        $time = $watch.Elapsed.TotalSeconds
        $cpuTime = $process.TotalProcessorTime.TotalSeconds
        $cpu = if (!$first -and $time -gt $lastTime) { 100 * ($cpuTime - $lastCpu) / ($time - $lastTime) / $logicalProcessors } else { 0 }
        $phaseFile = Join-Path $Output 'phase.json'
        if (Test-Path -LiteralPath $phaseFile) {
            try { $phase = (Get-Content -LiteralPath $phaseFile -Raw | ConvertFrom-Json).phase } catch { }
        }
        $samples.Add([PSCustomObject]@{
            elapsed_seconds = [Math]::Round($time, 3)
            phase = $phase
            working_set_bytes = $process.WorkingSet64
            private_bytes = $process.PrivateMemorySize64
            peak_working_set_bytes = $process.PeakWorkingSet64
            cpu_percent_normalized = [Math]::Round($cpu, 5)
            handles = $process.HandleCount
            threads = $process.Threads.Count
        })
        $lastTime = $time; $lastCpu = $cpuTime; $first = $false
        Start-Sleep -Seconds 1
    }
} finally {
    $samples | Export-Csv -LiteralPath (Join-Path $Output 'samples.csv') -NoTypeInformation -Encoding utf8
}
$summaries = foreach ($group in ($samples | Group-Object phase)) {
    $values = $group.Group
    [PSCustomObject]@{
        Phase = $group.Name; Samples = $values.Count
        WorkingSetMeanMiB = [Math]::Round(($values | Measure-Object working_set_bytes -Average).Average / 1MB, 2)
        WorkingSetMaxMiB = [Math]::Round(($values | Measure-Object working_set_bytes -Maximum).Maximum / 1MB, 2)
        PrivateMeanMiB = [Math]::Round(($values | Measure-Object private_bytes -Average).Average / 1MB, 2)
        PrivateMaxMiB = [Math]::Round(($values | Measure-Object private_bytes -Maximum).Maximum / 1MB, 2)
        CpuMean = [Math]::Round(($values | Measure-Object cpu_percent_normalized -Average).Average, 4)
        CpuMax = [Math]::Round(($values | Measure-Object cpu_percent_normalized -Maximum).Maximum, 4)
    }
}
$summaries | Export-Csv -LiteralPath (Join-Path $Output 'summary.csv') -NoTypeInformation -Encoding utf8
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# Benchmark Sélio')
$lines.Add('')
$lines.Add("Date : $(Get-Date -Format o). Windows : $([Environment]::OSVersion.Version). Processeurs logiques : $logicalProcessors.")
$lines.Add('')
$lines.Add('CPU = delta du temps processeur / durée écoulée / processeurs logiques × 100. Échantillonnage externe à 1 Hz. Aucune mesure périodique dans Sélio au repos.')
$lines.Add('')
$lines.Add('| Phase | Échantillons | WS moyen Mio | WS max Mio | Private moyen Mio | Private max Mio | CPU moyen % | CPU max % |')
$lines.Add('|---|---:|---:|---:|---:|---:|---:|---:|')
foreach ($s in $summaries) { $lines.Add("| $($s.Phase) | $($s.Samples) | $($s.WorkingSetMeanMiB) | $($s.WorkingSetMaxMiB) | $($s.PrivateMeanMiB) | $($s.PrivateMaxMiB) | $($s.CpuMean) | $($s.CpuMax) |") }
$cyclesFile = Join-Path $Output 'cycles.csv'
if (Test-Path -LiteralPath $cyclesFile) {
    $cycles = Import-Csv -LiteralPath $cyclesFile
    $good = @($cycles | Where-Object success -eq 'True').Count
    $lines.Add('')
    $lines.Add("Cycles réussis : $good / $($cycles.Count).")
    foreach ($group in ($cycles | Group-Object phase)) {
        $opened = @($group.Group | ForEach-Object { [double]::Parse($_.popup_ms, [Globalization.CultureInfo]::InvariantCulture) })
        $ocr = @($group.Group | ForEach-Object { [double]::Parse($_.ocr_ms, [Globalization.CultureInfo]::InvariantCulture) })
        $lines.Add("$($group.Name) : popup moyenne $([Math]::Round(($opened | Measure-Object -Average).Average, 2)) ms ; OCR moyen $([Math]::Round(($ocr | Measure-Object -Average).Average, 2)) ms ; OCR max $([Math]::Round(($ocr | Measure-Object -Maximum).Maximum, 2)) ms.")
    }
    $lines.Add('')
    $lines.Add('Le premier cycle inclut le chargement à froid du moteur OCR. Les cycles utilisent le vrai contrôleur, la capture écran unique, la vraie fenêtre transparente et le vrai presse-papiers. Le rectangle est fourni par le banc de test : les gestes souris et la reconnaissance du raccourci restent des vérifications graphiques distinctes. La latence popup inclut le rendu/DwmFlush, pas la latence matérielle clavier-écran.')
} else { $lines.Add('Cycles absents : benchmark interrompu ou instance déjà ouverte. Ne pas interpréter ce résultat comme un succès.') }
$lines.Add('')
$lines.Add('Comparer les repos après 20 et 50 cycles, ainsi que les objets GDI/USER dans cycles.csv. Une croissance ponctuelle suivie de stabilisation peut provenir du JIT, des caches Windows OCR et du GC ; aucune collecte forcée ni réduction artificielle du working set. Une seule exécution ne prouve pas une absence de fuite à long terme. Le maximum CPU échantillonné à 1 Hz ne capture pas les pointes plus courtes.')
$lines.Add('')
$lines.Add('Les CSV contiennent uniquement des durées et compteurs. Aucun texte OCR du bureau ni capture n’est écrit sur disque. Le presse-papiers est remplacé par le texte synthétique de la mire pendant le test.')
$lines | Set-Content -LiteralPath (Join-Path $Output 'rapport.md') -Encoding utf8
$summaries | Format-Table -AutoSize
Write-Host "Rapport : $(Join-Path $Output 'rapport.md')"
if (Test-Path -LiteralPath (Join-Path $Output 'error.txt')) { Get-Content -LiteralPath (Join-Path $Output 'error.txt'); exit 1 }
if (!(Test-Path -LiteralPath $cyclesFile) -or $good -ne 50) { exit 1 }
