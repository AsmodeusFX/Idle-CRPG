# 第一章地图与自动攻略：实施记录

实施日期：2026-10-18～19。以下保留实施前审计作为历史依据，涉及“拟采用/待确认”的描述已由 [正式规则](../../design/act1.md)和 [配置字典](../../technical/act1_config.md)覆盖。原始资料见 [冻结归档](../../references/act1/2026-10-18/manifest.md)。

## 已执行验收

| 验证 | 结果与边界 |
|---|---|
| 核心回归 | PASS；全部旧回归及Act1Checks通过，正式加载使用新地图，旧夹具显式LoadLegacy |
| 地图/数值 | PASS；64格、49可走、15阻断、8关口、41POI、单营地；123条挑战等级、207层；三难度预算2517/2601/3439 |
| any/all | PASS；OR各项独立满足/均未满足；AND部分/全部满足；非法模式拒绝，地图门禁与详情一致 |
| 主线实际执行 | PASS；强化QA队伍通过移动、战斗、出口实际完成37个主线POI，未自动进支线，安达利尔仅末层一次，交易后进第二章 |
| 支线/读档 | PASS；从第二章手动授权旧章支线、执行中读档、实际通关后返回最新章，保留41POI进度 |
| 失败恢复 | PASS；两次真实全灭、匹配等级回刷、普通等级变化重试、要塞手动再次挑战，损坏主备份重置提示 |
| 构建 | PASS；0警告、0错误 |
| Godot冒烟 | PASS；CLIENT/SCENE/HUD/CHAPTER/ACT1/GM/STAR；ACT1专项验证营地、支线卡、8城堡、AND详情及红字再次挑战 |
| 地图截图 | PASS；8×8、单营地、阻断、关口、右栏和章节锁定布局已查看；截图仅在artifacts |

NOT RUN：自然成长下的三难度平衡、耗时及收益实测。尚未投放：专属Boss机制、CellKind布局生成、完整Arena遭遇和规划密度。楼层预算已校验，但实际房间/怪数沿用原规则，不按预算发奖励；强化QA队伍通关不等同数值平衡通过。章节2～5结构未改。未执行Git提交或推送。

用户后续裁定全部已落实：主线完成并交易后直接下一章，埋骨之地不阻断；支线一次授权整条链；单村庄(x4,y5)；小队最低等级及2次失败阈值；普通自动重试、要塞手动重试；区域门禁可逐区any/all。以下为历史审计。

## 来源与优先级

- `docs/temp/Chapter1_Act1_ClaudeCode_Development_Spec_V0.4.md`：地图、POI、关口及支线结构。
- `docs/temp/Chapter1_Act1_Interactive_Map_V0.3.html`：gridRows、area、gates静态数据。
- `docs/temp/D2_全自动攻略与地牢规模配置规则_V0.3.md`：攻略排序、等级工作基线及Floor规划接口。
- 用户本轮明确纠正：维持玩家手动攻要塞；埋骨之地不阻断第一章主线完成。文档中的自动Castle候选不能覆盖此裁定。重大玩法变化须先征询用户。

原始参考文件保持原样；实现和后续归档须显式记录裁定及原文差异，不能把AI生成文档全部视为已批准玩法。

## 已有接入点

| 概念 | 现有实现 | 处理方向 |
|---|---|---|
| 地图与章节配置 | Config、WorldConfig、ChapterConfiguration | 第一章从16×16替换为已批准8×8，其他章节先保留现有占位结构 |
| 格子及区域 | RegionSpec、Rulers、AreaParts、RegionPrerequisites | 当前可进入格必须归属要塞；需支持独立营地格，不凭空增加第九座要塞 |
| 不可进入格 | enterable、mountain/ocean、WorldNavigation | 复用阻断、绘制及旧存档安全迁移 |
| 世界寻路 | WorldNavigation.FindPath | 当前跨区域邻接即可通行；需接入8对显式Gate，非Gate边界阻断，同区连续移动 |
| 解锁与攻城资格 | WorldProgress.AreaPartUnlocked、CastleUnlocked | 复用OR前置；增加RequiredForCastle和支线元数据，第二区征服同时开放第三、四区 |
| 自动选普通地牢 | ExpeditionPlanner.Select | 当前偏好本格/邻格、再按等级；拟改已解锁且可达普通地牢的显式ChallengeLevel稳定排序，要塞仍只由玩家队列发起 |
| 手动要塞排队 | GameSession.RequestCastleChallenge、WorldCampaign.RequestCastle/Plan | 保留，不能由自动选择器绕过玩家操作 |
| 真实进入与通关 | GameSession.BeginDungeon、TickDungeon、WorldCampaign.CompleteDungeon | 复用战斗事实提交；地图UI不得授予通关 |
| 随机地牢 | Dungeon.Generate、Room、Floor、GameSession.Reveal | 现有房间/走廊与进房快照继续使用；规划DungeonCell与导航单位不得混为一项 |
| Boss内容 | SpawnRule.Create及房间Boss标记 | 只有通用原型Boss；无血乌/女伯爵/安达利尔专属遭遇、场地或阶段，不可声称已完成专属内容 |
| 跨章条件 | GameSession.AdvanceChapter | 当前要求全部地牢和要塞完成，须区分主线完成与全清；下一阶段切换时机待确认 |
| 进度存档 | SaveDocument v3、SessionSnapshot、章节历史及地形迁移 | 地图ID/坐标和完成条件变化须新增版本迁移，保留成长资源，不按旧cN编号错误继承新要塞 |
| 展示 | WorldPage、WorldView、ChapterMapUi、FollowScene | 保留左地图右详情、难度/章节栏；增加营地、区域色、POI、Gate墙和内容状态说明 |

## 拟采用的配置映射

继续使用工程已有CSV；不因参考文档建议JSON就复制一套权威数据。导入HTML数据时统一从0-based行列转换至工程x/y，归档源坐标对照。

- 第一章8×8完整网格：64格、1营地、48区域格、15阻断格；营地独立于8个统治区域。
- 8个区域、33普通地牢、8要塞：保存稳定POI ID、名称、坐标、来源和占位状态；内部多层不重复计为地图入口。
- 独立Gate表：8对相邻端点，双向许可仍受目标区域解锁限制；地底通道POI与跨区Gate坐标分别配置。
- 攻略配置：显式ChallengeLevel、主支线优先级、AutoOrder、RequiredForCastle、内容可用状态；玩家推荐等级不充当门禁。
- 章节×难度工作基线：配置挑战/推荐等级区间；41个POI逐项数值未给全，不擅自推导正式曲线。
- Floor规模接口：强类型载入、范围校验、Encounter唯一聚合及纯预算函数。未经批准的尺寸、密度、波次和Arena示例只进入QA样例，不修改正式生成器节奏或实际奖励。

## 本轮已确认与待裁定

已确认：手动攻要塞；埋骨之地不阻断第一章主线完成；8×8原图及名称/POI/Gate不擅改；三档难度和五章结构沿用已有授权；所有真实收益继续由现有战斗和拾取系统提交。

待确认后编码：主线完成但支线未清时是否立即按既有交易流程进入第二章；专属Boss未制作时使用明确标记的可战斗原型还是阻塞入口；缺失的逐POI挑战数值采用经批准的临时配置还是只完成数据接口。自动攻略范围默认保留当前激活章节，不扩展跨章/跨难度全局选目标。失败回刷沿用现有流程，不新增阈值或降级策略。

## 验证与交付计划

L1：逐格/逐POI与HTML对照、精确8关口、非Gate禁止跨越、OR门禁、第三/四区双开、必清资格、主支线完成分离、普通地牢稳定排序、手动要塞不自动发起、预算不发奖励、旧档迁移和章节恢复。

L2：真实启动Godot，检查营地/地图浏览、自动路径、关口限制、进地牢与返村、手动攻城、锁定和内容状态、进度保存。专属Boss未实现内容按实际状态列为原型或BLOCKED，不以编译通过代替玩法验收。

最终归档包括规则裁定、字段字典、源数据与导入映射、验证结果及未实现边界。不执行Git提交或推送。
