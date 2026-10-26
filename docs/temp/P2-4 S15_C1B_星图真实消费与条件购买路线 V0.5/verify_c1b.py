from pathlib import Path
import csv
D=Path(__file__).resolve().parent

def read(n):
 with open(D/n,encoding='utf-8-sig',newline='') as f:return list(csv.DictReader(f))
N=read('C1B_31节点真实消费者与售卖门禁.csv');P=read('C1B_78阶逐级配置与开售控制.csv');R=read('C1B_两阵容替代购买合法路线_条件性.csv')
nodes={r['NodeId']:r for r in N}; assert len(nodes)==len(N)==31
assert len(P)==78 and sum(int(n['MaxRank']) for n in N)==78
assert len(R)==112
banned={'C-NEC','S02','S03','P03','G03','G02','A02','A03','F03'}
from collections import defaultdict
for scenario in sorted({r['Scenario'] for r in R}):
 owned=defaultdict(int);rows=sorted([r for r in R if r['Scenario']==scenario],key=lambda z:int(z['PurchaseSequence']))
 expected=57 if scenario.startswith('甲') else 55
 assert len(rows)==expected
 for index,r in enumerate(rows,1):
  name=r['NodeId']; after=int(r['RankAfter']); n=nodes[name];p=n['PrerequisiteOR']
  assert int(r['PurchaseSequence'])==index and name not in banned
  assert after==owned[name]+1 and after<=int(n['MaxRank'])
  assert p=='ROOT' or any(owned[req.split('@')[0]]>=int(req.split('@')[1]) for req in p.split('|'))
  if name.startswith('C-'):
   assert name in ({'C-BAR','C-RAN','C-SOR','C-PAL'} if scenario.startswith('甲') else {'C-RAN','C-PAL'})
  owned[name]=after
print('C1B_DESIGN_STATIC_PASS nodes=31 ranks=78 paths=57+55=112; NOT_RUNTIME_VERIFIED')
