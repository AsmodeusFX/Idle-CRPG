param([string]$OutDir=(Get-ChildItem "d:/Project-Asmo/Idle-CRPG/artifacts/qa" -Directory -Filter 's15-c1-c3-recon-final-*' | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName)
$ErrorActionPreference="Stop"
$utf8=New-Object System.Text.UTF8Encoding($false)
function W([string]$n,[string]$raw){
    $lines=New-Object System.Collections.Generic.List[string]
    foreach($row in ($raw -split "`r?`n")){
        if($row.Trim() -eq ''){continue}
        $f=$row -split '\|'
        $lines.Add((($f | ForEach-Object { '"' + ($_ -replace '"','""') + '"' }) -join ','))
    }
    [IO.File]::WriteAllText((Join-Path $OutDir $n),(($lines -join "`r`n")+"`r`n"),$utf8)
}
W "recon_status_corrections.csv" @'
AuditId|original_status|new_status|authority|evidence|reason|owner
C1A2_TALENT98_UNIFORM|98 项统一 IMPLEMENTED_DIFFERENT|29 MATCHED_EVIDENCED + 69 IMPLEMENTED_DIFFERENT(OPEN)|P|design_S07_98nodes_v0.8.csv + config/skill/talent.csv + --c1a2-checks + 维度一致性规则|逐节点按"身份匹配/消费者存在/维度一致"三分；条件、评分、路径、站位、精英、下一次语义一律记近似并标 OPEN|工程+设计
C1A_SKILL_BEHAVIOR|16 项统一 MATCHED_EVIDENCED|13 EQUIVALENT + 3 APPROX(OPEN)|P|Review §22.3；config/skill/skill.csv|旋风斩/陨星术/祝福之锤为机制近似，ID 匹配但行为不等价|设计
C1A_T_BEHAVIOR|5 项 MATCHED_EVIDENCED|5 APPROX(OPEN)|P|Review §22.3；talent.csv|设计T路线分支未拆分，当前为单节点/互斥双光环近似|设计
C1B_ROUTE|NOT_VERIFIED(SOURCE_MISSING)|MATCHED_EVIDENCED|P|设计包迁移至 P2-基础循环搭建/P2-Part2-新版数据重构/P2-4-2-第2批实施包；临时恢复输入后 --c1b3-checks 退出0|源找回，非实现变化|工程
C2B_SCROLL|NOT_VERIFIED(SOURCE_MISSING)|MATCHED_EVIDENCED|P|C2B_5卷轴4药剂真实行为与工程扩展合同.csv；SC01-05→scroll_bolt/arc/flame/frost/banner；--c2b-checks 退出0|设计源已定位并映射|工程
C2B_POTION|NOT_VERIFIED(SOURCE_MISSING)|3 MATCHED_EVIDENCED + 1 SUPERSEDED_BY_USER|P|同上；PO01/03/04→potion_might/greed/swift；PO02→potion_tough|PO02 设计[P/H]+15%MaxHP 被 POT-01[C] 覆盖为百分比减伤，15%仍[H]|工程+用户
C2B_DROP31|NOT_VERIFIED(SOURCE_MISSING)|IMPLEMENTED_DIFFERENT|H|C3_31怪级联合参数待校准_H-B1.csv（表内 H_NOT_AUTHORIZED）；drop_table.csv 31行|31档覆盖存在，数值为工程首轮[H]，未逐项采用设计表|工程
TEST_PATH_HARDCODED|NOT_APPLICABLE|ENGINEERING_ADVICE_ONLY|E|tests/C1B3ClosureChecks.cs:13 常量指向旧路径 docs/temp/P2-4 S15_C1B_…|建议改为可配置路径或在新目录放 manifest 供测试解析；本轮按只读要求不修改测试源码|工程
'@
W "recon_path_dependency.csv" @'
Item|Value|Note
TestFile|tests/C1B3ClosureChecks.cs|RouteCsv 常量
HardcodedPath|docs/temp/P2-4 S15_C1B_星图真实消费与条件购买路线 V0.5/C1B_两阵容替代购买合法路线_条件性.csv|设计包已迁移后该路径不存在
CurrentBehavior|DirectoryNotFoundException|测试在读取输入端失败，不是实现回归
WorkaroundUsed|从迁移后路径临时复制到旧路径 → 跑 → 删除|未改测试源码；docs/temp 临时目录已清理
Recommendation|改为可配置路径/搜索新目录/读取 manifest|属后续工程小改，本轮未实施
'@
"written $OutDir"
