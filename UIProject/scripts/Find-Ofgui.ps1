<#
.SYNOPSIS
    探测本机 OpenFairyGUI(ofgui) / Node.js / FairyGUI Editor 的实际安装位置。

.DESCRIPTION
    OpenFairyGUI 与 FairyGUI Editor 均为“外部工具”，不放在游戏 Git 仓库内，
    不同电脑的盘符与目录可能不同。本脚本不写死任何绝对路径，按以下顺序解析：

      1. 命令行参数 -OfguiHome / -NodeExe / -FairyGuiEditor
      2. 环境变量 OFGUI_HOME / OFGUI_NODE / FAIRYGUI_EDITOR
      3. 本机本地配置 UIProject/.ofgui.local.json（已被 .gitignore 忽略）
      4. 系统 PATH
      5. 常见安装目录（各固定磁盘的 Program Files / Programs / Tools / nodejs 等）
      6. 有限深度的目录搜索

    只报告“真实可执行入口”，不做“同名文件夹存在即判定安装”的推断。

.PARAMETER AsObject
    输出对象而非文本，供其它脚本消费（启动器使用）。

.PARAMETER Json
    输出单行 JSON。

.PARAMETER SaveLocal
    将解析结果写入 UIProject/.ofgui.local.json，作为本机缓存/兜底配置。

.EXAMPLE
    powershell -NoProfile -File scripts/Find-Ofgui.ps1
#>
[CmdletBinding()]
param(
    [string]$OfguiHome,
    [string]$NodeExe,
    [string]$FairyGuiEditor,
    [switch]$AsObject,
    [switch]$Json,
    [switch]$SaveLocal,
    [switch]$EmitLaunch,
    [int]$MaxSearchDepth = 3
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$localConfigPath = Join-Path $projectRoot '.ofgui.local.json'

$mcpRelative = 'node_modules\@openfairygui\mcp\bin\ofgui-mcp.cjs'
$cliRelative = 'node_modules\@openfairygui\cli\bin\cli.cjs'

$diag = New-Object System.Collections.ArrayList

function Add-Diag([string]$msg) { [void]$diag.Add($msg) }

function Read-LocalConfig {
    if (-not (Test-Path -LiteralPath $localConfigPath)) { return $null }
    try {
        return Get-Content -LiteralPath $localConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        Add-Diag "本地配置解析失败，已忽略: $localConfigPath"
        return $null
    }
}

function Get-CandidateRoots {
    $roots = New-Object System.Collections.ArrayList

    $envRoots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:LOCALAPPDATA, $env:APPDATA)
    foreach ($r in $envRoots) {
        if ($r -and (Test-Path -LiteralPath $r)) { [void]$roots.Add($r) }
    }

    $drives = Get-PSDrive -PSProvider FileSystem -ErrorAction SilentlyContinue |
        Where-Object { $_.Root -match '^[A-Za-z]:\\$' }
    foreach ($d in $drives) {
        $base = $d.Root
        foreach ($sub in @('Program Files', 'Program Files (x86)', 'Programs', 'Tools', 'Apps', 'Software')) {
            $p = Join-Path $base $sub
            if (Test-Path -LiteralPath $p) { [void]$roots.Add($p) }
        }
    }

    return $roots | Select-Object -Unique
}

function Find-OfguiHomeUnder([string]$root) {
    $directNames = @('ofgui', 'OpenFairyGUI', 'openfairygui', 'Open-FairyGUI')
    foreach ($n in $directNames) {
        $p = Join-Path $root $n
        if (Test-Path -LiteralPath (Join-Path $p $mcpRelative)) { return $p }
    }
    # 一层模糊匹配（大小写/后缀差异）
    $children = Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match 'ofgui|fairygui' }
    foreach ($c in $children) {
        if (Test-Path -LiteralPath (Join-Path $c.FullName $mcpRelative)) { return $c.FullName }
    }
    return $null
}

function Find-NodeUnder([string]$root, [int]$depth) {
    $names = @('nodejs', 'node', 'Node')
    foreach ($n in $names) {
        $p = Join-Path $root (Join-Path $n 'node.exe')
        if (Test-Path -LiteralPath $p) { return $p }
    }
    if ($depth -le 0) { return $null }
    $found = Get-ChildItem -LiteralPath $root -Depth ([Math]::Min($depth, 2)) -Filter 'node.exe' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch 'Adobe|Photoshop|Creative Cloud|NvNode' } |
        Select-Object -First 1
    if ($found) { return $found.FullName }
    return $null
}

function Find-EditorUnder([string]$root) {
    $p = Join-Path $root 'FairyGUI-Editor\FairyGUI-Editor.exe'
    if (Test-Path -LiteralPath $p) { return $p }
    $children = Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match 'fairygui' }
    foreach ($c in $children) {
        $exe = Join-Path $c.FullName 'FairyGUI-Editor.exe'
        if (Test-Path -LiteralPath $exe) { return $exe }
    }
    return $null
}

function Get-NodeVersion([string]$exe) {
    try {
        $v = & $exe -v 2>$null
        if ($LASTEXITCODE -eq 0 -and $v) { return ($v | Select-Object -First 1).ToString().Trim() }
    } catch { }
    return $null
}

# ---------------------------------------------------------------- 本地配置
$local = Read-LocalConfig

# ---------------------------------------------------------------- ofgui
$ofguiSource = $null
$resolvedOfgui = $null

if ($OfguiHome) { $resolvedOfgui = $OfguiHome; $ofguiSource = 'param' }
elseif ($env:OFGUI_HOME) { $resolvedOfgui = $env:OFGUI_HOME; $ofguiSource = 'env' }
elseif ($local -and $local.ofguiHome) { $resolvedOfgui = $local.ofguiHome; $ofguiSource = 'local-config' }

if ($resolvedOfgui) {
    if (-not (Test-Path -LiteralPath (Join-Path $resolvedOfgui $mcpRelative))) {
        Add-Diag "$ofguiSource 指定的 ofgui 目录缺少 MCP 入口，继续自动探测: $resolvedOfgui"
        $resolvedOfgui = $null
    }
}

if (-not $resolvedOfgui) {
    $cmd = Get-Command 'ofgui' -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) {
        # 全局安装形态: <prefix>/ofgui(.cmd) 或 node_modules/.bin/ofgui
        $binDir = Split-Path -Parent $cmd.Source
        $guess = Split-Path -Parent $binDir
        if (Test-Path -LiteralPath (Join-Path $guess $mcpRelative)) {
            $resolvedOfgui = $guess
            $ofguiSource = 'PATH'
        }
    }
}

if (-not $resolvedOfgui) {
    foreach ($root in (Get-CandidateRoots)) {
        $hit = Find-OfguiHomeUnder $root
        if ($hit) { $resolvedOfgui = $hit; $ofguiSource = "auto:$root"; break }
    }
}

# ---------------------------------------------------------------- node
$nodeSource = $null
$resolvedNode = $null

if ($NodeExe) { $resolvedNode = $NodeExe; $nodeSource = 'param' }
elseif ($env:OFGUI_NODE) { $resolvedNode = $env:OFGUI_NODE; $nodeSource = 'env' }
elseif ($local -and $local.nodeExe -and (Test-Path -LiteralPath $local.nodeExe)) { $resolvedNode = $local.nodeExe; $nodeSource = 'local-config' }

if (-not $resolvedNode) {
    $nodeCmd = Get-Command 'node.exe' -ErrorAction SilentlyContinue
    if (-not $nodeCmd) { $nodeCmd = Get-Command 'node' -ErrorAction SilentlyContinue }
    if ($nodeCmd -and $nodeCmd.Source -and (Test-Path -LiteralPath $nodeCmd.Source)) {
        $resolvedNode = $nodeCmd.Source
        $nodeSource = 'PATH'
    }
}

if (-not $resolvedNode) {
    # ofgui 目录旁若自带 node 运行时优先复用
    if ($resolvedOfgui) {
        $sibling = Find-NodeUnder (Split-Path -Parent $resolvedOfgui) 1
        if ($sibling) { $resolvedNode = $sibling; $nodeSource = 'auto-sibling' }
    }
}

if (-not $resolvedNode) {
    foreach ($root in (Get-CandidateRoots)) {
        $hit = Find-NodeUnder $root 1
        if ($hit) { $resolvedNode = $hit; $nodeSource = "auto:$root"; break }
    }
}

$nodeVersion = $null
if ($resolvedNode) { $nodeVersion = Get-NodeVersion $resolvedNode }

# ---------------------------------------------------------------- FairyGUI Editor
$editorSource = $null
$resolvedEditor = $null

if ($FairyGuiEditor) { $resolvedEditor = $FairyGuiEditor; $editorSource = 'param' }
elseif ($env:FAIRYGUI_EDITOR) { $resolvedEditor = $env:FAIRYGUI_EDITOR; $editorSource = 'env' }
elseif ($local -and $local.fairyguiEditor -and (Test-Path -LiteralPath $local.fairyguiEditor)) { $resolvedEditor = $local.fairyguiEditor; $editorSource = 'local-config' }

if (-not $resolvedEditor) {
    foreach ($root in (Get-CandidateRoots)) {
        $hit = Find-EditorUnder $root
        if ($hit) { $resolvedEditor = $hit; $editorSource = "auto:$root"; break }
    }
}

$editorVersion = $null
if ($resolvedEditor -and (Test-Path -LiteralPath $resolvedEditor)) {
    $editorVersion = (Get-Item -LiteralPath $resolvedEditor).VersionInfo.FileVersion
}

# ---------------------------------------------------------------- 版本约束
$nodeOk = $false
$nodeMajor = $null
if ($nodeVersion -match '^v(\d+)\.') {
    $nodeMajor = [int]$Matches[1]
    $nodeOk = $nodeMajor -ge 22
}

$mcpEntry = if ($resolvedOfgui) { Join-Path $resolvedOfgui $mcpRelative } else { $null }
$cliEntry = if ($resolvedOfgui) { Join-Path $resolvedOfgui $cliRelative } else { $null }

$ofguiVersion = $null
if ($resolvedOfgui) {
    $pkg = Join-Path $resolvedOfgui 'node_modules\@openfairygui\mcp\package.json'
    if (Test-Path -LiteralPath $pkg) {
        try { $ofguiVersion = (Get-Content -LiteralPath $pkg -Raw -Encoding UTF8 | ConvertFrom-Json).version } catch { }
    }
}

$result = [ordered]@{
    ok                   = [bool]($mcpEntry -and $nodeOk)
    nodeExe              = $resolvedNode
    nodeVersion          = $nodeVersion
    nodeMajor            = $nodeMajor
    nodeSatisfies        = $nodeOk
    nodeSource           = $nodeSource
    ofguiHome            = $resolvedOfgui
    ofguiVersion         = $ofguiVersion
    ofguiMcpEntry        = $mcpEntry
    ofguiCliEntry        = $cliEntry
    ofguiSource          = $ofguiSource
    fairyguiEditor       = $resolvedEditor
    fairyguiEditorVersion = $editorVersion
    fairyguiEditorSource = $editorSource
    projectRoot          = $projectRoot
    localConfigPath      = $localConfigPath
    diagnostics          = @($diag)
}

if ($SaveLocal) {
    if (-not $result.ok) {
        Write-Error "工具未就绪，拒绝写入本地配置。诊断: $($diag -join '; ')"
    }
    $payload = [ordered]@{
        '$schema'       = 'ofgui-local-config/1'
        ofguiHome       = $resolvedOfgui
        nodeExe         = $resolvedNode
        fairyguiEditor  = $resolvedEditor
        recordedAt      = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
        note            = '本机专用路径缓存，已被 .gitignore 忽略，请勿提交'
    }
    $payload | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $localConfigPath -Encoding UTF8
    Add-Diag "已写入本地配置: $localConfigPath"
    $result.diagnostics = @($diag)
}

# 供 ofgui.cmd / ofgui-mcp.cmd 消费：单行 "node|mcp|cli"，stdout 不夹杂任何其它内容
if ($EmitLaunch) {
    if (-not $result.ok) {
        [Console]::Error.WriteLine('[Find-Ofgui] 工具链未就绪：需要 ofgui 安装目录与 Node.js >= 22')
        [Console]::Error.WriteLine(('[Find-Ofgui] node={0} ofgui={1} diagnostics={2}' -f $resolvedNode, $resolvedOfgui, ($diag -join '; ')))
        [Console]::Error.WriteLine('[Find-Ofgui] 可用环境变量 OFGUI_HOME / OFGUI_NODE 或本地配置 .ofgui.local.json 指定路径')
        exit 3
    }
    [Console]::Out.WriteLine(('{0}|{1}|{2}' -f $result.nodeExe, $result.ofguiMcpEntry, $result.ofguiCliEntry))
    exit 0
}

if ($AsObject) { return [pscustomobject]$result }

if ($Json) {
    ([pscustomobject]$result) | ConvertTo-Json -Depth 6 -Compress
    return
}

Write-Host '=== FGUI 外部工具探测 ===' -ForegroundColor Cyan
Write-Host ("工程目录      : " + $result.projectRoot)
Write-Host ("Node.js       : " + ($(if ($resolvedNode) { "$resolvedNode  ($nodeVersion)" } else { '未找到' })) + "  [" + $nodeSource + "]")
Write-Host ("Version >= 22 : " + $nodeOk)
Write-Host ("ofgui 目录    : " + ($(if ($resolvedOfgui) { $resolvedOfgui } else { '未找到' })) + "  [" + $ofguiSource + "]")
Write-Host ("ofgui 版本    : " + $ofguiVersion)
Write-Host ("MCP 入口      : " + $mcpEntry)
Write-Host ("CLI  入口     : " + $cliEntry)
Write-Host ("FairyGUI 编辑器: " + ($(if ($resolvedEditor) { "$resolvedEditor  ($editorVersion)" } else { '未找到' })) + "  [" + $editorSource + "]")
Write-Host ("本地配置      : " + $localConfigPath + $(if (Test-Path -LiteralPath $localConfigPath) { ' (存在)' } else { ' (不存在)' }))

if ($diag.Count -gt 0) {
    Write-Host '--- 诊断 ---' -ForegroundColor Yellow
    $diag | ForEach-Object { Write-Host ("  " + $_) }
}

if ($result.ok) { Write-Host '结论: 工具链就绪' -ForegroundColor Green }
else { Write-Host '结论: 工具链未就绪（缺少 ofgui 或 Node.js >= 22）' -ForegroundColor Red }
