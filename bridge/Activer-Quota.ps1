param([ValidateRange(1, 65535)][int]$Port = 43187)
$ErrorActionPreference = 'Stop'

# User-run upgrade helper. Only the recognized Codex Monitor relay may be stopped.
$bridgeScript = Join-Path $PSScriptRoot 'bridge.mjs'
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'usage.mjs'))) { throw 'Ce dossier ne contient pas le relais avec quota.' }
$node = (Get-Command node.exe -ErrorAction Stop).Source
$uri = "http://127.0.0.1:$Port/api/threads"
function Read-Status {
    try { Invoke-RestMethod -Uri $uri -TimeoutSec 2 } catch { $null }
}
function Has-FreshQuota($status) {
    if (-not $status.connected -or -not $status.usage.windows) { return $false }
    try { return [Math]::Abs(([DateTimeOffset]::UtcNow - [DateTimeOffset]::Parse($status.usage.fetchedAt)).TotalSeconds) -le 120 }
    catch { return $false }
}
$status = Read-Status
if (Has-FreshQuota $status) {
    Write-Host 'Le relais fournit déjà un quota récent. Aucun processus modifié.'
    return
}

$limit = 20
$listeners = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
if ($listeners.Count -gt 1) { throw 'Plusieurs écoutes détectées : aucun processus modifié.' }
if ($listeners.Count -eq 1) {
    $owner = Get-CimInstance Win32_Process -Filter "ProcessId = $($listeners[0].OwningProcess)"
    if ($owner.Name -ne 'node.exe') { throw 'Le port appartient à un autre programme : aucun processus modifié.' }
    $outputs = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $known = @($bridgeScript, (Join-Path $outputs 'codex-dalamud-bridge\bridge.mjs'))
    foreach ($version in @('0.3.0', '0.4.0', '0.5.0', '0.5.1', '0.5.2')) {
        $known += Join-Path $outputs "codex-monitor-$version\bridge\bridge.mjs"
    }
    $currentScript = $known | Select-Object -Unique | Where-Object {
        $owner.CommandLine.IndexOf(('"' + $_ + '"'), [StringComparison]::OrdinalIgnoreCase) -ge 0
    } | Select-Object -First 1
    if (-not $currentScript) { throw 'Le relais actif est dans un emplacement non reconnu. Aucun processus modifié.' }
    if ($owner.CommandLine -match '\s--(?:output|codex-home)\s') { throw 'Le relais utilise un dossier personnalisé. Conserver ses paramètres lors de la bascule manuelle.' }
    if ($owner.CommandLine -match '\s--limit\s+(\d+)(?:\s|$)') { $limit = [Math]::Min(100, [Math]::Max(1, [int]$Matches[1])) }
    $oldRuntime = Join-Path (Split-Path -Parent $currentScript) 'runtime'
    if (-not (Test-Path -LiteralPath $oldRuntime -PathType Container)) { throw "Le dossier d’arrêt du relais est absent." }
    if ((Get-NetTCPConnection -LocalPort $Port -State Listen).OwningProcess -ne $owner.ProcessId) { throw 'Le propriétaire du port a changé.' }
    Set-Content -LiteralPath (Join-Path $oldRuntime 'stop') -Value 'stop' -Encoding utf8
    $stopDeadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        Start-Sleep -Milliseconds 250
        $stillListening = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    } while ($stillListening -and [DateTime]::UtcNow -lt $stopDeadline)
    if ($stillListening) { throw 'Le port reste occupé : aucun second relais lancé.' }
}

$runtime = Join-Path $PSScriptRoot 'runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
$relayProcess = Start-Process -FilePath $node -ArgumentList @('--disable-warning=ExperimentalWarning', ('"' + $bridgeScript + '"'), '--port', $Port, '--limit', $limit) `
    -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runtime 'console.log') `
    -RedirectStandardError (Join-Path $runtime 'error.log') -PassThru
$readDeadline = [DateTime]::UtcNow.AddSeconds(20)
do {
    Start-Sleep -Milliseconds 700
    $status = Read-Status
    if ($relayProcess.HasExited) { throw "Le relais s’est arrêté. Consulter $runtime\error.log" }
} until ((Has-FreshQuota $status) -or [DateTime]::UtcNow -ge $readDeadline)
if (-not (Has-FreshQuota $status)) { throw 'Relais lancé, mais quota indisponible. Vérifier que le CLI Codex est installé et connecté.' }
$window = $status.usage.windows | Sort-Object windowDurationMins -Descending | Select-Object -First 1
Write-Host ("Quota disponible : {0}% restants. Le HUD s’actualise automatiquement. Relais en arrière-plan, PID {1}." -f [Math]::Floor($window.remainingPercent), $relayProcess.Id)
