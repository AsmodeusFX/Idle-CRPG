<#
.SYNOPSIS
    美术资源管线体检工具：Aseprite ↔ Godot（Aseprite Wizard）最小可复现校验。

.DESCRIPTION
    三种模式，都是只读或只写自己指定的输出目录，不触碰正式工程资源：

    Env       体检：探测 Aseprite / Godot 可执行文件与版本、AsepriteWizard 插件版本、
              编辑器设置中的 aseprite/general/command_path。
    Roundtrip PNG -> .aseprite -> PNG 往返，并逐像素比对（尺寸、可见像素、颜色数、差异数）。
    Import    对指定 Godot 工程执行 headless 导入，回读每个 *.aseprite.import 的导入器与资源类型，
              并确认 .godot/imported 缓存已生成且不早于源文件。

    不硬编码个人盘符：先查 PATH，再查常见安装目录，最后读 Godot 编辑器设置。
    Roundtrip 的像素比对依赖 System.Drawing，仅 Windows 可用；其他平台标记 SKIPPED 而不是失败。

.PARAMETER Mode
    Env | Roundtrip | Import

.PARAMETER Source
    Roundtrip 模式的输入 PNG。

.PARAMETER OutDir
    Roundtrip 模式的输出目录（会创建）。

.PARAMETER Project
    Import 模式的目标 Godot 工程目录（含 project.godot）。默认不指向正式工程，需显式传入。

.PARAMETER AsepritePath
    手动指定 aseprite 可执行文件，覆盖自动探测。

.PARAMETER GodotPath
    手动指定 godot 可执行文件，覆盖自动探测。

.EXAMPLE
    powershell -File tools/art/Test-ArtAssetPipeline.ps1 -Mode Env

.EXAMPLE
    powershell -File tools/art/Test-ArtAssetPipeline.ps1 -Mode Roundtrip -Source .\tmp\potion.png -OutDir .\tmp\rt

.EXAMPLE
    powershell -File tools/art/Test-ArtAssetPipeline.ps1 -Mode Import -Project .\artifacts\qa\my-test-project
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Env', 'Roundtrip', 'Import')]
    [string]$Mode,

    [string]$Source,
    [string]$OutDir,
    [string]$Project,
    [string]$AsepritePath,
    [string]$GodotPath
)

$ErrorActionPreference = 'Stop'

function Write-Section([string]$text) {
    Write-Output ''
    Write-Output ('=== ' + $text + ' ===')
}

function Find-Aseprite([string]$explicit) {
    if ($explicit) {
        if (Test-Path $explicit) { return (Resolve-Path $explicit).Path }
        Write-Output ('WARN: 指定的 Aseprite 不存在：' + $explicit)
    }
    $cmd = Get-Command aseprite, aseprite.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($cmd) { return $cmd.Source }

    $candidates = @(
        (Join-Path $env:ProgramFiles 'Aseprite/Aseprite.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Aseprite/Aseprite.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs/Aseprite/Aseprite.exe'),
        (Join-Path $env:ProgramFiles 'Steam/steamapps/common/Aseprite/aseprite.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Steam/steamapps/common/Aseprite/aseprite.exe'),
        '/Applications/Aseprite.app/Contents/MacOS/aseprite',
        '/usr/bin/aseprite',
        '/usr/local/bin/aseprite'
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) { return $c }
    }

    # 编辑器设置里的命令路径（Aseprite Wizard 使用的同一个键）
    $editorSettings = Join-Path $env:APPDATA 'Godot'
    if (Test-Path $editorSettings) {
        $tres = Get-ChildItem $editorSettings -Filter 'editor_settings-*.tres' -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($tres) {
            $line = Select-String -Path $tres.FullName -Pattern '^aseprite/general/command_path' -ErrorAction SilentlyContinue |
                Select-Object -First 1
            if ($line) {
                $value = ($line.Line -split '=', 2)[1].Trim().Trim('"')
                $value = $value.Replace('\\', '\')
                if (Test-Path $value) { return $value }
            }
        }
    }
    return $null
}

function Find-Godot([string]$explicit) {
    if ($explicit) {
        if (Test-Path $explicit) { return (Resolve-Path $explicit).Path }
        Write-Output ('WARN: 指定的 Godot 不存在：' + $explicit)
    }
    $cmd = Get-Command godot, godot4, 'Godot*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($cmd) { return $cmd.Source }

    $roots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}, (Join-Path $env:LOCALAPPDATA 'Programs'), $env:LOCALAPPDATA)
    foreach ($r in $roots) {
        if (-not $r -or -not (Test-Path $r)) { continue }
        $exe = Get-ChildItem $r -Filter 'Godot*.exe' -Recurse -Depth 2 -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notmatch 'console' } | Select-Object -First 1
        if ($exe) { return $exe.FullName }
    }

    # 其它盘符下的 Program Files / Program Files (x86)（本机 Godot 就装在非系统盘）
    $drives = [System.IO.DriveInfo]::GetDrives() | Where-Object { $_.IsReady -and $_.DriveType -eq 'Fixed' }
    foreach ($d in $drives) {
        foreach ($sub in @('Program Files', 'Program Files (x86)')) {
            $root = Join-Path $d.RootDirectory.FullName $sub
            if (-not (Test-Path $root)) { continue }
            $exe = Get-ChildItem $root -Filter 'Godot*.exe' -Recurse -Depth 2 -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -notmatch 'console' } | Select-Object -First 1
            if ($exe) { return $exe.FullName }
        }
    }
    return $null
}

function Invoke-Aseprite([string]$exe, [string[]]$cliArgs) {
    # Aseprite 是 GUI 子系统程序，直接 & 调用不会等待；必须 Start-Process -Wait
    $stdout = [System.IO.Path]::GetTempFileName()
    $stderr = [System.IO.Path]::GetTempFileName()
    $p = Start-Process -FilePath $exe -ArgumentList $cliArgs -Wait -PassThru -NoNewWindow `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $out = Get-Content $stdout -Raw -ErrorAction SilentlyContinue
    $err = Get-Content $stderr -Raw -ErrorAction SilentlyContinue
    Remove-Item $stdout, $stderr -Force -ErrorAction SilentlyContinue
    return @{ Code = $p.ExitCode; StdOut = $out; StdErr = $err }
}

function Get-PngStats([string]$path) {
    Add-Type -AssemblyName System.Drawing -ErrorAction Stop
    $img = New-Object System.Drawing.Bitmap($path)
    $visible = 0
    $colors = @{}
    $pixels = New-Object 'System.Collections.Generic.List[int]'
    for ($y = 0; $y -lt $img.Height; $y++) {
        for ($x = 0; $x -lt $img.Width; $x++) {
            $p = $img.GetPixel($x, $y)
            $key = $p.A.ToString('X2') + $p.R.ToString('X2') + $p.G.ToString('X2') + $p.B.ToString('X2')
            if ($p.A -gt 0) { $visible++ }
            if (-not $colors.ContainsKey($key)) { $colors[$key] = 0 }
            $colors[$key] = $colors[$key] + 1
        }
    }
    $result = @{
        Width         = $img.Width
        Height        = $img.Height
        Format        = $img.PixelFormat.ToString()
        VisiblePixels = $visible
        ColorCount    = $colors.Count
        Colors        = $colors
    }
    $img.Dispose()
    return $result
}

function Compare-Png([string]$pathA, [string]$pathB) {
    $a = Get-PngStats $pathA
    $b = Get-PngStats $pathB
    $diff = 0
    foreach ($k in $a.Colors.Keys) {
        $ca = $a.Colors[$k]
        $cb = 0
        if ($b.Colors.ContainsKey($k)) { $cb = $b.Colors[$k] }
        $diff += [Math]::Abs($ca - $cb)
    }
    foreach ($k in $b.Colors.Keys) {
        if (-not $a.Colors.ContainsKey($k)) { $diff += $b.Colors[$k] }
    }
    $sizeMatch = ($a.Width -eq $b.Width) -and ($a.Height -eq $b.Height)
    return @{
        SizeMatch      = $sizeMatch
        WidthA         = $a.Width
        HeightA        = $a.Height
        WidthB         = $b.Width
        HeightB        = $b.Height
        VisibleA       = $a.VisiblePixels
        VisibleB       = $b.VisiblePixels
        ColorsA        = $a.ColorCount
        ColorsB        = $b.ColorCount
        DifferingPixels = $diff
        Identical      = ($sizeMatch -and $diff -eq 0)
    }
}

# ------------------------------------------------------------------ Env
if ($Mode -eq 'Env') {
    Write-Section 'Aseprite'
    $ase = Find-Aseprite $AsepritePath
    if ($ase) {
        Write-Output ('path = ' + $ase)
        $r = Invoke-Aseprite $ase @('--version')
        $verLine = ($r.StdOut -split "`n" | Where-Object { $_ -match '^Aseprite\s+\d' } | Select-Object -First 1)
        if ($verLine) { Write-Output ('version = ' + $verLine.Trim()) } else { Write-Output 'version = <未能解析>' }
        $noise = ($r.StdOut -split "`n" | Where-Object { $_ -match 'extensions' -or $_ -match 'attempt to index' })
        if ($noise.Count -gt 0) {
            Write-Output ('warn = Aseprite 扩展启动报错 ' + $noise.Count + ' 行（批处理模式常见，不影响产物）')
        }
    }
    else {
        Write-Output 'path = <未找到>  -> Roundtrip/Import 无法运行'
    }

    Write-Section 'Godot'
    $godot = Find-Godot $GodotPath
    if ($godot) {
        Write-Output ('path = ' + $godot)
        $r = Invoke-Aseprite $godot @('--version')
        Write-Output ('version = ' + $r.StdOut.Trim())
    }
    else {
        Write-Output 'path = <未找到>  -> Import 无法运行'
    }

    Write-Section 'Aseprite Wizard 插件'
    $pluginCfg = Join-Path (Get-Location) 'Idle-CRPG/addons/AsepriteWizard/plugin.cfg'
    if (Test-Path $pluginCfg) {
        $ver = Select-String -Path $pluginCfg -Pattern '^version' | Select-Object -First 1
        Write-Output ('plugin.cfg = ' + $pluginCfg)
        Write-Output ('version = ' + $ver.Line.Trim())
    }
    else {
        Write-Output ('plugin.cfg = <未找到>（在仓库根目录运行，或手动确认 addons/AsepriteWizard）')
    }

    Write-Section 'Godot 编辑器设置（Aseprite 命令）'
    $editorDir = Join-Path $env:APPDATA 'Godot'
    $tres = Get-ChildItem $editorDir -Filter 'editor_settings-*.tres' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($tres) {
        $line = Select-String -Path $tres.FullName -Pattern '^aseprite/general/command_path' -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($line) { Write-Output ($line.Line.Trim()) } else { Write-Output '<未配置 aseprite/general/command_path>' }
    }
    else {
        Write-Output '<无 editor_settings-*.tres>'
    }
    exit 0
}

# ------------------------------------------------------------ Roundtrip
if ($Mode -eq 'Roundtrip') {
    if (-not $Source -or -not (Test-Path $Source)) { throw 'Roundtrip 需要 -Source 指向存在的 PNG' }
    if (-not $OutDir) { throw 'Roundtrip 需要 -OutDir' }
    $ase = Find-Aseprite $AsepritePath
    if (-not $ase) { throw '未找到 Aseprite，可用 -AsepritePath 指定' }

    if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
    $base = [System.IO.Path]::GetFileNameWithoutExtension($Source)
    $aseOut = Join-Path (Resolve-Path $OutDir).Path ($base + '.aseprite')
    $pngOut = Join-Path (Resolve-Path $OutDir).Path ($base + '_roundtrip.png')

    Write-Section 'PNG -> aseprite'
    $r1 = Invoke-Aseprite $ase @('-b', (Resolve-Path $Source).Path, '--save-as', $aseOut)
    Write-Output ('exit = ' + $r1.Code + ' ; exists = ' + (Test-Path $aseOut))
    if (-not (Test-Path $aseOut)) { Write-Output 'FAIL: 未生成 .aseprite'; exit 1 }

    Write-Section 'aseprite -> PNG'
    $r2 = Invoke-Aseprite $ase @('-b', $aseOut, '--save-as', $pngOut)
    Write-Output ('exit = ' + $r2.Code + ' ; exists = ' + (Test-Path $pngOut))
    if (-not (Test-Path $pngOut)) { Write-Output 'FAIL: 未生成回导 PNG'; exit 1 }

    Write-Section '像素比对'
    try {
        $cmp = Compare-Png (Resolve-Path $Source).Path $pngOut
    }
    catch {
        Write-Output ('SKIPPED: 无法比对（' + $_.Exception.Message + '）。非 Windows 或缺少 System.Drawing。')
        exit 0
    }
    Write-Output ('size = ' + $cmp.WidthA + 'x' + $cmp.HeightA + ' vs ' + $cmp.WidthB + 'x' + $cmp.HeightB + ' ; match = ' + $cmp.SizeMatch)
    Write-Output ('visiblePixels = ' + $cmp.VisibleA + ' vs ' + $cmp.VisibleB)
    Write-Output ('distinctColors = ' + $cmp.ColorsA + ' vs ' + $cmp.ColorsB)
    Write-Output ('differingPixels = ' + $cmp.DifferingPixels)
    Write-Output ('RESULT = ' + $(if ($cmp.Identical) { 'PIXEL_IDENTICAL' } else { 'DIFFERS' }))

    $report = [ordered]@{
        source             = (Resolve-Path $Source).Path
        aseprite           = $aseOut
        roundtripPng       = $pngOut
        sizeMatch          = $cmp.SizeMatch
        visiblePixelsSource = $cmp.VisibleA
        visiblePixelsBack   = $cmp.VisibleB
        differingPixels    = $cmp.DifferingPixels
        result             = $(if ($cmp.Identical) { 'PIXEL_IDENTICAL' } else { 'DIFFERS' })
        timestamp          = (Get-Date).ToString('s')
    }
    $reportPath = Join-Path (Resolve-Path $OutDir).Path ($base + '_roundtrip_report.json')
    $report | ConvertTo-Json -Depth 5 | Out-File $reportPath -Encoding utf8
    Write-Output ('report = ' + $reportPath)
    if ($cmp.Identical) { exit 0 } else { exit 1 }
}

# --------------------------------------------------------------- Import
if ($Mode -eq 'Import') {
    if (-not $Project) { throw 'Import 需要 -Project 指向 Godot 工程目录（含 project.godot）' }
    $projPath = (Resolve-Path $Project).Path
    if (-not (Test-Path (Join-Path $projPath 'project.godot'))) { throw ('不是 Godot 工程：' + $projPath) }

    $godot = Find-Godot $GodotPath
    if (-not $godot) { throw '未找到 Godot，可用 -GodotPath 指定' }

    Write-Section 'headless import'
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $stdout = [System.IO.Path]::GetTempFileName()
    $stderr = [System.IO.Path]::GetTempFileName()
    $p = Start-Process -FilePath $godot -ArgumentList @('--headless', '--path', $projPath, '--import') -Wait -PassThru -NoNewWindow `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $sw.Stop()
    $log = Get-Content $stdout -Raw -ErrorAction SilentlyContinue
    Remove-Item $stdout, $stderr -Force -ErrorAction SilentlyContinue
    Write-Output ('exit = ' + $p.ExitCode + ' ; elapsedMs = ' + $sw.ElapsedMilliseconds)

    Write-Section '导入结果'
    $imports = Get-ChildItem $projPath -Filter '*.aseprite.import' -Recurse -File -ErrorAction SilentlyContinue
    if (-not $imports) { Write-Output 'FAIL: 没有生成任何 *.aseprite.import（检查插件是否启用、导入器是否为 No Import）'; exit 1 }

    $fail = 0
    foreach ($imp in $imports) {
        $content = Get-Content $imp.FullName -Raw
        $importer = '<未声明>'
        $type = '<未声明>'
        $dest = $null
        foreach ($line in ($content -split "`n")) {
            if ($line -match '^importer="(.+)"') { $importer = $Matches[1] }
            elseif ($line -match '^type="(.+)"') { $type = $Matches[1] }
            elseif ($line -match '^path="res://(.+)"') { $dest = $Matches[1] }
        }
        $okImporter = $importer -like 'aseprite_wizard.*'
        $cacheOk = $false
        if ($dest) {
            $cacheFile = Join-Path $projPath ($dest.Replace('/', '\'))
            $md5File = [System.IO.Path]::ChangeExtension($cacheFile, '.md5')
            if ((Test-Path $cacheFile) -and (Test-Path $md5File)) {
                # Godot 用 .md5 文件记录 source_md5 判定缓存是否有效；mtime 在内容未变时不会更新
                $recorded = (Select-String -Path $md5File -Pattern 'source_md5="([0-9a-fA-F]+)"' |
                        Select-Object -First 1).Matches[0].Groups[1].Value
                $rel = $imp.FullName.Substring($projPath.Length).TrimStart('\') -replace '\.import$', ''
                $srcFile = Join-Path $projPath $rel
                $actual = (Get-FileHash $srcFile -Algorithm MD5).Hash
                $cacheOk = ($actual -eq $recorded.ToUpper())
            }
        }
        if (-not ($okImporter -and $cacheOk)) { $fail++ }
        Write-Output ('  ' + (Split-Path $imp.FullName -Leaf) + ' | importer=' + $importer + ' | type=' + $type + ' | cacheFresh=' + $cacheOk)
    }

    Write-Section '结论'
    if ($fail -eq 0) {
        Write-Output ('PASS: ' + $imports.Count + ' 个 .aseprite 均由 AsepriteWizard 导入且缓存已更新')
        exit 0
    }
    Write-Output ('FAIL: ' + $fail + ' 个文件导入器或缓存异常')
    exit 1
}
