# FGUI 外部工具链接入规范（ofgui / FairyGUI Editor）

适用对象：接手 `UIProject` 的执行 AI 与开发者。目标是在不把工具复制进 Git 仓库的前提下，
让任意一台电脑都能把 OpenFairyGUI（ofgui）的 CLI 与 MCP 接到本机的 FGUI 工程上。

工程本身的拼界面规范见 `UIProject/docs/RULES.md`；本文只讲工具链与接入。

## 1. 边界

- `UIProject` 是 FairyGUI 源工程（`.fairy` + `assets/<Package>/*.xml` + `settings/*.json`）。
- OpenFairyGUI（ofgui）与 FairyGUI Editor 属于**外部工具**，装在仓库之外，不纳入版本管理。
- 仓库内只保留：MCP 接入配置、工具发现脚本、启动脚本、验证脚本、本文档。
- 不把 ofgui 目录、`node_modules`、临时缓存或任何机器专属绝对路径写入仓库。

## 2. 组件与版本（2026-10-27 本机实测）

| 组件 | 版本 | 本机路径 | 说明 |
| --- | --- | --- | --- |
| FairyGUI Editor | `2022.3.5.8607574` | `D:\Program Files\FairyGUI-Editor\FairyGUI-Editor.exe` | Unity IL2CPP 构建；只用于人工可视化编辑/预览 |
| OpenFairyGUI 包装目录 | `ofgui 1.0.0` | `D:\Program Files\ofgui` | 仅 `package.json` + `node_modules` + `patches` |
| `@openfairygui/cli` | `0.7.0` | 同上 | CLI：`validate` / `inspect` / `publish` / `restore` / `doctor` / `tx` / `docs` |
| `@openfairygui/mcp` | `0.7.0` | 同上 | MCP stdio 服务，暴露 18 个工具 |
| `@openfairygui/backend` | `0.7.0` | 同上 | `BACKEND_CONTRACT_VERSION 3.0.0`，`capabilitySchemaVersion 15` |
| Node.js | `v22.12.0` | `D:\Program Files\nodejs\node.exe` | ofgui 要求 `>=22` |

> 这两个版本组合是**已验证组合**：`ofgui doctor` 返回 `status: ready`，`validate` 返回
> `status: valid / complete: true`，MCP `initialize + tools/list` 正常。升级任一组件后必须重跑第 7 节。

### 2.1 本机补丁

`D:\Program Files\ofgui\patches\apply-ofgui-patches.cjs` 对 `@openfairygui/* 0.7.0` 打了两个必要补丁：

1. `backend-fidelity-unhandled-rejection`：`ProjectWriter` 的并发写会产生未处理的 Promise reject，
   在 `package.xml` 出现重复条目时会让整个 MCP 服务崩溃。
2. `read-schema-codegen-packagename-null`：`Publish.json` 的 `codeGeneration.packageName` 被编辑器写成
   `null`，但读取 schema 只允许 string，导致 `read_session_state` 必然失败。

补丁脚本幂等，`npm install` 的 `postinstall` 会自动执行。脚本对“锚点缺失”会明确报错并返回非 0，
所以升级后若报 `anchor not found`，说明上游可能已修复或已改结构，需重新确认后再决定是否保留补丁。

## 3. 路径发现（不写死盘符）

`UIProject/scripts/Find-Ofgui.ps1` 按以下顺序解析，命中即停：

1. 命令行参数 `-OfguiHome` / `-NodeExe` / `-FairyGuiEditor`
2. 环境变量 `OFGUI_HOME` / `OFGUI_NODE` / `FAIRYGUI_EDITOR`
3. 本机本地配置 `UIProject/.ofgui.local.json`（已被 `.gitignore` 忽略）
4. 系统 `PATH`（`ofgui`、`node`）
5. 常见安装目录：各固定磁盘的 `Program Files` / `Program Files (x86)` / `Programs` / `Tools` / `Apps` / `Software`，
   以及 `%ProgramFiles%`、`%LOCALAPPDATA%`、`%APPDATA%`
6. 必要时做有限深度目录搜索（跳过 Adobe / Photoshop / NVIDIA 等自带 `node.exe` 的目录）

判定标准是**真实可执行入口存在**（`<ofgui>\node_modules\@openfairygui\mcp\bin\ofgui-mcp.cjs`），
不是“同名文件夹存在”。Node 版本必须 `>= 22`，否则判定未就绪。

```powershell
# 人类可读
powershell -NoProfile -File scripts\Find-Ofgui.ps1
# 机器可读
powershell -NoProfile -File scripts\Find-Ofgui.ps1 -Json
# 写入本机兜底配置（自动发现失败时最省事）
powershell -NoProfile -File scripts\Find-Ofgui.ps1 -SaveLocal
```

`.ofgui.local.json` 示例（本机专用，不提交）：

```json
{
  "$schema": "ofgui-local-config/1",
  "ofguiHome": "D:\\Program Files\\ofgui",
  "nodeExe": "D:\\Program Files\\nodejs\\node.exe",
  "fairyguiEditor": "D:\\Program Files\\FairyGUI-Editor\\FairyGUI-Editor.exe"
}
```

## 4. MCP 接入

`UIProject/.mcp.json`（随仓库提交，不含绝对路径）：

```json
{
  "mcpServers": {
    "openfairygui": {
      "type": "stdio",
      "command": "cmd",
      "args": ["/c", "${workspaceFolder}\\scripts\\ofgui-mcp.cmd"],
      "cwd": "${workspaceFolder}"
    }
  }
}
```

### 4.1 客户端行为（读 IDE 扩展实现得知，非猜测）

- `workspaceFolder` / `workspaceFolderBasename` / `userHome` 三个变量会在 `command`、`args`、`cwd` 内替换；
  替换表 = 这三个内置变量 + 该 server 自己的 `env` 块。
- stdio server 未写 `cwd` 时默认用**工作区根目录**；因此本工程直接使用 `${workspaceFolder}` 即可，无需绝对路径。
- **`env` 的值不参与变量替换**。所以不能写 `"OPENFAIRYGUI_ALLOWED_PROJECT_ROOTS": "${workspaceFolder}"`，
  只能在 `ofgui-mcp.cmd` 里以绝对路径注入（脚本已处理，用户自行设置的值会被保留）。
- 项目级（`.mcp.json`）的 server 受**项目级审批门禁**约束：`configSource = project` 时必须由用户在 MCP 面板
  显式允许后才能连接。审批状态按 `server 名 + 工程路径 + 配置指纹` 记录，**指纹由 command/args/cwd/env 等算出**，
  所以只要改动 `.mcp.json` 的任何一项，就需要重新授权一次；这是预期行为，不是故障。
- 工作区必须打开到 `UIProject` 目录（不是仓库根），否则客户端只会在 `<仓库根>/.mcp.json` 里找配置。

### 4.2 为什么用 `cmd` 包一层

Node 的 stdio 子进程在 Windows 下不能直接启动 `.cmd`；而用 PowerShell 包一层会把子进程 stdout 重新编码，
破坏 JSON-RPC 字节流。`cmd.exe` 是纯进程启动器，句柄原样透传，因此：

```
cmd /c scripts\ofgui-mcp.cmd
  └─ for /f  ← powershell Find-Ofgui.ps1 -EmitLaunch   （只取一行 "node|mcp|cli"，输出被捕获，不污染 stdout）
      └─ node <ofgui-mcp.cjs>                          （stdio 直连客户端）
```

## 5. CLI 用法

```powershell
cd UIProject
scripts\ofgui.cmd validate . --json      # 交付前必跑：status=valid 且 complete=true
scripts\ofgui.cmd inspect . --json       # 结构统计：包/资源/组件/节点/gear/控制器/动效
scripts\ofgui.cmd doctor .               # 诊断版本、原生图像、目录可访问性；只读
scripts\ofgui.cmd tx preflight . --ops ops.json --json   # 事务预检（不落盘）
scripts\ofgui.cmd tx apply . --ops ops.json --json       # 预检→应用→校验→保存
scripts\ofgui.cmd publish . --json       # 发布二进制产物（会写 Publish.json 配置的目标目录）
```

`cmd` 包装脚本内部同样走 `Find-Ofgui.ps1`，因此换机后命令不变。

## 6. 换机接入步骤

1. 自备安装 FairyGUI Editor（任选目录）与 Node.js `>= 22`。
2. 安装 ofgui：建一个空目录（如 `<盘符>:\Program Files\ofgui`），写入
   `package.json` 依赖 `@openfairygui/cli`/`@openfairygui/mcp` 为 `0.7.0`，然后在该目录 `npm install`；
   如需第 2.1 节的补丁，把 `patches/apply-ofgui-patches.cjs` 一并放进去并挂到 `postinstall`。
3. 克隆游戏仓库，确认 `UIProject/.mcp.json` 存在（无需改动）。
4. 用 IDE **打开 `UIProject` 目录**（不是仓库根）。
5. 跑 `powershell -NoProfile -File scripts\Find-Ofgui.ps1`，确认 `结论: 工具链就绪`。
   自动探测失败时用 `-OfguiHome` / `-NodeExe` 指定，或写 `.ofgui.local.json`。
6. 跑 `scripts\ofgui-verify.cmd`，确认 MCP `initialize + tools/list` 通过且 `stdoutClean: true`。
7. 重开 IDE（或重连 MCP），在 MCP 面板对 `openfairygui` 点击允许（项目级审批）。
8. 打开 FairyGUI Editor 载入 `FGUIProject.fairy`，确认 12 个组件可见、能打开 `MainView` 并预览。

## 7. 验证命令（可重复执行）

```powershell
cd UIProject
powershell -NoProfile -File scripts\Find-Ofgui.ps1     # 工具发现
scripts\ofgui-verify.cmd                               # MCP 握手 + 工具清单
scripts\ofgui.cmd validate . --json                    # 工程校验
scripts\ofgui.cmd doctor . --json                       # 环境诊断
```

`ofgui-verify.cmd` 输出 `ok: true` 表示：MCP 服务能从**外部安装目录**启动、协议版本协商成功、
`tools/list` 返回 18 个工具、stdout 无非协议内容。

## 8. 已知风险与限制

| 项 | 状态 | 说明 |
| --- | --- | --- |
| 跨设备真机验证 | **未实测** | 只在唯一一台机器上验证；另做过“受控路径模拟”（`OFGUI_HOME` 指向另一个绝对路径的目录联接 + `-SaveLocal` 覆盖）通过，但这不等价于另一台真实电脑 |
| MCP 面板绿灯 | 依赖用户点击 | 项目级 server 需人工允许一次；改配置后需再次允许 |
| `publish` / `restore` 走 MCP | 不可用 | 0.7.0 的 MCP 后端把 `artifact.publish/restore` 标为 `bridge-required`；必须用 CLI（`scripts\ofgui.cmd publish`） |
| FairyGUI Editor 无人值守操作 | 不稳定 | Editor 是 Unity IL2CPP 应用，无私用自动化接口；本轮用坐标点击 + 截屏取证，受窗口焦点与多显示器影响，不能作为长期流水线依赖 |
| Node 版本 | 需 `>=22` | 低于 22 时 `Find-Ofgui.ps1` 判定未就绪；本机原先未安装 Node，本轮补装到 `D:\Program Files\nodejs` |
| ofgui 升级 | 需复核补丁 | 见 2.1 |

## 9. 工程侧（`UIProject`）关键设置

- `FGUIProject.fairy`：`type="MonoGame"`。FairyGUI Editor 没有官方 Godot 工程类型；MonoGame 是编辑器真实支持、
  且产出纯资源（不绑定 Unity 目录结构与 C# 代码生成）的类型，适合作为“先出资源、后接 Godot 运行时”的过渡目标。
- `settings/Adaptation.json`：设计分辨率 `1440x900`（PC 横屏），测试设备 `设计尺寸 1440x900` / `高极限 1440x1080` / `宽极限 2560x1080`。
- `settings/Publish.json`：发布目录 `../artifacts/ui-publish`（`artifacts/` 已被仓库 `.gitignore` 忽略），
  关闭 Unity 风格 C# 代码生成（`allowGenCode: false`、`codePath: ""`）。
- `assets/_Preview/package.xml`：保留独立发布配置，路径改为 `../artifacts/ui-publish/preview`。
- `settings/i18n.json`：清空指向 Unity `Assets/GameRes/Configs/languages` 的语言文件引用。
- `settings/Common.json`：清空指向不存在包的字体 `ui://` 引用；默认字号按 1440×900 下调到 18，字号方案等比下调。
