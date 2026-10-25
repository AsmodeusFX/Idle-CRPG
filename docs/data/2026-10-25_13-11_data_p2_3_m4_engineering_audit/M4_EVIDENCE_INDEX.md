# P2-3-M4 证据索引（V2 Review 回填版）

- 版本：**V2**，2026-10-25 13:54（新增引擎侧实测、旧档样本统计、git 可执行性复核）
- 采集环境：Windows / PowerShell、.NET SDK 10.0.301（target `net8.0`）、Godot `4.7.stable.mono.official.5b4e0cb0f`
- 版本控制：系统内**无 `git.exe`**（PATH、`C:\Program Files\Git`、`%LOCALAPPDATA%\Programs`、`E:\Git`、`D:\Program Files*` 均未命中）→ 以 mtime + SHA256 前 16 位替代；分支/提交/未提交变更不可判定
- 只读边界：仅产生标准构建产物、`artifacts/simulation/**`、`artifacts/preview-settings.png`、Godot 冒烟用的 `artifacts/qa` 隔离存档；未触碰玩家 `user://` 正式进度

---

## 1. 关键文件指纹（SHA256 前 16 位 / 字节数，采集于 13:11，配置与源码 mtime 未变）

| 文件 | SHA256(16) | 字节 |
|---|---|---|
| `Scripts/Core/SessionSnapshot.cs` | C3077E238D56AF17 | 37755 |
| `Scripts/Core/SaveStore.cs` | 11677B1384934E61 | 6604 |
| `Scripts/Core/GameSession.cs` | 00FD07C7E85D9D65 | 37637 |
| `Scripts/Core/DamagePipeline.cs` | F34E8A77D1521FFD | 8841 |
| `Scripts/Core/FightAttributes.cs` | 450BA5D1435B47D9 | 5686 |
| `Scripts/Core/HeroExperience.cs` | 2DCBBCAFBB0BC1D1 | 3349 |
| `Scripts/Core/LevelConfiguration.cs` | 9A7C7FDA2EEFFCD5 | 2837 |
| `Scripts/Core/FormationPlans.cs` | 988735BDECB7430D | 6952 |
| `Scripts/Core/Talents.cs` | 170DA5DBF3B73B0B | 20829 |
| `Scripts/Core/Stars.cs` | DD31DEFBDF14C942 | 2607 |
| `Scripts/Core/StarConfiguration.cs` | 6E4557AD176AD26C | 7919 |
| `Scripts/Core/WorldConfig.cs` | 3C5B7D1EF61EAE9A | 7696 |
| `config/combat/fightattr.csv` | 5BC6AC2CF71A8B29 | 2691 |
| `config/combat/armor_curve.csv` | 0F28E6C2B337030A | 21 |
| `config/combat/damage_rule.csv` | 57E93C399B27E2F6 | 84 |
| `config/hero/herolevel.csv` | 927E9BAB4560BFCB | 233800 |
| `config/hero/experience_level_rate.csv` | 2A2E210D4CB46351 | 88 |
| `config/hero/hero_unlocks.csv` | 8FDB8AC731845953 | 65 |
| `config/skill/talent.csv` | 7EF6E149972F8A55 | 9141 |
| `config/skill/skill.csv` | 2734A1DD651B30DD | 2123 |
| `config/star/star.csv` | 1EAE3FA8388BD11B | 1154 |
| `config/item/item.csv` | ED52AD3566D90FD1 | 127 |
| `config/monster/monsterlevel.csv` | A9C2B0B54092FA9C | 193554 |
| `config/monster/spawn_rule.csv` | 2A0A16C9A2B77170 | 128 |
| `config/system/config.csv` | 6C1AEB32B1484260 | 1038 |

基线时间：`Scripts/**/*.cs` 最新 mtime 2026-10-23 22:25:47；`config/**/*.csv` 最新 2026-10-23 20:45:01（共 195 个 CSV）；`tests/*.cs` 业务文件最新 2026-10-24 01:42。

---

## 2. 命令与结果

### 2.1 .NET

| 命令 | 结果 | 时间 |
|---|---|---|
| `dotnet --version` | `10.0.301` | 13:11 |
| `dotnet build tests\IdleCrpg.Checks.csproj -v m` | 成功，0 警告 0 错误（7.8s） | 13:11 |
| `dotnet build Idle-CRPG\Idle-CRPG.csproj -v m` | 成功，0 警告 0 错误（6.2s） | 13:12 |
| `dotnet run --no-build --project tests\IdleCrpg.Checks.csproj` | 全量 PASS（含 3 场模拟） | 13:12 |

```text
SIM_HASH 218ab6bd1617c274d2b540ca4b433eaf796981ccb1a0f0f6ced3616dad4a38fc
SIM_LEDGER in=626 out=28 expected=598 actual=598 diff=0 unattributed=0
SIM_XP verified_ticks=196 level_up_ticks=10 event_ticks=9 unexplained_ticks=0 verified_hundredths=44480 unexplained_hundredths=0
SIM_PERF ticks=7200 elapsed_ms=353 us_per_tick=49.0 events=335 ledger=113 snapshots=8 diagnostics=0
```

### 2.2 Godot 引擎侧（V2 新增，R-05 门禁解除）

可执行：`D:\Program Files (x86)\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe`
版本：`4.7.stable.mono.official.5b4e0cb0f`；渲染：`OpenGL API 3.3.0 NVIDIA 560.94 / NVIDIA GeForce RTX 4060 Laptop GPU`

| 命令 | 结果 | 时间 |
|---|---|---|
| `--headless --path Idle-CRPG -- --smoke-test` | `SMOKE_OK: 交战中`；12 组 UI 冒烟全 PASS（CLASS_TALENT/INTERACTION/TALENT/HERO/CLIENT/SCENE/HUD/CHAPTER/ACT1/GM/STAR/SKILL_PREVIEW） | 13:49 |
| `--headless --path Idle-CRPG -- --persistence-write` | `PERSISTENCE_WRITE_OK` | 13:50 |
| `--headless --path Idle-CRPG -- --persistence-read` | `PERSISTENCE_READ_OK: automatic startup restore matches saved snapshot` | 13:50 |
| `--path Idle-CRPG -- --smoke-test --display-smoke` | `DISPLAY_SMOKE_OK: preset size, fullscreen, restored window size` | 13:51 |
| `--path Idle-CRPG -- --capture --capture-settings` | `CAPTURE_OK` → `artifacts/preview-settings.png`（178,153 B，2026-10-25 13:51:21） | 13:51 |

**边界**：以上为**现存功能的实机自检**；不证明新版 UI/UE 需求（右侧成长队列、批量换装、离线提示）已达成。

---

## 3. 旧档迁移样本统计（V2 新增，R-04 支撑）

`artifacts/**` 下 `progress*.json` 共 **914 份**：

| 组合（Version/CombatVersion/HeroExperienceVersion/SkillTreeVersion） | 数量 |
|---|---|
| `1/-/-/-` | 32 |
| `2/-/-/-` | 109 |
| `2/1/-/-` | 22 |
| `3/-/-/-` | 12 |
| `3/1/-/-` | 27 |
| `4/-/-/-` | 131 |
| `4/1/-/-` | 111 |
| `4/1/1/-` | 79 |
| `4/2/1/1` | 109 |
| 内容为 `invalid`（故意损坏档，用于备份/重置恢复） | 282 |

用途：验证"旧角色等级不因 XP 折算下降"、跨内部版本（Combat/HeroExp/SkillTree）迁移；**这些是测试生成档，不等于真实玩家档**。

---

## 4. 逐项证据索引（D → 代码位置）

| D | 主要证据（文件:行/符号） |
|---|---|
| D01 | `SaveStore.cs:9-15`；`SaveStore.cs:77-82`；`SessionSnapshot.cs:10-50`；`Stars.cs:8` |
| D02 | `HeroCreation.cs:7-11`；`Main.cs:167`；`FormationPlans.cs:16/26/47`；`HeroRoster.cs:18` |
| D03 | `FormationPlans.cs:18-25/40-52/67-74`；`GameSession.cs:278-292`；`WorldCampaign.cs:235-243` |
| D04 | `HeroExperience.cs:31/34-37/41-54`；`GameSession.cs:15`；`HeroPage.cs:58` |
| D05 | `config/hero/experience_level_rate.csv`；`HeroExperience.cs:23-26`；`config/hero/herolevel.csv` |
| D06 | `HeroExperience.cs:49-51`；`AttributeLevelChecks.cs:23`；`fightattr.csv`（无 rating） |
| D07 | `DamagePipeline.cs:42-48/65-93/34`；`armor_curve.csv`；`DamageFormulaChecks.cs:23-25` |
| D08 | `skill.csv`、`talent.csv`（16 行 skill_id）；`Talents.cs:86/165`；`TalentUi.cs:47-60` |
| D09 | `Talents.cs:10/56/78/115/117`；`TalentChecks.cs:81` |
| D10 | `GameSession.cs:459-510`（:465-469）；`FloorPlanning.cs:25/40`；`ParameterAudit.cs:662` |
| D11 | `GameSession.cs:126/69`；`MonsterSpawning.cs:7`；全库 `KP|KillPoint` 0 命中 |
| D12 | `MonsterSpawning.cs`（无保底分支）；M3 结项 V0.8 §2 |
| D13 | `config/item/item.csv`、`itemtype.csv`；`WorldConfig.cs:16-18/84-86`；`GameSession.cs:570-586/597-610`；`HeroPage.cs:37` |
| D14 | `GameSession.cs:611-621`；`WorldCampaign.cs:162-192`；`TownScene.cs:113-166`；`HudLayout.cs:60/131`；`TownLayoutChecks.cs:54-60` |
| D15 | `star.csv`；`StarConfiguration.cs:30-33`；`FormationPlans.cs:16`；`Talents.cs:164`；`StarUi.cs:114` |
| D16 | `hero_unlocks.csv`；`HeroRoster.cs:20`；全库 `achievement/成就` 0 命中 |
| D17 | `ChapterCampaign.cs:17-24`；`config/world/chapter.csv`（5 行） |
| D18 | `config/village/town_npc.csv`（卷轴商店待开放）；无 scroll 表 |
| D19 | `HudLayout.cs:96-104`（药剂占位槽） |
| D20 | `SaveStore.cs:9-15`；`SessionSnapshot.cs:142`；`ClientSettings.cs:114-121`；`Main.cs:174-202` |
| D21 | `HudLayout.cs:56/66-69/147-174`；`FormationUi.cs:35/100-108`；`--smoke-test` 实跑 |
| D22 | `SimulationRunner.cs:311-319/757`；`SimulationToolChecks.cs`；P2-2 D-13 |
| D23 | `Scenes/Main.tscn`；`project.godot:14/20-22`；`HudLayout.cs:40`；`--display-smoke`/`--capture` 实跑 |
| D24 | `SummonRuntime.cs:54/64/94-96`；`SkillRuntime.cs:49-51/283-286` |
| D25 | `StarUi.cs:114`；`StarSmoke.cs:25-27`（已实跑 PASS）；`StarEditorStore.cs:22` |
| D26 | `SaveStore.cs:29/37-64`；`SessionSnapshot.cs:12-89`；5 个迁移器；§3 旧档样本 |
| D27 | Godot 可执行路径 + 5 条实跑命令（§2.2）；`git` 全盘常见位置未命中 |

---

## 5. 历史与上游引用

| 文档 | 关系 |
|---|---|
| `docs/temp/P2-3｜M3结项与M4工程审计交接包/`（M2 V1.2 / M3 V0.8 / M4 任务包 / 预填清单） | 本轮任务授权来源 |
| `docs/temp/P2-3-M4 设计Owner独立 Review V1.0.md` | V2 回填依据（R-01~R-06 与 §5 补证） |
| `docs/data/2026-10-24_01-25_data_p2_2_m3_design_readiness.md` + 同名目录 8 份 CSV | 142 ACTIVE、962 列参数、23 公式/消费链、31 接口、16 工作包、23 旧 `CastTalent` 的存量来源（本轮只复核三项，未重跑） |
| `docs/data/2026-10-24_00-03_data_p2_2_m1_inventory.md` + `config_manifest.csv` | ACTIVE/LEGACY/TEST_ONLY 状态分布来源 |
| `docs/data/2026-10-23_23-32_data_p2_1_m3_simulation_acceptance.md` | 模拟账本/停滞分级口径 |
| `docs/technical/persistence_settings.md`、`docs/technical/architecture.md` | 存档与架构合同 |

---

## 6. 未采集项（不得当作已验证）

| 项 | 原因 |
|---|---|
| 逐技能施放/命中/伤害、召唤生命周期、城堡失败序列 | Core 只有聚合计数（P2-2 D-13 待批） |
| 最小化/挂起后的长时间推进 | 本轮只跑冒烟与单次截图，未做长跑观察 |
| 真实玩家旧档 | 仅有测试生成档（§3） |
| `--param-audit` 机器表重跑 | 输出目录硬编码指向历史归档（G-08 未实施） |
| 数值表逐行合理性 | 本轮为结构审计，数值标定属 M5/数值包 |
| 跨平台（Linux/macOS）与 Steam 打包形态 | 本轮仅 Windows 单机 |

---

## 7. 变更记录

| 版本 | 时间 | 变更 |
|---|---|---|
| V1 | 2026-10-25 13:11 | 首版证据索引 |
| V2 | 2026-10-25 13:54 | 新增 §2.2 Godot 五条实跑证据与截图、§3 914 份旧档样本统计、git 可执行性复核；补 D21/D23/D25/D27 的引擎侧证据；更新未采集项（移除引擎缺口，新增长跑与跨平台） |
