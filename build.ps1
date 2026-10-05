<#
.SYNOPSIS
  Builds LePa Sentinel: self-contained publish, portable zip, Inno Setup installer and (optionally)
  the MSIX package for the Microsoft Store. Output goes to ./artifacts.
.PARAMETER Sign
  Code-sign the app, the installer and the uninstaller with Azure Artifact Signing.
  Needs the Windows SDK (signtool) and an Azure CLI login (`az login`) with the
  "Artifact Signing Certificate Profile Signer" role; account details are in installer/artifact-signing.json.
.PARAMETER Msix
  Also build the MSIX package for the Microsoft Store, with the identity in packaging/msix/identity.json.
  Store packages are left unsigned: Microsoft signs them on submission.
.PARAMETER MsixPublisher
  Build a sideload MSIX (outside the Store) instead: the publisher must equal the subject of the
  signing certificate (e.g. "CN=LePa s.r.l., O=LePa s.r.l., L=..., C=IT"); combine with -Sign.
#>
param(
    [switch]$Sign,
    [switch]$SkipInstaller,
    [switch]$Msix,
    [string]$MsixPublisher
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src/LePaSentinel.csproj'
$artifacts = Join-Path $root 'artifacts'
$publish = Join-Path $artifacts 'publish'

$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
Write-Host "LePa Sentinel $version" -ForegroundColor Cyan

function Find-SdkTool([string]$name) {
    $tool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\$name" -ErrorAction SilentlyContinue |
        Sort-Object { [version]$_.Directory.Parent.Name } | Select-Object -Last 1 -ExpandProperty FullName
    if (-not $tool) { throw "$name not found: install the Windows SDK" }
    $tool
}

if ($Sign) {
    $signtool = Find-SdkTool 'signtool.exe'

    # signtool plug-in that signs through Azure Artifact Signing, downloaded once into .tools/
    $dlibVersion = '1.0.128'
    $dlibDir = Join-Path $root ".tools/artifact-signing-$dlibVersion"
    $dlib = Join-Path $dlibDir 'bin/x64/Azure.CodeSigning.Dlib.dll'
    if (-not (Test-Path $dlib)) {
        $nupkg = Join-Path $root ".tools/artifact-signing-$dlibVersion.zip"
        New-Item -ItemType Directory -Force (Split-Path $nupkg) | Out-Null
        Invoke-WebRequest "https://www.nuget.org/api/v2/package/Microsoft.ArtifactSigning.Client/$dlibVersion" -OutFile $nupkg
        Expand-Archive $nupkg -DestinationPath $dlibDir -Force
        Remove-Item $nupkg
    }
    $metadata = Join-Path $root 'installer/artifact-signing.json'
    $signArgs = @('sign', '/fd', 'SHA256', '/tr', 'http://timestamp.acs.microsoft.com', '/td', 'SHA256',
                  '/dlib', $dlib, '/dmdf', $metadata)

    function Invoke-Sign([string[]]$files) {
        & $signtool @signArgs @files
        if ($LASTEXITCODE) { throw "Signing failed: $files" }
    }
}

# ---- publish (self-contained: users need no .NET install) ----
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained -o $publish
if ($LASTEXITCODE) { throw 'dotnet publish failed' }

$exe = Join-Path $publish 'LePaSentinel.exe'
if ($Sign) { Invoke-Sign @($exe, (Join-Path $publish 'LePaSentinel.dll')) }

# ---- portable zip ----
$zip = Join-Path $artifacts "LePa-Sentinel-$version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Portable: $zip" -ForegroundColor Green

# ---- installer ----
if (-not $SkipInstaller) {
    $iscc = (Get-Command iscc -ErrorAction SilentlyContinue).Source
    if (-not $iscc) {
        $iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
                  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if (-not $iscc) { throw 'Inno Setup 6 not found (https://jrsoftware.org/isinfo.php), or run with -SkipInstaller' }

    $isccArgs = @('/Q', "/DAppVersion=$version", "/DPublishDir=$publish")
    if ($Sign) {
        # Inno signs both Setup.exe and the uninstaller; $q is a quote, $f the quoted file name.
        $command = (@($signtool) + $signArgs | ForEach-Object { if ($_ -match ' ') { "`$q$_`$q" } else { $_ } }) -join ' '
        $isccArgs += @('/DSign', "/Sartifactsigning=$command `$f")
    }
    & $iscc @isccArgs (Join-Path $root 'installer/LePaSentinel.iss')
    if ($LASTEXITCODE) { throw 'Inno Setup failed' }
    $setup = Join-Path $artifacts "LePa-Sentinel-Setup-$version-x64.exe"
    Write-Host "Installer: $setup" -ForegroundColor Green
}

# ---- MSIX (Microsoft Store, or sideload with -MsixPublisher) ----
if ($Msix -or $MsixPublisher) {
    $makeappx = Find-SdkTool 'makeappx.exe'
    $makepri = Find-SdkTool 'makepri.exe'
    $identity = Get-Content (Join-Path $root 'packaging/msix/identity.json') -Raw | ConvertFrom-Json
    $publisher = if ($MsixPublisher) { $MsixPublisher } else { $identity.Publisher }

    $stage = Join-Path $artifacts 'msix-stage'
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    Copy-Item $publish $stage -Recurse
    Copy-Item (Join-Path $root 'packaging/msix/Assets') (Join-Path $stage 'Assets') -Recurse

    $manifest = (Get-Content (Join-Path $root 'packaging/msix/AppxManifest.xml') -Raw).
        Replace('$IDENTITY_NAME$', $identity.IdentityName).
        Replace('$PUBLISHER$', [Security.SecurityElement]::Escape($publisher)).
        Replace('$PUBLISHER_DISPLAY_NAME$', [Security.SecurityElement]::Escape($identity.PublisherDisplayName)).
        Replace('$VERSION$', "$version.0")
    Set-Content (Join-Path $stage 'AppxManifest.xml') $manifest -Encoding utf8

    # resources.pri maps the scaled/unplated logo variants
    $priConfig = Join-Path $artifacts 'priconfig.xml'
    & $makepri createconfig /cf $priConfig /dq en-US /o | Out-Null
    & $makepri new /pr $stage /cf $priConfig /mn (Join-Path $stage 'AppxManifest.xml') /of (Join-Path $stage 'resources.pri') /o | Out-Null
    if ($LASTEXITCODE) { throw 'makepri failed' }
    Remove-Item $priConfig

    $suffix = if ($MsixPublisher) { 'sideload' } else { 'store' }
    $msixFile = Join-Path $artifacts "LePa-Sentinel-$version-x64-$suffix.msix"
    & $makeappx pack /d $stage /p $msixFile /o | Out-Null
    if ($LASTEXITCODE) { throw 'makeappx failed' }
    Remove-Item $stage -Recurse -Force
    if ($Sign -and $MsixPublisher) { Invoke-Sign @($msixFile) }
    Write-Host "MSIX ($suffix): $msixFile" -ForegroundColor Green
}

if ($Sign) {
    $signed = @($exe) + @(if (-not $SkipInstaller) { $setup })
    & $signtool verify /pa @signed
    if ($LASTEXITCODE) { throw 'Signature verification failed' }
    Write-Host 'Signatures verified' -ForegroundColor Green
}
