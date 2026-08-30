[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "linux-x64",
    [string]$Version,
    [bool]$SelfContained = $true,
    [switch]$SkipBuild,
    [switch]$SkipTarball,
    [switch]$SkipAppImage
)

# Builds a Linux distribution from the Shadow project.
#
# Usage:
#   pwsh scripts/build-linux.ps1
#   pwsh scripts/build-linux.ps1 -Runtime linux-arm64
#   pwsh scripts/build-linux.ps1 -SkipAppImage
#
# Produces:
#   artifacts/linux/Shadow-<version>-<runtime>.tar.gz  - portable tarball
#   artifacts/linux/Shadow-<version>-<runtime>.AppImage - optional, when appimagetool is present
#
# Requirements:
#   - .NET SDK (net10.0)
#   - For AppImage: appimagetool on PATH (https://appimage.github.io/appimagetool).
#     On hosts without appimagetool the tarball is still produced; AppImage is skipped.

$ErrorActionPreference = "Stop"

# Path fragments use forward slashes so the script also runs on Linux
# (pwsh on Unix does not treat "\" as a separator).
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactRoot = Join-Path $repoRoot "artifacts/linux"
$stagingRoot = Join-Path $artifactRoot "staging"
$pluginProjects = @(
    @{
        Name = "ParadoxGameLauncher"
        Project = "Shadow.ParadoxGameLauncher/Shadow.ParadoxGameLauncher.csproj"
        Output = Join-Path $stagingRoot "Plugins/ParadoxGameLauncher"
    }
)

$desktopEntrySource = Join-Path $repoRoot "packaging/linux/com.posdacacommune.shadow.desktop"

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

if (-not $Version) {
    $Version = Get-ProjectVersion
}

if (Test-Path -LiteralPath $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
foreach ($plugin in $pluginProjects) {
    New-Item -ItemType Directory -Path $plugin.Output -Force | Out-Null
}

if (-not $SkipBuild) {
    $selfContainedValue = $SelfContained.ToString().ToLowerInvariant()

    dotnet publish (Join-Path $repoRoot "Shadow/Shadow.csproj") `
        --configuration $Configuration `
        --runtime $Runtime `
        --self-contained:$selfContainedValue `
        -p:Version=$Version `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $stagingRoot

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for the host app (exit code $LASTEXITCODE)."
    }

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

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed for plugin $($plugin.Name) (exit code $LASTEXITCODE)."
        }
    }
}

# Prune Windows-only manifest that leaks into cross-publish output.
$windowsManifest = Join-Path $stagingRoot "Shadow.exe.manifest"
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

# Copy the .desktop entry and icon next to the app for downstream packaging.
$desktopDir = Join-Path $stagingRoot "share/applications"
$iconDir = Join-Path $stagingRoot "share/icons/hicolor/512x512/apps"
New-Item -ItemType Directory -Path $desktopDir -Force | Out-Null
New-Item -ItemType Directory -Path $iconDir -Force | Out-Null

Copy-Item -LiteralPath $desktopEntrySource -Destination (Join-Path $desktopDir "com.posdacacommune.shadow.desktop") -Force

# Prefer a dedicated png icon; fall back to the SVG branding asset.
$pngIcon = Join-Path $repoRoot "packaging/linux/com.posdacacommune.shadow.png"
$svgIcon = Join-Path $repoRoot "packaging/branding/shadow-icon.svg"
if (Test-Path -LiteralPath $pngIcon) {
    Copy-Item -LiteralPath $pngIcon -Destination (Join-Path $iconDir "com.posdacacommune.shadow.png") -Force
}
elseif (Test-Path -LiteralPath $svgIcon) {
    $scalableDir = Join-Path $stagingRoot "share/icons/hicolor/scalable/apps"
    New-Item -ItemType Directory -Path $scalableDir -Force | Out-Null
    Copy-Item -LiteralPath $svgIcon -Destination (Join-Path $scalableDir "com.posdacacommune.shadow.svg") -Force
}

# Provide a top-level launcher script so users can run `./Shadow.sh` after extract.
# Single-quoted lines keep $(...) and $ literal for the shell. Written via
# WriteAllText so line endings stay LF (Set-Content would emit CRLF on Windows
# hosts, which breaks the shebang with "bad interpreter").
$launcherPath = Join-Path $stagingRoot "Shadow.sh"
$launcher = @(
    '#!/bin/sh',
    'DIR="$(dirname "$(readlink -f "$0")")"',
    'exec "$DIR/Shadow" "$@"'
)
$launcherText = ($launcher -join "`n") + "`n"
[System.IO.File]::WriteAllText($launcherPath, $launcherText)

# Ensure the launcher and the apphost binary carry the execute bit before tar
# packs them; on Linux dotnet publish already marks the apphost, Copy-Item does
# not preserve modes for newly created files such as Shadow.sh.
$chmod = Get-Command chmod -ErrorAction SilentlyContinue
if ($chmod) {
    & $chmod.Source +x $launcherPath
    if ($LASTEXITCODE -ne 0) {
        throw "chmod +x failed for $launcherPath (exit code $LASTEXITCODE)."
    }

    $mainExecutable = Join-Path $stagingRoot "Shadow"
    if (Test-Path -LiteralPath $mainExecutable) {
        & $chmod.Source +x $mainExecutable
        if ($LASTEXITCODE -ne 0) {
            throw "chmod +x failed for $mainExecutable (exit code $LASTEXITCODE)."
        }
    }
}

if ($SkipTarball) {
    Write-Host "Linux staging directory generated at: $stagingRoot"
    return
}

$tarballName = "Shadow-$Version-$Runtime.tar.gz"
$tarballPath = Join-Path $artifactRoot $tarballName

# Pack the staging directory contents directly (-C) so extracting the tarball
# yields Shadow.sh and friends at the extraction root instead of under staging/.
$tar = Get-Command tar -ErrorAction SilentlyContinue
if ($tar) {
    & $tar.Source -czf $tarballPath -C $stagingRoot "."

    if ($LASTEXITCODE -ne 0) {
        throw "tar failed to create $tarballName"
    }
}
else {
    Write-Warning "tar not found on PATH; skipping tarball. Staging dir is at $stagingRoot"
}

if (Test-Path -LiteralPath $tarballPath) {
    Write-Host "Linux tarball generated: $tarballPath"
}

if ($SkipAppImage) {
    return
}

# AppImage requires linux platform + appimagetool. Detect and skip gracefully.
$appimagetool = Get-Command appimagetool -ErrorAction SilentlyContinue
if (-not $appimagetool) {
    Write-Warning "appimagetool not found on PATH; skipping AppImage. Tarball is at $tarballPath"
    return
}

# Build an AppDir layout under the staging tree.
$appDirRoot = Join-Path $artifactRoot "AppDir"
if (Test-Path -LiteralPath $appDirRoot) {
    Remove-Item -LiteralPath $appDirRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $appDirRoot -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $stagingRoot "*") -Destination $appDirRoot -Recurse -Force

# AppImage wants the launcher named AppRun and the .desktop + icon at the root.
$appRunPath = Join-Path $appDirRoot "AppRun"
Copy-Item -LiteralPath $launcherPath -Destination $appRunPath -Force
Copy-Item -LiteralPath $desktopEntrySource -Destination (Join-Path $appDirRoot "com.posdacacommune.shadow.desktop") -Force

if (Test-Path -LiteralPath $pngIcon) {
    Copy-Item -LiteralPath $pngIcon -Destination (Join-Path $appDirRoot "com.posdacacommune.shadow.png") -Force
}
elseif (Test-Path -LiteralPath $svgIcon) {
    Copy-Item -LiteralPath $svgIcon -Destination (Join-Path $appDirRoot "com.posdacacommune.shadow.svg") -Force
}

if ($chmod) {
    & $chmod.Source +x $appRunPath
    if ($LASTEXITCODE -ne 0) {
        throw "chmod +x failed for $appRunPath (exit code $LASTEXITCODE)."
    }
}

$appImagePath = Join-Path $artifactRoot "Shadow-$Version-$Runtime.AppImage"
& $appimagetool.Source "$appDirRoot" "$appImagePath"

if ($LASTEXITCODE -ne 0) {
    throw "appimagetool failed (exit code $LASTEXITCODE); AppImage not produced."
}

Write-Host "Linux AppImage generated: $appImagePath"
