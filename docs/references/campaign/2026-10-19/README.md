# 五章来源与导入归档

- source：temp的Chapter1～5规范、数据、HTML及生成工具原件；未执行来源脚本，未改temp。
- pre_import：本轮导入前Git已跟踪地图CSV，只读导出；基线提交号在pre_import_commit.txt。
- patched/act1～5/map.json：打完V0.2后的格子、区域及关口镜像；不是游戏运行配置。
- import_summary.json：尺寸、数量与楼层汇总；sha256.csv：原件和导入前配置哈希。
- 可复现转换器：[import_campaign.py](../../../../tools/import_campaign.py)。`--check`不写文件。
- 规则与未实现范围：[五章合同](../../../design/campaign_five_acts.md)；冲突决策、测试、性能：[验收记录](../../../reviews/2026-10-19_campaign.md)。

运行权威只有Idle-CRPG/config下各章CSV，不将参考HTML、JSON或源生成器作为第二份动态配置。共享角色、怪物、技能与奖励未由导入器覆盖。
