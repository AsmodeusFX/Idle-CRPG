# 美术资源管线（PixelLab → Aseprite → Godot）

## 0. 状态与标记

本文件是 2026-10-27 POC 验证后的**最小工程约定归档**，不是正式美术规范，也不代表生产验收通过。正式工程当前没有任何美术资源（`Idle-CRPG/Assets/` 只有 `Audio/`），本文件不创建目录、不改插件、不动场景。

标记：

- **[E]** 本轮已实测通过（Windows + Godot 4.7.2 mono + AsepriteWizard 9.8.0 + Aseprite 1.3.18.6-dev）
- **[P]** 推荐生产约定，尚未在正式工程落地，采用需另行批准
- **[O]** 未验证项，不能据此推断可行

证据与逐项矩阵见 [PixelLab MCP 与资源管线执行归档](../reviews/2026-10-27_pixellab_mcp.md) §9。

## 1. 源文件管理

- **[E]** PNG → `.aseprite` → PNG 往返像素无损：64×64 透明底药水，可见像素 1332 / 1332、颜色数 30 / 30、差异 0。
- **[E]** `.aseprite` 可用 Aseprite 脚本批量读写（改色 598 px、改帧时长后保存均生效）。
- **[P]** `.aseprite` 是唯一真源，入库；由它导出的中间 PNG/JSON 视为构建产物，不入库（插件默认 `remove_json_file=true` 会清理 JSON）。
- **[P]** 生成与精修的工作区沿用仓库根 `ArtProject/<Category>/`，分类与现有空目录一致：`Characters/`、`Effects/`、`Environment/`、`Monsters/`、`UI/`；PixelLab 导出结构 `<角色>/<动画>/rotations/*.png` + `metadata.json` 保持原样以便回溯生成参数。
- **[P]** AI 生成物先落 `ArtProject/`，经精修确认后才进入 Godot 导入目录，不直接写入 `Idle-CRPG/Assets/`。
- **[O]** 多图层、图层组、切片（slice）在 `.aseprite` 内的组织约定未验证。

## 2. 素材导入路径

- **[P]** 正式导入目录拟为 `Idle-CRPG/Assets/Art/<Category>/`，与现有 `Assets/Audio/` 同级同构；**该目录尚未建立**。
- **[E]** 素材文件旁生成 `x.aseprite.import`，导入缓存写入 `.godot/imported/x.aseprite-<hash>.res`（静态 998 B、动画 1393 B 量级）。
- **[E]** 运行时 `load("res://.../x.aseprite")` 直接得到导入结果：静态 `PortableCompressedTexture2D`（64×64，`resource_path` 指回 `.aseprite`），动画 `SpriteFrames`。
- **[P]** 场景只引用 `.aseprite`（或其 uid），禁止引用导出的 PNG，避免两处真源。
- **[O]** 导出包（export template）对 `.res` 的加载与协作时的缓存策略未验证。

## 3. Aseprite Wizard 必要参数

- **[E]** 插件版本 `9.8.0`；Aseprite 可执行文件路径在**编辑器设置** `aseprite/general/command_path`（全局，不在工程内，CI/换机需预置）。
- **[E]** 项目设置 `aseprite/import/import_plugin/enable_automatic_importer=true` 才会自动导入；`aseprite/import/import_plugin/default_automatic_importer` 决定默认导入器（`Static Texture` / `SpriteFrames` / …）。
- **[E]** 导入器 id 与产物类型：静态 `aseprite_wizard.plugin.static-texture` → `PortableCompressedTexture2D`（默认 `first_frame_only=true`）；动画 `aseprite_wizard.plugin.spriteframes` → `SpriteFrames`。
- **[P]** **必须显式设 `animation/round_fps=false`**。默认 `true` 会把帧时长取整改写：实测 300 ms/帧变成 `fps=4.0`（250 ms）；关闭后 `fps=3.3333`（= 1/0.3）。
- **[P]** 同一目录混放静态图与动画时，逐文件在 `.import` 的 `[params]` 固定导入器，不依赖项目默认值切换。
- **[E]** 动画标签即动画名：`Idle` 标签 → `SpriteFrames` 中 `animation="Idle"`，`loop=true`（项目设置 `aseprite/animation/loop/enabled` 默认开）。
- **[O]** 混合不等帧长（如 100/300 ms）、`layer/split_layers`、`Tileset Texture` 导入器未验证。

## 4. 外部工具定位

- **PixelLab（MCP）**：只产出候选像素资产。[E] 连接、鉴权与单张 64×64 生成已实测（1 代/张）；[O] `create_map_object` 的 8 小时清理只有官方描述，未实测过期行为。接入细节见 [PixelLab MCP 接入规范](pixellab_mcp_setup.md)。
- **Aseprite**：唯一精修与原生工程载体。批处理用 `-b --script`；Aseprite 是 GUI 子系统程序，**必须 `Start-Process -Wait`**，否则 PowerShell 不等进程结束，会出现"无产物"的假失败。[E]
- **Godot + Aseprite Wizard**：只消费 `.aseprite`，不反向写源文件。[E]
- **[P]** 三者职责边界：AI 生成不代替美术评审；Aseprite 精修不依赖程序；Godot 端不手工挂 PNG 补丁。

## 5. 重导入验收要求

- **[E]** 改源 `.aseprite` 后执行 `--import`，**场景无需修改**即可显示新内容（静态改色 598 px、动画改色 1175 px + 改 150 ms 均验证）。
- **[P]** 每次导入后至少断言：
  1. `.import` 的 `importer` 以 `aseprite_wizard.` 开头，`type` 与预期一致；
  2. `.godot/imported/*.md5` 里的 `source_md5` 等于源文件当前 MD5（**内容未变时 mtime 不更新，不能用时间戳判断新鲜度**）；
  3. 动画 `fps ≈ 1/帧时长`，动画名等于标签名，`loop` 符合预期；
  4. 静态图尺寸与可见像素数与源一致。
- **[P]** 保留视觉证据：真实窗口渲染截图；动画取固定间隔采样帧或录制。
- **[P]** 自动化入口统一用 `tools/art/Test-ArtAssetPipeline.ps1`（见 §6），人工结论写回模块 Review。
- **[O]** 未接入 CI；未做导出包内加载校验。

## 6. 复测工具与命令

工具在 `tools/art/`（`artifacts/` 被 Git 忽略，不能作为跨机复现入口）：

```powershell
powershell -File tools/art/Test-ArtAssetPipeline.ps1 -Mode Env
powershell -File tools/art/Test-ArtAssetPipeline.ps1 -Mode Roundtrip -Source <png> -OutDir <dir>
powershell -File tools/art/Test-ArtAssetPipeline.ps1 -Mode Import -Project <godot 工程目录>
```

- `Env`：探测 Aseprite / Godot 版本、插件版本、编辑器里的 Aseprite 命令路径。
- `Roundtrip`：PNG ↔ `.aseprite` 往返 + 逐像素比对，输出 `*_roundtrip_report.json`。
- `Import`：headless 导入 + 逐文件 importer/type/缓存 md5 核验，输出 PASS/FAIL。
- 动画播放、fps、循环需运行场景读取 `SpriteFrames`，不在脚本覆盖范围（场景级人工验收）。
- 脚本含中文，须保存为 UTF-8 with BOM，否则 Windows PowerShell 5.1 按 ANSI 解析会报语法错误。

## 7. 未验证清单与风险

| 编号 | 项 | 说明 |
| --- | --- | --- |
| [O]-1 | 混合不等帧长 | 等时长路径已通；`round_fps=false` 下非整数 fps 的行为只验证了单一等长场景 |
| [O]-2 | 多图层 / split layers / tileset | 仅验证 Static Texture 与 SpriteFrames |
| [O]-3 | 导出包加载 | 只在编辑器/源码运行下验证 |
| [O]-4 | macOS / Linux | 仅 Windows 实测；脚本像素比对依赖 System.Drawing |
| [O]-5 | 批量规模 | 单文件增量 5.07 s、冷启动全量 16.19 s 已测；数十个资源的批量成本未测 |
| [O]-6 | 正式目录落地 | `Idle-CRPG/Assets/Art/` 尚不存在，路径约定未经正式工程验证 |

风险：帧时长精度依赖插件参数（默认会静默改写）；导入常驻依赖本机 Aseprite 与编辑器全局设置；导入产物依赖 `.godot/imported` 缓存；Aseprite 侧 PixelLab 扩展在批处理模式固定报 `handle-pose.lua` 错误（不影响产物，但污染日志）。

## 8. 追溯

- 执行与证据：[2026-10-27 PixelLab MCP 与资源管线归档](../reviews/2026-10-27_pixellab_mcp.md)
- MCP 接入：[PixelLab MCP 接入规范](pixellab_mcp_setup.md)
- 复测工具：[tools/art/README.md](../../tools/art/README.md)
