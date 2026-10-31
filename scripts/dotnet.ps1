$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$localSdk = Join-Path $env:LOCALAPPDATA 'HDRPilot\sdk\dotnet.exe'
if (Test-Path -LiteralPath $localSdk) { & $localSdk @args } else { & dotnet @args }
exit $LASTEXITCODE
