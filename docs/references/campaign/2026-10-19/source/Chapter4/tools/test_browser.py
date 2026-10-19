from pathlib import Path
from playwright.sync_api import sync_playwright
root=Path('/mnt/data/D2_Act4_Map_V0_1')
with sync_playwright() as pw:
 b=pw.chromium.launch(headless=True,executable_path='/usr/bin/chromium',args=['--no-sandbox','--disable-gpu'])
 p=b.new_page(viewport={'width':1536,'height':1120},device_scale_factor=1)
 errors=[];p.on('pageerror',lambda e: errors.append(str(e)))
 p.set_content((root/'Chapter4_Act4_Interactive_Map_V0.1.html').read_text(encoding='utf-8'),wait_until='load')
 p.wait_for_timeout(180)
 assert p.locator('.tile').count()==49,p.locator('.tile').count()
 assert p.locator('.tile.town').count()==1
 assert p.locator('.tile.void').count()==20
 assert p.evaluate('MAP.poi.length')==24
 assert p.evaluate('MAP.floorPlans.length')==231
 print('BROWSER',p.title(),'CELLS',p.locator('.tile').count(),'ERR',errors)
 print('INITIAL',p.locator('#nextBox').inner_text()[:140])
 (root/'preview').mkdir(exist_ok=True)
 p.screenshot(path=str(root/'preview/Chapter4_Act4_Interactive_Preview_V0.1.png'),full_page=True)
 p.locator('.layout > div:first-child > section.panel').first.screenshot(path=str(root/'preview/Chapter4_Act4_Map_Grid_V0.1.png'))
 # Step auto -> reaches first Castle wait
 p.locator('#run').click()
 assert p.evaluate('simulations[difficulty].done.size')==3
 assert p.locator('#nextBox').inner_text().find('要塞')>=0 or 'Castle' in p.locator('#nextBox').inner_text()
 # No player authorization for side yet
 assert p.evaluate('simulations.NORMAL.sideApproved.size')==0
 # Approve sequential castles, ensure all main POIs complete but not optional
 for reg in (1,2,3,4):
  p.evaluate('(r)=>setRegion(r,null)',reg)
  btn=p.locator('#approveCastle')
  assert btn.is_enabled(),('cannot approve',reg,p.locator('#regionPanel').inner_text()[:200])
  btn.click()
  p.locator('#run').click()
  # branch not accidentally started
  assert all(not pid.startswith('D05-') for pid in p.evaluate('Array.from(simulations.NORMAL.done)'))
 done=p.evaluate('simulations.NORMAL.done.size')
 print('MAIN_DONE',done)
 assert done==20,done
 assert p.evaluate('simulations.NORMAL.sideApproved.size')==0
 # approve side after main completion, independently approve castle
 p.evaluate('setRegion(5,null)')
 assert p.locator('#approveSide').is_enabled()
 p.locator('#approveSide').click()
 p.locator('#run').click()
 assert p.evaluate('simulations.NORMAL.done.size')==23
 p.evaluate('setRegion(5,null)')
 assert p.locator('#approveCastle').is_enabled()
 p.locator('#approveCastle').click()
 p.locator('#run').click()
 assert p.evaluate('simulations.NORMAL.done.size')==24
 # per-difficulty isolation
 p.locator('#difficulty').select_option('HELL')
 assert '89–92' in p.locator('#nextBox').inner_text()
 assert p.evaluate('simulations.HELL.done.size')==0
 p.locator('#run').click()
 assert p.evaluate('simulations.HELL.done.size')==3
 print('BROWSER PASS completed normal 20 main + 4 side, independent Hell, no runtime JS errors')
 assert not errors,errors
 b.close()
