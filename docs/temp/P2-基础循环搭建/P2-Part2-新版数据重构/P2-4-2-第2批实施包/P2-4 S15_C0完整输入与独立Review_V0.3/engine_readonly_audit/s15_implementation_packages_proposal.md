# Proposed implementation packages (NOT EXECUTED)

1. Obtain the original S04-S13 row-level candidate tables and stable design IDs; complete schema_diff/id_crosswalk first. A full migration estimate is unavailable until INPUT_MISSING is resolved.
2. Review every skill/talent effect using the existing DamagePipeline/Buff/Summon/Chain authority. Split NEEDS_RUNTIME_SUPPORT into behavior tests and save compatibility work. Existing skill names are insufficient to mark REUSE.
3. Validate all 31 star effect keys and prices before production. Missing capacity effects and retired slots cannot pad purchase counts. Resolve quality-ID naming and H-parameter source versions.
4. Connect content to real P0 IDs and verify AND/OR, optional branches, Boss/event cells and pools in QA copies. Do not replace topology.
5. Split five-scroll/four-potion work by real damage/control/movement/team effect. Verify inventory, occupied active slot, source ownership and reload; no silent substitute damage behavior.
6. Runtime asset_ledger.csv now records actual XP/KP/gold/equipment/scroll/potion transactions. Existing snapshots expose levels/chapter/retries. TTK, Boss attempts/win rates, actual monster-level histograms and per-hero talent sampling still need dedicated metric verification. Fixed offline H quotas are not foreground earning estimates. Ten-hour balance is not validated.

This is a proposal/dependency list only. No new content, effects or numerical tuning is implemented by this audit.
