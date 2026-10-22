import json,re
from pathlib import Path
ROOT=Path('/mnt/data/D2_Act3_Map_V0_1')
T=Path('/mnt/data/_act2_ref/D2_Act2_Map_V0_1/tools/html_template.html').read_text(encoding='utf-8')
MAP=json.loads((ROOT/'data/ACT03_MAP_V0.1.json').read_text(encoding='utf-8'))
import csv
with (ROOT/'data/ACT03_FLOOR_PLAN_V0.1.csv').open(encoding='utf-8-sig',newline='') as f:
 MAP['floorPlans']=[{k:int(v) if k in {'FloorIndex','FloorCount','LayoutRows','LayoutCols','ActiveCellTarget','EntryCellCount','CorridorCellCount','CombatCellCount','EliteCellCount','EventCellCount','ExitCellCount','BossArenaCellCount','FloorMonsterLevel','PoiChallengeLevel','RecommendedPlayerLevel','CombatMonstersPerCellTarget','EliteGroupMonsterTarget','BossActorCount','BossEscortTarget','PlannedKillTarget'} else v for k,v in row.items()} for row in csv.DictReader(f)]
T=T.replace('Chapter 2 · 鲁·高因｜地图方案 V0.1','Chapter 3 · 库拉斯海港｜地图方案 V0.1')
T=T.replace('ACT II · MAP DESIGN · V0.1','ACT III · MAP DESIGN · V0.1')
T=T.replace('鲁·高因：沙漠、古墓与都瑞尔','库拉斯：雨林、神庙与墨菲斯托')
T=T.replace('9×9 世界地图','10×10 世界地图')
T=T.replace('格子右上角', '格子右上角')
T=T.replace('鲁·高因｜主城（不战斗）','库拉斯海港｜主城（不战斗）')
T=T.replace('<span class="short">鲁·高因</span>', '<span class="short">库拉斯海港</span>')
T=T.replace('鲁·高因', '库拉斯海港')
T=T.replace('碎石荒地','蜘蛛森林')
T=T.replace('塔·拉夏古墓入口','经典任务碎片')
T=T.replace('const levelBand={NORMAL:\'怪物 13–20 / 推荐玩家 12–21\',NIGHTMARE:\'怪物 50–55 / 推荐玩家 49–56\',HELL:\'怪物 79–83 / 推荐玩家 78–84\'};', 'const levelBand={NORMAL:\'怪物 21–26 / 推荐玩家 20–27\',NIGHTMARE:\'怪物 56–61 / 推荐玩家 55–62\',HELL:\'怪物 84–88 / 推荐玩家 83–89\'};')
T=T.replace("selectedPoi='A01-D01'", "selectedPoi='B01-D01'")
T=T.replace('ri<9;ri++','ri<MAP.rows;ri++').replace('ci<9;ci++','ci<MAP.columns;ci++')
T=T.replace('const size=tileW*9+gap*8;', 'const size=tileW*MAP.columns+gap*(MAP.columns-1);')
T=T.replace("const side=gate.id==='G-T-08'||gate.id==='G-04-09';", "const side=gate.kind.startsWith('SIDE_');")
T=T.replace("const cls=gate.kind==='ARCANE_PORTAL'?'portal':side?'side':'road';", "const cls=side?'side':gate.kind.includes('BRIDGE')?'portal':'road';")
T=T.replace('grid-template-columns:repeat(9,var(--cell));grid-template-rows:repeat(9,var(--cell))','grid-template-columns:repeat(10,var(--cell));grid-template-rows:repeat(10,var(--cell))')
T=T.replace('--cell:clamp(35px,4.8vw,65px)', '--cell:clamp(35px,4.4vw,61px)')
T=T.replace('.tile.R1{background:#856046}.tile.R2{background:#aa8553}.tile.R3{background:#597c77}.tile.R4{background:#b39267}.tile.R5{background:#806a5b}.tile.R6{background:#685b86}.tile.R7{background:#a16a4b}.tile.R8{background:#536d72}.tile.R9{background:#69735f}', '.tile.R1{background:#366d46}.tile.R2{background:#537a39}.tile.R3{background:#697f5d}.tile.R4{background:#8b8466}.tile.R5{background:#92865e}.tile.R6{background:#687680}.tile.R7{background:#835b51}.tile.R8{background:#3d7475}.tile.R9{background:#64577d}')
T=T.replace('radial-gradient(ellipse at 30% 0%,#26312d 0%,#10171c 45%,#0b1014 100%)','radial-gradient(ellipse at 30% 0%,#273b31 0%,#101a17 49%,#0b1014 100%)')
T=T.replace("['','#856046','#aa8553','#597c77','#b39267','#806a5b','#685b86','#a16a4b','#536d72','#69735f']", "['','#366d46','#537a39','#697f5d','#8b8466','#92865e','#687680','#835b51','#3d7475','#64577d']")
T=T.replace('<div class="metric"><b>9×9</b>', '<div class="metric"><b>10×10</b>')
T=T.replace('<div class="metric"><b>40 + 9</b>', '<div class="metric"><b>44 + 9</b>')
T=T.replace('<div class="metric"><b>7</b><span>经典任务碎片</span>', '<div class="metric"><b>3</b><span>克林姆任务必得物件</span>')
T=T.replace('${done} / 49', '${done} / 53')
start=T.index('   <ol class="noteList">')
end=T.index('</ol>',start)+5
T=T[:start]+'''   <ol class="noteList"><li>库拉斯海港 → <b>蜘蛛森林</b> → <b>剥皮丛林</b> → <b>下层库拉斯</b> → <b>库拉斯商场</b> → <b>上层库拉斯</b> → <b>库拉斯堤道</b> → <b>崔凡克</b>。</li><li><b>庞大湿地</b>在蜘蛛森林 Castle 后开放；<b>古神庙群</b>在库拉斯商场 Castle 后开放。两条支线均必须玩家批准，且不阻断主线。</li><li>克林姆之眼（蜘蛛洞窟）、之脑（剥皮地牢）、之心（库拉斯下水道）均放在主线必清地牢；议会连枷与强制球体放在崔凡克 Castle 内，绝不形成前置死锁。</li><li>终局 Castle 内先打议会，再深入 <b>憎恨囚牢 1–3 层</b>，最后对战墨菲斯托。</li></ol>'''+T[end:]
T=T.replace('这只是本地浏览器内的状态模拟，不连接游戏、不会修改 Godot 存档。', '这只是本地浏览器内的状态模拟，不连接游戏、不会修改 Godot 存档。')
T=T.replace('Castle 与支线需要手动批准','Castle 与支线需要手动批准')
T=T.replace('D2 情怀区域','D2 原典记忆点 + 改造')
T=T.replace('const MAP=/*__MAP_DATA__*/;', 'const MAP='+json.dumps(MAP,ensure_ascii=False,separators=(',',':')).replace('</','<\\/')+';')
# A caution: no inline HTML event injection: all content is constant designer-config in static local file.
assert '/*__MAP_DATA__*/' not in T
assert 'A01-D01' not in T
out=ROOT/'Chapter3_Act3_Interactive_Map_V0.1.html'
out.write_text(T,encoding='utf-8')
print('HTML',out, len(T),'bytes')
