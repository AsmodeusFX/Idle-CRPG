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

