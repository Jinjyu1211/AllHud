# 发布流程

本文记录 AllHud 发布到 GitHub 并供用户通过 Dalamud 自定义仓库体验的流程。

## 1. 更新版本

在 `AllHud.csproj` 中更新：

```xml
<Version>0.0.0.1</Version>
<PackageProjectUrl>https://github.com/QiongHHHZZZ/AllHud</PackageProjectUrl>
```

首次公开测试建议使用 `0.1.0` 或继续使用当前 `0.0.0.1`，但 GitHub Release tag 需要和仓库清单里的版本一致。

## 2. 构建 Release 包

```powershell
dotnet build "AllHud.csproj" -c Release
```

构建成功后，发布用 zip 位于：

```text
bin/Release/AllHud/latest.zip
```

## 3. 准备仓库清单

复制 `repo.template.json` 为 `repo.json`，然后替换：

- `QiongHHHZZZ/AllHud` 为真实 GitHub 仓库地址
- `AssemblyVersion` 为当前版本
- `DownloadLinkInstall` / `DownloadLinkUpdate` / `DownloadLinkTesting` 为 GitHub Release 附件地址
- `LastUpdate` 为当前 Unix 时间戳

用户添加的自定义仓库地址通常是：

```text
https://raw.githubusercontent.com/QiongHHHZZZ/AllHud/main/repo.json
```

## 4. 创建 GitHub Release

1. 创建 tag，例如 `v0.0.0.1`
2. 上传 `bin/Release/AllHud/latest.zip` 到该 Release
3. 确认 `repo.json` 中的下载链接指向该 Release 附件
4. 推送源码和 `repo.json`

## 5. 用户安装

让用户在 Dalamud/XIVLauncherCN 的插件设置中添加自定义插件仓库 URL：

```text
https://raw.githubusercontent.com/QiongHHHZZZ/AllHud/main/repo.json
```

然后在插件安装器中搜索 `AllHud` 安装。
