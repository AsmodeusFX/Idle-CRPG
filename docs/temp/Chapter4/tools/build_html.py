from pathlib import Path
import json,csv
ROOT=Path('/mnt/data/D2_Act4_Map_V0_1')
SRC=Path('/mnt/data/_act2_ref/D2_Act2_Map_V0_1/tools/html_template.html')
T=SRC.read_text(encoding='utf-8')
M=json.loads((ROOT/'data/ACT04_MAP_V0.1.json').read_text(encoding='utf-8'))
INT={'FloorIndex','FloorCount','LayoutRows','LayoutCols','ActiveCellTarget','EntryCellCount','CorridorCellCount','CombatCellCount','EliteCellCount','EventCellCount','ExitCellCount','BossArenaCellCount','FloorMonsterLevel','PoiChallengeLevel','RecommendedPlayerLevel','CombatMonstersPerCellTarget','EliteGroupMonsterTarget','BossActorCount','BossEscortTarget','PlannedKillTarget','SealActivationCount','FixedUniqueActorCount'}
with (ROOT/'data/ACT04_FLOOR_PLAN_V0.1.csv').open(encoding='utf-8-sig',newline='') as fh:
 M['floorPlans']=[{k:int(v) if k in INT else v for k,v in x.items()} for x in csv.DictReader(fh)]
T=T.replace('Chapter 2 · 鲁·高因｜地图方案 V0.1','Chapter 4 · 群魔堡垒｜地狱远征地图 V0.1')
T=T.replace('ACT II · MAP DESIGN · V0.1','ACT IV · HELL EXPEDITION · V0.1')
T=T.replace('鲁·高因：沙漠、古墓与都瑞尔','群魔堡垒：火焰之河与混沌避难所')
T=T.replace('9×9 世界地图','7×7 世界地图')
T=T.replace('鲁·高因｜主城（不战斗）','群魔堡垒｜主城（不战斗）')
T=T.replace('<span class="short">鲁·高因</span>','<span class="short">群魔堡垒</span>')
T=T.replace('鲁·高因','群魔堡垒')
T=T.replace('碎石荒地','郊外大草原')
T=T.replace('塔·拉夏古墓入口','混沌避难所战斗阶段')
T=T.replace("const levelBand={NORMAL:'怪物 13–20 / 推荐玩家 12–21',NIGHTMARE:'怪物 50–55 / 推荐玩家 49–56',HELL:'怪物 79–83 / 推荐玩家 78–84'};", "const levelBand={NORMAL:'怪物 27–31 / 推荐玩家 26–32',NIGHTMARE:'怪物 62–65 / 推荐玩家 61–66',HELL:'怪物 89–92 / 推荐玩家 88–93'};")
T=T.replace("selectedPoi='A01-D01'", "selectedPoi='D01-D01'")
T=T.replace('ri<9;ri++','ri<MAP.rows;ri++').replace('ci<9;ci++','ci<MAP.columns;ci++')
T=T.replace('const size=tileW*9+gap*8;', 'const size=tileW*MAP.columns+gap*(MAP.columns-1);')
T=T.replace("const side=gate.id==='G-T-08'||gate.id==='G-04-09';", "const side=gate.kind.startsWith('SIDE_');")
T=T.replace("const cls=gate.kind==='ARCANE_PORTAL'?'portal':side?'side':'road';", "const cls=side?'side':gate.kind.includes('LAVA')?'portal':'road';")
T=T.replace('grid-template-columns:repeat(9,var(--cell));grid-template-rows:repeat(9,var(--cell))','grid-template-columns:repeat(7,var(--cell));grid-template-rows:repeat(7,var(--cell))')
T=T.replace('--cell:clamp(35px,4.8vw,65px)', '--cell:clamp(55px,5.4vw,80px)')
T=T.replace('clamp(37px,7.1vw,65px)','clamp(51px,8vw,78px)').replace('clamp(31px,9.3vw,60px)','clamp(36px,12vw,66px)')
T=T.replace('radial-gradient(ellipse at 30% 0%,#26312d 0%,#10171c 45%,#0b1014 100%)','radial-gradient(ellipse at 35% 0%,#3a231c 0%,#141a1b 46%,#090d10 100%)')
T=T.replace('.tile.R1{background:#856046}.tile.R2{background:#aa8553}.tile.R3{background:#597c77}.tile.R4{background:#b39267}.tile.R5{background:#806a5b}.tile.R6{background:#685b86}.tile.R7{background:#a16a4b}.tile.R8{background:#536d72}.tile.R9{background:#69735f}', '.tile.R1{background:#6a4b40}.tile.R2{background:#7b514d}.tile.R3{background:#6e4e5a}.tile.R4{background:#a35d30}.tile.R5{background:#714f77}')
T=T.replace("['','#856046','#aa8553','#597c77','#b39267','#806a5b','#685b86','#a16a4b','#536d72','#69735f']", "['','#6a4b40','#7b514d','#6e4e5a','#a35d30','#714f77']")
T=T.replace('<div class="metric"><b>9×9</b>', '<div class="metric"><b>7×7</b>')
T=T.replace('<div class="metric"><b>9</b><span>攻略区域（2 支线）</span>', '<div class="metric"><b>5</b><span>攻略区域（1 支线）</span>')
T=T.replace('<div class="metric"><b>40 + 9</b>', '<div class="metric"><b>19 + 5</b>')
T=T.replace('<div class="metric"><b>7</b><span>混沌避难所战斗阶段</span>', '<div class="metric"><b>6</b><span>混沌避难所连续阶段</span>')
T=T.replace('${done} / 49', '${done} / 24')
T=T.replace('D2 情怀区域','原典地狱主题')
T=T.replace('普通地牢','战斗地牢')
T=T.replace('每层格数和刷怪密度为候选建模值，非最终实机结果。','每层格数和刷怪密度为候选建模值；混沌避难所的六层表示连续战斗阶段，不是D2原版六个地下楼层。')
# The floor budget preview should explicitly display per-combat-cell density and fixed seal actors.
T=T.replace('`<tr><td>${f.FloorIndex}</td><td>${f.ActiveCellTarget}</td><td>${f.CombatCellCount}</td><td>${f.EliteCellCount}</td><td>${f.BossArenaCellCount}</td><td>${f.PlannedKillTarget}</td></tr>`', '`<tr title="${f.FloorDisplayName}"><td>${f.FloorIndex}</td><td>${f.ActiveCellTarget}</td><td>${f.CombatCellCount}×${f.CombatMonstersPerCellTarget}</td><td>${f.EliteCellCount}×${f.EliteGroupMonsterTarget}</td><td>${f.BossArenaCellCount}</td><td>${f.PlannedKillTarget}</td></tr>`')
T=T.replace('<th>战斗格</th><th>精英格</th><th>Boss 格</th><th>怪物</th>', '<th>战斗格×怪/格</th><th>精英格×怪/组</th><th>Boss 格</th><th>怪物</th>')
T=T.replace('本难度约 <b style="color:#e9c98b">${target.plannedKills[difficulty]}</b> 次击杀。', '本难度约 <b style="color:#e9c98b">${target.plannedKills[difficulty]}</b> 次击杀。${target.name==="混沌避难所"?"五封印与三位封印守卫为固定机制，分布在阶段3–5。":""}')

start=T.index('   <ol class="noteList">')
end=T.index('</ol>',start)+len('</ol>')
T=T[:start]+'''   <ol class="noteList"><li><b>群魔堡垒 → 郊外大草原 → 绝望平原 → 神罚之城 → 火焰之河与混沌避难所</b>。大地图只负责行走与解锁，不刷怪。</li><li>绝望平原 Castle 的最终 Boss 是 <b>衣卒尔</b>；火焰之河的终局 Castle 包含五处封印、三组封印守卫与 <b>迪亚波罗</b>。</li><li><b>地狱熔炉</b>位于火焰之河的下方岔路，解锁后仍要玩家手动批准进入，支线 Castle 也需再次批准。此支线不影响进入混沌避难所。</li><li>四条主线区域仅通过配置的石桥 / 城门 Gate 连通，其他毗邻颜色不同的地块视为被城墙、断崖、熔流阻断。</li><li>Castle 六阶段深地牢属于本游戏改造；官方 D2 Act 4 没有这些额外原创地下城，文件已清楚标注来源属性。</li></ol>'''+T[end:]
# Show correct floor-level Boss and special seal event counts in the detail table
T=T.replace('<th>Boss 格</th><th>怪物</th>', '<th>Boss 格</th><th>怪物</th>')
# strengthen scene-facing design for the scarce lava paths
T=T.replace('.tile.void{background:#262f33;', '.tile.void{background:repeating-linear-gradient(142deg,#25272a 0px,#25272a 9px,#303038 9px,#303038 10px);')
T=T.replace('.tile.R4{background:#a35d30}', '.tile.R4{background:linear-gradient(160deg,#a35d30,#6f3020)}')
T=T.replace('.tile.R5{background:#714f77}', '.tile.R5{background:linear-gradient(160deg,#714f77,#43304b)}')
T=T.replace('const MAP=/*__MAP_DATA__*/;', 'const MAP='+json.dumps(M,ensure_ascii=False,separators=(',',':')).replace('</','<\\/')+';')
assert '/*__MAP_DATA__*/' not in T
assert 'A01-D01' not in T
assert 'ri<9' not in T and 'ci<9' not in T
assert '40 + 9' not in T
path=ROOT/'Chapter4_Act4_Interactive_Map_V0.1.html'
path.write_text(T,encoding='utf-8')
print(path,len(T))
