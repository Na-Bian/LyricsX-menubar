# LyricsX Windows 私有签名工作流(可选)

- The default installer is unsigned and Windows may show a SmartScreen warning.
- A self-signed certificate does not create public publisher trust.
- Friends must verify the SHA-256 fingerprint with you through a separate channel before importing the public .cer.
- Import only the public .cer into Current User > Trusted Publishers and Current User > Trusted Root Certification Authorities.
- A password-protected .pfx is an optional private offline backup for the owner only.
- Never commit or share a .pfx, private key, password, or certificate-store backup.
- If the private key is exposed, stop distributing with it and create a new certificate.

## 创建证书(本机)

```powershell
pwsh -File tools/Create-PrivateSigningCertificate.ps1 -TrustLocally
# 输出 40 位 Thumbprint,记下来(仅本机使用)
```

## 导出公钥 .cer 给朋友

```powershell
pwsh -File tools/Export-PublicCertificate.ps1 -Thumbprint <40位> -OutputPath LyricsX.cer
```

朋友在本机核对 .cer 的 SHA-256 指纹(通过你确认的另一个渠道,例如电话):

```powershell
Get-FileHash .\LyricsX.cer -Algorithm SHA256
```

随后双击 .cer → 安装证书 → 当前用户 → "受信任的发布者" 与 "受信任的根证书颁发机构"。

## 签名发布文件并重建安装包

```powershell
pwsh -File tools/Sign-Release.ps1 -Thumbprint <40位> -Paths (Get-ChildItem out\publish\win-x64 -Recurse -File).FullName
pwsh -File tools/Build-Installer.ps1 -Version 0.1.0
pwsh -File tools/Sign-Release.ps1 -Thumbprint <40位> -Paths out\installer\LyricsX-Windows-Setup-x64.exe
```

## 验证

```powershell
Get-AuthenticodeSignature out\installer\LyricsX-Windows-Setup-x64.exe | Format-List
```

状态应为 `Valid`(在你与朋友的机器上)或 `UnknownError/NotTrusted`(未导入 .cer 的机器)。

## 可选:离线备份 .pfx(仅所有者)

```powershell
pwsh -File tools/Export-PrivateSigningCertificate.ps1 -Thumbprint <40位> -OutputPath D:\backup\lyricsx.pfx
```

把 .pfx 存到离线介质。绝不提交或传输它。
