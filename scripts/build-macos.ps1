[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "osx-x64",
    [string]$Version,
    [bool]$SelfContained = $true,
    [switch]$SkipBuild,
    [switch]$SkipBundle,
    [switch]$SkipDmg
)

# Builds a macOS .app bundle (and optional .dmg) from the Shadow project.
#
# Usage (run on macOS or cross-build from Windows for layout inspection):
#   pwsh scripts/build-macos.ps1
#   pwsh scripts/build-macos.ps1 -Runtime osx-arm64
#   pwsh scripts/build-macos.ps1 -SkipDmg
#
# Produces:
#   artifacts/macos/Shadow Studio.app      - ready-to-run bundle
#   artifacts/macos/Shadow-<version>-<runtime>.dmg  - disk image (unless -SkipDmg)
#
# Requirements:
#   - .NET SDK (net10.0)
#   - To produce a .dmg: hdiutil (built into macOS). On non-macOS hosts the
#     .app layout is still produced; .dmg creation is skipped automatically.

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactRoot = Join-Path $repoRoot "artifacts\macos"
$stagingRoot = Join-Path $artifactRoot "staging"
$publishRoot = Join-Path $artifactRoot "publish"
$appBundleRoot = Join-Path $artifactRoot "Shadow Studio.app"
$contentsRoot = Join-Path $appBundleRoot "Contents"
$macosRoot = Join-Path $contentsRoot "MacOS"
$resourcesRoot = Join-Path $contentsRoot "Resources"

$pluginProjects = @(
    @{
        Name = "ParadoxGameLauncher"
        Project = "Shadow.ParadoxGameLauncher\Shadow.ParadoxGameLauncher.csproj"
        Output = Join-Path $macosRoot "Plugins\ParadoxGameLauncher"
    }
)

$manifestTemplate = Join-Path $repoRoot "packaging\macos\Info.plist"

function Get-ProjectVersion {
    $propsPath = Join-Path $repoRoot "Directory.Build.props"
    [xml]$props = Get-Content -LiteralPath $propsPath
    $props.Project.PropertyGroup.Version
}

# Mirrors the shared-dependency pruning done by build-msix.ps1 so plugin
# directories only carry plugin-specific assemblies.
function Remove-SharedPluginFiles {
    param([string]$PluginDirectory)

    if (-not (Test-Path -LiteralPath $PluginDirectory)) {
        return
    }

    $sharedPatterns = @(
        "Avalonia*.dll",
        "CommunityToolkit.Mvvm.dll",
        "FluentAvalonia.dll",
        "HarfBuzzSharp.dll",
        "libHarfBuzzSharp.*",
        "libSkiaSharp.*",
        "MicroCom.Runtime.dll",
        "SkiaSharp.dll"
    )

    foreach ($pattern in $sharedPatterns) {
        Get-ChildItem -LiteralPath $PluginDirectory -Filter $pattern -File -ErrorAction SilentlyContinue |
            Remove-Item -Force
    }

    Get-ChildItem -LiteralPath $PluginDirectory -Filter "*.pdb" -File -ErrorAction SilentlyContinue |
        Remove-Item -Force
}

function Write-PackageManifest {
    param(
        [string]$TemplatePath,
        [string]$DestinationPath,
        [string]$AppVersion
    )

    $manifest = Get-Content -LiteralPath $TemplatePath -Raw
    $manifest = $manifest.Replace("__VERSION__", $AppVersion)
    Set-Content -LiteralPath $DestinationPath -Value $manifest -Encoding UTF8
}

if (-not $Version) {
    $Version = Get-ProjectVersion
}

if (Test-Path -LiteralPath $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
New-Item -ItemType Directory -Path $macosRoot -Force | Out-Null
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
New-Item -ItemType Directory -Path $resourcesRoot -Force | Out-Null
foreach ($plugin in $pluginProjects) {
    New-Item -ItemType Directory -Path $plugin.Output -Force | Out-Null
}

if (-not $SkipBuild) {
    $selfContainedValue = $SelfContained.ToString().ToLowerInvariant()

    dotnet publish (Join-Path $repoRoot "Shadow\Shadow.csproj") `
        --configuration $Configuration `
        --runtime $Runtime `
        --self-contained:$selfContainedValue `
        -p:Version=$Version `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $publishRoot

    # Publish to a space-free path, then move into the .app bundle so the
    # native dotnet argument parser doesn't split on the space in the bundle name.
    Copy-Item -Path (Join-Path $publishRoot "*") -Destination $macosRoot -Recurse -Force

    foreach ($plugin in $pluginProjects) {
        $pluginProjectPath = Join-Path $repoRoot $plugin.Project
        dotnet publish $pluginProjectPath `
            --configuration $Configuration `
            --runtime $Runtime `
            --self-contained:false `
            -p:Version=$Version `
            -p:CopyPluginToHost=false `
            -p:DebugType=None `
            -p:DebugSymbols=false `
            -o $plugin.Output
    }
}

# The .NET publish output lands directly in MacOS/. Prune the Windows-only
# app manifest and debug symbols that have no meaning on macOS.
$windowsManifest = Join-Path $macosRoot "Shadow.exe.manifest"
if (Test-Path -LiteralPath $windowsManifest) {
    Remove-Item -LiteralPath $windowsManifest -Force
}

foreach ($plugin in $pluginProjects) {
    $abstractionsInPlugin = Join-Path $plugin.Output "Shadow.Abstractions.dll"
    if (Test-Path -LiteralPath $abstractionsInPlugin) {
        Remove-Item -LiteralPath $abstractionsInPlugin -Force
    }

    Remove-SharedPluginFiles -PluginDirectory $plugin.Output
}

Write-PackageManifest `
    -TemplatePath $manifestTemplate `
    -DestinationPath (Join-Path $contentsRoot "Info.plist") `
    -AppVersion $Version

# Bundle icon: copy the .icns when present, otherwise fall back to the SVG in
# packaging/branding. A real .icns should be generated during release prep.
$icnsSource = Join-Path $repoRoot "packaging\macos\shadow.icns"
if (Test-Path -LiteralPath $icnsSource) {
    Copy-Item -LiteralPath $icnsSource -Destination (Join-Path $resourcesRoot "shadow.icns") -Force
}

if ($SkipBundle) {
    Write-Host "macOS app bundle layout generated at: $appBundleRoot"
    return
}

# Make the main executable runnable.
$mainExecutable = Join-Path $macosRoot "Shadow"
if (Test-Path -LiteralPath $mainExecutable) {
    # chmod via .NET; shells on Windows won't have chmod available.
    [System.IO.File]::SetAttributes($mainExecutable, [System.IO.FileAttributes]::Normal)
    Write-Host "macOS app bundle generated: $appBundleRoot"
}
else {
    throw "Expected main executable not found: $mainExecutable"
}

if ($SkipDmg) {
    Write-Host "Skipping .dmg creation (-SkipDmg). Bundle ready at: $appBundleRoot"
    return
}

# hdiutil only exists on macOS; detect and skip gracefully elsewhere.
$hdiutil = Get-Command hdiutil -ErrorAction SilentlyContinue
if (-not $hdiutil) {
    Write-Warning ".app bundle ready at $appBundleRoot. hdiutil not found (not macOS?), .dmg skipped."
    return
}

$dmgName = "Shadow-$Version-$Runtime.dmg"
$dmgPath = Join-Path $artifactRoot $dmgName

# Create a read/erase dmg from the bundle directory.
& $hdiutil.Source create -volname "Shadow Studio" -srcfolder "$appBundleRoot" -ov -format UDZO "$dmgPath"

if ($LASTEXITCODE -ne 0) {
    throw "hdiutil failed to create .dmg"
}

if (-not (Test-Path -LiteralPath $dmgPath)) {
    throw ".dmg creation reported success but file is missing: $dmgPath"
}

Write-Host "macOS .dmg generated: $dmgPath"
