[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version,
    [string]$PackageVersion,
    [string]$PackageIdentity = "PosdacaCommune.Shadow",
    [string]$Publisher = "CN=Posdaca Commune",
    [string]$MakeAppxPath,
    [string]$SignToolPath,
    [string]$CertificatePath,
    [securestring]$CertificatePassword,
    [string]$CertificateThumbprint,
    [string]$TimestampUrl,
    [bool]$SelfContained = $true,
    [switch]$SkipBuild,
    [switch]$SkipPackage,
    [switch]$SkipSign
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactRoot = Join-Path $repoRoot "artifacts\msix"
$packageRoot = Join-Path $artifactRoot "package"
$pluginProjects = @(
    @{
        Name = "ParadoxGameLauncher"
        Project = "Shadow.ParadoxGameLauncher\Shadow.ParadoxGameLauncher.csproj"
        Output = Join-Path $packageRoot "Plugins\ParadoxGameLauncher"
    }
)
$manifestTemplate = Join-Path $repoRoot "packaging\msix\AppxManifest.xml"

function Get-ProjectVersion {
    $propsPath = Join-Path $repoRoot "Directory.Build.props"
    [xml]$props = Get-Content -LiteralPath $propsPath
    $props.Project.PropertyGroup.Version
}

function Convert-ToPackageVersion {
    param([string]$InputVersion)

    if ($InputVersion -match "^(\d+)\.(\d+)\.(\d+)(?:[-+].*?(\d+))?") {
        $revision = if ($Matches[4]) { $Matches[4] } else { "0" }
        return "$($Matches[1]).$($Matches[2]).$($Matches[3]).$revision"
    }

    throw "无法从版本号 '$InputVersion' 推导 MSIX 四段数字版本。请用 -PackageVersion 显式指定，例如 1.0.0.0。"
}

function Resolve-WindowsSdkTool {
    param(
        [string]$ToolName,
        [string]$ExplicitPath
    )

    if ($ExplicitPath) {
        if (Test-Path -LiteralPath $ExplicitPath) {
            return (Resolve-Path -LiteralPath $ExplicitPath).Path
        }

        throw "指定的 $ToolName 不存在：$ExplicitPath"
    }

    $command = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $sdkBinRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path -LiteralPath $sdkBinRoot) {
        $candidate = Get-ChildItem -LiteralPath $sdkBinRoot -Directory |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName "x64\$ToolName" } |
            Where-Object { Test-Path -LiteralPath $_ } |
            Select-Object -First 1

        if ($candidate) {
            return $candidate
        }
    }

    throw "未找到 $ToolName。请安装 Windows 10/11 SDK，或用对应参数显式指定工具路径。"
}

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

function New-PackageAssets {
    param([string]$AssetsDirectory)

    $sourceAssetsDirectory = Join-Path $repoRoot "packaging\msix\Assets"
    if (-not (Test-Path -LiteralPath $sourceAssetsDirectory)) {
        throw "未找到 MSIX 图标资源目录：$sourceAssetsDirectory"
    }

    New-Item -ItemType Directory -Path $AssetsDirectory -Force | Out-Null

    $requiredAssets = @(
        "StoreLogo.png",
        "Square44x44Logo.png",
        "Square71x71Logo.png",
        "Square150x150Logo.png",
        "Square310x310Logo.png",
        "Wide310x150Logo.png"
    )

    foreach ($assetName in $requiredAssets) {
        $sourcePath = Join-Path $sourceAssetsDirectory $assetName
        if (-not (Test-Path -LiteralPath $sourcePath)) {
            throw "缺少 MSIX 图标资源：$sourcePath"
        }

        Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $AssetsDirectory $assetName) -Force
    }
}

function Write-PackageManifest {
    param(
        [string]$TemplatePath,
        [string]$DestinationPath,
        [string]$Identity,
        [string]$PublisherName,
        [string]$MsixVersion
    )

    $manifest = Get-Content -LiteralPath $TemplatePath -Raw
    $manifest = $manifest.Replace("__PACKAGE_IDENTITY__", $Identity)
    $manifest = $manifest.Replace("__PUBLISHER__", $PublisherName)
    $manifest = $manifest.Replace("__PACKAGE_VERSION__", $MsixVersion)
    Set-Content -LiteralPath $DestinationPath -Value $manifest -Encoding UTF8
}

function ConvertTo-PlainText {
    param([securestring]$SecureString)

    if (-not $SecureString) {
        return $null
    }

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

if (-not $Version) {
    $Version = Get-ProjectVersion
}

if (-not $PackageVersion) {
    $PackageVersion = Convert-ToPackageVersion -InputVersion $Version
}

if (Test-Path -LiteralPath $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
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
        -o $packageRoot

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

foreach ($plugin in $pluginProjects) {
    $abstractionsInPlugin = Join-Path $plugin.Output "Shadow.Abstractions.dll"
    if (Test-Path -LiteralPath $abstractionsInPlugin) {
        Remove-Item -LiteralPath $abstractionsInPlugin -Force
    }

    Remove-SharedPluginFiles -PluginDirectory $plugin.Output
}
New-PackageAssets -AssetsDirectory (Join-Path $packageRoot "Assets")
Write-PackageManifest `
    -TemplatePath $manifestTemplate `
    -DestinationPath (Join-Path $packageRoot "AppxManifest.xml") `
    -Identity $PackageIdentity `
    -PublisherName $Publisher `
    -MsixVersion $PackageVersion

if ($SkipPackage) {
    Write-Host "MSIX 包目录已生成：$packageRoot"
    return
}

$makeAppx = Resolve-WindowsSdkTool -ToolName "makeappx.exe" -ExplicitPath $MakeAppxPath
$msixPath = Join-Path $artifactRoot "Shadow-$Version.msix"

& $makeAppx pack /d $packageRoot /p $msixPath /o

if (-not (Test-Path -LiteralPath $msixPath)) {
    throw "MSIX 生成失败，未找到输出文件：$msixPath"
}

if ($SkipSign) {
    Write-Warning "已生成未签名 MSIX：$msixPath。未签名 MSIX 不能直接安装。"
    return
}

if (-not $CertificatePath -and -not $CertificateThumbprint) {
    Write-Warning "已生成未签名 MSIX：$msixPath。安装前需要用受信任证书签名。"
    return
}

$signTool = Resolve-WindowsSdkTool -ToolName "signtool.exe" -ExplicitPath $SignToolPath
$signArgs = @("sign", "/fd", "SHA256")

if ($TimestampUrl) {
    $signArgs += @("/tr", $TimestampUrl, "/td", "SHA256")
}

if ($CertificatePath) {
    $signArgs += @("/f", $CertificatePath)
    $plainPassword = ConvertTo-PlainText -SecureString $CertificatePassword
    if ($plainPassword) {
        $signArgs += @("/p", $plainPassword)
    }
}
else {
    $signArgs += @("/sha1", $CertificateThumbprint)
}

$signArgs += $msixPath
& $signTool @signArgs

Write-Host "已生成并签名 MSIX：$msixPath"

