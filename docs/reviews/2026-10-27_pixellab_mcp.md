# PixelLab MCP 接入（用户级工具链）

## 1. 需求与授权

- 需求来源：用户直接下达（非设计 AI 交接）。2026-10-27 要求"检索 pixellab.ai/mcp，确认 CodeBuddy 如何接入并实施"，随后要求"在 docs/technical 下生成接入规范，方便换机快速接入"。
- 授权范围：调查第三方 MCP 服务事实、确认客户端接入方式、修改本机用户级配置、真实连通验证、编写工程内技术文档。属于实施授权内的调查与实现，未涉及游戏逻辑、数值、存档与正式配置。
- 未授权事项：未执行 Git commit / push / merge；未把 token 写入仓库；未生成或引入任何美术资产到 `Idle-CRPG/`。

## 2. 规则覆盖

- 文档落位遵循 AGENTS：`docs/technical/` 放字段合同与架构类规范，单文件不建子目录；索引同步到 `docs/README.md`。
- 未新增临时统计报告，故不适用 `docs/data/` 的单份 Markdown 约定。
- 未改动 `Idle-CRPG/config/`、Core、UI、存档与 QA 隔离逻辑，因此不触发 Core Checks 与 Godot 冒烟；本章验证为"外部服务连通性 + 配置文件合法性"。

## 3. 实际变更

1. 新增 `docs/technical/pixellab_mcp_setup.md`：服务端事实、CodeBuddy 配置位置与传输/变量替换限制、标准配置、换机 8 步、PowerShell/bash 验证脚本、故障排查、卸载回退、CLI 可选写法、安全合规。
2. 更新 `docs/README.md` 索引，登记该文档。
3. 本机配置（工程外）：`C:\Users\MAR\.codebuddy\mcp.json` 新增 `pixellab` 服务器（`type: http`，端点 `https://api.pixellab.ai/mcp`，`headers.Authorization` 直填用户 token，`defer_loading: true`）。该文件属用户目录，不进仓库。
4. 调查用临时脚本写在 `artifacts/qa/`，用完即删，未留残留。

## 4. 自主判断与依据

- **token 直填而非环境变量占位**：读 IDE 扩展 `McpTransportManagerImpl` 实现，`getBuiltinVariables()` 仅含 `workspaceFolder`/`workspaceFolderBasename`/`userHome`，`url`/`headers` 的 `${VAR}` 仅在此基础上合并该服务器自身 `env` 块，不含系统环境变量；用 `${PIXELLAB_API_TOKEN}` 会原样发送导致 401。
- **`type: http` 而非 `sse`**：同一实现中"配置存在 `url` 即构造 `StreamableHTTPClientTransport`"，且实测端点 POST 返回 `text/event-stream`，无独立 SSE 端点。
- **`description` 改纯 ASCII**：Windows 控制台按本地代码页读取时中文描述显示为乱码，虽文件本身为 UTF-8，仍改用英文描述消除歧义。
- 官方页面仅给 Claude Code 命令，其他客户端 JSON 由页面端点与鉴权信息推导后实测验证，文档已标注推导来源，未冒充官方示例。

## 5. 验证与证据

| 项 | 方法 | 结果 |
| --- | --- | --- |
| 端点可达 | POST `initialize` | 200，`PixelLab MCP Server 0.2.0`，协议 `2025-06-18` |
| 免 token 握手 | 无鉴权 `initialize` / `tools/list` | 均 200；`tools/list` 返回 103 个工具 |
| 鉴权必要性 | 无鉴权 `tools/call get_balance` | `401: Missing Authorization header`（HTTP 200，`isError: true`） |
| 配置生效性 | 带配置文件内 token 调 `tools/call` | `get_balance` 成功；`tools/list` 返回 118 个工具 |
| 账号数据可读 | `list_projects` / `list_characters` | 0 个项目；1 个角色（8 方向 48×48） |
| 配置合法性 | `ConvertFrom-Json` 解析 + UTF-8 读回 | 通过 |
| 客户端读取路径 | 扩展日志 `getMcpSettingsFilePath result:` | 确认为用户级 `~/.codebuddy/mcp.json` |

未验证项（明确标注）：

- MCP 面板绿灯状态与对话内工具注入：配置在会话开始后写入，本机未重启客户端，未取得界面证据。
- `defer_loading` 是否被 IDE 版本识别：扩展代码未检索到该字段，CLI 文档有定义。
- 计费费率：官方页面未列，仅读到账户额度（`trial`，剩余 35.90/40 代）。
- 生成质量/风格匹配度：未做实际生成，未消耗额度。

## 6. 未决项

1. 用户重启 CodeBuddy 后确认 MCP 页状态与对话内调用，若面板报错需按文档第 7 节回查。
2. 若后续把 PixelLab 产物纳入正式美术流程，需另行按资源规范与美术评审立项，不在本次授权内。
3. token 为明文存储于用户目录；建议在站点轮换策略变化时同步更新文档第 10 节。

## 7. 验收状态

- 工程自检：通过（文档 + 索引 + 配置 + 连通性实测完成；未运行游戏侧测试，原因见第 2 节）。
- 设计 AI 复核：不适用（无设计交接，无玩法/数值影响）。
- 用户验收：待用户重启客户端确认面板状态与对话内工具可用后回填。

## 8. 第 2 轮：只读核验 + 单张素材生成测试（同日追加）

需求：用户要求先只读验证连接与额度，再用免费套餐支持的工具生成一张 64×64 像素风药水 PNG，存独立测试目录；不得改动正式美术与 Godot 工程，不得购买额度。

### 8.1 只读核验

`get_balance`（免费）四次采样：生成前 `generations_remaining 35.90 / used 4.10 / credits $0.00 / trial`；生成后 `34.90 / 5.10`，`credits` 不变。

### 8.2 尝试免费绘制工具（失败，记录为服务端问题）

`pixelart_workbench` 为零成本绘制接口。按 `describe draw` 的说明构造 64×64 单帧配方（`grid` 操作逐像素绘制药水，调色板 9 色），被拒：

```
Invalid view timeline — every view needs one entry per frame (job.frame_count is 1)
```

逐步排查的组合（全部失败，均未渲染）：

| 变体 | 结果 |
| --- | --- |
| 静帧仅 `nodes`（文档所述写法） | `Invalid view timeline` |
| `poses {rest:{}}` + `frames ["rest"]` | 同上 |
| `poses` 列表 / `poses` 含节点条目 / `frames` 用整数 | 同上 |
| `tracks` + `keys [[1,[0,0]]]`（帧数 1 与 2 均试） | 帧数 1/2 都报同一错误（改用变量名探测时曾回报 `Track keys are [frame, value] pairs`，改写为规范键值后仍失败） |
| 极简 `rect` 形状、无 `tracks`/`poses` | 同上 |
| 仅 `frames` 无 `poses` | 明确报"neither `tracks` nor `poses`"，说明该分支可被读到，但补齐后仍不通过 |

结论：该接口当前对 `view timeline` 的校验与自身文档不一致，`draw` 不可用；`search_help` 亦无对应条目（返回 `no information found`）。判定为服务端缺陷，不是本机配置问题。

### 8.3 实际采用的生成方式

`create_image_pixflux`（64×64，`no_background: true`），prompt 为红色治疗药水玻璃瓶。返回 `cost: 1 generation`、`asset_status: pending`，约 20 s 完成（`charged: 1 generation`），产物同步进账号画廊并提供下载地址。

### 8.4 产物与校验

- 本地路径：`artifacts/qa/pixellab-mcp-test/potion_64x64.png`（独立测试目录，`artifacts/` 已被 .gitignore 忽略）。
- 文件：1731 字节，PNG 签名 `89504E470D0A1A0A`，IHDR `64x64`，colorType 6（RGBA，带透明通道），SHA-256 `5DF13800C3CCFF058D78479725948615E012E53C41E0193AF81D296A0A8766E5`。
- 服务端副本：job `7d0cf075-c812-46f2-9b35-7c7f7cdd7c0d`，download `https://api.pixellab.ai/mcp/images/7d0cf075-c812-46f2-9b35-7c7f7cdd7c0d/download`。
- 视觉：红色药水 + 玻璃瓶 + 木塞，透明底，符合 64×64 像素风。

### 8.5 额度消耗与异常

- 实际消耗：**1.00 代**（35.90 → 34.90），credits `$0.00` 未变，未购买额度。
- 异常：`pixelart_workbench draw` 全类型配方被拒（见 8.2）；`get_image` 轮询过程中进度从 24% 回落到 3% 并给出 `eta ~463s`，约 20 s 后直接 `completed`，进度条非线性但最终成功。
- 未改动：`Idle-CRPG/` 工程、正式美术、配置 CSV、存档；测试脚本用完即删，仅保留 PNG。

### 8.6 文档同步

`docs/technical/pixellab_mcp_setup.md` 增补 §2.1（实测成本与 `draw` 不可用）与 §7 对应排查行。

## 9. 第 3 轮：Steam D2｜PixelLab → Aseprite → Godot 资源管线验证（同日追加）

任务性质：**独立技术可行性验证，不授权正式产品改造**。用户明确要求：只用已有素材，不调用 PixelLab、不消耗生成额度，不修改正式游戏代码/美术/场景/存档/UI，不升级正式工程插件。

### 9.1 环境与版本（只读调查）

| 项 | 值 | 来源 |
| --- | --- | --- |
| Aseprite | `D:\Program Files\Aseprite\aseprite.exe`，`1.3.18.6-dev`（开发版） | `--version` |
| Godot | `D:\Program Files (x86)\Godot\Godot_v4.7.2-stable_mono_win64.exe`，`4.7.2.stable.mono.official.ed1daf0bf` | `--version`，另用 `_console.exe` 捕获 stdout |
| Aseprite Wizard | `9.8.0`（`Idle-CRPG/Idle-CRPG/addons/AsepriteWizard/plugin.cfg`） | 只读读取 |
| 编辑器设置 | `aseprite/general/command_path = D:\Program Files\Aseprite\aseprite.exe` 已存在 | `%APPDATA%\Godot\editor_settings-4.7.tres`，未改写 |
| 渲染 | gl_compatibility，视口 320×180，窗口 960×540，`canvas_items` 拉伸，nearest 过滤 | 测试工程 `project.godot` |

隔离目录：`artifacts/qa/pixellab-aseprite-godot/`

- `godot_project/`：独立 Godot 工程（`addons/AsepriteWizard` 为正式插件 9.8.0 的**只读副本**，`assets/`、`scenes/`、`project.godot`）
- `work/`：Aseprite 导出与精灵表；`evidence/`：截图、采样帧、GIF、AVI；`logs/`：全部命令输出
- `lua/`：Aseprite 脚本；根目录 `*.ps1`：可重复运行的入口脚本

正式工程未写入：仅 `Copy-Item` 读取插件目录，未改动 `Idle-CRPG/` 任何文件。

### 9.2 A 阶段：静态资源链路

| 步骤 | 命令 / 方法 | 实测结果 |
| --- | --- | --- |
| PNG → `.aseprite` | `aseprite -b potion_source.png --save-as potion.aseprite` | exit 0；1319 B；文件头 magic `0xA5E0`，frames 1，`64x64`，RGBA(32) |
| `.aseprite` → PNG | `aseprite -b potion.aseprite --save-as potion_roundtrip.png` | exit 0；1759 B（原 1731 B，仅压缩差异） |
| 像素一致性 | `compare_pixels.ps1`（System.Drawing 逐像素含 alpha） | 尺寸一致；可见像素 1332 / 1332；**差异 0**；颜色数 30 / 30 → `PIXEL_IDENTICAL` |
| 原生工程可编辑 | Lua 脚本打开同一 `.aseprite`，改 598 像素并保存 | 保存成功、再次导入生效 |
| 自动纹理导入 | `godot --headless --path proj --import` | `.import` 中 `importer="aseprite_wizard.plugin.static-texture"`、`type="PortableCompressedTexture2D"`；缓存 `.res` 998 B |
| 运行时读取 | 场景 `load("res://assets/potion.aseprite")` | `class=PortableCompressedTexture2D`，`size=(64,64)`，`resource_path` 指回 `.aseprite` |
| 真实场景显示 | 窗口模式运行 `static_view.tscn`，视口抓图 | `evidence/static_view.png`（960×540），红色药水可见 |
| 改色后重导入 | Lua 把饱和红系（598/1332 px）改为蓝系 → `--import` → **场景不变**重跑 | `.res` 重建（1010 → 998 B）；同一引用显示**蓝色**药水（`evidence/static_view.png` 已更新） |

结论：`.aseprite` 作为唯一真源可用；改源文件后无需改场景即生效，说明读取的是 `.aseprite` 而非旁路 PNG（旁路 `potion_source.png` 仍在目录中但未被场景引用）。

### 9.3 B 阶段：动画资源链路

| 步骤 | 方法 | 实测结果 |
| --- | --- | --- |
| 制作动画（不调用 PixelLab） | `lua/make_idle_anim.lua`：64×64、2 帧、液体高度与气泡位置不同、`Idle` 标签、300 ms/帧 | `ANIM frames=2 tags=1 tag=Idle 1-2`；`1173 B` |
| 动画元数据核对 | `aseprite -b --sheet ... --data ... --format json-hash --list-tags` | 2 帧各 300 ms；`meta.frameTags = {name:Idle, from:0, to:1, direction:forward}`；sheet 128×64 |
| 导入为 SpriteFrames | 工程默认导入器切到 `SpriteFrames` 后 `--import` | `importer="aseprite_wizard.plugin.spriteframes"`、`type="SpriteFrames"`；缓存 `.res` 1393 B |
| 运行时数据结构 | `load("res://assets/potion_anim.aseprite")` | `class=SpriteFrames`；`animations=["Idle"]`；`frames=2`；`loop=true` |
| 帧时长保真 | 默认 `animation/round_fps=true` | `fps=4.0`（300 ms 被取整改写为 250 ms） |
| 帧时长保真（修正） | 把 `.import` 改为 `animation/round_fps=false` 后重导入 | `fps=3.3333`（= 1/0.3，300 ms 精确保留） |
| 播放与循环 | `AnimatedSprite2D.play("Idle")`，每 0.2 s 采样 8 次 | 帧序 1,1,0,1,0,1,1,0；`playing` 全程 true → 两帧循环成立 |
| 修改后重导入 | `lua/modify_anim.lua`：液体转绿（1175 px）+ 帧时长 150 ms → `--import` → 场景不变重跑 | `fps=3.3333 → 6.6667`；采样截图液体变绿 |
| 播放证据 | `--write-movie` + 视口采样 | `work/playback.avi`（106 帧 @60 fps，2.75 MB）；`evidence/anim_00..07.png`；`evidence/anim_playback.gif`（15 帧，Aseprite 合成） |

### 9.4 性能实测

| 场景 | 耗时 |
| --- | --- |
| 冷态全量重导入（清空 2 个 `.aseprite` 缓存后 `--import`，含编辑器启动） | 16.19 s |
| 单文件增量重导入（触碰 1 个 `.aseprite`） | 5.07 s |
| 单文件体积 | 源 PNG 1.7 KB；`.aseprite` 1.2–1.3 KB；导入 `.res` 1.0–1.4 KB |
| 场景启动到抓图 | < 2 s（含窗口创建） |

每次导入都会重新调用 Aseprite CLI 导出，增量成本基本等于 Godot 编辑器启动 + 每文件一次 Aseprite 导出。

### 9.5 错误与警告清单

1. Aseprite CLI **每次运行**打印 4 行 `D:\Program Files\Aseprite\extensions\pixellab\handle-pose.lua:58: attempt to index a nil value (local 'dlg')`。这是 Aseprite 侧 PixelLab 扩展在批处理模式下初始化对话框失败的报错，**与本任务无关**，退出码仍为 0，导出产物正确。
2. Aseprite 版本为 `1.3.18.6-dev` 开发版，非稳定发行版；官方 CLI 行为可能随构建变化。
3. Godot headless 导入打印 `.NET: Failed to load project assembly`：mono 构建 + 纯 GDScript 测试工程（无 `.csproj`），无实际影响。
4. `Failed to bind socket. Error: 3`（headless 编辑器单实例套接字），无影响。
5. 首次用 `& aseprite.exe ...` 直接调用时 PowerShell 未等待 GUI 进程返回，出现"无输出、无产物"的假失败；改用 `Start-Process -Wait` 后正常。**自动化脚本必须显式等待**。
6. `SpriteFrames.get_frame_duration()` 恒返回 `1.0`（等时长场景下以 `fps` 表达时长），不能用它逐帧校验毫秒级时长。
7. 本机无 ffmpeg，`playback.avi` 未转 GIF/MP4；动态证据以 AVI + 采样帧 + 合成 GIF 提供。

### 9.6 逐项测试矩阵

| ID | 验收项 | 结果 | 证据/说明 |
| --- | --- | --- | --- |
| A1 | 只读调查 Aseprite / Godot / 插件版本 | **PASS** | §9.1 |
| A2 | 隔离测试目录 + 独立 Godot 工程 | **PASS** | `artifacts/qa/pixellab-aseprite-godot/`，插件为只读副本 |
| A3 | PNG → `.aseprite` → PNG 往返 | **PASS** | 两次 exit 0，像素差异 0 |
| A4 | 尺寸/透明通道/可见像素一致 | **PASS** | 64×64、RGBA、可见 1332=1332、差异 0 |
| A5 | 原生工程可编辑 | **PASS** | Lua 改 598 px 并保存，重导入生效 |
| A6 | 自动纹理导入器读取 `.aseprite` | **PASS** | `aseprite_wizard.plugin.static-texture` → `PortableCompressedTexture2D` |
| A7 | Godot 场景真实显示 + 截图 | **PASS** | `evidence/static_view.png` |
| A8 | 改色后自动重导入 + 原引用更新 | **PASS** | 红→蓝，场景未改 |
| A9 | 错误/警告/性能/限制收集 | **PASS** | §9.4、§9.5 |
| B1 | 不调用 PixelLab、不消耗额度 | **PASS** | 全程无 MCP 生成调用；余额 34.90 → 34.90 |
| B2 | 脚本生成 2 帧 + `Idle` 标签动画 | **PASS** | 300 ms/帧，`Idle` 0-1 forward |
| B3 | 导入 SpriteFrames 并挂 AnimatedSprite2D | **PASS** | `aseprite_wizard.plugin.spriteframes` |
| B4 | 播放/循环/标签/帧时长 | **PASS（含限制）** | 循环与标签正确；时长需 `round_fps=false` 才保真 |
| B5 | 修改后重导入 | **PASS** | fps 3.333→6.667、颜色变绿 |
| B6 | 播放截图/短录屏 | **PASS** | 8 张采样 + `playback.avi` + GIF |
| X1 | 混合不等帧长（如 100/300 ms）保真 | **BLOCKED/未测** | 等时长路径已通；混合时长未构造 |
| X2 | 多图层 / split layers / tileset 导入器 | **未测** | 本次只验证 Static Texture 与 SpriteFrames |
| X3 | 导出包（export template）加载 `.aseprite` 资源 | **未测** | 仅编辑器/源码运行验证，未做导出构建 |
| X4 | macOS / Linux 复跑 | **未测** | 仅 Windows 实测 |
| X5 | 批量（数十个）动画资源首次导入耗时 | **未测** | 单文件 5 s 已测，批量外推未验证 |

### 9.7 对正式生产管线的建议与剩余风险

建议：

1. **统一导入参数**：把 `animation/round_fps=false` 作为项目约定，否则 300 ms 会被改写成 250 ms；导入后应断言 `fps ≈ 1/帧时长`。
2. **静态与动画分离策略**：静态图标走 `Static Texture`（`first_frame_only=true`），动画走 `SpriteFrames`；同一目录混放时用逐文件 `.import` 明确导入器，不依赖项目默认值。
3. **`.aseprite` 作为唯一真源**：把导出的中间 PNG/JSON 视为构建产物（插件默认 `remove_json_file=true` 会清理），不入库。
4. **CI / 换机预置**：Aseprite CLI 路径在 EditorSettings（全局），不在工程内；CI 需预置 `editor_settings-4.7.tres` 或启用插件的 bake 文件回退（`generate_bake_files`）。调用 Aseprite 必须 `Start-Process -Wait`。
5. **导入是同步阻塞**：单文件约 5 s，导入 60 个动画预计 5 分钟量级（外推，未实测），建议资源按模块分批提交，避免一次性大批量改动。
6. **接入正式工程前先清理 Aseprite 侧扩展报错**：`extensions/pixellab/handle-pose.lua` 在批处理模式报错，虽不影响产物，但会污染日志、干扰真实错误识别。

剩余风险：

1. 帧时长精度依赖插件参数，默认值会静默改写时长；若美术与程序对节奏敏感，需在验收脚本中显式核对。
2. 常驻依赖本机 Aseprite 安装与版本（当前为 dev 构建），换机或升级可能改变导出行为。
3. 导入产物是 `PortableCompressedTexture2D` / `SpriteFrames` 的 `.res`，依赖 `.godot/imported` 缓存；发布与协作流程需确认缓存/导入策略（未测）。
4. 多图层、切片（slice）、tileset、8 方向角色等更复杂资产未验证，不能据本结论推断可全量自动化。
5. 本次仅在 Windows + Godot 4.7.2 mono + 插件 9.8.0 组合下成立。

### 9.8 复跑方法

```
# 1. 环境与素材准备（只读复制插件、复制已生成 PNG）
powershell -File artifacts/qa/pixellab-aseprite-godot/setup.ps1

# 2. PNG ↔ aseprite 往返与像素比对
powershell -File .../aseprite_roundtrip.ps1
powershell -File .../compare_pixels.ps1 -PathA <原PNG> -PathB <回导PNG> -Label roundtrip

# 3. 导入（默认导入器在 project.godot 中切换）
powershell -File .../run_import.ps1 -LogName godot_import

# 4. 真实窗口渲染 + 截图
powershell -File .../run_scene.ps1 -Scene res://scenes/static_view.tscn -LogName run_static
powershell -File .../run_scene.ps1 -Scene res://scenes/anim_view.tscn   -LogName run_anim

# 5. Aseprite Lua：改色 / 造动画 / 改动画
powershell -File .../run_lua.ps1 -Script .../lua/recolor_potion.lua -LogName recolor
powershell -File .../run_lua.ps1 -Script .../lua/make_idle_anim.lua  -LogName make_anim
powershell -File .../run_lua.ps1 -Script .../lua/modify_anim.lua     -LogName modify_anim

# 6. 性能测量
powershell -File .../measure_import.ps1        # 冷态全量
powershell -File .../measure_incremental.ps1   # 单文件增量
```

### 9.9 验收状态

- 工程自检：**通过（A 全 PASS，B PASS 含 1 项参数限制；X1–X5 未测并已标注）**。正式工程、正式美术、存档、UI 均未改动；未升插件；未消耗生成额度。
- 设计复核：**未进行**。本轮是技术可行性验证，不产出玩法/数值方案；如需纳入正式美术流程需设计侧另立议题。
- 用户验收：**待用户确认**。重点待确认项：帧时长是否要求毫秒级保真、成批资源导入是否接受同步阻塞、是否允许把 Aseprite 纳入正式构建依赖。

### 9.10 可回传设计 AI 的摘要

1. `.aseprite` 可直接作为 Godot 的素材真源：静态图标与多帧动画都能由 Aseprite Wizard 9.8.0 自动导入，改源文件后场景引用自动更新，不需要手动重挂资源。
2. 静态链路像素无损（往返差异 0，透明通道保留），原生工程可用脚本批量编辑，适合"AI 生成 PNG → 转原生工程 → 美术微调"的分工。
3. 动画链路可用：标签即动画名（`Idle`），循环默认开启；但插件默认 FPS 取整会把 300 ms 改成 250 ms，**必须显式关闭取整**才能保真。
4. 导入是同步、依赖本机 Aseprite 的本地过程：单文件约 5 s，冷启动 16 s；不联网、不消耗 PixelLab 额度。
5. 现有限制：只验证了 Windows + Godot 4.7.2 + 插件 9.8.0；混合帧长、多图层、tileset、导出包加载均未测，不能视为全量自动化结论。
6. 对产品侧的含义：把"AI 出图"与"Aseprite 精修 + Godot 导入"拆成两段是可行的，精修阶段不需要程序介入；但资源节奏（帧时长）与批量导入成本需要纳入排期。

### 9.11 设计 AI 复核结论（经用户转达）

- 接收方式：由用户在 2026-10-27 直接转达，**非本工程自评，也无设计 AI 侧原文回执**；因此本工程只登记"已收到该结论"，不声称已与设计 AI 直接同步。
- 结论：`PASS_WITH_OPEN_ITEMS`。
- 对应开放项：§9.6 的 X1–X5（混合帧长、多图层/切片/tileset、导出包加载、macOS/Linux、批量规模），与 [art_asset_pipeline.md §7](../technical/art_asset_pipeline.md) 的 `[O]-1`–`[O]-6` 一致。
- 复核附带边界：本结论只覆盖"资源管线是否可自动跑通"的技术可行性，不构成正式美术管线验收，不解除 §9.9 中的用户验收项。

### 9.12 本轮归档变更（最小化，仅文档与复测工具）

| 类型 | 变更 |
| --- | --- |
| 新增 | `docs/technical/art_asset_pipeline.md`：源文件管理、导入路径、Aseprite Wizard 必要参数、外部工具定位、重导入验收要求；按 `[E]` 已验证 / `[P]` 推荐约定 / `[O]` 未验证分档 |
| 新增 | `tools/art/Test-ArtAssetPipeline.ps1`：`Env` / `Roundtrip` / `Import` 三模式复测入口（不写死盘符，凭据零依赖） |
| 新增 | `tools/art/README.md`：用法、边界、UTF-8 BOM 与 `Start-Process -Wait` 两个坑位 |
| 更新 | `docs/README.md`：登记上述两份文档 |
| 更新 | 本 Review：§9.11 复核结论、§9.12 变更清单 |
| 未改 | 正式 Godot 工程（`Idle-CRPG/`）、正式美术目录、场景、存档、UI、插件版本、配置 CSV；未生成正式美术资源；未新增独立 SKILL |

复测工具的入库原因与自检：`artifacts/` 在 `.gitignore` 中，POC 脚本无法随仓库跨机复现；`tools/art/` 版本实测三种模式均通过——`Env` 正确识别 Aseprite 1.3.18.6-dev、Godot 4.7.2、插件 9.8.0 与编辑器命令路径；`Roundtrip` 输出 `PIXEL_IDENTICAL`（差异 0）；`Import` 对两个测试 `.aseprite` 输出 `importer=aseprite_wizard.*`、`cacheFresh=True`、`PASS`。导入新鲜度改用 `.godot/imported/*.md5` 的 `source_md5` 比对，因为内容未变时缓存 mtime 不更新，时间戳判定会误报。

### 9.13 三态验收（最新）

- 工程自检：`PASS_WITH_OPEN_ITEMS`（A/B 链路证据完整；开放项为 X1–X5；文档与工具已入库自检通过）。
- 设计复核：`PASS_WITH_OPEN_ITEMS`（经用户转达，见 §9.11）。
- 用户验收：**待确认**（帧时长精度要求、批量导入阻塞可接受度、Aseprite 是否纳入正式构建依赖）。

