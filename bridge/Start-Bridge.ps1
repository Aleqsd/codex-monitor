param([int]$Port = 43187)
& node --disable-warning=ExperimentalWarning (Join-Path $PSScriptRoot 'bridge.mjs') --port $Port
