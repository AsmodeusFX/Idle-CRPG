# 美术资源管线复测工具

`Test-ArtAssetPipeline.ps1`：Aseprite ↔ Godot（Aseprite Wizard）最小可复现校验。

放在仓库而不是 `artifacts/qa/` 的原因：`artifacts/` 被 `.gitignore` 忽略，POC 脚本随目录不入库，换机器无法复现；这里只保留**不依赖测试素材**的三个入口。

## 用法

```powershell
# 1. 体检：Aseprite / Godot / AsepriteWizard 版本、编辑器里的 Aseprite 命令路径
powershell -File tools/art/Test-ArtAssetPipeline.ps1 -Mode Env

# 2. 往返：PNG -> .aseprite -> PNG，逐像素比对（Windows；无 System.Drawing 时标记 SKIPPED）
powershell -File tools/art/Test-ArtAssetPipeline.ps1 -Mode Roundtrip `
  -Source .\some.png -OutDir .\artifacts\qa\art-tool-test

# 3. 导入核验：headless 导入指定工程，检查每个 *.aseprite.import 的导入器、资源类型与缓存 md5
powershell -File tools/art/Test-ArtAssetPipeline.ps1 -Mode Import -Project .\artifacts\qa\my-test-project
```

`-AsepritePath` / `-GodotPath` 可覆盖自动探测（PATH → 常见安装目录 → 其余固定盘的 Program Files → Godot 编辑器设置里的 `aseprite/general/command_path`）。

## 边界

- 只读或只写 `-OutDir` 指定的输出目录；不修改正式工程资源。
- `Import` 必须显式传 `-Project`，不会默认指向 `Idle-CRPG/` 正式工程。
- 不校验画面、不生成素材、不调用 PixelLab。
- 判定有据可查：往返输出 `*_roundtrip_report.json`；导入输出逐文件 importer/type/cacheFresh 与 PASS/FAIL。
- 动画帧时长（fps）与循环需要实际运行场景读 `SpriteFrames`，本脚本不覆盖，属人工/场景级验收。

## 环境注意

- 脚本含中文，保存为 **UTF-8 with BOM**；Windows PowerShell 5.1 按 ANSI 解析无 BOM 文件会报语法错误。
- Aseprite 是 GUI 子系统程序，直接 `&` 调用不会等待进程结束，脚本内部统一用 `Start-Process -Wait`。
- Aseprite 若装了 PixelLab 扩展，批处理模式会打印 `handle-pose.lua` 报错，属无关噪声，`Env` 模式会单独提示。

约定与标记说明见 [美术资源管线](../../docs/technical/art_asset_pipeline.md)。
