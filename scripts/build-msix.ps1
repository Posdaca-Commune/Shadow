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
$pluginPublishRoot = Join-Path $packageRoot "Plugins\Hoi4Launcher"
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

function New-PackageLogo {
    param(
        [string]$Path,
        [int]$Width,
        [int]$Height
    )

    Add-Type -AssemblyName System.Drawing

    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $directory -Force | Out-Null

    $bitmap = New-Object System.Drawing.Bitmap -ArgumentList $Width, $Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $brush = New-Object System.Drawing.SolidBrush -ArgumentList ([System.Drawing.Color]::FromArgb(91, 124, 250))
    $textBrush = New-Object System.Drawing.SolidBrush -ArgumentList ([System.Drawing.Color]::White)
    $fontSize = [Math]::Max(16, [Math]::Min($Width, $Height) * 0.48)
    $font = New-Object System.Drawing.Font -ArgumentList "Segoe UI", $fontSize, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $format = New-Object System.Drawing.StringFormat

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.FillRectangle($brush, 0, 0, $Width, $Height)

        $format.Alignment = [System.Drawing.StringAlignment]::Center
        $format.LineAlignment = [System.Drawing.StringAlignment]::Center
        $rect = New-Object System.Drawing.RectangleF -ArgumentList 0, 0, $Width, $Height
        $graphics.DrawString("S", $font, $textBrush, $rect, $format)

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $format.Dispose()
        $font.Dispose()
        $textBrush.Dispose()
        $brush.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function New-PackageAssets {
    param([string]$AssetsDirectory)

    New-PackageLogo -Path (Join-Path $AssetsDirectory "StoreLogo.png") -Width 50 -Height 50
    New-PackageLogo -Path (Join-Path $AssetsDirectory "Square44x44Logo.png") -Width 44 -Height 44
    New-PackageLogo -Path (Join-Path $AssetsDirectory "Square71x71Logo.png") -Width 71 -Height 71
    New-PackageLogo -Path (Join-Path $AssetsDirectory "Square150x150Logo.png") -Width 150 -Height 150
    New-PackageLogo -Path (Join-Path $AssetsDirectory "Square310x310Logo.png") -Width 310 -Height 310
    New-PackageLogo -Path (Join-Path $AssetsDirectory "Wide310x150Logo.png") -Width 310 -Height 150
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
New-Item -ItemType Directory -Path $pluginPublishRoot -Force | Out-Null

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

    dotnet publish (Join-Path $repoRoot "Shadow.Hoi4Launcher\Shadow.Hoi4Launcher.csproj") `
        --configuration $Configuration `
        --runtime $Runtime `
        --self-contained:false `
        -p:Version=$Version `
        -p:CopyPluginToHost=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $pluginPublishRoot
}

$abstractionsInPlugin = Join-Path $pluginPublishRoot "Shadow.Abstractions.dll"
if (Test-Path -LiteralPath $abstractionsInPlugin) {
    Remove-Item -LiteralPath $abstractionsInPlugin -Force
}

Remove-SharedPluginFiles -PluginDirectory $pluginPublishRoot
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
