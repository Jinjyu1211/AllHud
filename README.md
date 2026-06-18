# AllHud

AllHud 是一个 Dalamud HUD 插件，用 ImGui 覆盖层把游戏中需要盯的关键信息集中显示出来，例如目标 / Boss 情报、队伍减伤与团辅冷却、以及自身状态。定位是一套可定制的信息 HUD，方便随时扫视。

## 功能

- **目标情报**：目标 / Boss 血条、咏唱栏（含隐藏技能兜底显示）、目标身上的 DoT / 减益、目标的目标（ToT）。
- **队伍减伤监控**：队友减伤 / 团辅技能冷却、食物检查、队伍 LB 条；掉线 / 不在场成员自动隐藏。
- **自身状态**：仿原生状态栏的增益 / 减益 / 其他状态图标。
- 使用游戏图标和进度条显示状态 / 冷却，方便战斗中快速扫视。
- 支持显示原始状态 ID，方便进游戏校准状态和技能数据。
- 支持锁定 / 解锁覆盖层位置、缩放面板、过滤目标状态、隐藏血量数字等。

## 构建

项目使用 `Dalamud.NET.Sdk/15.0.0`，目标框架为 `net10.0-windows`。

如果使用 XIVLauncherCN，需要把 `DALAMUD_HOME` 指向 XIVLauncherCN 的 Dalamud dev 目录：

```powershell
$env:DALAMUD_HOME="C:\Users\15868\AppData\Roaming\XIVLauncherCN\addon\Hooks\dev"
dotnet build "AllHud.csproj" -c Debug
```

如果使用原版 XIVLauncher，SDK 默认会查找：

```text
%APPDATA%\XIVLauncher\addon\Hooks\dev\
```

## 体验版安装

当前插件计划通过 GitHub Release + Dalamud 自定义插件仓库分发。发布流程见：

```text
PUBLISHING.md
```

用户拿到仓库地址后，在 Dalamud / XIVLauncherCN 的自定义插件仓库中添加：

```text
https://raw.githubusercontent.com/QiongHHHZZZ/AllHud/main/repo.json
```

## 追踪数据

追踪的状态和技能定义在：

```text
Data/TrackedDefinitions.cs
```

第一版数据是硬编码的初始表。进游戏后可以开启“显示状态 ID（用于校准）”，观察实际状态 ID 后继续补全或修正。

## 冷却来源

冷却显示会标记来源：

- `真实`：本地玩家通过 ActionManager 读取到的实际 recast。
- `监听`：通过技能释放事件观察到的冷却。
- `估算`：通过状态出现和剩余时间反推的冷却。

其中 `真实` 最准确，`估算` 是兜底方案。

## 已知限制

- 队友冷却依赖技能事件监听或状态观察；如果没有观察到释放或状态，面板不会凭空显示该冷却。
- 状态 ID / 技能 ID 可能随游戏版本变化，需要持续校准。
- 目前追踪定义仍在代码中维护，尚未外置为 JSON 或配置文件。
