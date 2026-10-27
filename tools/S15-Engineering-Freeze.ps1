param([string]$Workspace="d:/Project-Asmo/Idle-CRPG")
$ErrorActionPreference="Stop"
$utf8=New-Object System.Text.UTF8Encoding($false)
$stamp=Get-Date -Format "yyyyMMdd-HHmmss"
$out=Join-Path $Workspace "artifacts/qa/s15-engineering-freeze-$stamp"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$sha=[System.Security.Cryptography.SHA256]::Create()
function FileHash([string]$p){ if(-not(Test-Path $p)){return 'MISSING'}; return (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLower() }
function DirHash([string]$p){
    $files=@(Get-ChildItem -LiteralPath $p -Recurse -File | Where-Object { $_.Extension -in @('.cs','.csv','.godot','.tscn','.json') })
    $cat=($files | Sort-Object FullName | ForEach-Object { (Get-FileHash $_.FullName -Algorithm SHA256).Hash }) -join ''
    return (@{ hash=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($cat))) -replace '-','').ToLower(); files=$files.Count })
}

$review=Join-Path $Workspace "docs/reviews/2026-10-25_p2_4_first_batch.md"
$finalMaster=Join-Path $Workspace "artifacts/qa/s15-c1-c3-recon-final-20261027-110902"
$proof2=Join-Path $Workspace "artifacts/qa/s15-c1-c3-recon-proof2-20261027-110100"
$code=DirHash (Join-Path $Workspace "Idle-CRPG/Scripts")
$cfg=DirHash (Join-Path $Workspace "Idle-CRPG/config")

# 1) 冻结索引
$index=New-Object System.Collections.Generic.List[object]
$index.Add([pscustomobject]@{kind='review';path='docs/reviews/2026-10-25_p2_4_first_batch.md';sha256=(FileHash $review);bytes=(Get-Item $review).Length;note='模块唯一Review（§1-§35）'})
$index.Add([pscustomobject]@{kind='final_master';path='artifacts/qa/s15-c1-c3-recon-final-20261027-110902/recon_final_design_master_250.csv';sha256=(FileHash (Join-Path $finalMaster 'recon_final_design_master_250.csv'));bytes=(Get-Item (Join-Path $finalMaster 'recon_final_design_master_250.csv')).Length;note='250行逐设计ID主表'})
$index.Add([pscustomobject]@{kind='final_corrections';path='artifacts/qa/s15-c1-c3-recon-final-20261027-110902/recon_status_corrections.csv';sha256=(FileHash (Join-Path $finalMaster 'recon_status_corrections.csv'));bytes=(Get-Item (Join-Path $finalMaster 'recon_status_corrections.csv')).Length;note='定性更正表'})
$index.Add([pscustomobject]@{kind='final_summary';path='artifacts/qa/s15-c1-c3-recon-final-20261027-110902/final_closure_summary.md';sha256=(FileHash (Join-Path $finalMaster 'final_closure_summary.md'));bytes=(Get-Item (Join-Path $finalMaster 'final_closure_summary.md')).Length;note='最终收口摘要'})
$index.Add([pscustomobject]@{kind='proof2';path='artifacts/qa/s15-c1-c3-recon-proof2-20261027-110100/proof_summary.md';sha256=(FileHash (Join-Path $proof2 'proof_summary.md'));bytes=(Get-Item (Join-Path $proof2 'proof_summary.md')).Length;note='R-01..R-04补证'})
$index.Add([pscustomobject]@{kind='design_C1A';path='docs/temp/P2-基础循环搭建/P2-Part2-新版数据重构/P2-4-2-第2批实施包/P2-4 S15_C1A_正式实施授权与裁决回传_V1.0/C1A核心输入/02_C1A_98天赋逐节点补规表.csv';sha256=(FileHash (Join-Path $Workspace 'docs/temp/P2-基础循环搭建/P2-Part2-新版数据重构/P2-4-2-第2批实施包/P2-4 S15_C1A_正式实施授权与裁决回传_V1.0/C1A核心输入/02_C1A_98天赋逐节点补规表.csv'));bytes=0;note='C1-A-2 设计输入（已迁移）'})
$index.Add([pscustomobject]@{kind='design_C1B';path='docs/temp/P2-基础循环搭建/P2-Part2-新版数据重构/P2-4-2-第2批实施包/P2-4 S15_C1B_星图真实消费与条件购买路线 V0.5/C1B_两阵容替代购买合法路线_条件性.csv';sha256=(FileHash (Join-Path $Workspace 'docs/temp/P2-基础循环搭建/P2-Part2-新版数据重构/P2-4-2-第2批实施包/P2-4 S15_C1B_星图真实消费与条件购买路线 V0.5/C1B_两阵容替代购买合法路线_条件性.csv'));bytes=0;note='C1B 57/55 路线（已迁移）'})
$index.Add([pscustomobject]@{kind='design_C2B';path='docs/temp/P2-基础循环搭建/P2-Part2-新版数据重构/P2-4-2-第2批实施包/P2-4 S15_C2-B｜装备、卷轴与药剂完整投放/C2B_5卷轴4药剂真实行为与工程扩展合同.csv';sha256=(FileHash (Join-Path $Workspace 'docs/temp/P2-基础循环搭建/P2-Part2-新版数据重构/P2-4-2-第2批实施包/P2-4 S15_C2-B｜装备、卷轴与药剂完整投放/C2B_5卷轴4药剂真实行为与工程扩展合同.csv'));bytes=0;note='SC/PO 设计合同（已迁移）'})
$index | Export-Csv -LiteralPath (Join-Path $out "freeze_index.csv") -NoTypeInformation -Encoding UTF8

# 2) 测试入口与输入依赖
$entries=@(
 @('default 全量Core','dotnet run --project tests/IdleCrpg.Checks.csproj','无','0','0','已复跑'),
 @('--c1a-checks','… -- --c1a-checks','config/skill','0','0','已复跑'),
 @('--c1a2-checks','… -- --c1a2-checks','docs/temp/P2-4 S15_C1A_…/C1A核心输入/{02_C1A_98天赋逐节点补规表.csv,S07_8套24点构筑合法清单_V0.8.csv}','-532462766','0','迁移后旧路径缺失→HasDesignNode 返回 true→14个不存在编号被判缺失；临时恢复输入后 PASS'),
 @('--c1b-checks','… -- --c1b-checks','config/star','0','0','已复跑'),
 @('--c1b3-checks','… -- --c1b3-checks','docs/temp/P2-4 S15_C1B_…/C1B_两阵容替代购买合法路线_条件性.csv','-532462766','0','迁移后旧路径缺失→DirectoryNotFoundException；临时恢复输入后 PASS'),
 @('--c2a-checks','… -- --c2a-checks','config/chapters/act*/dungeon/floor_rhythm.csv','0','0','已复跑'),
 @('--c2a-boss-checks','… -- --c2a-boss-checks','castle_threat/boss_phase','0','0','已复跑'),
 @('--c2b-checks','… -- --c2b-checks','drop_table/equipment/scroll/potion','0','0','已复跑'),
 @('--c3-checks','… -- --c3-checks','C3 数值输入','未在本轮复核','未在本轮复核','历史 PASS（2026-10-26），本轮未重跑')
)
$entries | ForEach-Object {
    [pscustomobject]@{entry=$_[0];command=$_[1];input_dependency=$_[2];exit_as_found=$_[3];exit_with_input_restored=$_[4];note=$_[5]}
} | Export-Csv -LiteralPath (Join-Path $out "freeze_test_entries.csv") -NoTypeInformation -Encoding UTF8

# 3) 开放项
$openCsv=Join-Path $finalMaster "recon_final_open_items.csv"
$open=@(Import-Csv -LiteralPath $openCsv -Encoding UTF8)
$open | Group-Object family | ForEach-Object {
    [pscustomobject]@{group=$_.Name;count=$_.Count;sample=($_.Group | Select-Object -First 1).source_id}
} | Export-Csv -LiteralPath (Join-Path $out "freeze_open_items_by_family.csv") -NoTypeInformation -Encoding UTF8

# 4) 边界与回滚
$bounds=@'
item,value
生产改动,无（本轮仅写 artifacts/qa 与 docs/reviews 与 tools/ 审计脚本）
Scripts SHA,'+$code.hash+'
Scripts 文件数,'+$code.files+'
config SHA,'+$cfg.hash+'
config 文件数,'+$cfg.files+'
版本控制,git 不在 PATH，无法给出分支/提交；以文件哈希代替
回滚方式,删除 artifacts/qa/s15-* 与新增 tools/S15-*.ps1；docs/reviews 仅追加，可用 § 级删除
存档兼容,未改 SessionSnapshot/存档格式；未写玩家存档
'@
[IO.File]::WriteAllText((Join-Path $out "freeze_boundaries.csv"),$bounds,$utf8)
"out=$out"
Get-ChildItem $out | Select-Object Name,Length | Format-Table -AutoSize
