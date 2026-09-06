param([int]$Port = 43187, [string]$CodexExe)
$bridgeArgs = @('--port', $Port)
if ($CodexExe) { $bridgeArgs += @('--codex-exe', $CodexExe) }
& node --disable-warning=ExperimentalWarning (Join-Path $PSScriptRoot 'bridge.mjs') @bridgeArgs
