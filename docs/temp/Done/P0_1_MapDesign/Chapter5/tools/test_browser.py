from pathlib import Path
from playwright.sync_api import sync_playwright
R=Path(__file__).resolve().parents[1]
with sync_playwright() as pw:
 b=pw.chromium.launch(headless=True,executable_path='/usr/bin/chromium',args=['--no-sandbox','--disable-gpu'])
 p=b.new_page(viewport={'width':1710,'height':1080},device_scale_factor=1)
 errors=[];p.on('pageerror',lambda exc:errors.append(str(exc)))
 p.set_content((R/'Chapter5_Act5_Interactive_Map_V0.1.html').read_text(encoding='utf-8'),wait_until='load')
 p.wait_for_timeout(200)
 assert p.locator('.tile').count()==121,p.locator('.tile').count()
 assert p.locator('.tile.void').count()==45
 assert p.locator('.tile.town').count()==1
 assert p.evaluate('MAP.poi.length')==64
 assert p.evaluate('MAP.floorPlans.length')==573
 assert p.locator('.tile.R10').count()==5 and p.locator('.tile.R11').count()==5
 assert not errors,errors
 print('BOOT',p.title(),p.locator('.tile').count(),'TILES')
 (R/'preview').mkdir(exist_ok=True)
 p.screenshot(path=str(R/'preview/Chapter5_Act5_Interactive_Preview_V0.1.png'),full_page=True)
 p.locator('.layout > div:first-child > section.panel').first.screenshot(path=str(R/'preview/Chapter5_Act5_Map_Grid_V0.1.png'))
 for d,rank in [('NORMAL','32–43'),('NIGHTMARE','66–72'),('HELL','93–97')]:
  p.locator('#difficulty').select_option(d)
  assert rank in p.locator('#nextBox').inner_text()
  p.locator('#run').click()
  assert p.evaluate('(x)=>simulations[x].done.size',d)==5,(d,p.evaluate('(x)=>simulations[x].done.size',d))
  # approve the nine main Castles one by one; each region is locked behind previous Castle
  for rid in range(1,10):
   p.evaluate('(r)=>setRegion(r,null)',rid)
   assert p.locator('#approveCastle').is_enabled(),('castle cannot approve',d,rid)
   p.locator('#approveCastle').click();p.locator('#run').click()
   assert all(not q.startswith(('W10','W11')) for q in p.evaluate('(x)=>Array.from(simulations[x].done)',d))
  assert p.evaluate('(x)=>simulations[x].done.size',d)==54,('main count',d,p.evaluate('(x)=>simulations[x].done.size',d))
  # after main complete, side A and its castle, then side B explicitly
  p.evaluate('setRegion(10,null)');assert p.locator('#approveSide').is_enabled()
  p.locator('#approveSide').click();p.locator('#run').click()
  assert p.evaluate('(x)=>simulations[x].done.size',d)==58
  p.evaluate('setRegion(10,null)'); assert p.locator('#approveCastle').is_enabled()
  p.locator('#approveCastle').click();p.locator('#run').click()
  assert p.evaluate('(x)=>simulations[x].done.size',d)==59
  assert not any(q.startswith('W11') for q in p.evaluate('(x)=>Array.from(simulations[x].done)',d))
  p.evaluate('setRegion(11,null)');assert p.locator('#approveSide').is_enabled()
  p.locator('#approveSide').click();p.locator('#run').click()
  assert p.evaluate('(x)=>simulations[x].done.size',d)==63
  p.evaluate('setRegion(11,null)');assert p.locator('#approveCastle').is_enabled()
  p.locator('#approveCastle').click();p.locator('#run').click()
  assert p.evaluate('(x)=>simulations[x].done.size',d)==64
  print('PASS',d,'main=54,side=10,total=64')
 assert not errors,errors
 # reset only current difficulty test
 p.locator('#reset').click()
 assert p.evaluate('simulations.HELL.done.size')==0
 assert p.evaluate('simulations.NORMAL.done.size')==64
 print('BROWSER TEST PASS: no JS errors, three difficulty simulations, per-difficulty state isolation')
 b.close()
