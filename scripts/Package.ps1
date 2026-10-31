param([string]$Version = '')
$ErrorActionPreference = 'Stop'
if (!$Version) { $Version = ([xml](Get-Content (Join-Path $PSScriptRoot '../Directory.Build.props') -Raw)).Project.PropertyGroup.Version }
if ($Version -notmatch '^\d+\.\d+\.\d+([-.][a-zA-Z0-9.]+)?$') { throw 'Invalid version' }
$root = Split-Path -Parent $PSScriptRoot
$runner = Join-Path $PSScriptRoot 'dotnet.ps1'
Push-Location $root
try {
    & $runner restore HDRPilot.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
    & $runner build HDRPilot.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    & $runner test HDRPilot.sln -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    $artifacts = Join-Path $root 'artifacts'
    # A fresh staging directory prevents old binaries from entering a new release.
    $staging = Join-Path $artifacts ([Guid]::NewGuid().ToString('N'))
    $destination = Join-Path $staging "HDRPilot-$Version-win-x64"
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    foreach ($project in @('App', 'NativeHost', 'Diagnostics')) {
        & $runner publish "src/HDRPilot.$project/HDRPilot.$project.csproj" -c Release -r win-x64 --self-contained true '-p:RestoreLockedMode=true' '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' '-p:PublishTrimmed=false' "-p:Version=$Version" '-p:DebugType=None' -o $destination
        if ($LASTEXITCODE -ne 0) { throw "Publish failed: $project" }
    }
    foreach ($folder in @('browser', 'docs', 'assets')) { Copy-Item -LiteralPath (Join-Path $root $folder) -Destination $destination -Recurse -Force }
    $releaseScripts = Join-Path $destination 'scripts'
    New-Item -ItemType Directory -Force -Path $releaseScripts | Out-Null
    foreach ($script in @('Register-BrowserBridge.ps1', 'Unregister-BrowserBridge.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $script) -Destination $releaseScripts -Force }
    foreach ($doc in @('README.md', 'ARCHITECTURE.md', 'SECURITY.md', 'BUILDING.md', 'CHANGELOG.md', 'CONTRIBUTING.md', 'LICENSE')) { Copy-Item -LiteralPath (Join-Path $root $doc) -Destination $destination -Force }
    & (Join-Path $destination 'HDRPilot.Diagnostics.exe') --abi
    if ($LASTEXITCODE -ne 0) { throw 'Packaged diagnostics failed' }
    $exe = Join-Path $destination 'HDRPilot.exe'
    $metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
    if ($metadata.ProductName -ne 'HDR Pilot' -or $metadata.ProductVersion -notlike "$Version*") { throw 'Incorrect packaged application identity' }
    $standalone = Join-Path $artifacts "HDRPilot-$Version-win-x64.exe"
    Copy-Item -LiteralPath $exe -Destination $standalone -Force
    "{0}  {1}" -f (Get-FileHash -LiteralPath $standalone -Algorithm SHA256).Hash.ToLowerInvariant(), ([IO.Path]::GetFileName($standalone)) | Set-Content -LiteralPath ($standalone + '.sha256') -Encoding ascii
    $sums = Get-ChildItem -LiteralPath $destination -Filter *.exe | Sort-Object Name | ForEach-Object { "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name }
    $sums | Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS.txt') -Encoding ascii
    $compiler = & (Join-Path $PSScriptRoot 'Get-InnoSetup.ps1')
    & $compiler "/DAppVersion=$Version" "/DPackageDir=$destination" "/DOutputDir=$artifacts" (Join-Path $root 'installer/HDRPilot.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
    $setup = Join-Path $artifacts "HDRPilot-$Version-win-x64-Setup.exe"
    "{0}  {1}" -f (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant(), ([IO.Path]::GetFileName($setup)) | Set-Content -LiteralPath ($setup + '.sha256') -Encoding ascii
    $zip = Join-Path $artifacts "HDRPilot-$Version-win-x64.zip"
    Compress-Archive -LiteralPath $destination -DestinationPath $zip -Force
    "{0}  {1}" -f (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant(), ([IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ascii
    Write-Output "Portable release: $destination"
    Write-Output "ZIP: $zip"
    Write-Output "Standalone EXE: $standalone"
    Write-Output "Installer: $setup"
} finally { Pop-Location }
