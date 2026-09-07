param(
    [string]$Dotnet,
    [string]$DalamudHome = (Join-Path $env:APPDATA 'XIVLauncher\addon\Hooks\dev'),
    [switch]$RunChecks
)
$ErrorActionPreference = 'Stop'
if (-not $Dotnet) {
    $workspaceSdk = Join-Path $PSScriptRoot '..\.tools\dotnet\dotnet.exe'
    $Dotnet = if (Test-Path -LiteralPath $workspaceSdk) { (Resolve-Path -LiteralPath $workspaceSdk).Path } else { (Get-Command dotnet.exe).Source }
}
$savedEnvironment = @{}
foreach ($name in @('DALAMUD_HOME','DOTNET_CLI_HOME','DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_GENERATE_ASPNET_CERTIFICATE','NUGET_PACKAGES')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
Push-Location -LiteralPath $PSScriptRoot
try {
    $env:DALAMUD_HOME = (Resolve-Path -LiteralPath $DalamudHome).Path
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.local\dotnet-home'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.local\nuget'
    & $Dotnet restore 'src\CodexMonitor.csproj' --locked-mode --nologo
    if ($LASTEXITCODE -ne 0) { throw 'La restauration des dépendances a échoué.' }
    & $Dotnet build 'src\CodexMonitor.csproj' -c Release --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'La compilation a échoué.' }
    if ($RunChecks) {
        & node --test 'bridge\observer.test.mjs' 'bridge\usage.test.mjs' 'bridge\files.test.mjs'
        if ($LASTEXITCODE -ne 0) { throw 'Les contrôles du relais ont échoué.' }
        & $Dotnet run --project 'tests\CoreChecks.csproj' -c Release --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Les contrôles du client ont échoué. Le relais réel doit être lancé sur le port 43187.' }
        & $Dotnet run --project 'tests\LauncherChecks\LauncherChecks.csproj' -c Release --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Les contrôles du lanceur local ont échoué.' }
    }
    $output = Join-Path $PSScriptRoot 'plugin'
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    foreach ($name in @('CodexMonitor.dll','CodexMonitor.json','CodexMonitor.deps.json')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot "src\bin\Release\$name") -Destination (Join-Path $output $name)
    }
    Write-Output (Join-Path $output 'CodexMonitor.dll')
} finally {
    Pop-Location
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
}
