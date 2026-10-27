# PixelLab MCP 接入规范

## 1. 用途与边界

PixelLab 提供远程 MCP 服务，把像素美术生成能力挂到 AI 客户端里：角色 4/8 方向、角色动画、等距地块、俯视/横版 Wang 图块集、地图物件、通用图像生成与编辑、字体、UI 面板、地图编辑与项目 Git 读取。

- 该服务只产出**外部美术资产**，不参与本工程的战斗、数值、存档与流程逻辑；接入不改变任何 Core 规则。
- 生成物进入 `Idle-CRPG/` 时必须另走既有资源规范与美术评审，不得因"AI 生成的"就跳过命名、尺寸、调色板和导入检查。
- token 属个人凭据，**不写入仓库**，不放进 `docs/`、`.agents/`、配置 CSV 或提交历史。

## 2. 服务端事实（2026-10-27 实测）

| 项目 | 结论 |
| --- | --- |
| 端点 | `https://api.pixellab.ai/mcp` |
| 传输 | Streamable HTTP：POST 后返回 `text/event-stream`；本服务没有独立 SSE 端点 |
| 协议/身份 | 协议版本 `2025-06-18`，`serverInfo` = `PixelLab MCP Server 0.2.0` |
| 鉴权 | 请求头 `Authorization: Bearer <token>` |
| 握手特性 | `initialize` 与 `tools/list` 免 token 即可成功；**`tools/call` 必须带 token**，缺失时返回 `401: Missing Authorization header`（HTTP 仍是 200，错误在 JSON-RPC result 内，`isError: true`） |
| 工具规模 | 带 token 时 `tools/list` 返回 **118** 个工具（无 token 时 103 个，鉴权后解锁更多） |
| 生成模式 | 全部异步：先返回 job id，再用 `wait_for_jobs` / `list_jobs` / `get_image` 轮询取结果 |
| 计费 | 订阅代数 + credits 双轨；`get_balance` 可查。实测账户为 `trial`，`generations_total 40`。官方页面未列费率，需以账户页为准 |
| 数据保留 | `create_map_object` 产物 **8 小时后自动删除**，需及时下载或 `save_to_asset` |

官方页面只给 Claude Code 的 CLI 命令，未给其他客户端的 JSON；下列配置按实测端点与 CodeBuddy 实现自行推导并已验证。

## 3. CodeBuddy 侧接入位置与限制

### 3.1 配置文件

客户端按优先级读取第一个存在的文件，不合并同作用域多份：

| 作用域 | 路径 | 说明 |
| --- | --- | --- |
| 用户级（推荐） | `~/.codebuddy/mcp.json` | Windows = `C:\Users\<用户名>\.codebuddy\mcp.json`；本机该文件由客户端自动创建 |
| 项目级 | `<项目根>/.mcp.json` | 首次连接需在界面审批；**不要**把 token 写进仓库内文件 |

本机证据：扩展日志 `getMcpSettingsFilePath result:C:\Users\MAR\.codebuddy\mcp.json`，即 IDE 实际读的就是用户级这一份。

### 3.2 传输判定与变量替换（读实现得出，非文档推测）

- 配置里**有 `url` 就走 `StreamableHTTPClientTransport`**，`headers` 原样随请求下发；`type` 填 `http` 正确（`stdio` 走本地进程，`sse` 走 SSE 分支）。
- `url` 与 `headers` 中的 `${VAR}` 只会展开 `workspaceFolder`、`workspaceFolderBasename`、`userHome` 以及该服务器自身 `env` 块里的变量，**不读取系统环境变量**。因此不能用 `${PIXELLAB_API_TOKEN}` 间接取环境变量，**必须直填 token**。
- `defer_loading` / `tools` 在 CLI 文档中有定义，IDE 扩展代码里未检索到对应字段，是否生效未验证；118 个工具若挤占上下文，以 MCP 页开关为准。

## 4. 标准配置

写入用户级 `mcp.json`（已有其它服务器时，仅追加 `pixellab` 这一项）：

```json
{
  "mcpServers": {
    "pixellab": {
      "type": "http",
      "url": "https://api.pixellab.ai/mcp",
      "headers": {
        "Authorization": "Bearer <你的 PixelLab token>"
      },
      "defer_loading": true,
      "description": "PixelLab pixel-art generation: characters, animations, isometric tiles, topdown/sidescroller tilesets, maps"
    }
  }
}
```

字段说明：`type` 固定 `http`；`url` 固定为上表端点；`headers.Authorization` 为 `Bearer ` + token（注意 Bearer 后有一个空格）；`description` 用纯 ASCII，避免客户端或终端编码差异把描述读成乱码。

## 5. 换机快速接入（按序执行）

1. 安装并登录 CodeBuddy。
2. 浏览器登录 <https://www.pixellab.ai>，在账户页/页面底部取 API token（形如 UUID）。
3. 打开用户级 `mcp.json`：
   - Windows：`%USERPROFILE%\.codebuddy\mcp.json`
   - macOS / Linux：`~/.codebuddy/mcp.json`
   若文件不存在则新建，内容为 `{ "mcpServers": {} }` 后再按第 4 节追加。
4. 填入 token，做 JSON 校验（多余逗号会让整份配置失效）。
5. **完全退出并重开 CodeBuddy**（本机实测：会话中途改配置不会注入到当前对话的工具列表）。
6. 设置 → MCP 页确认 `pixellab` 状态为绿色；红灯表示连接失败，先查第 7 节。
7. 跑一次第 6 节的验证脚本，`get_balance` 返回账户额度即接入成功。
8. 在对话中让 Agent 调用时，工具名为 `mcp__pixellab__<工具名>`；权限规则可按 `mcp__pixellab`（整服务）或 `mcp__pixellab__create_character`（单工具）粒度配置。

## 6. 验证脚本（只读，不改工程）

脚本从配置文件读 token，不在命令行明文出现。

PowerShell（Windows）：

```powershell
$cfg = Get-Content "$HOME/.codebuddy/mcp.json" -Raw | ConvertFrom-Json
$srv = $cfg.mcpServers.pixellab
$h = @{
  'Content-Type'  = 'application/json'
  'Accept'        = 'application/json, text/event-stream'
  'Authorization' = $srv.headers.Authorization
}
$body = '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"get_balance","arguments":{}}}'
$r = Invoke-WebRequest -Uri $srv.url -Method Post -Headers $h -Body $body -UseBasicParsing -TimeoutSec 60
($r.Content -split "`n" | Where-Object { $_ -like 'data: *' })[0].Substring(6) | ConvertFrom-Json
```

bash（macOS / Linux，需 curl 与 python3）：

```bash
AUTH=$(python3 -c "import json,os;d=json.load(open(os.path.expanduser('~/.codebuddy/mcp.json')));print(d['mcpServers']['pixellab']['headers']['Authorization'])")
curl -sS -X POST https://api.pixellab.ai/mcp \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -H "$AUTH" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"get_balance","arguments":{}}}'
```

判定标准：返回 `result.content[0].text` 内含 `generations_remaining` 即通过；返回 `401` 文案说明 token 无效或未写入；`Server type must be one of: 'stdio', 'sse', or 'streamableHttp'` 说明配置结构写坏（缺 `url`）。

可选只读抽查：`list_characters`、`list_projects`、`list_jobs` 均不消耗生成额度。

## 7. 故障排查

| 现象 | 原因 | 处理 |
| --- | --- | --- |
| 工具调用返回 `401: Missing Authorization header` | token 缺失、拼错或 `${VAR}` 未展开（IDE 不读系统环境变量） | 直填 token，确认 `Bearer ` 前缀与空格 |
| MCP 页红灯 | JSON 非法、`url` 缺失或网络不可达 | 校验 JSON；`curl -i https://api.pixellab.ai/mcp` 看连通性 |
| 面板绿灯但对话里没有工具 | 配置在会话开始后写入，工具列表已固化 | 完全退出并重开客户端 |
| 上下文异常膨胀 | 118 个工具描述全部注入 | MCP 页按需启用；或确认 `defer_loading` 是否被该版本识别 |
| 生成一直 pending | 异步作业未完成 | 用 `wait_for_jobs` 等待，不要反复轮询 `get_image` |
| 物件几小时后消失 | `create_map_object` 8 小时清理 | 生成后立即下载或 `save_to_asset` |

## 8. 卸载与回退

删除 `mcpServers.pixellab` 一项（或整份 `mcp.json` 重置为 `{ "mcpServers": {} }`）并重开客户端即可；服务端无本地残留。账户额度和已生成资产在 PixelLab 站点，删除本地配置不会回收，也不会自动删除已下载素材。

## 9. 可选：CLI 方式

CodeBuddy CLI 支持等价写法（本机未安装 CLI，未实测，仅作换环境参考）：

```bash
codebuddy mcp add-json --scope user pixellab \
  '{"type":"http","url":"https://api.pixellab.ai/mcp","headers":{"Authorization":"Bearer <token>"}}'
codebuddy mcp list
```

## 10. 安全与合规

- token 等同于账号密码：只放用户级配置，权限尽量收紧，泄漏后在站点重新生成。
- 生成素材的使用范围受 PixelLab ToS 约束；商用前核对条款，不默认拥有独占权。
- 生成物默认落在 PixelLab 侧与本地下载目录；进入仓库前按本工程资源规范筛选，避免把草稿、截图、中间产物提交进 `Idle-CRPG/`。
- 本机接入事实记录于 [执行记录](../reviews/2026-10-27_pixellab_mcp.md)；文档与实测不符时以实测为准并回写本节。
