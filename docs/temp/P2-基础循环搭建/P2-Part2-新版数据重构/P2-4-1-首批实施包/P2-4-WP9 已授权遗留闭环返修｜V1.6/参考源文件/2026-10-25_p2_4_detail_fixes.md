# P2-4 细节修正汇总（用户反馈阶段）

- 模块：P2-4 第二批细节修正（用户逐条反馈驱动）
- 日期：2026-10-25 起（本文件持续追加，直到用户明确表示"本阶段反馈完成"）
- 权限：用户直接需求（IMPLEMENT_APPROVED_SCOPE 内）；未做 Git 提交/推送
- 状态：工程自检 `PASS_WITH_OPEN_ITEMS`；设计复核 **待反馈**；用户验收 **未验收**
- 与主 Review 的关系：功能/机制交付仍归档在 `2026-10-25_p2_4_first_batch.md`；**本文件只记录用户反馈驱动的细节修正**，不重复机制结论

## 记录规范

每条反馈分配 `F-xx`：原始需求 → 实现位置 → 行为规则 → 证据（截图/日志/断言）→ 未覆盖边界。后续反馈按序追加，不新建平行 Review。

---

## F-01 英雄页·装备页签（装备槽位 + 详情 + 背包 + 一键穿戴）

**需求**：英雄页签下的"装备"页签内，上半部分列出角色装备槽位与装备详情；下半部分是背包 UI，显示已拾取的装备；背包右上角增加"穿戴更好装备"按钮，点击给当前角色穿上背包里最好的装备。

**实现**
- `Scripts/UI/HeroPage.cs`：`BuildEquipmentContent` / `RefreshEquipmentPage`
  - 上半区"已装备道具"：六部位逐行显示 `部位：Lv.X 品质 · ATK/HP+数值`（空位显示"空"）。
  - 下半区"背包"：仅列出该英雄**未穿戴**的候选装备（部位 · 等级 · 品质 · 主属性数值），每行右侧"装备"按钮逐个穿戴。
  - 背包标题行右上角 `穿戴更好装备`（Name `WearBestEquipment`）：调用 `GameSession.EquipBestFor(hero)`，一次穿上该英雄所有"可替换"部位（空位或同属性更高），跨属性候选保留给玩家自行取舍。
  - 页签随角色选择/穿脱自动刷新（`heroStamp` 变化 → `RefreshEquipmentPage`）。
- `Scripts/Core/Equipment.cs`：新增 `HasReplaceable` / `ReplaceableCount` / `CanUpgradeHero` / `EquipBestFor` / `EquipAllReplaceable`（均按 `HeroInstanceId` 绑定，同职业实例互不共享）。

**证据**：`artifacts/qa/p2-4-detail-fixes/hero-equipment-bag.png`（上半 6 槽位详情 + 下半背包 1 件候选 + 右上"穿戴更好装备"）、`hero-equipment-worn.png`（点击后穿戴生效）；`fix-ui.log` 中 `FIRST_BATCH_UI_PASS` 断言 `EquippedSlots`/`BagItems`/`WearBestEquipment` 存在且点击后 `WornEquipment` 非空。

**未覆盖**：装备图标仍为文字行（未画品质底色/图标资产）；背包跨英雄共享仓库尚未做（当前按英雄实例绑定）；无"比较差值"高亮（仅显示当前值对照）。

---

## F-02 右侧交互栏显示规则与排序

**需求**
1. 每次交互完刷新列表；
2. 仅显示**可升级**的角色 icon；
3. 仅显示**可替换**的装备 icon；
4. 多个装备合批 → 变成**一个大按钮**；
5. 排序：英雄升级 → 装备穿戴 → KP 购买 → 挑战城堡。

**实现**
- `Scripts/UI/GrowthUi.cs` 重写为固定四段：
  1. `可升级英雄卡`：仅当 `CanUpgradeHero`（余额足够且未到上限）时可见，卡面只保留"可升级"文案；
  2. `装备按钮`（单个节点，Name `BatchEquip`）：全队可替换数量为 0 时隐藏；恰好 1 件时显示该件的部位/等级/品质/当前对比，点击即穿戴；≥2 件时合并为一个大按钮"穿戴所有可替换装备（N 件）"，点击执行 `EquipAllReplaceable`（仍是玩家一次明确点击，不自动循环）；
  3. `KP购买`（Name `BuyMonsterLevelMaximum`，独立卡）；
  4. 空态说明 + 反馈行；城堡卡由 `RefreshCastleInteractions` 追加在后（索引已同步为 `upgradeCards.Count+4`）。
- 每次点击（升级/装备/KP/城堡/药剂/卷轴）都调用 `Refresh()` → `RefreshGrowthInteractions()` 重排，因此"交互后立即刷新"由同一条路径保证。
- 装备节点无论可见与否都固定占位排序，避免隐藏时位置漂移。

**证据**：`fix-ui.log` 中断言 `BatchEquip.GetIndex() < BuyMonsterLevelMaximum.GetIndex()`；`artifacts/qa/p2-4-detail-fixes/equipment-batch.png`（单个大按钮"穿戴所有可替换装备（N 件）"样式）与 `hero-equipment-bag.png`。

**未覆盖**：需求示例图中的"英雄头像 icon"仍以文字卡呈现（无头像资产）；KP 不足时仍显示 KP 卡（保留原因文案，便于玩家判断缺口），如果要求"不可购买即隐藏"需再确认。

---

## 阶段验证（每轮追加）

| 命令 | 结果 |
|---|---|
| `dotnet build` tests / Godot | 0 警告 0 错误 |
| 默认全量 Core | PASS（含 WP4–WP7 定向用例） |
| `--regression-matrix` | **48/48** |
| Godot `--headless --smoke-test` | `SMOKE_OK` |
| Godot `--first-batch-smoke` | `FIRST_BATCH_UI_PASS`（含装备页签与右栏排序断言） |

日志与截图：`artifacts/qa/p2-4-detail-fixes/`。

## 待确认/后续

1. 装备/英雄"icon"的图形化（头像、品质底色）需美术资产，当前用文字行。
2. "仅显示可升级/可替换"后，若两者皆空，右栏显示一行空态说明；是否需要更明显的引导（如指向 XP/KP 获取）待定。
3. KP 不可购买时是否隐藏整卡，待用户确认。
4. 多尺寸/长文案/连续点击矩阵仍未覆盖（主 Review §16.3 已登记）。

---

## F-03 货币栏显示 XP

**需求**：右上货币栏显示 XP 货币。

**实现**：`HudLayout` 货币栏由 2 格扩为 3 格（金币 / KP / XP）：新增 `xp` 图标与 `PublicExperience` 数值，超过 1000 显示为 `x.xK` 以免挤压面板；悬停说明"公共XP：击杀全额入池，无等级差衰减；在右栏/英雄页逐人消费"。构建自检中的格数断言同步为 3。

**证据**：`artifacts/qa/p2-4-detail-fixes/f03-lmax-purchased.png`（右上三格）；`fix2-ui.log`。

---

## F-04 装备替换按钮的合并门槛与"最好5件"清单

**需求**：只有可替换装备 **> 4 件**时才合并成一个大按钮；该按钮要完整列出**最好的 5 件**装备（图标、装备名、品质）。

**实现**（`GrowthUi.RefreshGrowthInteractions`）
- 可替换 ≤4 件：**逐件**显示卡片（装备图标色块 + `部位·品质 Lv.X` + `ATK/HP+数值（当前值）`），点击即穿该件。
- 可替换 ≥5 件：隐藏逐件卡，合并为**一个大按钮**：
  `装备所有道具升级（N 件，只替换明确更优）` + 下面 5 行 `◆ 部位·品质 Lv.X`（按数值取最好 5 件），点击一次执行 `EquipAllReplaceable`。
- 隐藏时仍固定占位，保证"装备在 KP 之前"的排序稳定。

**证据**：`artifacts/qa/p2-4-detail-fixes/f03-equipment-batch.png`；`fix2-ui.log` 中右栏顺序断言（`BatchEquip` 索引 < KP 卡索引）。

**边界**：装备"名字"目前由 `部位·品质` 组合（尚未有逐件随机名与图标资产）；清单展示取值为准，不代表最终文案。

---

## F-05 城堡卡只保留两条信息

**需求**：挑战城堡的卡只显示"挑战/攻击 xx"和"需要怪物等级"两条。

**实现**（`HudLayout.RefreshCastleInteractions`）：标题固定为 `攻击城堡`，副行只显示 `需要怪物等级：N`（N = `CastleRequiredMonsterLevel`）；排队位次、失败重试、原型说明、不满足原因全部移入**悬停提示**，卡面保持两行。

**证据**：`fix2-ui.log`；城堡卡结构由既有 `HUD_SMOKE_OK` 与 UI 冒烟覆盖。

---

## F-06 角色升级按钮信息布局

**需求**：显示角色图标、角色名字、消耗 XP 数量、等级、攻击等级、防御等级。

**实现**：升级卡改为三行——第一行"图标 + 名字 #序号 + 右侧 XP 消耗"，第二行 `等级 N · AR x · DR y`，第三行"点击升级（消耗公共XP）"；图标用 `HudIcon(hero)` 按职业配色绘制在卡左侧（美术头像待替换）。仅余额足够的角色才出现该卡（沿用 F-02 规则）。

**证据**：`f03-lmax-purchased.png`（4 张升级卡的图标/名字/XP/等级/AR/DR 布局）。

---

## F-07 解锁/退休怪物等级（Lmax / Lmin）展示

**需求**：按示意图展示"解锁怪物等级"与"退休怪物等级"两张卡。

**实现**（`GrowthUi`）
- 卡一 `解锁怪物等级 {N+1}`（Name `BuyMonsterLevelMaximum`，可点击）：`费用 KP 10 · 最低角色等级需求 {N×每级评级}`、`评定：小菜一碟/略有挑战/势均力敌/危险/致命`、`伤害/攻击/护甲/生命`（按 `MonsterLevels` 倍率与基础怪计算）、不满足时显示原因（KP不足/逐人 AR 或 DR 不足）。点击仍走 `BuyKpUpgrade(Lmax)`，逐笔 +1。
- 卡二 `退休怪物等级 {Lmin-1}`（Name `RetireMonsterLevelMinimum`，**永久禁用**）：当前 `Lmin=1` 时显示"当前已是最低等级 1；该功能对玩家关闭"；若未来 Lmin>1 显示实际目标等级并注明"未通过安全验收，需独立裁决后方可开放"。悬停提示说明 G-H 门禁。
- 排序保持：升级 → 装备 → 解锁怪物等级 → 退休怪物等级 → 城堡（`growthTailIndex` 顺延）。

**证据**：`f03-lmax-purchased.png`（可见"解锁怪物等级 3 / 费用 KP 10 · 最低角色等级需求 30"与下方禁用卡）；`fix2-ui.log`。

**边界**：参考图中的金币计价与"每级重复购买多档"未采用——本项目 Lmax 仍用 KP（既有已批准规则），正式数值/价格归 P2-5A；Lmin 按 G-H 保持关闭，不因展示需要而开放。

---

## 阶段累计验证（截至 F-07）

| 命令 | 结果 |
|---|---|
| `dotnet build` tests / Godot | 0 警告 0 错误 |
| 默认全量 Core | PASS |
| `--regression-matrix` | **48/48** |
| Godot `--headless --smoke-test` | `SMOKE_OK` |
| Godot `--first-batch-smoke` | `FIRST_BATCH_UI_PASS` |

日志与截图：`artifacts/qa/p2-4-detail-fixes/`（`fix2-*.log`、`f03-*.png`）。

---

## F-08 货币栏三格等分

**需求**：金币/KP/XP 三格宽度一致，数值文本紧凑，不超出右侧面板。

**实现**（`HudLayout.BuildHud`）：三格步长由 `156` 改为 `108`（起止 `x=1088/1196/1304`），图标 24px + 数值偏移 28px，三格间距完全等分；数值沿用紧凑格式化（`CompactAmount`/`CompactXp`，万级转 `K`、百万级转 `M`），文字必然落在面板右边界 1416 之内。

**证据**：`HUD_SMOKE_OK` 新增断言——相邻格间距差 ≤0.5px，且任一数值标签右边缘不得越过 1416。

---

## F-09 卷轴快捷栏改为固定 5 槽

**需求**：去掉"文本条 + 对最近敌人使用按钮"的两段式，改为固定 5 个槽位，点击槽位直接对最近敌人释放，取消二次确认；空槽/无库存禁用并给 tooltip。

**实现**（`HudLayout`）
- 字段 `scrollBarLabel`/`scrollUseButton` 删除，改为 `private readonly Button[] scrollSlotButtons=new Button[5];`；卷轴栏面板 `ScrollQuickBar` 位于 `(24,610) 1040×40`，5 个 `Button`（`ScrollSlot_0..4`，宽 196、步长 204）等距排布。
- 槽位↔卷轴映射：`i < config.Scrolls.Count ? config.Scrolls[i] : null`（按配置顺序稳定映射，暂无独立槽位配置表）。
- `RefreshScrollSlots()`（由 `RefreshConsumablesHud()` 末尾调用）：未定义槽显示"空槽（未投放）"并禁用；已定义槽显示 `{Name} ×{库存}`，库存 0 时禁用，tooltip 分别说明"该槽位尚无卷轴定义"/"库存0（击杀掉落）"/"点击对最近敌人释放"。
- `UseScrollFromHud(slot)`：取当前房间第一个存活怪物作为最近敌人，走既有 `session.UseScroll(id,index,out reason)`（唯一伤害实现），失败原因写 `help.Text`，成功后 `Refresh();SaveNow();`。

**边界**：当前配置仅 `scroll_bolt` 一条卷轴，因此槽 1–4 恒为"空槽（未投放）"；卷轴图标仍用文字占位，等美术资源。可攻击目标判定沿用"当前房间第一个存活怪物"，与旧按钮行为一致，未引入新的目标选择规则。

**证据**：`HUD_SMOKE_OK` 新增断言——5 个槽位存在且父节点为 `ScrollQuickBar`、未定义槽必须禁用且文案含"未投放"、已定义槽的禁用状态与库存是否为零严格一致。

---

## 阶段累计验证（截至 F-09）

| 命令 | 结果 |
|---|---|
| `dotnet build` Godot 工程（Rebuild） | 0 警告 0 错误（顺带修复既有 `HeroPage.cs:102` CS8602） |
| `dotnet run --project tests/IdleCrpg.Checks.csproj` | PASS（含 WP6 卷轴真实伤害与恰好扣减） |
| Godot `--headless -- --smoke-test` | `SMOKE_OK`，`HUD_SMOKE_OK` 含等分货币格与 5 固定卷轴槽断言 |

**未覆盖**：F-08/F-09 尚无实窗口截图（本轮仅无窗口冒烟与断言），视觉间距与 5 槽在真实渲染下的观感待截图确认；卷轴点击释放的真实战斗链路由核心 WP6 覆盖，UI 侧未做实际点击消耗冒烟。