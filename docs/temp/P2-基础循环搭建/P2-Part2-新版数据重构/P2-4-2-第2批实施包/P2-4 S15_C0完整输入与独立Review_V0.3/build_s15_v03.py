from pathlib import Path
import csv,zipfile,hashlib,io,shutil,collections,re,json
base=Path('/mnt/data')
pre=base/'Steam_D2_S15_首轮配置候选与静态校验_V0.2'
evzip=base/'p2-4-v16-review-evidence.zip'
out=base/'Steam_D2_S15_C0完整输入与独立Review_V0.3'
if out.exists(): shutil.rmtree(out)
out.mkdir(exist_ok=True)
for dir in ['design_sources','design_narratives','authority','engine_readonly_audit','engine_evidence_selection','docs','matrices']:(out/dir).mkdir(exist_ok=True)

z=zipfile.ZipFile(evzip)
def loadz(name):return z.read(name).decode('utf-8-sig')
def readcsv(path):
 with open(path,encoding='utf-8-sig',newline='') as f:return list(csv.DictReader(f))
def writecsv(path, rows, columns=None):
 if not columns: columns=list(rows[0].keys()) if rows else []
 with open(path,'w',newline='',encoding='utf-8-sig') as f:
  w=csv.DictWriter(f,fieldnames=columns,extrasaction='ignore');w.writeheader();w.writerows(rows)

manifest=readcsv(pre/'S15_source_manifest.csv')
sourcefiles=[];problems=[]
for m in manifest:
 src=base/Path(m['source_path'])
 if not src.is_file(): problems.append('missing '+str(src));continue
 data=src.read_bytes();sha=hashlib.sha256(data).hexdigest(); n=len(readcsv(src))
 if sha!=m['sha256']:problems.append('checksum mismatch '+str(src))
 if n!=int(m['rows']):problems.append('row count mismatch '+str(src))
 dest=out/'design_sources'/src.name
 if dest.exists():
  problems.append('collision '+str(src));continue
 shutil.copy2(src,dest)
 sourcefiles.append({'source_key':m['source_key'],'file_name':src.name,'record_count':n,'sha256':sha,'declared_status':m['status'],'included_path':'design_sources/'+src.name,'source_path':m['source_path']})
writecsv(out/'matrices'/'source_manifest_verified.csv',sourcefiles)
# P0 original is authority; prose clarifies the behavior beyond the CSV summaries.
shutil.copy2(base/'P0-地图规则搭建.zip',out/'authority'/'P0-地图规则搭建.zip')
narratives=[
 'Steam_D2_完整首轮设计_V0.6/04_S04_英雄评级与公共XP_候选模型_V0.2.md',
 'Steam_D2_完整首轮设计_V0.6/05_S05_KP怪级与可选消费框架_V0.1.md',
 'Steam_D2_完整首轮设计_V0.8/06_S06_四职业16技能与核心特性_V0.8.md',
 'Steam_D2_完整首轮设计_V0.8/07_S07_98节点天赋设计与24点合法构筑_V0.8.md',
 'Steam_D2_完整首轮设计_V0.9/08_S08_十小时星图拓扑与31节点完整设计_V0.9.md',
 'Steam_D2_完整首轮设计_V1.0/09_S09_31城堡战斗内容与Boss规格_V1.0.md',
 'Steam_D2_完整首轮设计_V1.0/09B_Lmin最低英雄等级保护锁_设计研究_V0.1.md',
 'Steam_D2_完整首轮设计_V1.1/10_S10_136地牢与四章怪物投放设计_V1.1.md',
 'Steam_D2_完整首轮设计_V1.2/11_S11_六部位装备内容与十小时换装体验_V1.2.md',
 'Steam_D2_完整首轮设计_V1.3/12_S12_五卷轴四药剂与10小时内容投放_V1.3.md',
 'Steam_D2_完整首轮设计_V1.4/13_S13_完整十小时资源联立与体验风险_V1.4.md',
]
for rel in narratives:
 src=base/rel
 if not src.is_file(): problems.append('missing narrative '+rel)
 else: shutil.copy2(src,out/'design_narratives'/src.name)


for key in ['s15\\schema_diff.md','s15\\source_precedence.md','s15\\implementation_packages_proposal.md','s15\\read_only_audit_evidence.md','s15\\id_crosswalk.csv','s15\\effect_coverage.csv','migration\\legacy-disposition.csv']:
 dest=out/'engine_readonly_audit'/key.replace('\\','_')
 dest.write_bytes(z.read(key))
for key in ['integration\\int-results.txt','integration\\asset_ledger.csv','fixed-closure\\buff_sources.csv','data\\2026-10-26_12-14_data_p2_4_v16_closure.md','screenshots\\screenshots.csv']:
 dest=out/'engine_evidence_selection'/key.replace('\\','_')
 dest.write_bytes(z.read(key))
for key in ['screenshots\\fixed-skills-s2-unlearned-t-locked.png','screenshots\\fixed-skills-s2-learned.png','screenshots\\fixed-skills-local-reset.png','screenshots\\star-retired-slots-absent.png','screenshots\\four-same-class-buff-sources-and-cooldowns.png','screenshots\\layout-1280x720.png']:
 (out/'engine_evidence_selection'/key.split('\\')[-1]).write_bytes(z.read(key))

S15=readcsv(pre/'S15_design_id_crosswalk_pending.csv')
EFFECT=readcsv(pre/'S15_effect_consumer_coverage_pending.csv')
engine=list(csv.DictReader(io.StringIO(loadz('s15\\id_crosswalk.csv'))))
engine_eff=list(csv.DictReader(io.StringIO(loadz('s15\\effect_coverage.csv'))))
slotmap={}
for e in engine:
 if e['class_id']=='necromancer':continue
 cls={'warrior':'BAR','ranger':'RAN','mage':'SOR','priest':'PAL'}[e['class_id']]
 slotmap[(cls,e['kind'].replace('fixed_',''))]=e
skills=readcsv(base/'Steam_D2_完整首轮设计_V0.8/S06_四职业16技能身份行为表_V0.8.csv')
manual={
'BAR-S1':('PARTIAL_REUSE_CANDIDATE','barb_bash','现行S1：同为基础重击；需核对物理攻击行为、AI、表现和天赋联动'),
'BAR-S2':('SLOT_BEHAVIOR_CONFLICT','barb_whirlwind_window','现行S2=barb_taunt；旋风候选位于现行S3，不能按槽号导入'),
'BAR-S3':('SLOT_BEHAVIOR_CONFLICT','barb_taunt','现行S3=旋风；现行S2嘲讽只能算战吼的一部分，友军增益需核查'),
'BAR-S4':('SLOT_EMPTY','', '先祖援军未见该槽定义；新召唤机制与所有者保护需核查'),
'RAN-S1':('SLOT_BEHAVIOR_CONFLICT','', '现行S1为多重箭；设计需要基础单箭'),
'RAN-S2':('SLOT_BEHAVIOR_CONFLICT','ran_multishot','现行多重箭在S1；设计在S2，候选不能自动迁移'),
'RAN-S3':('SLOT_BEHAVIOR_CONFLICT','ran_guided_arrow','现行导引箭在S2；设计在S3'),
'RAN-S4':('SLOT_EMPTY','ran_valkyrie','现行女武神在S3；设计在S4，新槽为空'),
'SOR-S1':('PARTIAL_REUSE_CANDIDATE','mage_fireball','现行S1火球，需核对有界AOE/自动施法'),
'SOR-S2':('SLOT_BEHAVIOR_CONFLICT','', '现行S2连锁闪电；设计需要冰霜新星'),
'SOR-S3':('SLOT_EMPTY','sor_chain_lightning','现行连锁闪电在S2，设计在S3'),
'SOR-S4':('SLOT_EMPTY','', '陨星前摇/落点/Commit内容未投放'),
'PAL-S1':('PARTIAL_REUSE_CANDIDATE','paladin_smite','现行S1盾击；设计S1圣击，类似近战但有身份表现差异'),
'PAL-S2':('SLOT_BEHAVIOR_CONFLICT','paladin_smite','现行S2祈祷；设计S2盾击，候选在现行S1'),
'PAL-S3':('SLOT_BEHAVIOR_CONFLICT','pal_prayer','现行S3狂热光环；设计S3祈祷脉冲，不等同持续Aura'),
'PAL-S4':('SLOT_BEHAVIOR_CONFLICT','', '现行S4信念光环；设计S4祝福之锤；狂热/信念应核对T，不可算第五主动')
}
assert len(manual)==16
smat=[]
for s in skills:
 did=s['SkillId']; k=(s['ClassId'],s['技能槽位']); e=slotmap[k]; stat,possible,reason=manual[did]
 smat.append({'design_id':did,'design_class_id':s['ClassId'],'design_class_name':s['职业'],'design_slot':s['技能槽位'],'design_skill_name':s['技能名称'],'design_intended_behavior_P':s['战斗行为[P]'],'engine_class_id_observed':e['class_id'],'engine_slot':e['kind'],'engine_slot_skill_id':e['production_id'] or '(empty)','candidate_existing_id_REVIEW_ONLY':possible or '(none identified)','preliminary_design_comparison':stat,'reason_and_test_need':reason,'approved_for_automatic_mapping':'NO','engine_real_effect_test':'NOT_PROVIDED_FOR_S06_BEHAVIOR','proposed_stage':'S15-C1_AFTER_AUTHORIZATION'})
writecsv(out/'matrices'/'S15_四职业16技能槽位与行为差异.csv',smat)

mat=[]
for x in S15:
 typ=x['kind']; did=x['design_id']; extra={'engine_class_id_observed':'','engine_slot_skill_id':'','suggested_existing_behavior':'','crosswalk_status':'UNMAPPED_NO_ENGINE_PROOF','issue':'工程现有只读审计未对原设计ID逐项映射；不得以显示名等价','authorization':'READ_ONLY_RECON'}
 if typ=='HERO_SKILL':
  v=next(v for v in smat if v['design_id']==did)
  extra.update(engine_class_id_observed=v['engine_class_id_observed'],engine_slot_skill_id=v['engine_slot_skill_id'],suggested_existing_behavior=v['candidate_existing_id_REVIEW_ONLY'],crosswalk_status=v['preliminary_design_comparison'],issue=v['reason_and_test_need'])
 elif typ in ['DUNGEON','DUNGEON_NORMAL_FLOOR','CASTLE']:
  extra['crosswalk_status']='P0_DESIGN_SOURCE_KEY_PRESENT_ENGINE_JOIN_PENDING';extra['issue']='已按P0源表复核设计ID，但工程生产配置键/有效Gate未逐ID验证'
 elif typ=='HERO_TALENT':
  extra['crosswalk_status']='CONTENT_BEHAVIOR_NOT_DEPLOYED';extra['issue']='98个V0.8节点与现行原型节点需逐行为/互斥关系核对'
 elif typ=='STAR':
  extra['crosswalk_status']='STAR_EFFECT_CONSUMER_UNVERIFIED';extra['issue']='31节点效果/资格/价格和旧已退休购买ID必须逐一核对；禁无效节点消费'
 elif typ=='EQUIPMENT_TEMPLATE':
  extra['crosswalk_status']='QUALITY_AND_VALUE_CONFLICT';extra['issue']='设计品质普通/稀有/罕见/传奇与工程普通/优秀/稀有/传奇不同；倍率和卖价同样不同'
 elif typ in ['SCROLL','POTION']:
  extra['crosswalk_status']='MINIMAL_PROTOTYPE_ONLY';extra['issue']='工程仅有1卷轴+1药剂真实行为；设计候选需逐项Effect验证'
 elif typ=='MONSTER_LEVEL':
  extra['crosswalk_status']='H_B1_NOT_PRODUCTION';extra['issue']='H-B1与现行H参数不同，运行数值仍需P2-5A'
 elif typ in ['MONSTER_IDENTITY','MONSTER_POOL']:
  extra['crosswalk_status']='P1_REUSE_CANDIDATE';extra['issue']='历史身份/池不代表正式工程加载与Effect匹配，需源ID和实时行为验证'
 mat.append(dict(x,**extra))
writecsv(out/'matrices'/'S15_675项设计工程联合映射_待核对.csv',mat)

# Engine static effect evidence is a repertoire, not evidence for exact target behavior.
static_keys=collections.Counter(x['effectKey'] for x in engine_eff if x['status']=='STATIC_CONSUMER')
cmat=[]
for x in EFFECT:
 k=x['kind'];did=x['design_id']; generic=''
 if k=='HERO_SKILL':
  m=next(v for v in smat if v['design_id']==did)
  generic=f"工程现存{m['engine_slot_skill_id']} / 候选{m['candidate_existing_id_REVIEW_ONLY']}；需匹配S06完整行为"
 else:generic='工程审计仅证明部分Effect Key有静态消费者，不证明设计意图已满足'
 cmat.append(dict(x,engine_static_effect_keys=';'.join(sorted(static_keys)),engine_audit_static_consumer_count=sum(static_keys.values()),candidate_binding=generic,coverage_status='CANDIDATE_INPUT_NOW_PRESENT_CONSUMER_UNVERIFIED',implementation_gate='READ_ONLY_ID_SCHEMA_EFFECT_FIRST'))
writecsv(out/'matrices'/'S15_154项效果消费者联合核查_待核对.csv',cmat)

schema_text=loadz('s15\\schema_diff.md');loaded=collections.defaultdict(set)
for a,b in re.findall(r'\| unavailable: candidate source missing \| ([^|]+?) / ([^|]+?) \|',schema_text):loaded[a.strip()].add(b.strip())
schema_candidates=[]
mapfamily={'skills':'skill/skill_slot.csv','talents':'skill/talent.csv','stars':'star/star.csv','equipment_templates':'item/equipment.csv','scrolls':'item/scroll.csv','potions':'item/potion.csv'}
for m in sourcefiles:
 source=out/'design_sources'/m['file_name'];reader=csv.DictReader(open(source,encoding='utf-8-sig'));fields=reader.fieldnames
 family=mapfamily.get(m['source_key'],'(multiple tables/P0 read-only)')
 for f in fields:
  found=next((c for c in loaded[family] if f.lower()==c.lower()),'') if family in loaded else ''
  schema_candidates.append({'source_key':m['source_key'],'design_table':m['file_name'],'design_field':f,'suggested_engine_table_REVIEW_ONLY':family,'exact_label_column_present':found,'match_status':'NAME_ONLY_NOT_SEMANTIC_VALIDATION' if found else 'NO_NAME_MATCH_OR_OUTSIDE_AUDIT','unit_range_default_rounding':'UNKNOWN','runtime_consumer':'UNKNOWN','id_mapping':'PENDING'})
writecsv(out/'matrices'/'S15_设计字段与工程列名初步对账.csv',schema_candidates)

# rank audit and unproven nodes reference
stars=readcsv(base/'Steam_D2_完整首轮设计_V0.9/S08_31星图定义与前置候选_V0.9.csv')
starrows=[]
for s in stars:
 txt=s['Effect_P']+' '+s['DesignBoundary']
 if any(y in txt for y in ['自动','拾取','精英XP','整备','药剂槽','卷轴容量','离线']):issue='SPECIAL_CONSUMER_HIGH_RISK'
 else:issue='EFFECT_CONSUMER_NEEDS_TRACE'
 starrows.append({'node_id':s['NodeId'],'name':s['Name'],'rank_capacity':s['MaxRank'],'candidate_effect':s['Effect_P'],'candidate_gold':s['MaxRankCost_H'],'risk':issue,'runtime_effect_consumer':'UNVERIFIED','player_can_buy_in_production':'NOT_AUTHORIZED','route_count_should_include':'ONLY_AFTER_REAL_EFFECT_TEST'})
writecsv(out/'matrices'/'S15_31星图真实消费与风险清单.csv',starrows)

stats=collections.Counter(x['preliminary_design_comparison'] for x in smat)
stats675=collections.Counter(x['crosswalk_status'] for x in mat)
summary={'source_files':len(sourcefiles),'source_rows':sum(int(x['record_count']) for x in sourcefiles),'source_hash_errors':problems,'design_ids':len(mat),'effect_contracts':len(cmat),'engine_skill_slots_first_run':len([x for x in engine if x['class_id']!='necromancer']),'engine_nonempty_first_run':sum(1 for x in engine if x['class_id']!='necromancer' and x['production_id']),'skill_comparison':dict(stats),'design_statuses':dict(stats675),'audit_static_effect_key_count':len(static_keys),'audit_static_effect_row_count':sum(static_keys.values()),'S15_config_schema_rows':len(schema_candidates)}
(out/'matrices'/'metrics.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')

# design independent review
review=f'''# R-A V1.6｜设计Owner独立Review V1.7（2026-10-26）

## 1. 判定

- `[E]` 已读取用户回传的主Review第19节、工程证据压缩包81个条目、联合INT结果、逐笔资产流水、迁移分类、S15只读调查、关键源代码片段，并目视抽查固定技能页／星图／Buff／装备／卷轴／窗口截图。证据出自执行AI的Windows QA运行，不是设计AI在用户工程仓库独立重跑。
- **R-A1：PASS_WITH_OPEN_ITEMS（设计Owner证据复核）**。`4+T唯一权威`、旧槽位/装配退役、旧值隔离与一次性返还、旧星图金币不补偿符合已确认规则；技能实际内容仍为旧P1原型而非V0.8内容。
- **R-A2：PASS_WITH_OPEN_ITEMS（设计Owner证据复核）**。Lmax显示AR/DR、Lmin禁用、真实最近卷轴目标、装备单人入口和右栏合批语义均有代码/QA/截图交叉证据；3窗口尺寸与GUI输入仅本机自动化，未做正式人工UE验收。
- **R-A3：PASS_WITH_OPEN_ITEMS（设计Owner证据复核）**。INT01–08一键脚本、50/50回归、逐行金币XP/KP、装备卷轴药剂事件、HP装备救援路径有证；第四章通关是QA前置，不能作为真实10小时完成证据。
- **R-A4：PASS（交接记录）**。主Review附录、独立证据包、旧版本差异及开放问题均已提交；未写生产配置、未Git push。
- **用户验收：仍未作**。不将设计Owner审阅自动标为最终用户体验验收。

## 2. 范围内已关闭的旧阻断

1. 生产固定S1–S4+独立T，学习技能自动施放；旧EquipSkills/SetTalentLoadout生产拒绝。
2. 23个旧无绑定主动及10个过时付费星图功能按清单退休；旧历史原字段隔离跨Run保留；旧技能点由玩家显式免费领回一次，**金币不补偿**。
3. Star不再出售skill_slots/party_slots或收费全树洗点；同职业特性资格共享，但英雄独立花点；区域免费洗点保留S1。
4. 同目标四个英雄来源Buff和独立CD、S2真实效果、最近卷轴致死同类掉落守恒等已具备技术证据。
5. HP装备上限下降截断而不等比治疗；单人装备按钮与右栏≥5才合批两规则区分。

## 3. 不能越界的结论

- 工程回归50/50和37张UI PNG支持**机制阶段**复核；尚未完成玩家主观可读性、触达难度、全部职业表现验收。
- 原925份冻结历史档语料不在执行环境，`legacy.log`明确报告无法复跑。现有合成档回归不替代那批历史样本；若找回需补跑，不是本次版本的功能故障证据。
- 现有共16个历史技能定义跨5职业，前四职业只有12个；对照S06设计16个，**4槽为空、9个槽位行为冲突、3个现槽位仅为部分复用候选**（见专项矩阵），不得声称首轮16个设计技能已配置或战斗幻想验收完成。
- Effect审计34条静态消费者记录、10个退休项和1个RED，只说明引擎存在一定Effect通道，不等于S06–S12的154项候选行为已经可以消费。
- 6分钟无自动升级的工具样本、11.05秒QA联合事务不能证明四章10小时或S13经济设计成立。

## 4. S15后继关口

本轮S15-C0设计输入**已补齐**21份原始候选表／675设计ID／154行为合同并校验SHA256与记录数。必须让执行AI在最新V1.6代码上只读重跑：逐ID生成生产ID、字段类型/单位、Effect消费者、未实现的行为、存档迁移影响与真实测试证据；不得按中文名直接映射。

只读结果经设计Owner复核后，再按C1技能/天赋/星图、C2地图/敌人/装备/消耗品、C3数值联立获得实施授权；本文件不授权改代码或写生产配置。Lmin仍保持封闭。[P]准入“全队最低英雄等级≥目标Lmin”并不自动构成安全验收。

## 5. 证据与追溯

来源：用户附件 `2026-10-25_p2_4_first_batch(3).md` 第19节；`p2-4-v16-review-evidence.zip` 中 `data\\2026-10-26_12-14_data_p2_4_v16_closure.md`、`integration\\int-results.txt`、`integration\\asset_ledger.csv`、`logs\\regression.log`、`s15\\*`、`screenshots\\*`、`migration\\legacy-disposition.csv`。截图目视为关键页面抽查，不宣称逐像素全检。新的S15文件由设计Owner独立构建，不宣称工程在用户仓库已收到这些表。
'''
(out/'docs'/'01_RA_V1.6_独立设计Review_V1.7.md').write_text(review,encoding='utf-8')

c0='''# S15-C0 V0.3｜工程执行AI只读完整配置接口对账任务包

## 身份与授权

当前任务为 `READ_ONLY_RECON`，不授权修改Core/UI/生产配置/玩家存档、发布或改变P0地图，不授权导入H数值。此前V1.6返修已工程自检完成；这次**不重开原返修**。本包由设计Owner生产，交给执行AI时请用户按需转发。

## 输入文件

- `design_sources/`：21份**真实原始**S04–S13逐项CSV，另有`design_narratives/`完整行为设计稿与`authority/P0-地图规则搭建.zip`，原SHA和记录数由`matrices/source_manifest_verified.csv`记录；可与现有工程下的P0原权威对照。
- `matrices/S15_675项设计工程联合映射_待核对.csv`：675个设计ID，已有16技能槽差异预填，但状态为设计预判；生产ID除工程审计直接列出的槽位外均未确认。
- `matrices/S15_154项效果消费者联合核查_待核对.csv`：154个候选行为，**只读审计34个static consumer记录不能当作行为已完成**。
- `matrices/S15_四职业16技能槽位与行为差异.csv`：处理四职业12个历史槽定义、4个空槽及D2幻想差异；不可按槽位直接把旧P1技能迁入新版。
- `matrices/S15_设计字段与工程列名初步对账.csv`：列名相同仅代表字面匹配；单位、取整、默认值、调用者必须独立调查。
- `matrices/S15_31星图真实消费与风险清单.csv`：每个设计星图节点要求真实消费者；已退休槽位与未实现容量不允许继续消费金币。
- `engine_readonly_audit/`：V1.6之前执行AI产出的PARTIAL_INPUT报告，保留为旧证据；此包补足数据后要重新审计，不能仅把PARTIAL_INPUT改名PASS。

## 工程自主调查、输出

1. 比较原始表头、目标生产schema、FieldLoader、Consumer、回写/存档路径，输出`schema_diff_complete.md`，每列写`type/unit/range/default/rounding/version/consumer`。
2. 对全部675个ID输出`id_crosswalk_engine_complete.csv`：设计键、章节/职业上下文、现生产键、键的来源、强/弱匹配、重名冲突、可否直接映射、所需迁移、证据文件行号。P0地图按章节复合键，禁止只有局部POI编号。
3. 对全部154个候选效果输出`effect_coverage_engine_complete.csv`：Effect/AI/Trigger/Modifier需要的具体行为、当前消费者入口、实际效果与设计差异、代表测试、状态`REAL_REUSE / MODIFY / NEW_EFFECT / DESIGN_DECISION / INPUT_MISSING`。旧历史同名不得自动算REAL_REUSE。
4. 单列四职业技能S1–S4与T(职业特性)生产差异清单：如果现有技能ID候选可复用但位置不对，写清是否允许重绑定、天赋/存档/冷却影响。不得保留旧装配器或额外技能槽。
5. 对31星图给出真实消费者列表和**活跃可购买次数**，两队路线以真实Effect为准重算；不生效就标BLOCKED，禁止靠无效节点充购买次数。
6. 对28装备品质命名/ID/倍率、5卷轴4药剂、31怪级/金XP KP、多态Run存档逐项给出兼容方案；不批准产品取舍时只列两案，不先实施。
7. P0地图及当前有效AND/OR Gate采用只读对账；保持原ID和拓扑，不写回P0。
8. 返回`implementation_packages_proposal_v2.md`：按C1/C2/C3拆分`CONFIG_ONLY / RUNTIME_REQUIRED / CONTENT_APPROVAL / NUMERIC_H`及有序依赖与测试预算。必要只读工具可输出到`artifacts/qa`，禁止写正式配置、docs/data历史或玩家档。

## 门禁

- 行数覆盖必须等于675/154；未核实的字段明确UNKNOWN，不能以模型估算冒充工程事实。
- 每个预定上线的Effect有运行入口、存档路径、正确对角色/目标生效；无消费者=RED。
- 只有`READ_ONLY_RECON`结束并由设计Owner审查差异后，才可建议对C1/C2/C3分别请求用户授权。
- 用户本次聚焦第一次四章约10小时，S14/AP/死灵角色首轮投放/第五席不在内；死灵历史定义可作为兼容资料，不算首轮16技能。
- Lmin仍保持工程禁购，升级资格是[P]，不得偷偷开放。

## 最终回传

请把只读报告摘要追加到执行AI已有工程模块Review，注明本任务来源、验证环境、输入哈希、查到的差异、未覆盖/需用户裁决的产品事项、是否只读未改动、可转发设计AI摘要。无需按普通工程接口差异反复请示，但任何新玩法及正式配置导入属于未授权新里程碑。
'''
(out/'docs'/'02_S15_C0_工程只读完整对账执行任务_V0.3.md').write_text(c0,encoding='utf-8')

risks='''# S15｜核心设计差异和后续分包门槛 V0.3

## 优先处理的产品与工程对账项

**G1：技能行为不是槽位一致就算实现。** 首轮四职业16个设计技能中，现有历史定义只有12个，见逐技能矩阵。当前3个槽位属于“可能部分复用”、9个明显发生设计行为/槽位冲突、4个为空。职业核心T不能通过新增第五主动技能或把圣骑士旧Aura当作可装配技能来满足。

**G2：星图实际消费量仍有未证明的Effect。** S08原路线59/60次；S13排除卷轴容量/药槽扩容后的56/57次仅为条件购买；S15发现A02拾取整备、A03回城整备、精英XP等潜在无效效果。执行侧要逐节点确认真实消费者并逐步重算，不得声称满足55–65次即可验收。

**G3：S07天赋预算可能被星图额外点影响。** 自然Lv25每英雄24点为[H]模型；G03购买额外天赋点是否真实生效、是否每英雄发放及互斥合法性须纳入C3正式跑数。

**G4：装备品级冲突。** 工程当前普通/优秀/稀有/传奇vs设计普通/稀有/罕见/传奇，倍率不是一一可替换，不允许中文名称隐式重映射。须经产品确认最终四档身份后才做内容覆盖。

**G5：四章数值仅有候选算式，非10小时实战**。V1.6的联合QA约11.05秒、工具采样6分钟，第四章终点QA前置。S13在80%静态击杀下第二章XP/金币阶段缺口仍需要加入真实星图收益与实际击杀直方图再算，不因末章合计正数就宣称可达。

**G6：P0 ID与门禁**。31城堡、136普通地牢及226 Normal楼层为既有P0内容。按章节复合键对账；当前生效AND/OR Gate与章节补丁单独核对，不因配置首轮化更改地图拓扑。

**G7：Lmin仍封闭**。用户提案将Lmin购买准入改为全队最低英雄等级，属于[P]，还需收益断供与可逆恢复的安全证明。提高Lmax仍逐英雄AR和DR达标。

## 分包建议（用户另行授权）

- C1-A：四职业固定16技能/4T＋98节点：基于已闭环4+T权威补新Effect/AI/天赋表现和真实战斗UI，按P内容方案评审。
- C1-B：31星图/78阶有效消费，分别补消费者、合法星图前置与存档迁移，验收真实购买后反馈。
- C2-A：P0首轮城堡/怪物/地牢内容挂载；禁止改P0地图Gate。
- C2-B：28装备/5卷轴/4药剂内容投放、品质与掉落/产消模型。
- C3：以完整参数沙盘H为隔离候选，支持数值调参、模拟主动策略、10小时逐章跑数、TTK/Boss胜率/游戏操作密度/前后台状态；由设计Owner+用户另行批准正式参数。
'''
(out/'docs'/'03_设计差异风险与C1-C3后续门禁_V0.3.md').write_text(risks,encoding='utf-8')

readme=f'''# Steam D2｜S15-C0 只读完整输入与R-A独立Review V0.3

2026-10-26。设计Owner已独立复核用户回传的R-A V1.6工程证据；此次输入**设计源数据已完整**，但生产配置真实映射仍需执行AI在工程目录只读核查。

- 原始候选CSV：**{len(sourcefiles)}份，{sum(int(x['record_count']) for x in sourcefiles)}条内容/曲线记录**（各表行数和SHA256均经本次复核）；非全部互不重复的内容ID。
- 设计ID矩阵：{len(mat)}项；候选Effect合同{len(cmat)}项；4职业16技能差异逐条可见。
- 关键差异：现行12/16首轮历史固定技能定义，其余4为空；同位置3项可部分复用，9项行为/槽位冲突，4项缺槽。所有建议重映射仍需运行时验证。
- R-A设计复核：R-A1/R-A2/R-A3 PASS_WITH_OPEN_ITEMS，R-A4 PASS（工程证据层）；用户体验最终验收未执行；原925历史档未重跑，样本缺失应登记。
- 引入源文件：`design_sources/` 21份CSV；任何[P/H]未批准数值不得写生产；P0原始压缩包位于`authority/`，作为地图权威，与CSV一起交接但不得改写。
- 工程依据：`engine_readonly_audit/`与`engine_evidence_selection/`，后者含6张真实UI图和关键流水；完整原证据仍以用户上传的`p2-4-v16-review-evidence.zip`为准。
- 执行侧建议先读`docs/02_S15_C0_工程只读完整对账执行任务_V0.3.md`，再读设计Review、风险与matrices；本包**没有新内容代码实施授权**。
- 本包生成脚本：`build_s15_v03.py`，可对原始表复核SHA/记录数/输入总表并复建输出。

文件不依赖S14或第二轮AP商店。所有静态对账结论不能代替用户验收。[C]/[P]/[H]/[E]/[V]状态沿用项目工作法。
'''
(out/'README.md').write_text(readme,encoding='utf-8')

# copy generator
shutil.copy2(base/'build_s15_v03.py',out/'build_s15_v03.py')
zipout=base/'Steam_D2_S15_C0完整配置输入与RA独立Review_V0.3.zip'
with zipfile.ZipFile(zipout,'w',zipfile.ZIP_DEFLATED,compresslevel=7) as zz:
 for f in sorted(out.rglob('*')):
  if f.is_file():zz.write(f,arcname=str(f.relative_to(out.parent)))
print('OUT',zipout,zipout.stat().st_size)
print('METRICS',json.dumps(summary,ensure_ascii=False))
