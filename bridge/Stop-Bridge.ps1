$runtime = Join-Path $PSScriptRoot 'runtime'
if (-not (Test-Path -LiteralPath $runtime)) { throw 'Le dossier runtime est absent.' }
Set-Content -LiteralPath (Join-Path $runtime 'stop') -Value 'stop' -Encoding utf8
Write-Output 'Arrêt demandé au relais local.'
