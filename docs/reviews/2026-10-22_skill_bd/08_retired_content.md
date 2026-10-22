# 旧节点与旧技能退役备案

日期：2026-10-22。依据用户反馈“M5和M7的旧节点/旧技能保持退役状态，有备案即可”。本文件记录退役口径与归档位置：M5不物理删除48个稳定节点ID；M7的15个`skill_XX`已从正式表移除。

## 1. 口径

| 概念 | 处理 |
|---|---|
| 退役 | 不再作为新内容来源：不新增绑定、不作为初始技能、不进入玩家入口，不重新启用旧定义。 |
| 保留ID | 批准布局沿用的48个稳定节点ID继续作为正式节点ID，参与学习、装配、旧档兼容；它们**不是**退役对象。 |
| 物理删除 | M5节点不物理删除：48个稳定ID全部保留。M7的15个`skill_XX`已从正式`skill.csv`移除，旧回归改用TEST_ONLY基线；删除需要独立版本号、完整ID映射/退款、迁移前备份与回滚方案。 |
| 归档 | 迁移前配置快照保存在`tests/fixtures/legacy_skill_bd_2026_10_22/`，带`README.md`与`SHA256SUMS.txt`。 |

## 2. M5：旧节点退役

- 退役对象：旧树布局与被替换的旧节点定义。旧快照只用于旧回归与迁移前后对照。
- 归档：`tests/fixtures/legacy_skill_bd_2026_10_22/config/skill/talent.csv`（迁移前旧树），同目录`config/system/config.csv`记录旧点数/槽位规则。
- 现行：`Idle-CRPG/config/skill/talent.csv`为61节点正式布局；旧48个稳定ID在其中沿用，节点行为以现行配置和`skill_id`绑定为准。
- 旧档：`SkillTreeVersion` 0→1一次性迁移；同ID等级保留，不兼容节点按行退款并清理引用。详见[迁移清单](06_migration_inventory.md)。
- 不重新启用：没有“恢复旧树”入口；未来若需物理删除，另开迁移，不在本轮范围。

## 3. M7：旧`skill_XX`退役

15个导入技能保持退役：不绑定任何正式天赋节点，不作为初始技能，不进入GM或玩家技能入口，仅被显式旧回归引用（`CombatSkillChecks`、`DamageFormulaChecks`、`CombatTargetingChecks`等）。

| ID | 名称 | 现行状态 |
|---|---|---|
| `skill_01` | 幽魂骨矛 | 退役；仅旧回归 |
| `skill_02` | 陨石火雨 | 退役；仅旧回归 |
| `skill_04` | 狂热战吼 | 退役；仅旧回归 |
| `skill_05` | 震荡裂地波 | 退役；仅旧回归 |
| `skill_06` | 刀锋之雨 | 退役；仅旧回归 |
| `skill_07` | 雷霆审判 | 退役；仅旧回归 |
| `skill_09` | 杀戮狂热 | 退役；仅旧回归 |
| `skill_10` | 灵魂收割 | 退役；仅旧回归 |
| `skill_11` | 追魂飞刃 | 退役；仅旧回归 |
| `skill_12` | 冰霜漩涡 | 退役；仅旧回归 |
| `skill_14` | 骸骨护甲 | 退役；仅旧回归 |
| `skill_15` | 天启剑阵 | 退役；仅旧回归 |
| `skill_17` | 死亡横扫 | 退役；仅旧回归 |
| `skill_18` | 末日陨星 | 退役；仅旧回归 |
| `skill_19` | 暗影分身 | 退役；仅旧回归 |

- 归档：`tests/fixtures/legacy_skill_bd_2026_10_22/config/skill/skill.csv`及同目录`effect.csv`、`buff.csv`、`trigger.csv`等。
- 配置行：15行已从现行`Idle-CRPG/config/skill/skill.csv`物理移除，正式加载器不再读到这些ID。
- 旧回归：`tests/LegacySkillConfig`显式叠加`tests/fixtures/retired_skill_baseline_2026_10_22/skill/`，只有旧回归入口能看到这些定义；默认检查与正式加载器不叠加。

## 4. 边界

- 未执行`git commit`、`push`、`merge`或覆盖远端历史。
- M5未删除任何天赋节点ID或玩家存档字段；M7只从正式表移除15个`skill_XX`。
- 归档目录不参与正式加载，不是可独立运行的完整Config包。
- 最终数值平衡由用户后续统一重算；本备案不包含任何平衡结论。

## 更正：恢复 M5 稳定节点（2026-10-22 复核）

此前误把“未绑定`skill_id`”当成M5退役判据，物理删除了23个仍走`CastTalent`的正式主动节点，导致游侠元素箭术、法师冰霜、死灵白骨等分支缺失，违反本文件§1“批准布局沿用的48个稳定节点ID继续生效”。现已恢复：

- M5：正式`talent.csv`恢复为61节点（野蛮人14、游侠12、法师10、圣骑士12、死灵13），48个稳定旧ID全部保留并参与学习、装配与旧档兼容；其中39个主动（16个绑定`skill_id`、23个沿用`CastTalent`旧分派）、22个被动。被删节点的前置边按迁移前布局一并恢复。
- 旧节点依赖的`talent_battle_shout`、`talent_weaken`两个Buff同步恢复；它们由`CastTalent`直接引用，不依赖退役的`skill_XX`。
- M7：15个`skill_XX`仍保持退役，正式`skill.csv`保留18条定义（2普攻+16主动）；`effect`、`buff`、`trigger`、`ability`、`ability_effect`、`proc_limit`按正式技能引用闭包清理，`trigger`/`proc_limit`仅保留表头。
- 技能预览直接读取正式表：`SkillPreviewChecks`覆盖39个主动的1级/满级共78组检查；旧15个`skill_XX`不在预览范围。
- 原61节点和旧技能完整基线保存在`tests/fixtures/retired_skill_baseline_2026_10_22/skill/`。`tests/LegacySkillConfig`只在`LoadLegacy`路径叠加该基线；`Load`与默认检查直接读取生产表，避免基线再次掩盖正式配置误删。
- 不保留已删`skill_XX`的运行时迁移映射；旧档引用已删技能时校验拒绝，继续使用SaveStore既有备份恢复；主备份均不兼容时备份旧文件并自动重置进度，显示重置提示，显示/音频设置不受影响。兼容账号仍保留进度。
- 已按错误口径退款过的账号不会自动补回旧节点；沿用既有备份恢复/重置流程，不新增强制重置。
