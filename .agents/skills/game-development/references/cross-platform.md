# 跨平台验证

## 环境发现

从工作区相对路径运行；先确认当前系统、shell、仓库根与工具实际版本。Idle-CRPG当前csproj为Godot.NET.Sdk/4.7.2、net8.0，检查工程SDK和实际Godot .NET可执行文件匹配。版本升级是独立变更，不能为了跑测试擅自改依赖。

Windows可用Get-Command，POSIX可用command -v发现dotnet、Godot和Python。工具名、WindowsApps别名或进程退出码不足以证明脚本执行成功：核对版本输出、预期文件差异、测试结束标记和退出码。本项目已发生Python别名未落盘但原代码测试通过的误判。

不写死开发者盘符、用户名、Godot安装位置或shell。提供明确可执行文件路径时放在本机环境变量/命令参数；不要提交本机路径。Windows调用带空格的可执行文件用PowerShell调用运算符；POSIX使用带引号的路径。路径拼接使用Path.Combine或引擎路径API。

## 文件与存档

资源引用大小写必须与文件实际拼写一致；不要依赖Windows大小写不敏感行为。文本采用UTF-8，CSV保留标准转义；避免无意义的全仓换行符重写。CSV导入元数据与正式本地化分开。

玩家数据使用引擎user://解析结果，不假设Windows的AppData路径。Linux/macOS/Windows的user目录、权限、锁和原子替换行为分别验证。QA只用artifacts/qa隔离存档，不从玩家现场试重置或迁移。

## 验证矩阵

| 层级 | 内容 | 可证明范围 |
|---|---|---|
| 纯逻辑检查 | 状态、公式、配置校验、重放、存档恢复 | 运行该检查的系统与运行时 |
| 引擎无头冒烟 | 资源导入、脚本启动、流程与退出 | 无头环境运行 |
| 实际渲染 | 截图、裁切、文字、缩放、输入 | 已运行的渲染后端/系统 |
| 平台发布 | 目标导出、启动、保存/加载、输入与分辨率 | 实际测试的导出目标 |

只在Windows通过不能标记Linux/macOS通过；没有目标机器就列为未验证，不猜测兼容。不要把开发主机跨平台与面向移动端/Web的导出支持混为一谈。

## Idle-CRPG命令模板（仓库根目录）

```text
dotnet run --project tests/IdleCrpg.Checks.csproj
dotnet build Idle-CRPG/Idle-CRPG.csproj
<Godot .NET executable> --headless --path Idle-CRPG -- --smoke-test
<Godot .NET executable> --path Idle-CRPG -- --capture <已有的场景参数>
```

占位符不是可直接执行的路径。仅使用代码中已存在的QA参数。无头截图可能等待图形帧而无法完成，实际渲染任务用可用图形环境，并遵守运行权限。网络/工具缺失须明确报告，不能把未运行当通过。