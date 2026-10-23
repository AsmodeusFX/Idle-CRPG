"""Import frozen chapter packages and apply the explicit V0.2 topology patch.
No player saves or shared combat values are touched. Run --check to compare.
"""
from pathlib import Path
import argparse, csv, io, json, hashlib, shutil, random

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'docs/temp'
TARGET=ROOT/'Idle-CRPG/config'
# 转换产物只写忽略目录；docs/references 已于 2026-10-23 删除，不能由本脚本重建。
ARCHIVE=ROOT/'artifacts/campaign-import'
DIFFS=['NORMAL','NIGHTMARE','HELL']

def readcsv(path):
    with path.open(encoding='utf-8-sig',newline='') as f:return list(csv.DictReader(f))

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
    changed=[];summary=[]
    def emit(path,fields,rows):
        out=io.StringIO(newline='');w=csv.DictWriter(out,fields,lineterminator='\n');w.writeheader();w.writerows(rows);text=out.getvalue()
        full=TARGET/path
        if not full.exists() or full.read_text(encoding='utf-8-sig')!=text:
            changed.append(str(path))
            if not args.check:full.parent.mkdir(parents=True,exist_ok=True);full.write_text(text,encoding='utf-8')
        if not args.check:Path(str(full)+'.import').write_text('[remap]\n\nimporter="keep"\n',encoding='utf-8')
    chapter_rows=[]
    for act in range(1,6):
        data=SOURCE/f'Chapter{act}'/'data';m=json.loads(next(data.glob('*MAP*.json')).read_text(encoding='utf-8-sig'))
        region=readcsv(next(data.glob('*REGION*.csv')));poi=readcsv(next(data.glob('*POI*.csv')));floors=readcsv(next(data.glob('*FLOOR_PLAN*.csv')))
        grid=[list(r) for r in m['gridRows']];height=len(grid);width=len(grid[0]);town=m['town']
        towns=town.get('tiles',[[town.get('row'),town.get('col')]])
        if act==1:grid[2][5]='4'
        if act==5:grid[5][7]='6'
        castles={int(r['RegionId'].split('_')[-1]):r['CastlePoiId'] for r in region}
        parents={int(r['RegionId'].split('_')[-1]):[int(p.split('_')[-1]) if act==1 else next(k for k,v in castles.items() if v==p) for p in r.get('UnlockParents',r.get('UnlockParentCastleIds','')).split(';') if p] for r in region}
        if act==1:parents[6]=[4];parents[7]=[5,6]
        if act==2:parents[3]=[1];parents[4]=[2,3]
        if act==3:parents[6]=[4];parents[7]=[5,6]
        if act==5:parents[6]=[4];parents[7]=[5,6]
        gates=[]
        for g in m['gates']:
            if act==1:a=[g['from']['row'],g['from']['col']];b=[g['to']['row'],g['to']['col']];gid=g['gateId'];kind='ROAD'
            else:a=g['a'];b=g['b'];gid=g['id'];kind=g.get('kind','ROAD')
            gates.append((gid,a,b,kind))
        new={1:('G04-06',[3,6],[4,6]),2:('G-01-03',[4,5],[3,5]),3:('G-04-06',[6,9],[6,10]),5:('G-04-06',[5,8],[6,8])}
        if act in new:gid,a,b=new[act];gates.append((gid,a,b,'PATCH_ROAD'))
        tile=lambda row,col:(row-1)*width+col
        anchor=tile(*towns[0]);base=Path(f'chapters/act{act}')
        terrain=['grass','desert','tropical','hell','snow'][act-1]
        regions=[];rulers=[];styles=[];parts=[];prereq=[];oldprereq=[]
        for y,row in enumerate(grid):
            for x,char in enumerate(row):
                town_cell=[y+1,x+1] in towns;index=0 if char in 'xT0' else int(char,16)
                enter=char!='x';name=town['name'] if town_cell else (region[index-1].get('Name',region[index-1].get('RegionName')) if index else '\u5c71\u8109')
                rid=tile(y+1,x+1);regions.append(dict(id=rid,x=x,y=y,name=name,terrain=terrain if enter else 'mountain',enterable=int(enter)))
                if enter and not town_cell:rulers.append(dict(region_id=rid,castle_id=castles[index]))
        colors=['875548','6e8c8d','786579','a78a59','516d60','4e6871','8b7e61','6a627c','665b77','6b8299','786677']
        for r in region:
            i=int(r['RegionId'].split('_')[-1]);optional=r['IsOptional']=='1';ids=[t['region_id'] for t in rulers if t['castle_id']==castles[i]]
            parts.append(dict(id=f'ap{i}',castle_id=castles[i],initially_unlocked=int(not parents[i]),unlock_mode='all' if len(parents[i])>1 else 'any'))
            styles.append(dict(id=castles[i],region_index=i,name=r.get('Name',r.get('RegionName')),color=colors[(i-1)%len(colors)]))
            for n,p in enumerate(parents[i]):
                prereq.append(dict(id=f'ap{i}_{n}',region_id=ids[0],required_dungeon_id=castles[p]))
                # Explicit dungeon prerequisites are authoritative; legacy adjacent-only table stays empty.
        normals=[];profiles=[];challenges=[]
        for p in poi:
            ident=p['PoiId'];row=int(p['TileRow']);col=int(p['TileCol']);kind='castle' if p['Type'].lower()=='castle' else 'normal'
            normals.append(dict(id=ident,name=p['DisplayName'],region_id=tile(row,col),kind=kind,level_min=p['ChallengeNormal'],level_max=p['ChallengeNormal'],x=.5,y=.5))
            profiles.append(dict(id=ident,optional=p['IsOptionalRegion'],required_for_castle=p['RequiredForCastle'],floors=p['FloorCount'],auto_order=p['AutoOrder'],boss_name=p.get('FinalBossDisplayName',''),interior=p.get('Interior',''),status='prototype'))
            for d,label in enumerate(['Normal','Nightmare','Hell']):challenges.append(dict(id=f'{ident}_{d}',poi_id=ident,difficulty=d,challenge=p['Challenge'+label],recommended=p['Recommend'+label]))
        final=[p['PoiId'] for p in poi if p['Type'].lower()=='castle' and p['IsOptionalRegion']=='0'][-1]
        emit(base/'world/world.csv',['width','height','start_region_id','travel_speed','village_speed','trade_seconds','trade_range'],[dict(width=width,height=height,start_region_id=anchor,travel_speed=.65,village_speed=3.5,trade_seconds=2,trade_range=.7)])
        emit(base/'world/region.csv',list(regions[0]),regions);emit(base/'world/castle_region.csv',['region_id','castle_id'],rulers)
        emit(base/'world/areapart.csv',['id','castle_id','initially_unlocked','unlock_mode'],parts)
        emit(base/'world/areapart_prerequisite.csv',['areapart_id','required_areapart_id'],[])
        emit(base/'world/region_prerequisite.csv',['id','region_id','required_dungeon_id'],prereq)
        emit(base/'world/area_style.csv',['id','region_index','name','color'],styles)
        emit(base/'world/gate.csv',['id','from_region','to_region','kind'],[dict(id=gid,from_region=tile(*a),to_region=tile(*b),kind=kind) for gid,a,b,kind in gates])
        emit(base/'world/map_policy.csv',['id','value'],[dict(id='town_region_id',value=anchor),dict(id='town_regions',value=';'.join(str(tile(*t)) for t in towns)),dict(id='main_clear_poi',value=final),dict(id='version',value=3)])
        emit(base/'world/obstacle.csv',['id','kind','x','y','width','height'],[dict(id='map_edge',kind='rock',x=width-.05,y=height-.05,width=.04,height=.04)])
        emit(base/'village/village.csv',['id','name','region_id','x','y','npc_id'],[dict(id=town['id'],name=town['name'],region_id=anchor,x=.5,y=.5,npc_id='trader')])
        emit(base/'dungeon/dungeon.csv',list(normals[0]),normals);emit(base/'dungeon/poi_profile.csv',list(profiles[0]),profiles);emit(base/'dungeon/poi_challenge.csv',list(challenges[0]),challenges)
        converted=[]
        for f in floors:
            f=dict(f);d=f['DifficultyId'];f['id']=f"{f['PoiId']}_{d}_{f['FloorIndex']}"
            if act>1:
                f['MonsterLevel']=f['FloorMonsterLevel'];f['ElitePerCell']='1'
                for suffix in ['Min','Target','Max']:
                    f['CombatMonstersPerCell'+suffix]=f['CombatMonstersPerCellTarget']
                    f['EliteEscortPerCell'+suffix]=str(int(f['EliteGroupMonsterTarget'])-1)
                    f['BossEscort'+suffix]=f['BossEscortTarget'];f['PlannedKill'+suffix]=f['PlannedKillTarget']
            converted.append(f)
        emit(base/'dungeon/floor_plan.csv',['id']+[k for k in converted[0] if k!='id'],converted)
        bands=readcsv(SOURCE/'Chapter1/data/ACT01_DIFFICULTY_CHAPTER_LEVEL_BANDS_V0.5.csv')
        for i,b in enumerate(bands):b['id']=str(i+1)
        emit(base/'dungeon/difficulty_bands.csv',['id']+[k for k in bands[0] if k!='id'],bands)
        waypoint_path=TARGET/base/'world/waypoint.csv';existing=readcsv(waypoint_path) if waypoint_path.exists() else []
        waypoints=[]
        for i,castle in castles.items():
            candidates=[p for p in normals if p['kind']=='normal' and next(r['castle_id'] for r in rulers if r['region_id']==p['region_id'])==castle]
            old=next((w for w in existing if w['castle_id']==castle and any(str(p['region_id'])==w['region_id'] for p in candidates)),None) if act==1 else None
            chosen=random.Random(20261019+act*397+i).choice(candidates)
            waypoints.append(old or dict(id='wp_'+castle,castle_id=castle,region_id=chosen['region_id'],x=.5,y=.75))
        emit(base/'world/waypoint.csv',['id','castle_id','region_id','x','y'],waypoints)
        chapter_rows.append(dict(id=f'act{act}',name=f'\u7b2c{act}\u5e55\uff1a'+town['name'],terrain='mixed',width=width,height=height,directory=f'chapters/act{act}'))
        summary.append(dict(chapter=act,regions=len(region),pois=len(poi),floors=len(floors)//3,gates=len(gates),width=width,height=height))
        if not args.check:
            out=ARCHIVE/f'patched/act{act}';out.mkdir(parents=True,exist_ok=True)
            m['gridRows']=[''.join(r) for r in grid];m['runtimeUnlockGraph']={str(i):{'mode':'all' if len(p)>1 else 'any','castles':[castles[n] for n in p]} for i,p in parents.items()};m['runtimeGates']=[dict(id=g,fromTile=tile(*a),toTile=tile(*b),kind=k) for g,a,b,k in gates]
            m['schemaVersion']=str(m['schemaVersion'])+'_RUNTIME_PATCH_V02'
            m['unlockGraph']=m['runtimeUnlockGraph']
            for r in m.get('regions',[]):
                r['parents']=parents[int(r['id'])];r['unlockMode']='all' if len(r['parents'])>1 else 'any';r['tiles']=sum(1 for row in grid for char in row if char!='x' and char not in 'T0' and int(char,16)==int(r['id']))
            m['gates']=[]
            for gid,a,b,kind in gates:
                ch=grid[b[0]-1][b[1]-1];index=0 if ch in 'xT0' else int(ch,16)
                required=[castles[p] for p in parents.get(index,[])];rule=('AND('+','.join(required)+')') if len(required)>1 else (required[0] if required else 'NONE')
                m['gates'].append(dict(gateId=gid,fromTile=tile(*a),toTile=tile(*b),a=a,b=b,kind=kind,unlockBy=rule))
            m['policy']=dict(selection='CHALLENGE_ASC_RANDOM_TIES',castleRequiresExplicitApproval=True,sideRegionRequiresExplicitApproval=False,sideDoesNotBlockMain=True)
            (out/'map.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf-8')
    emit(Path('world/chapter.csv'),list(chapter_rows[0]),chapter_rows)
    if not args.check:
        (ARCHIVE/'import_summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
    print(json.dumps(dict(chapters=summary,changed=changed),indent=2))
    if args.check and changed:raise SystemExit(1)

if __name__=='__main__':main()
