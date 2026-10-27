param([string]$Workspace="d:/Project-Asmo/Idle-CRPG")
$ErrorActionPreference="Stop"
$root=Join-Path $Workspace "artifacts/handoff/S15_FINAL_HANDOFF_EVIDENCE"
$utf8=New-Object System.Text.UTF8Encoding($false)
$utf8b=New-Object System.Text.UTF8Encoding($true)

# 1) 证据索引
$ev=@'
system|entry|command|fixed_seed|runtime_evidence|screenshots|status
C1-A|默认全量 Core|dotnet run --project tests/IdleCrpg.Checks.csproj|test-internal|03_evidence/logs/core.log|--|PASS(exit0)
C1-A|--c1a-checks|dotnet run --project tests/IdleCrpg.Checks.csproj -- --c1a-checks|20261027/20261028/20261029|03_evidence/logs/c1a.log|03_evidence/screenshots/fixed-skills-s3-learned.png|PASS(exit0)
C1-A-2|--c1a2-checks|dotnet run --project tests/IdleCrpg.Checks.csproj -- --c1a2-checks|7000+index/8801|03_evidence/logs/c1a2.log|03_evidence/screenshots/talent-region2-modifiers.png;talent-region5-traits.png|KNOWN_UNFIXED(输入路径)
C1-B|--c1b-checks|dotnet run --project tests/IdleCrpg.Checks.csproj -- --c1b-checks|test-internal|03_evidence/logs/c1b.log|03_evidence/screenshots/star-class-trait-purchased.png;star-b3-rejections.png|PASS(exit0)
C1-B|--c1b3-checks|dotnet run --project tests/IdleCrpg.Checks.csproj -- --c1b3-checks|test-internal|历史产物 s15-c1b3-7b9ce5eb…（本轮恢复输入后PASS）|03_evidence/screenshots/star-b3-rejections.png|KNOWN_UNFIXED(输入路径)
C2-A|--c2a-checks|dotnet run --project tests/IdleCrpg.Checks.csproj -- --c2a-checks|777001/777002|03_evidence/logs/c2a.log|--|PASS(exit0)
C2-A|--c2a-boss-checks|dotnet run --project tests/IdleCrpg.Checks.csproj -- --c2a-boss-checks|test-internal|03_evidence/logs/c2a-boss.log;03_evidence/boss/*.csv|--|PASS(exit0)
C2-B|--c2b-checks|dotnet run --project tests/IdleCrpg.Checks.csproj -- --c2b-checks|test-internal|03_evidence/logs/c2b.log|03_evidence/screenshots/four-same-class-buff-sources-and-cooldowns.png;layout-1280x720.png|PASS(exit0)
C3|--c3-checks|dotnet run --project tests/IdleCrpg.Checks.csproj -- --c3-checks|3301/3302|UNAVAILABLE（本轮只读补交未重跑）|--|UNAVAILABLE
'@
[IO.File]::WriteAllText((Join-Path $root "03_evidence/evidence_index.csv"),$ev.Replace("`r`n","`n").Replace("`n","`r`n"),$utf8)

# 2) 指纹
$cs=@(Get-ChildItem (Join-Path $Workspace "Idle-CRPG/Scripts") -Recurse -File -Filter *.cs)
$cf=@(Get-ChildItem (Join-Path $Workspace "Idle-CRPG/config") -Recurse -File -Filter *.csv)
$sha=[System.Security.Cryptography.SHA256]::Create()
function ComputeHashFiles($files){ $cat=($files|Sort-Object FullName|ForEach-Object{(Get-FileHash $_.FullName -Algorithm SHA256).Hash}) -join ''; return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($cat))) -replace '-','').ToLower() }
$review=Join-Path $Workspace "docs/reviews/2026-10-25_p2_4_first_batch.md"
$fp='{"generated":"'+(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')+'","cs_files":'+$cs.Count+',"cs_sha256":"'+(ComputeHashFiles $cs)+'","csv_files":'+$cf.Count+',"csv_sha256":"'+(ComputeHashFiles $cf)+'","review_sha256":"'+((Get-FileHash $review -Algorithm SHA256).Hash.ToLower())+'","dotnet":"'+((& dotnet --version|Out-String).Trim())+'","os":"'+[Environment]::OSVersion.VersionString+'","git":"UNAVAILABLE(git not in PATH)","c1a2_exit":-532462766,"c1b3_exit":-532462766}'
[IO.File]::WriteAllText((Join-Path $root "05_fingerprint/workspace_fingerprint.json"),$fp,$utf8)

# 3) 清单（含 UNAVAILABLE 项）
$rows=New-Object System.Collections.Generic.List[object]
$files=@(Get-ChildItem $root -Recurse -File | Where-Object { $_.Name -ne 'FILE_MANIFEST.csv' })
foreach($f in ($files | Sort-Object FullName)){
    $rel=$f.FullName.Substring($root.Length+1)
    $rows.Add([pscustomobject]@{relative_path=$rel;bytes=$f.Length;sha256=(Get-FileHash $f.FullName -Algorithm SHA256).Hash.ToLower();status='INCLUDED';note=''})
}
foreach($miss in @(
 @('S15_FINAL_HANDOFF_EVIDENCE/05_fingerprint/git_commit.txt','','UNAVAILABLE','git 不在 PATH，无法给出分支/提交/未提交差异；以 cs/csv 整体 SHA 作为冻结依据'),
 @('S15_FINAL_HANDOFF_EVIDENCE/03_evidence/logs/c3.log','','UNAVAILABLE','本轮仅补交原件，未重跑 C3；历史 PASS 见 Review §31'),
 @('S15_FINAL_HANDOFF_EVIDENCE/03_evidence/c3/*','','UNAVAILABLE','C3 逐场模拟输出未纳入本次精简包，可在 artifacts/simulation 复现'))){
    $rows.Add([pscustomobject]@{relative_path=$miss[0];bytes=$miss[1];sha256='';status=$miss[2];note=$miss[3]})
}
$rows | Export-Csv -LiteralPath (Join-Path $root "04_manifest/FILE_MANIFEST.csv") -NoTypeInformation -Encoding UTF8

# 4) 打包
$zip=Join-Path $Workspace "artifacts/handoff/S15_FINAL_HANDOFF_EVIDENCE.zip"
if(Test-Path $zip){ Remove-Item $zip -Force }
Compress-Archive -Path $root -DestinationPath $zip -CompressionLevel Optimal
"zip=$zip bytes=$((Get-Item $zip).Length)"
Get-ChildItem $root -Recurse -File | Measure-Object -Property Length -Sum | ForEach-Object { "files=$($_.Count) bytes=$($_.Sum)" }
