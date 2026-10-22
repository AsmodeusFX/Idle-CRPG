# E1 TEST_ONLY fixture

这些CSV仅供MonsterEncounterChecks读取，不由游戏加载，不是正式平衡配置。
字段及实施/验收边界见 docs/reviews/2026-10-22_monster_encounter_e1.md。
运行：dotnet run --project tests/IdleCrpg.Checks.csproj -- --monster-encounter-checks
所有会话只在内存运行，不调用玩家进度写盘。
