using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using IdleCrpg.Core;

/// <summary>
/// P2-2-M1 只读普查器：从正式加载入口反推配置文件清单、系统清单、内容ID登记与扩容缺口。
/// 只读正式配置与源码；仅写出 5 份机器字典与统计摘要，不修改配置/Core/存档。
/// </summary>
public static class InventoryAudit
{
    /// <summary>机器字典归档文件夹：与同轮报告同名，放在 docs/data/ 下持久保存。</summary>
    public const string ArchiveFolder = "2026-10-24_00-03_data_p2_2_m1_inventory";

    private static string Q(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    private static readonly UTF8Encoding Utf8 = new(false);

    public static void Run(string root, string outDir)
    {
        string configRoot = Path.Combine(root, "Idle-CRPG/config");
        Directory.CreateDirectory(outDir);
        var config = Config.Load(configRoot);              // 正式加载入口（只读）
        var sources = SourceIndex.Load(root);
        var manifest = BuildManifest(configRoot, sources, root);
        var systems = BuildSystems();
        var (registry, danglingTotal) = BuildRegistry(configRoot, config);
        var gaps = BuildGaps(config, registry);
        var issues = BuildIssues(manifest, registry, config);

        Write(outDir, "config_manifest.csv",
            "path,format,bytes,lines,data_rows,columns,sha256,scope,scope_reason,loader_refs,shared_or_chapter,notes", manifest);
        Write(outDir, "system_inventory.csv",
            "system_id,domain,name,entry,state_owner,config_sources,test_entry,status,numeric_relevant,evidence", systems);
        Write(outDir, "content_registry.csv",
            "content_type,id,source_path,parent_id,referenced_by,deployment_scope,runtime_state,active_evidence,duplicate_id,dangling_reference,remarks", registry);
        Write(outDir, "content_expansion_gaps.csv",
            "gap_id,domain,current_support,content_shortage,proposed_direction,expansion_class,evidence,requires_M2_verification,impact_on_P2_3", gaps);
        Write(outDir, "issues_and_decisions.csv",
            "issue_id,category,severity,statement,evidence,impact,suggestion,decision_needed", issues);

        int Count(string scope) => manifest.Count(row => SplitCsv(row)[7] == scope);
        Console.WriteLine($"INVENTORY_OUT {outDir}");
        Console.WriteLine($"INVENTORY_FILES total={manifest.Count} csv={Count("ACTIVE")} partial={Count("PARTIAL")} preview={Count("PREVIEW_ONLY")} legacy={Count("LEGACY")} test={Count("TEST_ONLY")} dead={Count("NOT_REFERENCED")} imported={Count("GODOT_IMPORT")} stray={Count("NOT_FOUND")}");
        Console.WriteLine($"INVENTORY_SYSTEMS {systems.Count}");
        Console.WriteLine($"INVENTORY_CONTENT rows={registry.Count} dangling={danglingTotal}");
        Console.WriteLine($"INVENTORY_GAPS {gaps.Count} ISSUES {issues.Count}");
        Console.WriteLine($"INVENTORY_FP config={SimulationRecorder.ConfigFingerprint(configRoot)} source={SimulationRecorder.SourceFingerprint(root)}");
        Console.WriteLine($"INVENTORY_CODE {SimulationRecorder.CodeVersion(root)}");
    }

    private static void Write(string dir, string name, string header, List<string> rows)
    {
        var all = new List<string> { header };
        all.AddRange(rows);
        File.WriteAllLines(Path.Combine(dir, name), all, Utf8);
    }

    private static string[] SplitCsv(string line)
    {
        var cells = new List<string>();
        var value = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else value.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { cells.Add(value.ToString()); value.Clear(); }
            else value.Append(ch);
        }
        cells.Add(value.ToString());
        return cells.ToArray();
    }

    // ---------- 源码引用索引 ----------

    private sealed class SourceIndex
    {
        public List<(string File, int Line, string Text)> Lines { get; } = new();
        public static SourceIndex Load(string root)
        {
            var index = new SourceIndex();
            // 只索引正式脚本：测试代码会写表路径做变异，计入会产生假的“已引用”。
            foreach (string dir in new[] { Path.Combine(root, "Idle-CRPG/Scripts") })
            {
                if (!Directory.Exists(dir)) continue;
                foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                             .Where(p => !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) && !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
                {
                    string name = Path.GetRelativePath(root, file).Replace('\\', '/');
                    var lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++) index.Lines.Add((name, i + 1, lines[i]));
                }
            }
            return index;
        }

        /// <summary>按候选相对路径查找引用，返回最多 3 处 file:line；命中口径为字符串字面量包含。</summary>
        public (string refs, bool onlyFileName) Find(params string[] candidates)
        {
            var hits = new List<string>();
            // 空候选会匹配任意行，必须先剔除；否则章节外的文件会被误判为“有引用”。
            string[] valid = candidates.Where(c => !string.IsNullOrWhiteSpace(c) && c.Length > 3).ToArray();
            if (valid.Length == 0) return ("NOT_REFERENCED", false);
            foreach (var line in Lines)
            {
                if (!valid.Any(c => line.Text.Contains(c, StringComparison.Ordinal))) continue;
                string hit = line.File + ":" + line.Line;
                if (!hits.Contains(hit)) hits.Add(hit);
                if (hits.Count >= 3) break;
            }
            return (hits.Count == 0 ? "NOT_REFERENCED" : string.Join(';', hits), false);
        }
    }

    // ---------- 配置清单 ----------

    private static readonly HashSet<string> ActiveRoot = new(StringComparer.Ordinal)
    {
        "audio/audio.csv","system/window.csv","system/client.csv","system/camera.csv","system/config.csv",
        "hero/heroes.csv","hero/herolevel.csv","hero/experience_level_rate.csv","hero/hero_unlocks.csv",
        "monster/monsters.csv","monster/monsterlevel.csv","monster/spawn_rule.csv","monster/monster_drop.csv",
        "monster/act_monsters.csv","monster/act_behaviors.csv","monster/elite.csv","monster/boss_skill.csv",
        "combat/armor_curve.csv","combat/damage_rule.csv","combat/fightattr.csv","combat/fightattr_rules.csv","combat/unit_attributes.csv",
        "skill/skill.csv","skill/effect.csv","skill/buff.csv","skill/trigger.csv","skill/summon.csv","skill/chain.csv",
        "skill/proc_icd.csv","skill/proc_limit.csv","skill/runtime.csv","skill/talent.csv","skill/hero_starting_skill.csv",
        "skill/ability.csv","skill/ability_effect.csv","skill/ability_settings.csv",
        "star/star.csv","star/star_layout.csv","star/star_plan.csv",
        "item/item.csv","item/itemtype.csv",
        "village/npc.csv","village/portal_rule.csv","village/town_scene.csv","village/town_npc.csv","village/town_prop.csv","village/town_zone.csv",
        "world/chapter.csv","world/difficulty.csv","world/recovery.csv",
        "dungeon/dungeon_rule.csv","gm/gm.csv"
    };

    private static readonly HashSet<string> LegacyRoot = new(StringComparer.Ordinal)
    {
        "world/world.csv","world/region.csv","world/castle_region.csv","world/areapart.csv","world/areapart_prerequisite.csv",
        "world/region_prerequisite.csv","world/obstacle.csv","dungeon/dungeon.csv","village/village.csv"
    };

    private static readonly Dictionary<string, string> Notes = new(StringComparer.Ordinal)
    {
        ["skill/trigger.csv"] = "空表（仅表头）：触发链已实现，当前未投放任何触发配置",
        ["skill/proc_icd.csv"] = "空表（仅表头）：Proc ICD 已实现，当前无 ICD 配置",
        ["skill/proc_limit.csv"] = "空表（仅表头）：Proc 限次已实现，当前无上限配置",
        ["skill/ability.csv"] = "由 SkillConfiguration 以表名拼接加载（Table(\"ability\")）；字符串字面量扫描看不到，需按加载器判定",
        ["skill/ability_settings.csv"] = "SkillConfiguration 全局调参表（Table(\"ability_settings\")）",
        ["skill/chain.csv"] = "由 SkillConfiguration 以 Table(\"chain\") 加载；ChainEffect 运行时读取 Chains",
        ["chapters/act1/dungeon/spawn_planning.csv"] = "源码未发现读取点；首版建议表，未进入加载链",
        ["dungeon/dungeon.csv"] = "根目录旧16×16世界地牢表，正式章节使用 chapters/act*/dungeon/dungeon.csv",
        ["village/village.csv"] = "根目录旧世界村庄表，正式章节使用 chapters/act*/village/village.csv",
        ["world/region.csv"] = "根目录旧世界区域表，仅被基础 Config 读取（正式 ChapterMaps 全部来自 chapters/）",
        ["world/world.csv"] = "根目录旧世界参数（travel_speed=0.65），正式章节为 0.31",
        ["world/obstacle.csv"] = "根目录旧障碍表，正式章节按章读取 obstacles",
        ["world/areapart_prerequisite.csv"] = "仅旧世界使用；正式章节为空表，功能前置改由 region_prerequisite.csv 表达",
        ["world/region_prerequisite.csv"] = "空表（仅表头）",
        ["monster/preview_archetypes.csv"] = "GM 技能试场专用原型，非正式刷怪",
        ["monster/preview_enemy_summon.csv"] = "GM 预览敌方召唤，status=TEST_ONLY_TEMPORARY",
        ["system/client.csv"] = "自动保存/检查点参数；checkpoint_seconds 无运行时消费者",
        ["chapters/act1/dungeon/floor_plan.csv"] = "楼层预算（Planned*），不驱动实际生成数量",
        ["chapters/act2/dungeon/floor_plan.csv"] = "楼层预算（Planned*），不驱动实际生成数量",
        ["chapters/act3/dungeon/floor_plan.csv"] = "楼层预算（Planned*），不驱动实际生成数量",
        ["chapters/act4/dungeon/floor_plan.csv"] = "楼层预算（Planned*），不驱动实际生成数量",
        ["chapters/act5/dungeon/floor_plan.csv"] = "楼层预算（Planned*），不驱动实际生成数量",
        ["hero/hero_unlocks.kills.translation"] = "误生成的翻译残留，应移出 config"
    };

    private static List<string> BuildManifest(string configRoot, SourceIndex sources, string root)
    {
        var rows = new List<string>();
        foreach (string file in Directory.GetFiles(configRoot, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            string rel = Path.GetRelativePath(configRoot, file).Replace('\\', '/');
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".import")
            {
                rows.Add(string.Join(',', Q(rel), Q("godot-import"), new FileInfo(file).Length.ToString(CultureInfo.InvariantCulture), "", "", "", Hash(file), Q("GODOT_IMPORT"), Q("Godot 导入元数据（importer=keep），不参与游戏读取"), Q("n/a"), Q("shared"), Q("")));
                continue;
            }
            if (ext != ".csv")
            {
                rows.Add(string.Join(',', Q(rel), Q(ext.TrimStart('.')), new FileInfo(file).Length.ToString(CultureInfo.InvariantCulture), "", "", "", Hash(file), Q("NOT_FOUND"), Q("非 CSV 残留文件，无加载入口"), Q("NOT_REFERENCED"), Q("shared"), Q(Notes.GetValueOrDefault(rel, ""))));
                continue;
            }
            var (dataRows, columns) = Shape(file);
            string duplicateKeys = KeyNote(file);
            int lines = File.ReadAllLines(file).Length;
            string note = Notes.GetValueOrDefault(rel, "");
            if (duplicateKeys != "") note = (note == "" ? "" : note + "；") + duplicateKeys;
            string chapterRel = rel.StartsWith("chapters/", StringComparison.Ordinal) ? rel[(rel.IndexOf('/', "chapters/".Length) + 1)..] : "";
            var (refs, _) = sources.Find(rel, chapterRel);
            if (refs == "NOT_REFERENCED" && chapterRel == "")
            {
                // skill/combat/system 等表由 Table("name") 拼接读取，字面量里只有表名。
                string stem = Path.GetFileNameWithoutExtension(rel);
                var (byName, _) = sources.Find("\"" + stem + "\"");
                if (byName != "NOT_REFERENCED") refs = byName + "（按表名匹配）";
            }
            var (scope, reason) = Classify(rel, refs);
            string sharedOrChapter = rel.StartsWith("chapters/", StringComparison.Ordinal) ? "chapter" : "shared";
            rows.Add(string.Join(',', Q(rel), Q("csv"), new FileInfo(file).Length.ToString(CultureInfo.InvariantCulture), lines.ToString(CultureInfo.InvariantCulture),
                dataRows.ToString(CultureInfo.InvariantCulture), columns.ToString(CultureInfo.InvariantCulture), Hash(file), Q(scope), Q(reason), Q(refs), Q(sharedOrChapter), Q(note)));
        }
        return rows;
    }

    /// <summary>唯一性证据：无 id 列时不判定；带 scene_id 的表按组合键判定；其余按 id 判定。</summary>
    private static string KeyNote(string file)
    {
        var records = CsvTable.ReadRecords(file, File.ReadAllText(file));
        if (records.Count < 2) return "";
        var header = records[0].Cells;
        int key = Array.IndexOf(header, "id");
        if (key < 0) return "无 id 列（首列为参数/外键），不做唯一性判定";
        int scene = Array.IndexOf(header, "scene_id");
        if (scene >= 0)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int duplicates = 0;
            foreach (var record in records.Skip(1))
            {
                if (record.Cells.Length <= Math.Max(key, scene)) continue;
                if (!seen.Add(record.Cells[scene] + "|" + record.Cells[key])) duplicates++;
            }
            return $"组合键(scene_id+id)：同场景重复 {duplicates} 个";
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int dup = 0;
        foreach (var record in records.Skip(1))
        {
            if (record.Cells.Length <= key) continue;
            if (!ids.Add(record.Cells[key])) dup++;
        }
        return dup == 0 ? "id 唯一" : $"id 重复键 {dup} 个";
    }

    private static (string scope, string reason) Classify(string rel, string refs)
    {
        if (rel.StartsWith("legacy/", StringComparison.Ordinal)) return ("TEST_ONLY", "旧回归夹具：仅 Config.LoadLegacy/旧测试读取");
        if (rel == "hero/hero_unlocks.kills.translation") return ("NOT_FOUND", "非配置残留，无读取入口");
        if (rel.StartsWith("chapters/", StringComparison.Ordinal))
            return refs == "NOT_REFERENCED"
                ? ("NOT_REFERENCED", "章节目录内但源码无读取点")
                : ("ACTIVE", "章节加载器 LoadChapters→LoadWorld/LoadSceneConfig/LoadMonsterPools 按章读取");
        if (rel.StartsWith("monster/preview_", StringComparison.Ordinal)) return ("PREVIEW_ONLY", "仅 GM 技能试场 MonsterPreview/SkillPreview 读取");
        if (LegacyRoot.Contains(rel)) return ("LEGACY", "仅被基础 Config（旧16×16地图）读取；正式 ChapterMaps 全部来自 chapters/");
        if (ActiveRoot.Contains(rel)) return ("ACTIVE", "正式加载链读取（Config.Load 或其共享子加载器）");
        return refs == "NOT_REFERENCED" ? ("NOT_REFERENCED", "源码无读取点") : ("UNVERIFIED", "有字符串引用但加载链未确认");
    }

    private static (int rows, int columns) Shape(string file)
    {
        var records = CsvTable.ReadRecords(file, File.ReadAllText(file));
        if (records.Count == 0) return (0, 0);
        return (Math.Max(0, records.Count - 1), records[0].Cells.Length);
    }

    private static string Hash(string file)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(file))).ToLowerInvariant();
    }

    // ---------- 系统清单 ----------

    private static List<string> BuildSystems()
    {
        string Row(string id, string domain, string name, string entry, string owner, string cfg, string test, string status, string numeric, string evidence)
            => string.Join(',', Q(id), Q(domain), Q(name), Q(entry), Q(owner), Q(cfg), Q(test), Q(status), Q(numeric), Q(evidence));
        return new List<string>
        {
            Row("world_campaign","世界","自动远征与阶段机","WorldCampaign.Tick","WorldCampaign.Stage/route/target","chapters/act*/world/*,world/chapter.csv","--simulation A/B","ACTIVE","是","WorldCampaign.cs:158-260；阶段枚举 CampaignStage"),
            Row("dungeon_session","地牢","地牢会话与房间战斗","GameSession.Tick→TickDungeon","GameSession(Floors/CurrentRoom/destination)","dungeon/dungeon_rule.csv","--room-entry-checks","ACTIVE","是","GameSession.cs:165-330"),
            Row("entry_flow","战斗","入房通行与门口交接","CombatTargeting.AdvanceRoomEntry/EntryMoveTarget","CombatTargetingState.EntryPassMask/EntryEngagedMask","system/config.csv(combat_entry_*)","--room-entry-checks","ACTIVE","是","CombatTargeting.cs:113-200；SpatialTolerance.cs"),
            Row("targeting","战斗","索敌、接敌容量与预约","CombatTargeting.PrepareMonsterTargets","CombatTargetingState.Locks","combat/unit_attributes.csv,combat/fightattr*.csv","--targeting-checks","ACTIVE","是","CombatTargeting.cs:314-423"),
            Row("damage_formula","战斗","伤害/抗性/格挡/暴击","DamagePipeline.Resolve","DamageBreakdown","combat/damage_rule.csv,armor_curve.csv,fightattr*.csv","--formula-checks","ACTIVE","是","DamagePipeline.cs；DamageFormulaChecks"),
            Row("skills","战斗","主动技能与效果队列","SkillRuntime.RunActiveSkill/Commit/DrainEffects","SkillBattleState.Pending/Actions","skill/skill.csv,effect.csv","--skill-bd-*-checks","ACTIVE","是","SkillRuntime.cs:160-374"),
            Row("buffs_triggers","战斗","Buff/触发/Proc 限次与 ICD","SkillRuntime.ApplyBuff/React/ProcAllowed","SkillBattleState.Buffs/ProcFires/ProcIcds","skill/buff.csv,trigger.csv(空),proc_*.csv(空)","--skill-bd-proc-checks","ACTIVE","是","触发链已实现但配置为空表（见 issues）"),
            Row("summons","战斗","玩家/敌方召唤与跟随入场","SummonRuntime.*","GameSession.Summons","skill/summon.csv,monster/preview_enemy_summon.csv","--skill-bd-summon-checks","ACTIVE","是","SummonRuntime.cs；容量取自 summon.csv"),
            Row("talents","成长","三系天赋树与主动技能装配","Talents.LearnTalent/SetTalentLoadout","Actor.Talents/TalentBonuses","skill/talent.csv","--talent-checks","ACTIVE","是","Talents.cs；Config.Talents.Nodes"),
            Row("stars","成长","星图购买与全队加成","Stars.BuyStar/ApplyStarTotals","StarProgression(ranks)","star/star.csv,star_layout.csv,star_plan.csv","--star-checks","ACTIVE","是","Stars.cs:11-19；GM 节点编辑器 StarEditorStore"),
            Row("heroes_levels","成长","职业、等级与个人经验","HeroExperience.GrantHeroExperience","Actor.Level/Experience/ExperienceFraction","hero/heroes.csv,herolevel.csv,experience_level_rate.csv","--hero-experience-checks","ACTIVE","是","HeroExperience.cs:10-60"),
            Row("hero_unlock","成长","职业解锁条件","HeroRoster/Config.HeroUnlockKills","HeroUnlockKills","hero/hero_unlocks.csv","--hero-checks","ACTIVE","是","按累计击杀解锁 priest/necromancer"),
            Row("items_loot","经济","掉落、拾取、背包与自动售卖","GameSession.RollDrops/CollectLoot/SellJunk","GameSession.inventory/Gold","item/item.csv,itemtype.csv,monster/monster_drop.csv","--simulation","ACTIVE","是","GameSession.cs:567-620"),
            Row("village_trade","经济","村庄补给与售卖","WorldCampaign.Tick(Village)","WorldCampaign.TradeElapsed/Trades","chapters/act*/village/village.csv,village/npc.csv","--simulation","ACTIVE","是","WorldCampaign.cs:162-193"),
            Row("town_scene","场景","固定城镇/NPC/传送设施","TownScene.TickTown","WorldCampaign.TownPhase/Stage","village/town_*.csv,npc.csv,portal_rule.csv","--town-layout-checks","ACTIVE","是","TownScene.cs:108-200"),
            Row("castle_progression","世界","城堡、统治区与章节推进","WorldCampaign.RequestCastle/CompleteDungeon/AdvanceChapter","WorldProgress/AreaParts","chapters/act*/world/castle_region.csv,areapart*.csv,region_prerequisite.csv","--campaign-package-checks","ACTIVE","是","AreaParts.cs:56-72；RegionPrerequisites.cs"),
            Row("portals","世界","传送阵与回城门","Waypoints/WorldCampaign.BeginPortal","WorldCampaign.PortalPhase","chapters/act*/world/waypoint.csv","--town-layout-checks","ACTIVE","是","Waypoints.cs"),
            Row("monster_content","内容","怪物身份、行为、精英与 Boss 技能","MonsterContent.ApplyMonsterContent/ApplyCombatProfile","Actor 属性与行为字段","monster/act_monsters.csv,act_behaviors.csv,elite.csv,boss_skill.csv","--act-content-checks","ACTIVE","是","MonsterContent.cs；行为分离于身份"),
            Row("monster_pools","内容","区域加权投放池","MonsterPools.PickSpawnMonster","ActMonsterPools","chapters/act*/monster/region_pool.csv","--act-monster-checks","ACTIVE","是","MonsterPools.cs:39-70；缺省回退全局怪物表"),
            Row("monster_preview","内容","GM 技能试场与遭遇预览","MonsterPreview/SkillPreview","SkillPreview session","monster/preview_*.csv","--skill-preview-checks","PREVIEW_ONLY","是","仅 GM 入口，不影响正式刷怪"),
            Row("room_events","内容","房间事件（宝箱/营火）","GameSession.TickDungeon(EventUsed)","Room.Event/EventUsed","dungeon/dungeon_rule.csv(event_chance)","--simulation","PARTIAL","是","仅两种硬编码事件名，无事件配置表"),
            Row("rescue_revive","流程","清房救援与读条复活","GameSession.Rescue","ReviveTarget/ReviveElapsed","dungeon/dungeon_rule.csv(revive_*)","--room-entry-checks","ACTIVE","是","GameSession.cs:200-260"),
            Row("failure_retry","流程","全灭返村与重试","FailureRecovery/WorldCampaign.RespawnInVillage","GameSession.Retries","world/recovery.csv","--simulation C/D","ACTIVE","是","FailureRecovery.cs"),
            Row("follow_formation","流程","队形跟随与编队计划","FormationPlans/PartyFormation/OutdoorFollowing","PartyCapacity/PendingFormation","star/star.csv(party_slots)","--class-follow-checks","ACTIVE","是","编队上限来自星图节点"),
            Row("floor_planning","内容","楼层预算与规划报表","FloorPlanning.FloorReports","FloorPlan 列表","chapters/act*/dungeon/floor_plan.csv","--dungeon-travel-checks","PARTIAL","是","Planned* 仅用于报表，不驱动实际生成"),
            Row("combat_stats","诊断","本房战斗观测统计","CombatStatsRuntime.SampleCombatStats","combatStats(内存)","无","--combat-stats-checks","ACTIVE","否","只读诊断，不进存档"),
            Row("persistence","运行","存档、备份与设置","SaveStore/SettingsStore","SaveDocument/AppSettings","system/client.csv","--save-checks,--simulation --persistence-*","ACTIVE","是","原子替换+备份；设置与进度分文件"),
            Row("settings_audio","运行","设置界面与音频","ClientSettings/Main.InitializeAudio","AppSettings/audio.csv","system/window.csv,audio/audio.csv","--smoke-test","ACTIVE","否","窗口/音量持久化"),
            Row("camera_render","表现","镜头与连续世界绘制","Main/SceneRenderer","viewMode/selectedRegion","system/camera.csv","--capture","ACTIVE","否","默认跟随排头，房间战斗固定构图"),
            Row("gm_tools","调试","GM 资源/战斗/章节与节点编辑器","GmCommands.SetGmBattle/StarEditorStore","GmBattleSettings","gm/gm.csv","--gm-checks","ACTIVE","是","GM 例外不入正式规则"),
            Row("simulation","测试","模拟运行器与验收工具","SimulationRunner","SimulationConfig/Policy","tests/Simulation/**","--simulation*","TEST_ONLY","是","M3：M3_TEST_ENV_PASS")
        };
    }

    // ---------- 内容登记 ----------

    private sealed class Rows
    {
        public string File = "";
        public List<Dictionary<string, string>> Data = new();
        public HashSet<string> Ids = new(StringComparer.Ordinal);
    }

    private static Rows Table(string configRoot, string rel)
    {
        var table = new Rows { File = rel };
        string path = Path.Combine(configRoot, rel);
        if (!File.Exists(path)) return table;
        var records = CsvTable.ReadRecords(path, File.ReadAllText(path));
        if (records.Count == 0) return table;
        var header = records[0].Cells;
        foreach (var record in records.Skip(1))
        {
            if (record.Cells.Length != header.Length) continue;
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < header.Length; i++) row[header[i]] = record.Cells[i];
            table.Data.Add(row);
            if (row.TryGetValue("id", out string? id) && id != "") table.Ids.Add(id);
        }
        return table;
    }

    private static (List<string> rows, int dangling) BuildRegistry(string configRoot, Config config)
    {
        var rows = new List<string>();
        int dangling = 0;
        string Row(string type, string id, string file, string parent, string referencedBy, string scope, string state, string evidence, string dup, string danglingRef, string remark)
            => string.Join(',', Q(type), Q(id), Q(file), Q(parent), Q(referencedBy), Q(scope), Q(state), Q(evidence), Q(dup), Q(danglingRef), Q(remark));
        void Add(string type, IEnumerable<(string id, string parent, string referencedBy, string scope, string state, string evidence, string dangling, string remark)> items, string file)
        {
            foreach (var item in items)
            {
                if (item.dangling != "") dangling++;
                rows.Add(Row(type, item.id, file, item.parent, item.referencedBy, item.scope, item.state, item.evidence, "", item.dangling, item.remark));
            }
        }

        // 英雄与初始技能
        var heroes = Table(configRoot, "hero/heroes.csv");
        var starting = Table(configRoot, "skill/hero_starting_skill.csv");
        var unlocks = Table(configRoot, "hero/hero_unlocks.csv");
        Add("hero", heroes.Data.Select(r => (r["id"], "", "Config.Heroes→HeroCreation/PartyFormation", "ACTIVE", "LOADED",
            $"Config.Heroes.Count={config.Heroes.Count}；解锁阈值={(unlocks.Data.FirstOrDefault(u => u["id"] == r["id"])?["kills"] ?? "0")}",
            "", "")), "hero/heroes.csv");
        Add("hero_starting_skill", starting.Data.Select(r =>
        {
            string skill = r.GetValueOrDefault("skill_id", "");
            bool ok = config.Skills.Skills.ContainsKey(skill) || config.Talents.Nodes.ContainsKey(skill);
            return (r["id"], skill, "StartingSkills.GrantStarterSkill", "ACTIVE", "LOADED", "创建角色时自动装配", ok ? "" : "skill_id 未找到：" + skill, "");
        }), "skill/hero_starting_skill.csv");

        // 天赋节点
        var talents = Table(configRoot, "skill/talent.csv");
        Add("talent_node", talents.Data.Select(r =>
        {
            string id = r["id"];
            string hero = r.GetValueOrDefault("hero", "");
            string prereq = r.GetValueOrDefault("prerequisite", "");
            string kind = r.GetValueOrDefault("kind", "");
            string skillId = r.GetValueOrDefault("skill_id", "");
            var problems = new List<string>();
            if (prereq != "" && !talents.Ids.Contains(prereq)) problems.Add("前置缺失:" + prereq);
            if (!heroes.Ids.Contains(hero)) problems.Add("职业缺失:" + hero);
            if (skillId != "" && !config.Skills.Skills.ContainsKey(skillId)) problems.Add("绑定技能缺失:" + skillId);
            return (id, hero, "Talents.LearnTalent/SetTalentLoadout", "ACTIVE", kind == "active" ? "LOADED|EQUIPPABLE" : "LOADED|PASSIVE_ONLY",
                $"Config.Talents.Nodes={config.Talents.Nodes.Count}；kind={kind}；row/col={r.GetValueOrDefault("row", "")}/{r.GetValueOrDefault("col", "")}",
                string.Join('|', problems), skillId == "" ? "旧 CastTalent 分派（effect 列）" : "skill_id 绑定");
        }), "skill/talent.csv");

        // 技能/效果/Buff/召唤/能力
        var skills = Table(configRoot, "skill/skill.csv");
        var effects = Table(configRoot, "skill/effect.csv");
        var buffs = Table(configRoot, "skill/buff.csv");
        var summons = Table(configRoot, "skill/summon.csv");
        var triggers = Table(configRoot, "skill/trigger.csv");
        var abilities = Table(configRoot, "skill/ability_effect.csv");
        Add("skill", skills.Data.Select(r =>
        {
            var refs = (r.GetValueOrDefault("effect_ids", "") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);
            var missing = refs.Where(e => !effects.Ids.Contains(e)).ToArray();
            return (r["id"], r.GetValueOrDefault("role", ""), "SkillRuntime.RunActiveSkill/Commit", "ACTIVE", r.GetValueOrDefault("kind", ""),
                $"Config.Skills.Skills={config.Skills.Skills.Count}；effects={refs.Length}", missing.Length == 0 ? "" : "效果缺失:" + string.Join('|', missing), "");
        }), "skill/skill.csv");
        Add("effect", effects.Data.Select(r =>
        {
            string buff = r.GetValueOrDefault("buff_id", "");
            string mechanic = r.GetValueOrDefault("mechanic_id", "");
            var problems = new List<string>();
            if (buff != "" && !buffs.Ids.Contains(buff)) problems.Add("buff缺失:" + buff);
            if (mechanic != "" && !summons.Ids.Contains(mechanic) && !abilities.Ids.Contains(mechanic)
                && !config.Skills.Abilities.ContainsKey(mechanic) && !config.Skills.AbilityEffects.ContainsKey(mechanic)
                && !config.Skills.Chains.ContainsKey(mechanic)) problems.Add("mechanic未解析:" + mechanic);
            return (r["id"], r.GetValueOrDefault("kind", ""), "SkillRuntime.DrainEffects", "ACTIVE", r.GetValueOrDefault("kind", ""),
                $"Config.Skills.Effects={config.Skills.Effects.Count}", string.Join('|', problems), "");
        }), "skill/effect.csv");
        Add("buff", buffs.Data.Select(r =>
        {
            var trig = (r.GetValueOrDefault("trigger_ids", "") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);
            var missing = trig.Where(t => !triggers.Ids.Contains(t)).ToArray();
            return (r["id"], r.GetValueOrDefault("mechanic", ""), "SkillRuntime.ApplyBuff/React", "ACTIVE", r.GetValueOrDefault("mechanic", ""),
                $"Config.Skills.Buffs={config.Skills.Buffs.Count}", missing.Length == 0 ? "" : "触发缺失:" + string.Join('|', missing), "");
        }), "skill/buff.csv");
        Add("summon", summons.Data.Select(r => (r["id"], r.GetValueOrDefault("role", ""), "SummonRuntime.SummonEffect", "ACTIVE", "LOADED",
            $"Config.Skills.Summons={config.Skills.Summons.Count}；max_count={r.GetValueOrDefault("max_count", "")}；capacity={r.GetValueOrDefault("engagement_capacity", "")}；duration={r.GetValueOrDefault("duration_seconds", "")}",
            "", "")), "skill/summon.csv");
        Add("ability", abilities.Data.Select(r => (r["id"], r.GetValueOrDefault("source_id", ""), "AbilityRuntime.CastAbility", "ACTIVE", r.GetValueOrDefault("form", ""),
            $"Config.Skills.AbilityEffects={config.Skills.AbilityEffects.Count}", "", "")), "skill/ability_effect.csv");

        // 星图
        var stars = Table(configRoot, "star/star.csv");
        var layout = Table(configRoot, "star/star_layout.csv");
        var plan = Table(configRoot, "star/star_plan.csv");
        var layoutById = layout.Data.ToDictionary(r => r["id"], r => r, StringComparer.Ordinal);
        Add("star_node", stars.Data.Select(r =>
        {
            string id = r["id"];
            var problems = new List<string>();
            layoutById.TryGetValue(id, out var l);
            if (l == null) problems.Add("布局缺失");
            else
            {
                foreach (string p in (l.GetValueOrDefault("prereq", "") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
                    if (!stars.Ids.Contains(p)) problems.Add("前置缺失:" + p);
            }
            string attr = r.GetValueOrDefault("attr_id", "");
            bool functional = attr is "party_slots" or "skill_slots" or "talent_reset" or "talent_reset_discount";
            return (id, attr, "Stars.BuyStar→ApplyStarTotals", "ACTIVE", "PURCHASABLE",
                $"Config.Stars.Nodes={config.Stars.Nodes.Count}；max_level={r.GetValueOrDefault("max_level", "")}；cost_levels={(r.GetValueOrDefault("cost", "") ?? "").Split(';').Length}；intent={(plan.Data.FirstOrDefault(p => p["id"] == id)?["intent"] ?? "")}",
                string.Join('|', problems), functional ? "功能性节点（非属性）" : "");
        }), "star/star.csv");

        // 怪物身份/行为/精英/Boss/池
        var actMonsters = Table(configRoot, "monster/act_monsters.csv");
        var behaviors = Table(configRoot, "monster/act_behaviors.csv");
        var elites = Table(configRoot, "monster/elite.csv");
        var bossSkills = Table(configRoot, "monster/boss_skill.csv");
        Add("monster_identity", actMonsters.Data.Select(r =>
        {
            string id = r["id"];
            var behavior = behaviors.Data.FirstOrDefault(b => b.GetValueOrDefault("monster_id", "") == id);
            return (id, "", "MonsterPools.PickSpawnMonster→Spawn.Create", "ACTIVE", "SPAWNABLE",
                $"Config.ActMonsters={config.ActMonsters.Count}；behavior_policy={(behavior?.GetValueOrDefault("policy", "") ?? "默认")}",
                "", "");
        }), "monster/act_monsters.csv");
        Add("monster_behavior", behaviors.Data.Select(r =>
        {
            string skill = r.GetValueOrDefault("skill_id", "");
            return (r.GetValueOrDefault("monster_id", "") + "->" + r.GetValueOrDefault("policy", ""), r.GetValueOrDefault("monster_id", ""),
                "MonsterContent.ApplyCombatProfile", "ACTIVE", r.GetValueOrDefault("policy", ""),
                "身份与行为分离；行为只改 policy/技能包", skill != "" && !skills.Ids.Contains(skill) ? "技能缺失:" + skill : "", "");
        }), "monster/act_behaviors.csv");
        Add("elite_affix", elites.Data.Select(r =>
        {
            string buff = r.GetValueOrDefault("buff_id", "");
            return (r.GetValueOrDefault("affix_id", "") + "@" + r.GetValueOrDefault("difficulty", ""), r.GetValueOrDefault("difficulty", ""),
                "MonsterContent.TryApplyElite", "ACTIVE", r.GetValueOrDefault("policy", "") == "" ? "默认策略" : r.GetValueOrDefault("policy", ""),
                $"weight={r.GetValueOrDefault("weight", "")}；chance_pct={r.GetValueOrDefault("chance_pct", "")}",
                buff != "" && !buffs.Ids.Contains(buff) ? "buff缺失:" + buff : "", "仅普通房，Boss 房不参与");
        }), "monster/elite.csv");
        Add("boss_skill_package", bossSkills.Data.Select(r =>
        {
            string skill = r.GetValueOrDefault("skill_id", "");
            return (r.GetValueOrDefault("poi_id", "") + "->" + skill, r.GetValueOrDefault("poi_id", ""),
                "MonsterContent.ApplyMonsterContent", "ACTIVE", "BOSS_ONLY", "",
                skills.Ids.Contains(skill) ? "" : "技能缺失:" + skill, "");
        }), "monster/boss_skill.csv");

        // 章节内容
        foreach (var map in config.ChapterMaps)
        {
            string chapter = "chapters/act" + (map.ChapterIndex + 1) + "/";
            var dungeons = Table(configRoot, chapter + "dungeon/dungeon.csv");
            var profiles = Table(configRoot, chapter + "dungeon/poi_profile.csv");
            var challenges = Table(configRoot, chapter + "dungeon/poi_challenge.csv");
            var plans = Table(configRoot, chapter + "dungeon/floor_plan.csv");
            var regions = Table(configRoot, chapter + "world/region.csv");
            var gates = Table(configRoot, chapter + "world/gate.csv");
            var pools = Table(configRoot, chapter + "monster/region_pool.csv");
            var waypoints = Table(configRoot, chapter + "world/waypoint.csv");
            var areaparts = Table(configRoot, chapter + "world/areapart.csv");
            string ch = (map.ChapterIndex + 1).ToString(CultureInfo.InvariantCulture);
            Add("dungeon", dungeons.Data.Select(r =>
            {
                bool castle = r.GetValueOrDefault("kind", "") == "castle";
                bool hasProfile = profiles.Ids.Contains(r["id"]);
                return (r["id"], r.GetValueOrDefault("region_id", ""), "ExpeditionPlanner/WorldCampaign", "ACTIVE", castle ? "MANUAL_CASTLE" : "AUTO_ELIGIBLE",
                    $"ch={ch}；level={r.GetValueOrDefault("level_min", "")}–{r.GetValueOrDefault("level_max", "")}；challenge={(challenges.Data.FirstOrDefault(c => c.GetValueOrDefault("poi_id", "") == r["id"])?["challenge"] ?? "n/a")}；profile={(hasProfile ? "有" : "无")}",
                    regions.Ids.Contains(r.GetValueOrDefault("region_id", "")) ? "" : "区域缺失:" + r.GetValueOrDefault("region_id", ""),
                    hasProfile ? "POI 等级由 poi_profile/poi_challenge 覆盖" : "沿用 dungeon.csv 等级");
            }), chapter + "dungeon/dungeon.csv");
            Add("poi_floor_plan", plans.Data.Select(r => (r["id"], r.GetValueOrDefault("PoiId", ""), "FloorPlanning.FloorReports（只读报表）", "ACTIVE", "PLANNED_ONLY",
                $"ch={ch}；ActiveCellTarget={r.GetValueOrDefault("ActiveCellTarget", "")}；PlannedKill={r.GetValueOrDefault("PlannedKillMin", "")}/{r.GetValueOrDefault("PlannedKillTarget", "")}/{r.GetValueOrDefault("PlannedKillMax", "")}；status={r.GetValueOrDefault("ConfigStatus", "")}",
                dungeons.Ids.Contains(r.GetValueOrDefault("PoiId", "")) ? "" : "POI缺失:" + r.GetValueOrDefault("PoiId", ""), "预算不驱动实际生成")), chapter + "dungeon/floor_plan.csv");
            Add("region", regions.Data.Select(r => (r["id"], "", "WorldCampaign/WorldNavigation", "ACTIVE", r.GetValueOrDefault("enterable", "") == "1" ? "ENTERABLE" : "BLOCKED",
                $"ch={ch}；terrain={r.GetValueOrDefault("terrain", "")}", "", "")), chapter + "world/region.csv");
            Add("gate", gates.Data.Select(r =>
            {
                var problems = new List<string>();
                if (!regions.Ids.Contains(r.GetValueOrDefault("from_region", ""))) problems.Add("起点缺失");
                if (!regions.Ids.Contains(r.GetValueOrDefault("to_region", ""))) problems.Add("终点缺失");
                return (r["id"], r.GetValueOrDefault("from_region", ""), "TerrainGate/WorldNavigation", "ACTIVE", r.GetValueOrDefault("kind", "ROAD"), $"ch={ch}", string.Join('|', problems), "");
            }), chapter + "world/gate.csv");
            Add("region_monster_pool", pools.Data.Select(r => (r.GetValueOrDefault("areapart_id", "") + ":" + r.GetValueOrDefault("monster_id", ""), r.GetValueOrDefault("areapart_id", ""), "MonsterPools.PickSpawnMonster", "ACTIVE", "WEIGHTED",
                $"ch={ch}；weight={r.GetValueOrDefault("weight", "")}", actMonsters.Ids.Contains(r.GetValueOrDefault("monster_id", "")) ? "" : "怪物缺失:" + r.GetValueOrDefault("monster_id", ""), "")), chapter + "monster/region_pool.csv");
            Add("areapart", areaparts.Data.Select(r => (r["id"], r.GetValueOrDefault("castle_id", ""), "AreaParts.AreaPartUnlocked", "ACTIVE", r.GetValueOrDefault("unlock_mode", "any"),
                $"ch={ch}；initially_unlocked={r.GetValueOrDefault("initially_unlocked", "")}", dungeons.Ids.Contains(r.GetValueOrDefault("castle_id", "")) ? "" : "城堡缺失:" + r.GetValueOrDefault("castle_id", ""), "")), chapter + "world/areapart.csv");
            Add("waypoint", waypoints.Data.Select(r => (r["id"], r.GetValueOrDefault("castle_id", ""), "Waypoints/WorldCampaign.BeginPortal", "ACTIVE", "LOADED",
                $"ch={ch}；region={r.GetValueOrDefault("region_id", "")}", regions.Ids.Contains(r.GetValueOrDefault("region_id", "")) ? "" : "区域缺失", "")), chapter + "world/waypoint.csv");
        }

        // 经济/城镇/运行参数
        var items = Table(configRoot, "item/item.csv");
        var itemTypes = Table(configRoot, "item/itemtype.csv");
        var drops = Table(configRoot, "monster/monster_drop.csv");
        Add("item", items.Data.Select(r => (r["id"], r.GetValueOrDefault("type_id", ""), "RollDrops/Inventory", "ACTIVE", "LOADED",
            $"Config.Items={config.Items.Count}", itemTypes.Ids.Contains(r.GetValueOrDefault("type_id", "")) ? "" : "类型缺失", "")), "item/item.csv");
        Add("item_type", itemTypes.Data.Select(r => (r["id"], "", "WorldConfig/ValidateWorld", "ACTIVE", r.GetValueOrDefault("kind", ""), "", "", "")), "item/itemtype.csv");
        Add("drop", drops.Data.Select(r =>
        {
            string monster = r.GetValueOrDefault("monster_id", "");
            bool ok = actMonsters.Ids.Contains(monster) || Table(configRoot, "monster/monsters.csv").Ids.Contains(monster);
            return (monster + "->" + r.GetValueOrDefault("item_id", ""), monster, "GameSession.RollDrops", "ACTIVE", "LOADED",
                $"chance={r.GetValueOrDefault("chance", "")}", (ok ? "" : "怪物缺失:" + monster) + (items.Ids.Contains(r.GetValueOrDefault("item_id", "")) ? "" : "|物品缺失"), "");
        }), "monster/monster_drop.csv");
        Add("audio", Table(configRoot, "audio/audio.csv").Data.Select(r => (r["id"], r.GetValueOrDefault("kind", ""), "Main.InitializeAudio/PlaySound", "ACTIVE", "LOADED", r.GetValueOrDefault("path", ""), "", "")), "audio/audio.csv");
        Add("window_preset", Table(configRoot, "system/window.csv").Data.Select(r => (r["width"] + "x" + r["height"], "", "ClientSettings.BuildSettings", "ACTIVE", "LOADED", "", "", "")), "system/window.csv");
        Add("town_npc", Table(configRoot, "village/town_npc.csv").Data.Select(r => (r["id"], r.GetValueOrDefault("scene_id", ""), "TownScene.Npc(role)", "ACTIVE", "LOADED", r.GetValueOrDefault("role", ""), "", "")), "village/town_npc.csv");
        Add("town_scene", Table(configRoot, "village/town_scene.csv").Data.Select(r => (r["id"], r.GetValueOrDefault("chapter_id", ""), "TownScene.Load", "ACTIVE", "LOADED", r.GetValueOrDefault("name", ""), "", "")), "village/town_scene.csv");

        // 其余按表登记：调参常量、属性注册、规则表、每章难度带与 POI 挑战档
        void Simple(string type, string rel, string scope, string state, string evidence, string idField = "id", string parentField = "")
        {
            var table = Table(configRoot, rel);
            foreach (var r in table.Data)
            {
                string id = r.GetValueOrDefault(idField, "");
                if (id == "") id = r.Values.FirstOrDefault() ?? "";
                string parent = parentField == "" ? "" : r.GetValueOrDefault(parentField, "");
                rows.Add(Row(type, id, rel, parent, "加载器直读", scope, state, evidence, "", "", ""));
            }
            if (table.Data.Count == 0)
                rows.Add(Row(type, Path.GetFileNameWithoutExtension(rel) + "（空表）", rel, "", "加载器直读", scope, "EMPTY_TABLE", evidence + "；0 行", "", "", ""));
        }
        Simple("system_rule", "system/config.csv", "ACTIVE", "LOADED", "CombatRules/Talents.Rule 消费");
        Simple("skill_runtime", "skill/runtime.csv", "ACTIVE", "LOADED", "TickRate/预算/上限");
        Simple("ability_setting", "skill/ability_settings.csv", "ACTIVE", "LOADED", "SkillConfig.Setting 消费");
        Simple("chain", "skill/chain.csv", "ACTIVE", "LOADED", "ChainEffect→SkillConfig.Chains");
        Simple("trigger", "skill/trigger.csv", "ACTIVE", "EMPTY_TABLE", "触发链已实现，未投放配置");
        Simple("proc_icd", "skill/proc_icd.csv", "ACTIVE", "EMPTY_TABLE", "Proc ICD 已实现，未投放配置");
        Simple("proc_limit", "skill/proc_limit.csv", "ACTIVE", "EMPTY_TABLE", "Proc 限次已实现，未投放配置");
        Simple("attribute", "combat/fightattr.csv", "ACTIVE", "LOADED", "伤害/属性聚合 Key 白名单", "attr_id");
        Simple("attribute_rule", "combat/fightattr_rules.csv", "ACTIVE", "LOADED", "聚合基线与上下限");
        Simple("unit_attribute_profile", "combat/unit_attributes.csv", "ACTIVE", "LOADED", "职业/怪物战斗属性底表");
        Simple("damage_rule", "combat/damage_rule.csv", "ACTIVE", "LOADED", "DamagePipeline 常量");
        Simple("armor_curve", "combat/armor_curve.csv", "ACTIVE", "LOADED", "护甲 K 曲线");
        Simple("chapter", "world/chapter.csv", "ACTIVE", "LOADED", "章节定义与目录");
        Simple("difficulty", "world/difficulty.csv", "ACTIVE", "LOADED", "三档难度稳定 ID");
        Simple("recovery_rule", "world/recovery.csv", "ACTIVE", "LOADED", "失败阈值");
        Simple("world_npc", "village/npc.csv", "ACTIVE", "LOADED", "村庄交易 NPC");
        Simple("town_zone", "village/town_zone.csv", "ACTIVE", "LOADED", "城镇可行走区", "id", "scene_id");
        Simple("town_prop", "village/town_prop.csv", "ACTIVE", "LOADED", "城镇道具碰撞", "id", "scene_id");
        Simple("hero_unlock", "hero/hero_unlocks.csv", "ACTIVE", "LOADED", "累计击杀解锁");
        Simple("preview_archetype", "monster/preview_archetypes.csv", "PREVIEW_ONLY", "PREVIEW", "GM 技能试场原型");
        Simple("runtime_constant", "gm/gm.csv", "ACTIVE", "LOADED", "GM 默认发放/缩放档位");
        Simple("runtime_constant", "dungeon/dungeon_rule.csv", "ACTIVE", "LOADED", "地牢规模/复活/事件概率");
        Simple("runtime_constant", "system/camera.csv", "ACTIVE", "LOADED", "镜头与投影参数");
        Simple("runtime_constant", "system/client.csv", "ACTIVE", "LOADED", "自动保存/检查点（checkpoint_seconds 无消费者）");
        Simple("runtime_constant", "village/portal_rule.csv", "ACTIVE", "LOADED", "传送表演与使用距离");
        foreach (var map in config.ChapterMaps)
        {
            string chapter = "chapters/act" + (map.ChapterIndex + 1) + "/";
            Simple("difficulty_band", chapter + "dungeon/difficulty_bands.csv", "ACTIVE", "LOADED", "等级带与推荐等级；Status 字段为人读备注");
            Simple("poi_profile", chapter + "dungeon/poi_profile.csv", "ACTIVE", "LOADED", "POI 楼层数/Boss/自动顺序");
            Simple("poi_challenge", chapter + "dungeon/poi_challenge.csv", "ACTIVE", "LOADED", "挑战/推荐等级（按难度）", "id", "poi_id");
            Simple("region_prerequisite", chapter + "world/region_prerequisite.csv", "ACTIVE", "LOADED", "统治区→必需地牢前置", "id", "region_id");
            Simple("areapart_prerequisite", chapter + "world/areapart_prerequisite.csv", "ACTIVE", "EMPTY_TABLE", "空表：正式前置改由 region_prerequisite 表达");
        }

        // 等级曲线按表登记（连续 ID 由加载器校验），不逐行展开
        var heroLevels = Table(configRoot, "hero/herolevel.csv");
        var monsterLevels = Table(configRoot, "monster/monsterlevel.csv");
        rows.Add(Row("level_curve", "hero/herolevel.csv", "hero/herolevel.csv", "", "LevelConfiguration.LoadLevels", "ACTIVE", "LOADED",
            $"行数={heroLevels.Data.Count}；Config.HeroLevels={config.HeroLevels.Count}；ID 连续 1..N 由加载器校验", "", "", "按表登记，不逐行展开"));
        rows.Add(Row("level_curve", "monster/monsterlevel.csv", "monster/monsterlevel.csv", "", "LevelConfiguration.LoadLevels", "ACTIVE", "LOADED",
            $"行数={monsterLevels.Data.Count}；Config.MonsterLevels={config.MonsterLevels.Count}；倍率随等级单调", "", "", "按表登记，不逐行展开"));
        return (rows, dangling);
    }

    // ---------- 扩容缺口 ----------

    private static List<string> BuildGaps(Config config, List<string> registry)
    {
        string Row(string id, string domain, string support, string shortage, string direction, string cls, string evidence, string m2, string p23)
            => string.Join(',', Q(id), Q(domain), Q(support), Q(shortage), Q(direction), Q(cls), Q(evidence), Q(m2), Q(p23));
        return new List<string>
        {
            Row("GAP-TALENT-01","天赋树","talent.csv 61 节点；row/col 三列布局；prerequisite+required_rank+max_rank；TalentConfiguration 校验行列、前置、循环与点数",
                "每职业约 12 节点，主动节点仅 5 个（含初始技能）；缺少可用于 BD 的主动/被动组合密度",
                "在既有 talent.csv 增节点/连线/绑定 skill_id；新增被动只需属性 Key 已支持",
                "LIKELY_DATA_ONLY_NEEDS_M2",
                "Talents.cs 校验；Config.Talents.Nodes；TalentChecks 覆盖学习/前置/洗点；新增节点不改代码",
                "需要 M2 验证：新节点在战斗中的属性聚合与快照是否无需改 Core",
                "P2-3 可先补每职业主动/被动节点数量，再谈数值"),
            Row("GAP-ATTR-01","属性/Effect","combat/fightattr.csv 36 项定义 + fightattr_rules.csv 聚合规则；unit_attributes.csv 56 列；DamageBucket/Modifier 多桶",
                "属性 Key 与 Effect 种类受枚举与聚合规则约束；新增一种属性需要同时改注册表、聚合与消费方",
                "优先在既有 Key 上扩展内容；新增 Key 属现有系统表达能力扩展",
                "REQUIRES_EXISTING_SYSTEM_EXTENSION",
                "FightAttributes.cs 定义/规则；DamagePipeline 消费；buff.Attributes 只能引用已注册 Key",
                "需要 M2 验证新增 Key 的最小改动面（加载校验/聚合/消费）",
                "P2-3 设计新属性前必须先列 Key 白名单"),
            Row("GAP-STAR-01","星图","star.csv 17 节点 + layout 前置 + plan 意图；功能节点 party_slots/skill_slots/respec 已接入；属性节点走 ApplyStarTotals",
                "总量 17 节点、等级上限多为 3，长期增量空间有限；功能性节点仅 3 类",
                "同表增节点与前置；新增功能性节点需在 GameSession 增加对应消费者",
                "LIKELY_DATA_ONLY_NEEDS_M2",
                "Stars.cs:11-19；StarConfiguration；StarChecks（OR 前置、满级、金币不足）；party/skill/respec 有消费者",
                "需要 M2 确认新增功能性节点需要哪些消费点",
                "P2-3 先定义长期增量结构，价格后置"),
            Row("GAP-EVENT-01","地牢事件","仅两种硬编码事件名（宝箱/营火），触发来源于 dungeon_rule.event_chance 与 Reveal 随机",
                "无事件配置表、无事件奖励/风险表达、无事件类型枚举",
                "新增事件配置表 + 事件执行接口（属现有房间流程扩展，不是新独立系统）",
                "REQUIRES_EXISTING_SYSTEM_EXTENSION",
                "GameSession.Reveal/TickDungeon 中 Event==\"宝箱\"/\"营火\" 分支；无 event.csv",
                "需要 M2 给出事件表字段与执行点",
                "P2-3 事件是最明显的空白内容域"),
            Row("GAP-MONSTER-01","怪物/Boss","act_monsters.csv 49 身份 + act_behaviors 17 行为 + elite 10 词缀 + boss_skill 34 包；身份与行为已分离",
                "行为类型有限（area/hunter/support），Boss 阶段与脚本化波次未实现；词缀覆盖率与效果种类有限",
                "新增身份为纯数据；新增行为需要 Core 分支",
                "LIKELY_DATA_ONLY_NEEDS_M2",
                "MonsterContent.cs 行为开关；boss_skill 绑定 boss；elite 仅普通房",
                "需要 M2 核对新增行为的最小编码面",
                "P2-3 先补身份/技能包，行为逻辑随后"),
            Row("GAP-LEVEL-01","关卡/地图","5 章独立地图、415 区域、231 POI、1515 楼层预算；gate/waypoint/areapart/region_prerequisite 全为数据",
                "楼层预算（Planned*/ActiveCellTarget）不驱动实际生成；章节结构扩需同时改 CSV 与章节头",
                "在既有 chapters/actN 结构内扩 POI/区域；新增章节属于结构扩展",
                "DATA_ONLY_CONFIRMED",
                "FloorPlanning.FloorReports 仅报表；ChapterConfiguration 要求恰好 5 章 3 难度",
                "需要 M2 确认楼层预算接入实际生成的代价",
                "P2-3 扩内容不需新章节，但预算与生成需对齐"),
            Row("GAP-EQUIP-01","装备/商店","当前无装备、无商店、无新养成系统",
                "—",
                "本轮不设计；如后续需要属新独立系统",
                "OUT_OF_SCOPE_NEW_SYSTEM",
                "配置目录与 Core 均无装备/商店表与消费者",
                "否",
                "P2-3 明确不新增")
        };
    }

    // ---------- 问题与裁决 ----------

    private static List<string> BuildIssues(List<string> manifest, List<string> registry, Config config)
    {
        string Row(string id, string category, string severity, string statement, string evidence, string impact, string suggestion, string decision)
            => string.Join(',', Q(id), Q(category), Q(severity), Q(statement), Q(evidence), Q(impact), Q(suggestion), Q(decision));
        return new List<string>
        {
            Row("ISS-001","死配置","中","act1 spawn_planning.csv 无任何源码读取点","chapters/act1/dungeon/spawn_planning.csv；源码检索 NOT_REFERENCED","容量建议表不影响运行，易被误读为已生效生成参数","列入死配置清单，或在 P2-3 决定是否接入","需要：保留为设计输入或删除"),
            Row("ISS-002","审计方法","中","仅按路径字面量扫描会把 ability.csv/chain.csv/ability_settings.csv 误判为死配置","SkillConfiguration 用 Table(\"ability\"/\"chain\"/\"ability_settings\") 拼接表名加载；字面量不可见","若只依赖字符串扫描，会漏掉按表名加载的表并误报","配置清单必须同时按加载器与表名匹配（本工具已同时使用两种证据）","否"),
            Row("ISS-003","空表","中","skill/trigger.csv、proc_icd.csv、proc_limit.csv 仅有表头","行数=0，但 Reactive/Proc 代码已实现并通过专项检查","触发链、Proc ICD/限次能力存在但零投放，P2-3 可用空间未使用","在 P2-3 设计触发/Proc 内容后再填表","需要：确认是否允许空表长期存在"),
            Row("ISS-004","配置污染","低","hero/hero_unlocks.kills.translation 残留于 config","非 CSV 残留；.gitignore 已忽略","不参与运行，但违反“config 只放 CSV+import”约定","移出 config","否"),
            Row("ISS-005","双来源","中","地牢等级同时来自 dungeon.csv 的 level_min/max 与 poi_profile/poi_challenge","Reveal 中 profile 存在时用 challenge 覆盖等级","调参时可能只改一处导致不一致","P2-3 明确唯一权威列并加一致性校验","需要：P2-3 裁决"),
            Row("ISS-006","根目录旧表","中","根目录 world/*、dungeon/dungeon.csv、village/village.csv 仍被基础 Config 读取","Config.Load→LoadWorld(directory) 读到后被 ChapterMaps 覆盖；travel_speed 0.65 vs 0.31","误读根目录数值会得到与正式运行不同的结论","文档与审计统一以 chapters/ 为准；旧表标注 LEGACY","否"),
            Row("ISS-007","规划≠运行","中","floor_plan 的 ActiveCellTarget/PlannedKill 不驱动实际生成","FloorPlanning.FloorReports 仅报表；Reveal 用 spawn_rule 与房间尺寸","预算表容易被当作实际每层数量","报告与设计文档必须区分","需要：P2-3 决定是否接入"),
            Row("ISS-008","空前置","低","五章 areapart_prerequisite.csv 均为空表；功能前置改由 region_prerequisite.csv 表达","AreaPartUnlocked 优先读取 RegionPrerequisites","两套前置机制并存，仅一套在用","保留一套并注明另一套未使用","否"),
            Row("ISS-009","无效旋钮","低","system/client.csv 的 checkpoint_seconds 无运行时消费者","仅 ClientSmoke 断言“无周期写盘”","配置旋钮存在但无效，易误判保存频率","文档标注或删除字段","需要：是否删除字段"),
            Row("ISS-010","注册表覆盖","低","技能/召唤的逐次事实仍 UNAVAILABLE（M3）","M3 报告；本轮未改 Core","P2-4 不能逐技能核验","按 M3-C 提案审批只读采集接口","需要：审批"),
            Row("ISS-011","行为上限","中","怪物行为仅 area/hunter/support 三类分支","MonsterContent.ApplyMonsterContent；其余为默认","新增行为类型必须改 Core，不是纯数据","P2-3 排期时区分“新增身份”和“新增行为”","否"),
            Row("ISS-012","事件硬编码","中","房间事件仅宝箱/营火两种字符串分支，无配置表","GameSession.Reveal 中 Event=\"宝箱\"/\"营火\"","事件内容无法数据扩充","P2-3 增 event 配置表与执行接口","需要：P2-3 立项"),
            Row("ISS-013","属性空转","中","fightattr 注册 36 个 Key，配置实际只投放少数几个","unit_attributes 提供 12 列（含 4 抗性）；buff 提供 armor/attack_speed_pct/move_speed/4 抗性/physical_vulnerable_damage_pct；星图只给 attack/hp；元素增伤、技能增伤、boss/ranged 增伤、power/skill_coefficient、其余易伤 Key 无任何配置投放","大量已实现的伤害桶没有内容使用，P2-3 有现成表达空间","P2-3 直接在既有 Key 上设计内容，不新增 Key","否"),
            Row("ISS-014","双路径","中","24 个 active 天赋节点 skill_id 为空，走旧 CastTalent 简化结算","talent.csv 中 shout/weaken/leap/whirlwind/shot/nova 共 24 节点；CastTalent 对非 leap/nova 按自身范围单次结算；正式 skill.csv 另有 barb_whirlwind_window 等技能版","同一技能存在旧简化版与正式版两种行为，逐技能数值核验会分叉","P2-3 裁决是否把旧节点迁移到 skill_id 绑定","需要：P2-3 裁决")
        };
    }
}
