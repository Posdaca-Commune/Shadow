[CmdletBinding()]
param(
    [string]$Subject = "CN=Posdaca Commune",
    [string]$FriendlyName = "Shadow MSIX Test Signing",
    [string]$OutputDirectory,
    [string]$CertificateName = "Shadow",
    [int]$YearsValid = 5,
    [securestring]$Password,
    [switch]$Force,
    [switch]$InstallLocally
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repoRoot "certs"
}

if ($YearsValid -lt 1) {
    throw "YearsValid 必须大于等于 1。"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$pfxPath = Join-Path $OutputDirectory "$CertificateName.pfx"
$cerPath = Join-Path $OutputDirectory "$CertificateName.cer"
$passwordPath = Join-Path $OutputDirectory "$CertificateName.pfx.password.txt"

foreach ($path in @($pfxPath, $cerPath, $passwordPath)) {
    if ((Test-Path -LiteralPath $path) -and -not $Force) {
        throw "已存在 '$path'。若要重新生成，请加 -Force。"
    }
}

if (-not $Password) {
    $generated = -join (
        (48..57) + (65..90) + (97..122) |
            Get-Random -Count 24 |
            ForEach-Object { [char]$_ }
    )
    $Password = ConvertTo-SecureString $generated -AsPlainText -Force
    Set-Content -LiteralPath $passwordPath -Value $generated -Encoding UTF8
    Write-Host "已生成随机证书密码，并写入：$passwordPath"
}
else {
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
    try {
        $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }

    Set-Content -LiteralPath $passwordPath -Value $plainPassword -Encoding UTF8
    Write-Host "已将指定证书密码写入：$passwordPath"
}

$notAfter = (Get-Date).AddYears($YearsValid)
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $Subject `
    -KeyUsage DigitalSignature `
    -FriendlyName $FriendlyName `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @(
        "2.5.29.37={text}1.3.6.1.5.5.7.3.3",
        "2.5.29.19={text}"
    ) `
    -KeyExportPolicy Exportable `
    -KeySpec Signature `
    -KeyLength 2048 `
    -HashAlgorithm SHA256 `
    -NotAfter $notAfter

try {
    Export-PfxCertificate `
        -Cert $cert `
        -FilePath $pfxPath `
        -Password $Password | Out-Null

    Export-Certificate `
        -Cert $cert `
        -FilePath $cerPath | Out-Null
}
finally {
    # Keep the private key only in the exported PFX for packaging machines.
    Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force -ErrorAction SilentlyContinue
}

if ($InstallLocally) {
    $trustedPeople = "Cert:\CurrentUser\TrustedPeople"
    $existing = Get-ChildItem -LiteralPath $trustedPeople -ErrorAction SilentlyContinue |
        Where-Object { $_.Thumbprint -eq $cert.Thumbprint }

    if (-not $existing) {
        Import-Certificate -FilePath $cerPath -CertStoreLocation $trustedPeople | Out-Null
        Write-Host "已将公钥证书安装到当前用户 Trusted People：$cerPath"
    }
    else {
        Write-Host "Trusted People 中已存在相同指纹的证书，跳过安装。"
    }
}

Write-Host ""
Write-Host "MSIX 测试证书已生成："
Write-Host "  Subject     : $Subject"
Write-Host "  Thumbprint  : $($cert.Thumbprint)"
Write-Host "  NotAfter    : $($cert.NotAfter.ToString('u'))"
Write-Host "  PFX (签名)  : $pfxPath"
Write-Host "  CER (分发)  : $cerPath"
Write-Host "  Password    : $passwordPath"
Write-Host ""
Write-Host "签名示例："
Write-Host "  `$password = Get-Content .\\certs\\$CertificateName.pfx.password.txt | ConvertTo-SecureString -AsPlainText -Force"
Write-Host "  .\\scripts\\build-msix.ps1 -CertificatePath .\\certs\\$CertificateName.pfx -CertificatePassword `$password"
Write-Host ""
Write-Host "测试机信任证书（管理员 PowerShell）："
Write-Host "  Import-Certificate -FilePath .\\certs\\$CertificateName.cer -CertStoreLocation Cert:\\LocalMachine\\TrustedPeople"
Write-Host "然后开启“开发人员模式”或“旁加载应用”，再双击安装 .msix。"
