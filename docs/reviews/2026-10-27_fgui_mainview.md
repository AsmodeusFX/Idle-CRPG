# FGUI 工具链接入与 MainView 原生工程（Steam D2 · V0.3 修订）

> 本文件是 FGUI 模块的**唯一 Review**。后续 FGUI 相关开发（发布、Godot 导入、界面迭代、复跑）一律在本文件追加小节，不新建并列文件。

## 1. 需求与授权

- 需求来源：用户直接下达（产品负责人修订 V0.3），非设计 AI 交接。
- 本轮目标：把 OpenFairyGUI 从“仓库内 `Tools/ofgui`”改为“外部安装 + 可迁移接入”，并交付能在 FairyGUI Editor 里真实打开、修改、预览的 MainView 原生 FGUI 工程。
- 授权范围：工具发现、MCP/CLI 接入改造、`UIProject` 旧手游/Unity 配置修正、MainView 界面生成、编辑器实机验证、文档与 Review。
- 明确不在本轮：FGUI → Godot 运行时集成；修改 `Idle-CRPG/` 正式游戏 UI、Core、数值与存档。
- 未执行：Git commit / push / merge（按仓库规则由用户提交）。

## 2. 规则覆盖

- `UIProject/AGENTS.md` 要求改 UIProject 前先读 `UIProject/docs/RULES.md`，已读并按其资源管理、分组、命名、交付标准、动效与校验条款执行。
- 命名：`btn*`、`lst*`、`txt*`、`con*`、`grp*`，顶级页面用 `View` 后缀（`MainView`）。
- 适配尺寸以 `settings/Adaptation.json` 为准，已改为 `1440×900` 后再拼界面。
- 未改 `Idle-CRPG/config/`、Core、存档与 QA 隔离逻辑，未触发 Core Checks 与 Godot 冒烟；本章验证为“工程格式校验 + 编辑器实机渲染”。

## 3. 本机环境调查（实测）

| 项 | 结论 | 依据 |
| --- | --- | --- |
| FairyGUI Editor | `D:\Program Files\FairyGUI-Editor\FairyGUI-Editor.exe`，`2022.3.5.8607574` | 注册表无记录，靠目录探测 + `VersionInfo.FileVersion` |
| OpenFairyGUI | `D:\Program Files\ofgui`，`@openfairygui/{cli,mcp,core,backend,functions} 0.7.0` | 读取 `node_modules/*/package.json` |
| Node.js | **原先未安装**（PATH、注册表、`C:\Program Files`、用户目录、项目目录均无可用 `node.exe`，只有 Adobe 自带的） | 全盘有限深度搜索 |
| 其它 | `git` 也不在 PATH（GitHub Desktop 内置），本轮未使用 git 命令 | `where.exe git` |
| 屏幕 | 主屏 2048×1152，左侧另有 2048×1152 副屏（虚拟桌面 x 从 -2048 起） | `GetSystemMetrics(SM_*)` |

处理：从 IDE 已有的 node 22.12.0 压缩包缓存解压到 `D:\Program Files\nodejs`（`install_binary` 因目录重命名 EPERM 失败，故手工解压并展平），`node -v` = `v22.12.0`，满足 `>=22`。

## 4. 实际变更

### 4.1 工具链（仓库内新增，均不含绝对路径）

| 文件 | 作用 |
| --- | --- |
| `UIProject/.mcp.json` | MCP 接入配置：`cmd /c ${workspaceFolder}\scripts\ofgui-mcp.cmd`，`cwd=${workspaceFolder}` |
| `UIProject/scripts/Find-Ofgui.ps1` | 工具发现：参数 → 环境变量 → `.ofgui.local.json` → PATH → 常见目录 → 有限深度搜索；输出人读/JSON/`-EmitLaunch` 三种形态；`-SaveLocal` 落本机兜底配置 |
| `UIProject/scripts/ofgui-mcp.cmd` | MCP 启动器：`for /f` 取一行 `node|mcp|cli`，再以 `cmd` 直连 `node`，保持 stdio 字节流干净 |
| `UIProject/scripts/ofgui.cmd` | CLI 包装器，参数原样透传 |
| `UIProject/scripts/verify-ofgui-mcp.mjs` | MCP 自检客户端：`initialize` + `notifications/initialized` + `tools/list`，校验 stdout 无非协议内容 |
| `UIProject/scripts/ofgui-verify.cmd` | 一行式验证入口（解析 node 后跑上面的 mjs） |
| `UIProject/.gitignore` | 新增 `.ofgui.local.json`（本机路径，不提交） |
| `docs/technical/fgui_toolchain.md` | 换机接入规范（版本、依赖、路径发现、MCP 行为、换机 8 步、验证、风险） |

### 4.2 `UIProject` 旧配置修正

| 文件 | 变更 | 原因 |
| --- | --- | --- |
| `settings/Adaptation.json` | `1080×1920` 竖屏 → `1440×900`；设备改为 `设计尺寸 1440×900` / `高极限 1440×1080` / `宽极限 2560×1080` | 旧手游竖屏参数不适用于 PC 横屏 |
| `settings/Publish.json` | `path` `../Assets/Generated/ui` → `../artifacts/ui-publish`；`allowGenCode` 关闭、`codePath` 清空；`compressPNG` 开启 | 去掉 Unity 发布路径与 Unity 风格 C# 代码生成 |
| `assets/_Preview/package.xml` | 包级发布路径 `../Assets/Generated/preview~` → `../artifacts/ui-publish/preview`，去掉 `codePath` | 同上 |
| `settings/i18n.json` | `langFiles` 清空 | 指向不存在的 Unity `Assets/GameRes/Configs/languages/simplifiedChinese.xml` |
| `settings/Common.json` | `font` 清空、`fontScheme` 去掉悬空 `ui://` 字体、`fontSize` 24→18、`fontSizeScheme` 等比下调 | 字体引用指向不存在的包 `ccmhb643`；字号方案按 1440×900 重标 |
| `.objs/starmap-work/` | 删除 | 上一轮遗留的临时脚本/占位文件（`.objs/` 已在 gitignore 内） |

### 4.3 MainView 原生工程

包归属按 RULES：可复用叶子件进 `Common`，页面与页面级面板进 `MainGame`。

`Common`（8 个，均 `exported`）：

| 组件 | id | 结构要点 |
| --- | --- | --- |
| `Button_Tab` | `cbtn_tab` | Button 扩展 + `conSelected`（未选中/选中）+ 高亮与金色下划线 gearDisplay + `title` 文本 + 入场淡入动效 |
| `Button_Menu` | `cbtn_men` | GM / 设置 / 暂停 通用文本按钮 |
| `Bar_HP` | `cbar_hp0` | 底槽 + 血量填充 + 居中数值 |
| `Item_PartyRow` | `citem_prw` | 头像占位 + `txtName` + `txtLv` + 内嵌 `Bar_HP` + `txtCombat`；`conState` 切“正常/空席” |
| `Item_ExpeditionRow` | `citem_erw` | 头像 + `txtName` + `txtXp` + `txtLevel` + `txtAction` + 分隔线 |
| `Item_Scroll` | `citem_scr` | 图标占位 + `txtName` + `txtCount` |
| `Item_Potion` | `citem_ptn` | `conState`（待用/空槽）+ 名称/状态 + 内嵌 `btnDrop`（Button_Menu） |
| `Item_SkillTag` | `citem_skl` | 技能名 + 学习状态 |

`MainGame`（4 个，均 `exported`）：

| 组件 | id | 结构要点 |
| --- | --- | --- |
| `MainView` | `cmaiview` | 1440×900 页面；`conMode`（游戏/英雄/星图/地图）；`grpTopBar`/`grpRouteInfo`/`grpResource`/`grpStage`/`grpModePlaceholder`；`lstScroll`；三个面板实例；`tAppear` 动效 |
| `Panel_Expedition` | `cpnl_exp` | 标题 + `lstExpedition`（4 项，纵向滚动）+ 装备区 + 解锁条件四项 |
| `Panel_Party` | `cpnl_pty` | 标题 + `lstParty`（5 项：3 正常 + 2 空席，走 `conState,1`）+ 挑战等级/公共 XP/席位 + `lstSkills` |
| `Panel_Potion` | `cpnl_ptn` | 标题 + `lstPotion`（1 待用 + 3 空槽，走 `conState,1`） |

结构统计（`ofgui inspect`）：3 个包、12 个组件、93 个显示对象、23 个 gear、4 个控制器、2 个动效。

布局对照参考图（`docs/UE/MainView.png`）：顶栏（页签/标题/GM/设置/暂停）、路线与击杀信息、右侧资源数、战场区（占位）、右侧远征交互、速度倍率、卷轴横条、小队状态与药剂面板，均按信息结构落位。

## 5. 自主判断与依据

1. **工程类型保留 `MonoGame`**：FairyGUI Editor 没有官方 Godot 工程类型，不能假定存在。`MonoGame` 是编辑器真实支持的类型（`ofgui inspect` 读出 `projectType: 11`），产出纯资源、不绑定 Unity 目录结构与 C# 代码生成，适合“先出资源、后接 Godot 运行时”的过渡目标。未擅自改成别的技术栈。
2. **`.mcp.json` 用 `${workspaceFolder}` 而不是本机绝对路径**：读 IDE 扩展实现确认 stdio server 的 `command`/`args`/`cwd` 都会做变量替换，且 `cwd` 缺省就是工作区根。因此配置本身可跨机复用，机器差异全部由 `Find-Ofgui.ps1` 在运行时吸收。
3. **不用 PowerShell 当 MCP 启动层**：PowerShell 会把子进程 stdout 重新编码，破坏 JSON-RPC；`cmd` 只做进程启动、句柄原样透传。已用真实握手验证 stdout 纯净。
4. **`OPENFAIRYGUI_ALLOWED_PROJECT_ROOTS` 改为脚本内注入绝对路径**：该变量要求绝对根目录（分隔符 `;`），传 `"."` 会抛“lists no project roots”；且 `env` 的值不做变量替换。脚本以 `scripts\..` 的绝对路径注入，并保留用户已设值。
5. **补丁留在 ofgui 安装目录**：补丁是 ofgui 的运行期修复，不属于游戏仓库；仓库只记录版本组合与补丁清单（见工具链文档 §2.1）。
6. **页面级面板做成组件而不是纯分组**：三个面板内部结构自洽、后续英雄/星图页可复用，符合“需要独立复用或封装内部逻辑时使用组件”。
7. **动效只做 alpha 淡入且 `autoPlay=false`**：位移类动效若用 `value` 表达终点会把元素停在错位处，风险高于收益；先交付结构正确、可手动播放的动效，位移版留待有播放器联调时再加。
8. **列表项文字在编辑器预览里是模板默认值**：`<item title>` 只对 Button/Label 类条目绑定到子文本，`Item_*` 是自定义布局组件，逐项数据由程序渲染。为不违背 RULES 的 `txt*` 命名约定，未把文本节点改名成 `title`；此项在截图里表现为同类条目文字重复，属预期，不是绑定缺失。

## 6. 验证与证据

### 6.1 命令行

| 项 | 命令 | 结果 |
| --- | --- | --- |
| 工具发现 | `scripts\Find-Ofgui.ps1` | `结论: 工具链就绪`；node `v22.12.0`、ofgui `0.7.0`、Editor `2022.3.5.8607574` |
| 环境诊断 | `scripts\ofgui.cmd doctor .` | `status: ready`；原生图像 `0.34.5` 通过；`BACKEND_CONTRACT 3.0.0` / capability schema `15` |
| 工程校验 | `scripts\ofgui.cmd validate . --json` | `status: valid`、`complete: true`、`diagnostics: []` |
| 结构统计 | `scripts\ofgui.cmd inspect . --json` | 3 包 / 12 组件 / 93 显示对象 / 23 gear / 4 控制器 / 2 动效 |
| MCP 握手 | `scripts\ofgui-verify.cmd` | `openfairygui-mcp 0.7.0`、协议 `2024-11-05`、18 个工具、`stdoutClean: true`、握手约 1.0 s |
| 补丁状态 | `node patches/apply-ofgui-patches.cjs` | 两个补丁均 `already applied`，退出码 0 |
| 校验拦截能力 | 故意写 `selectionMode="0"` | `validate` 报 `invalid_project_value`（要求 `single/multiple/multipleSingleClick/none`），证明校验不是空转 |

### 6.2 路径可迁移性（受控模拟）

- `Find-Ofgui.ps1 -SaveLocal` 生成 `.ofgui.local.json` 后重跑，解析来源变为 `local-config`，路径一致。
- 用目录联接把 ofgui 暴露到 `artifacts/qa/ofgui-pathsim/ofgui`（另一个绝对路径），设 `OFGUI_HOME` 指向它后：解析来源 `env`、`ofguiHome` 变为新路径，`ofgui-verify.cmd` 与 `validate` 仍全部通过 → 证明配置不依赖固定盘符。
- **注意**：这属于模拟，不等价于另一台真实电脑。

### 6.3 编辑器实机

证据目录：`artifacts/qa/fgui-mainview/`（`artifacts/` 已被 gitignore）

| 文件 | 内容 |
| --- | --- |
| `shot01-open.png` | FairyGUI Editor 打开 `FGUIProject.fairy`，资源库列出 `Common` 8 个 + `MainGame` 4 个组件 |
| `shot08-mainview.png` | 双击打开 `MainView`：页签 `游戏/英雄/星图/地图`、标题、GM 按钮正确；显示列表列出 `grpTopBar`、`imgTopBarBg`、`btnTabGame (Button_Tab)`、`txtTitle`、`btnGM (Button_Menu)` 等节点 |
| `shot11-help.png` / `shot15-scroll.png` | 同一文档在更大可视范围下的渲染：右侧`远征交互`面板、卷轴横条、`小队状态`（含三条绿色血条与 `506/504`）、`药剂`面板（含`丢弃`按钮） |
| `crop-check.png` | 1:1 局部核对：顶栏深色底 + 金色标题 + `前往：冰原兽窟 / 击杀：1,553` 正常渲染 |
| `crop-toolbar.png` | 工具栏放大，用于定位预览/缩放控件 |

**已知缺口（如实记录）**：最后一次成功截图之后，为了让“当前页签”在视觉上更贴近参考图，又在 `MainView` 里加了两个节点
（`imgTabActiveBg`、`imgTabActiveLine`，位于 `btnTabGame` 之下）。这两个节点只经过 `ofgui validate` 结构校验，
**没有再取到渲染截图**：编辑器是 Unity IL2CPP 构建、没有自动化接口，本轮用坐标点击 + 截屏，操作几次之后合成输入不再被窗口接收
（疑似会话焦点/多显示器问题），无法稳定复现“打开文档”这一步。因此“顶部页签选中态”属于结构已验证、视觉未复核。

## 7. 未决项与风险

1. **跨设备未实测**：只在唯一一台电脑验证；换机流程只做了配置层与受控路径模拟。不得对外声称跨设备迁移已实测通过。
2. **MCP 面板需要人工允许一次**：`.mcp.json` 里的 server 属于项目级配置，受审批门禁；审批指纹包含 command/args/cwd/env，本次改动过配置，用户需在 MCP 面板重新允许 `openfairygui` 后才能在本会话内直接调用其工具。本轮的 MCP 可用性是用独立握手脚本证明的，不是靠 IDE 面板。
3. **页签选中态的视觉未复核**（见 6.3 缺口）。下次打开编辑器时确认：若同时出现“控制器高亮”和“静态高亮”两套叠加，删掉 `imgTabActiveBg`/`imgTabActiveLine` 其中之一即可。
4. **列表项预览文字为模板默认值**：截图里同类条目文字重复。若后续要求编辑器预览也逐项不同，需要把条目改为 Button/Label 扩展并用 `title` 绑定，届时要同步调整 RULES 的命名例外。
5. **`publish` 只能走 CLI**：0.7.0 的 MCP 后端把 `artifact.publish/restore` 标为 `bridge-required`，下一阶段做发布时必须用 `scripts\ofgui.cmd publish`。
6. **Godot 导入未开始**：本轮不产出任何 Godot 侧资源或运行时接入；`Publish.json` 的发布目标目前落在被忽略的 `artifacts/ui-publish`，正式接入时再决定产物落位。
7. **`_Preview` 包说明文字仍提到 Unity**（`RULES.md` 第 6 行）：该包目前为空，等真正放 Godot 侧占位预览图时再一并改写，避免本轮改动扩散。
8. **`git` 不在 PATH**：本轮未做任何提交；用户自行提交时若需命令行 git，请用 GitHub Desktop 或把 git 加进 PATH。

## 8. 验收状态

- 工程自检：**通过**（工具发现、MCP 握手、CLI 校验、结构统计、编辑器打开与渲染均已取得证据；缺口见 6.3 与第 7 节）。
- 设计 AI 复核：**不适用**（无设计交接；MainView 直接依据 `docs/UE/MainView.png` 参考图实现）。
- 用户验收：**待用户确认**，重点看两项：① MCP 面板允许后 `openfairygui` 工具是否可用；② 顶栏页签选中态与参考图是否一致（见未决项 3）。
