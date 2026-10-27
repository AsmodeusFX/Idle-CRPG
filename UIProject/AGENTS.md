# FGUI 界面工作

修改 UIProject 或通过 openfairygui MCP 拼界面之前，必须先读 docs/RULES.md 并遵守。

## 工具链（外部安装，不随仓库走）

OpenFairyGUI（ofgui）与 FairyGUI Editor 装在仓库之外，不同电脑盘符/目录可能不同。接手时按顺序做三件事：

1. `powershell -NoProfile -File scripts\Find-Ofgui.ps1` —— 确认本机 node / ofgui / 编辑器位置与版本（要求 Node >= 22）。
2. `scripts\ofgui-verify.cmd` —— 确认 MCP 能从外部安装目录启动并完成握手。
3. `scripts\ofgui.cmd validate . --json` —— 交付前必须 `status: valid` 且 `complete: true`。

换机接入、配置原理、版本与补丁清单、已知风险见 `../docs/technical/fgui_toolchain.md`。

MCP 由 `.mcp.json` 提供（`cmd /c ${workspaceFolder}\scripts\ofgui-mcp.cmd`）。该配置属项目级 server，
首次使用需在 MCP 面板人工允许；改动 `.mcp.json` 任一字段后需重新允许。
