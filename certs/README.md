# MSIX 测试证书

这里存放用于本地/内测签名的证书。

## 生成

```powershell
.\scripts\create-msix-cert.ps1
```

可选参数：

```powershell
# 覆盖已有证书
.\scripts\create-msix-cert.ps1 -Force

# 生成本机可直接安装测试包时，同时把 .cer 装到当前用户 Trusted People
.\scripts\create-msix-cert.ps1 -Force -InstallLocally

# 自定义密码
.\scripts\create-msix-cert.ps1 -Force -Password (Read-Host -AsSecureString)
```

默认主题名是 `CN=B480C9D8-DB1E-4E4A-A7D7-900209EF2663`，与
`scripts/build-msix.ps1` 的 `-Publisher` 默认值一致；如果覆盖了
`-Subject`，签名时也要传对应的 `-Publisher`。

## 产物

| 文件 | 用途 |
| --- | --- |
| `Shadow.pfx` | 打包签名用（含私钥，不要公开） |
| `Shadow.cer` | 发给测试者安装信任（仅公钥） |
| `Shadow.pfx.password.txt` | 本地密码文件（不要提交） |

## 打包签名

```powershell
$password = Get-Content .\certs\Shadow.pfx.password.txt |
    ConvertTo-SecureString -AsPlainText -Force

.\scripts\build-msix.ps1 `
    -Version 1.0.0-beta.1 `
    -CertificatePath .\certs\Shadow.pfx `
    -CertificatePassword $password
```

## 测试者安装

1. 把 `Shadow.cer` 发给测试者。
2. 在测试机用**管理员** PowerShell 执行：

```powershell
Import-Certificate `
    -FilePath .\Shadow.cer `
    -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

3. 开启 Windows 设置中的“开发人员模式”，或至少开启“旁加载应用”。
4. 双击安装签名后的 `.msix`。

卸载测试证书：

```powershell
Get-ChildItem Cert:\LocalMachine\TrustedPeople |
    Where-Object { $_.Subject -eq 'CN=B480C9D8-DB1E-4E4A-A7D7-900209EF2663' } |
    Remove-Item
```

## 注意

- `.pfx` 和密码文件属于私钥材料，已在 `.gitignore` 中忽略。
- 自签证书只适合内测；正式发布应使用受信任的代码签名证书，或走 Microsoft Store。
- 仓库里已提交的 `Shadow.cer` 是旧主题名（`CN=Posdaca Commune`）生成的示例；
  如需用它给默认 `-Publisher` 的包签名，请用 `-Force` 重新生成证书，
  或在打包时显式传 `-Publisher "CN=Posdaca Commune"`。
