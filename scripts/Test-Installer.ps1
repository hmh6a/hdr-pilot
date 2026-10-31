param([Parameter(Mandatory)][string]$Installer)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$installerPath = (Resolve-Path -LiteralPath $Installer).Path
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{22D9452B-1DDC-49D3-A931-12E4661EAA77}_is1'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'HDR Pilot.lnk'
if ((Test-Path -LiteralPath $uninstallKey) -or (Test-Path -LiteralPath $shortcut)) { throw 'Run installer validation on a clean user profile without an installed HDR Pilot.' }
$testRoot = Join-Path $root ('artifacts/installer-test-' + [Guid]::NewGuid().ToString('N'))
$destination = Join-Path $testRoot 'Custom Folder/HDR Pilot تجربة'
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
$settings = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'HDRPilot/config.json'
$settingsHash = if (Test-Path -LiteralPath $settings) { (Get-FileHash -LiteralPath $settings).Hash } else { $null }
function Invoke-Installer([string]$Executable, [string[]]$Arguments) {
    $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(120000)) { throw "Installer timed out; inspect process $($process.Id)" }
    if ($process.ExitCode -ne 0) { throw "Installer failed: $($process.ExitCode)" }
}
try {
    Invoke-Installer $installerPath @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/TASKS=""', "/DIR=`"$destination`"", "/LOG=`"$testRoot/install.log`"")
    foreach ($file in @('HDRPilot.exe', 'HDRPilot.NativeHost.exe', 'HDRPilot.Diagnostics.exe', 'browser/manifest.json', 'unins000.exe')) {
        if (!(Test-Path -LiteralPath (Join-Path $destination $file))) { throw "Missing installed file: $file" }
    }
    if (!(Test-Path -LiteralPath $shortcut)) { throw 'Start menu shortcut was not created' }
    $installedPath = (Get-ItemProperty -LiteralPath $uninstallKey).InstallLocation.TrimEnd('\')
    if ($installedPath -ne $destination.Replace('/', '\').TrimEnd('\')) { throw 'Installer ignored the custom directory' }
    $expected = (Get-FileHash -LiteralPath ($installerPath.Replace('-Setup.exe', '.exe'))).Hash
    if ((Get-FileHash -LiteralPath (Join-Path $destination 'HDRPilot.exe')).Hash -ne $expected) { throw 'Installed executable does not match the tested build' }
    $marker = Join-Path $destination 'user-created-note.txt'
    Set-Content -LiteralPath $marker -Value 'Keep this user file.'
    # Upgrade without /DIR must reuse the selected location and preserve user files.
    Invoke-Installer $installerPath @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/TASKS=""', "/LOG=`"$testRoot/upgrade.log`"")
    if (!(Test-Path -LiteralPath $marker)) { throw 'Upgrade removed a user-created file' }
    if ((Get-ItemProperty -LiteralPath $uninstallKey).InstallLocation.TrimEnd('\') -ne $installedPath) { throw 'Upgrade changed the installation directory' }
} finally {
    $uninstaller = Join-Path $destination 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller) {
        Invoke-Installer $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$testRoot/uninstall.log`"")
    }
}
if ((Test-Path -LiteralPath (Join-Path $destination 'HDRPilot.exe')) -or (Test-Path -LiteralPath $uninstallKey) -or (Test-Path -LiteralPath $shortcut)) { throw 'Uninstall left application files or registrations behind' }
if (!(Test-Path -LiteralPath $marker)) { throw 'Uninstall removed a user-created file' }
$afterHash = if (Test-Path -LiteralPath $settings) { (Get-FileHash -LiteralPath $settings).Hash } else { $null }
if ($settingsHash -ne $afterHash) { throw 'Install/uninstall modified application settings' }
Write-Output 'Installer verified: custom Unicode path, payload, shortcut, uninstall entry, upgrade location, and preservation of settings/user files.'
