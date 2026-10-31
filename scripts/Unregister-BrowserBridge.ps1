$ErrorActionPreference = 'Stop'
foreach ($browser in @('Google\Chrome', 'Microsoft\Edge')) {
    $key = "HKCU:\Software\$browser\NativeMessagingHosts\org.hdrpilot.video"
    if (Test-Path -LiteralPath $key) { Remove-Item -LiteralPath $key }
}
Write-Output 'Browser native host registration removed. Remove the extension in Chrome or Edge as well.'
