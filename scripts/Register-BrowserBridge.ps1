param([Parameter(Mandatory=$true)][ValidatePattern('^[a-p]{32}$')][string]$ExtensionId, [Parameter(Mandatory=$true)][string]$NativeHostPath)
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $NativeHostPath).Path
if ([IO.Path]::GetFileName($exe) -ne 'HDRPilot.NativeHost.exe') { throw 'Select HDRPilot.NativeHost.exe from the portable release.' }
$destination = Join-Path $env:LOCALAPPDATA 'HDRPilot\browser'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$manifest = Join-Path $destination 'host.json'
$origins = @("chrome-extension://$ExtensionId/")
if (Test-Path -LiteralPath $manifest) { $existing = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json; $origins += @($existing.allowed_origins | Where-Object { $_ -match '^chrome-extension://[a-p]{32}/$' }) }
@{name='org.hdrpilot.video';description='HDR Pilot local video bridge';path=$exe;type='stdio';allowed_origins=@($origins | Sort-Object -Unique)} | ConvertTo-Json | Set-Content -LiteralPath $manifest -Encoding utf8
foreach ($browser in @('Google\Chrome', 'Microsoft\Edge')) {
    $key = "HKCU:\Software\$browser\NativeMessagingHosts\org.hdrpilot.video"
    New-Item -Path $key -Force | Out-Null
    Set-Item -Path $key -Value $manifest
}
Write-Output 'Registered for this Windows user. Enable the bridge in HDR Pilot, then reload the YouTube tab.'
