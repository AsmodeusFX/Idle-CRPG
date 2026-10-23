using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IdleCrpg.Core;

/// <summary>
/// P2-2-M2/M3 只读审计：把 M1 的内容清单推进为「参数—公式—消费方—验证方法」。
/// 参数台账按 (正式来源路径, 列名) 生成；公式/属性/消费链/扩容接口为人工核对后写入。
/// M3 起把「投放(has_content_deployment)／消费(has_code_consumer)／运行验证级别」拆成独立列。
/// 只读正式配置与源码，仅写出 5 份机器表，不改 Core/配置/存档。
/// </summary>
public static class ParameterAudit
{
    public const string ArchiveFolder = "2026-10-24_01-25_data_p2_2_m3_design_readiness";
    private const string M1Folder = "2026-10-24_00-03_data_p2_2_m1_inventory";

    private static readonly UTF8Encoding Utf8 = new(false);
    private static string Q(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
    /// <summary>供 DesignInterfaceAudit 复用的 CSV 引号转义。</summary>
    internal static string Quote(string? v) => Q(v);

    public static void Run(string root, string outDir)
    {
        string configRoot = Path.Combine(root, "Idle-CRPG/config");
        Directory.CreateDirectory(outDir);
        var config = Config.Load(configRoot);
        var manifest = ReadManifest(Path.Combine(root, "docs/data", M1Folder, "config_manifest.csv"));
        var registry = BuildRegistry(configRoot, config, manifest);
        var formulas = FormulaRows();
        var attributes = AttributeRows(configRoot, config);
        var chains = ChainRows();
        var expansion = ExpansionRows();
        var registryByFile = registry.ToDictionary(r => r.Path, r => r.Columns.Count);

        Write(outDir, "parameter_registry.csv",
            "parameter_id,source_path,column,schema_group,system,mechanism,value_type,unit,semantics,role,loader,runtime_consumer,default_and_fallback,override_priority,validation_and_bounds,apply_stage,aggregation,scope_status,verification_level,test_reference,notes",
            registry.SelectMany(r => r.Columns.Select(c => string.Join(',',
                Q(c.Id), Q(r.Path), Q(c.Column), Q(r.Group), Q(r.System), Q(r.Mechanism), Q(c.ValueType), Q(c.Unit),
                Q(c.Semantics), Q(c.Role), Q(r.Loader), Q(c.Consumer), Q(c.Default), Q(r.Priority), Q(c.Bounds),
                Q(c.Stage), Q(c.Aggregation), Q(c.Scope), Q(c.Verification), Q(c.Tests), Q(c.Notes)))));
        Write(outDir, "formula_dependency.csv",
            "formula_id,mechanism,inputs,formula_and_order,defaults,clamp_and_rounding,random,trigger,output,write_point,affected_systems,source_evidence,existing_check,verification_level,risk",
            formulas);
        Write(outDir, "attribute_effect_matrix.csv",
            "category,key,definition_source,default_and_baseline,config_supplied_by,aggregation,consumer,has_content_deployment,has_code_consumer,runtime_verification_level,status,notes",
            attributes);
        Write(outDir, "runtime_consumption_matrix.csv",
            "chain_id,system,config_source,loader,rule_or_aggregation,runtime,result,observable_evidence,verification_level",
            chains);
        Write(outDir, "expansion_interface_matrix.csv",
            "system,existing_capability,candidate_content_extension,data_only_or_core_extension_or_out_of_scope,configuration_schema_support,new_consumer_needed,minimum_code_touchpoints,testability,missing_fact,P2_3_decision",
            expansion);

        // M3：设计接口就绪矩阵 / P2-3 工作包 / schema 与裁决登记
        var readiness = IdleCrpg.Checks.DesignInterfaceAudit.ReadinessRows();
        var packages = IdleCrpg.Checks.DesignInterfaceAudit.WorkPackageRows();
        var decisions = IdleCrpg.Checks.DesignInterfaceAudit.DecisionRows();
        Write(outDir, "design_interface_readiness.csv",
            "system_id,system_name,authoritative_config,code_consumer,current_capability,deployed_content,content_expansion_boundary,extension_class,observation_method,verification_level,p2_3_design_need,pending_decision,work_package",
            readiness);
        Write(outDir, "content_work_packages.csv",
            "work_id,system,current_state_and_gap,content_category,needs_new_runtime_rule,authoritative_tables_keys,extension_class,key_design_questions,p2_4_verification,unavailable_observations,depends_on,min_code_touchpoints,product_decisions",
            packages);
        Write(outDir, "schema_and_decision_register.csv",
            "decision_id,topic,current_finding,evidence,options,affected_scope,cost_and_risk,recommendation,status,approval_gate,related_work_package",
            decisions);

        var actives = manifest.Where(m => m.Scope == "ACTIVE").ToArray();
        int rows = registry.SelectMany(r => r.Columns).Count();
        int covered = actives.Count(a => registryByFile.ContainsKey(a.Path));
        Console.WriteLine($"PARAM_OUT {outDir}");
        Console.WriteLine($"PARAM_FILES active={actives.Length} covered={covered} rows={rows}");
        Console.WriteLine($"PARAM_STATUS {string.Join(' ', registry.SelectMany(r => r.Columns).GroupBy(c => c.Scope).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}"))}");
        Console.WriteLine($"PARAM_LEVEL {string.Join(' ', registry.SelectMany(r => r.Columns).GroupBy(c => c.Verification).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}"))}");
        Console.WriteLine($"PARAM_FORMULAS {formulas.Count} ATTR {attributes.Count} CHAINS {chains.Count} EXPANSION {expansion.Count}");
        Console.WriteLine($"PARAM_M3 READINESS {readiness.Count} PACKAGES {packages.Count} DECISIONS {decisions.Count}");
    }

    // ---------- M1 清单对账 ----------

    private sealed record ManifestRow(string Path, string Scope, string Rows, string Columns, string Refs);

    private static List<ManifestRow> ReadManifest(string file)
    {
        var rows = new List<ManifestRow>();
        if (!File.Exists(file)) return rows;
        var records = CsvTable.ReadRecords(file, File.ReadAllText(file));
        var header = records[0].Cells;
        int p = Array.IndexOf(header, "path"), s = Array.IndexOf(header, "scope"), r = Array.IndexOf(header, "data_rows"),
            c = Array.IndexOf(header, "columns"), f = Array.IndexOf(header, "loader_refs");
        foreach (var record in records.Skip(1))
            rows.Add(new ManifestRow(record.Cells[p], record.Cells[s], record.Cells[r], record.Cells[c], record.Cells[f]));
        return rows;
    }

    // ---------- 参数台账 ----------

    private sealed class ColumnSpec
    {
        public string Column = "", ValueType = "text", Unit = "-", Semantics = "", Role = "", Consumer = "",
            Default = "", Bounds = "", Stage = "", Aggregation = "", Scope = "UNVERIFIED", Verification = "PENDING", Tests = "", Notes = "";
        public string Id => Column;
    }

    private sealed class FileSpec
    {
        public string Path = "", Group = "", System = "", Mechanism = "", Loader = "", Priority = "";
        public List<ColumnSpec> Columns = new();
    }

    private static List<FileSpec> BuildRegistry(string configRoot, Config config, List<ManifestRow> manifest)
    {
        var files = new List<FileSpec>();
        foreach (var row in manifest.Where(m => m.Scope == "ACTIVE"))
        {
            string path = row.Path;
            var spec = new FileSpec
            {
                Path = path,
                Group = System.Text.RegularExpressions.Regex.Replace(path, "^chapters/act\\d/", "chapters/act*/"),
                System = SystemOf(path), Mechanism = MechanismOf(path), Loader = row.Refs, Priority = PriorityOf(path)
            };
            var table = ReadCsv(Path.Combine(configRoot, path));
            string[] header = table.header;
            var specLines = SpecFor(spec.Group, header);
            for (int i = 0; i < header.Length; i++)
            {
                var column = specLines.TryGetValue(header[i], out var line) ? line : Fallback(header[i]);
                column.Column = header[i];
                if (column.Notes == "") column.Notes = "";
                spec.Columns.Add(column);
            }
            files.Add(spec);
        }
        return files;
    }

    private static (string[] header, int rows) ReadCsv(string path)
    {
        var records = CsvTable.ReadRecords(path, File.ReadAllText(path));
        return records.Count == 0 ? (Array.Empty<string>(), 0) : (records[0].Cells, records.Count - 1);
    }

    private static string SystemOf(string path)
    {
        if (path.StartsWith("chapters/", StringComparison.Ordinal)) return "章节内容";
        if (path.StartsWith("hero/", StringComparison.Ordinal)) return "英雄成长";
        if (path.StartsWith("monster/", StringComparison.Ordinal)) return "怪物内容";
        if (path.StartsWith("combat/", StringComparison.Ordinal)) return "战斗属性与公式";
        if (path.StartsWith("skill/", StringComparison.Ordinal)) return "技能天赋";
        if (path.StartsWith("star/", StringComparison.Ordinal)) return "星图";
        if (path.StartsWith("item/", StringComparison.Ordinal)) return "经济物品";
        if (path.StartsWith("village/", StringComparison.Ordinal)) return "城镇村庄";
        if (path.StartsWith("world/", StringComparison.Ordinal)) return "世界关卡";
        if (path.StartsWith("dungeon/", StringComparison.Ordinal)) return "地牢生成";
        if (path.StartsWith("system/", StringComparison.Ordinal)) return "运行参数";
        if (path.StartsWith("audio/", StringComparison.Ordinal)) return "音频";
        if (path.StartsWith("gm/", StringComparison.Ordinal)) return "GM调试";
        return "未分类";
    }

    private static string MechanismOf(string path) => path switch
    {
        var p when p.Contains("/dungeon/dungeon.csv") => "地牢定义与等级",
        var p when p.Contains("/dungeon/difficulty_bands.csv") => "难度等级带",
        var p when p.Contains("/dungeon/floor_plan.csv") => "楼层规划预算",
        var p when p.Contains("/dungeon/poi_challenge.csv") => "挑战等级",
        var p when p.Contains("/dungeon/poi_profile.csv") => "POI 档案与楼层数",
        var p when p.Contains("/monster/region_pool.csv") => "区域怪物投放池",
        var p when p.Contains("/village/village.csv") => "世界村庄",
        var p when p.Contains("/world/areapart") => "统治区解锁",
        var p when p.Contains("/world/area_style.csv") => "区域样式",
        var p when p.Contains("/world/castle_region.csv") => "区域统治归属",
        var p when p.Contains("/world/gate.csv") => "地块连接门",
        var p when p.Contains("/world/map_policy.csv") => "地图策略开关",
        var p when p.Contains("/world/obstacle.csv") => "世界障碍",
        var p when p.Contains("/world/region.csv") => "世界区域",
        var p when p.Contains("/world/region_prerequisite.csv") => "区域前置",
        var p when p.Contains("/world/waypoint.csv") => "传送阵",
        var p when p.Contains("/world/world.csv") => "世界移动与交易参数",
        "hero/heroes.csv" => "职业基础属性",
        "hero/herolevel.csv" => "英雄等级曲线",
        "hero/experience_level_rate.csv" => "等级差经验倍率",
        "hero/hero_unlocks.csv" => "职业解锁",
        "monster/monsters.csv" => "基础怪物属性",
        "monster/monsterlevel.csv" => "怪物等级倍率",
        "monster/spawn_rule.csv" => "刷怪规模与奖励",
        "monster/monster_drop.csv" => "掉落表",
        "monster/act_monsters.csv" => "章节怪物身份",
        "monster/act_behaviors.csv" => "怪物行为策略",
        "monster/elite.csv" => "精英词缀",
        "monster/boss_skill.csv" => "Boss 技能包",
        "combat/fightattr.csv" => "属性注册表",
        "combat/fightattr_rules.csv" => "属性聚合边界",
        "combat/unit_attributes.csv" => "职业/怪物战斗档案",
        "combat/damage_rule.csv" => "伤害常量",
        "combat/armor_curve.csv" => "护甲 K 曲线",
        "skill/skill.csv" => "主动技能定义",
        "skill/effect.csv" => "效果定义",
        "skill/buff.csv" => "Buff 定义",
        "skill/trigger.csv" => "触发链",
        "skill/summon.csv" => "召唤定义",
        "skill/chain.csv" => "连锁定义",
        "skill/proc_icd.csv" => "Proc ICD",
        "skill/proc_limit.csv" => "Proc 限次",
        "skill/runtime.csv" => "技能运行时参数",
        "skill/ability.csv" => "Ability 定义",
        "skill/ability_effect.csv" => "Ability 效果",
        "skill/ability_settings.csv" => "Ability 全局参数",
        "skill/talent.csv" => "天赋树",
        "skill/hero_starting_skill.csv" => "职业初始技能",
        "star/star.csv" => "星图节点与价格",
        "star/star_layout.csv" => "星图布局与前置",
        "star/star_plan.csv" => "星图设计意图",
        "item/item.csv" => "物品",
        "item/itemtype.csv" => "物品类型",
        "village/npc.csv" => "村庄 NPC",
        "village/portal_rule.csv" => "传送表演",
        "village/town_scene.csv" => "城镇场景",
        "village/town_npc.csv" => "城镇 NPC",
        "village/town_prop.csv" => "城镇道具",
        "village/town_zone.csv" => "城镇可行走区",
        "world/chapter.csv" => "章节定义",
        "world/difficulty.csv" => "难度定义",
        "world/recovery.csv" => "失败恢复",
        "dungeon/dungeon_rule.csv" => "地牢规模与复活",
        "gm/gm.csv" => "GM 默认值",
        "audio/audio.csv" => "音频资源",
        "system/window.csv" => "窗口预设",
        "system/camera.csv" => "镜头参数",
        "system/client.csv" => "自动保存",
        "system/config.csv" => "全局规则常量",
        _ => "未分类"
    };

    private static string PriorityOf(string path)
    {
        if (path.StartsWith("chapters/", StringComparison.Ordinal))
            return path.Contains("/world/dungeon.csv") || path.EndsWith("/dungeon/dungeon.csv")
                ? "章节目录优先；根目录同表仅旧地图读取"
                : "章节目录为正式来源（按章加载）";
        if (path is "item/item.csv" or "item/itemtype.csv" or "monster/monster_drop.csv" or "village/npc.csv")
            return "共享表：章节加载时由 sharedDirectory 提供，五章共用";
        return "配置根目录唯一来源";
    }

    private static ColumnSpec Fallback(string column)
    {
        var spec = new ColumnSpec { Verification = "PENDING", Scope = "UNVERIFIED", Notes = "按列名规则归类，未逐项读码" };
        string c = column.ToLowerInvariant();
        if (c == "id") { spec.Role = "主键"; spec.Semantics = "行标识"; spec.Scope = "VALIDATION_ONLY"; spec.Verification = "LOADER_VERIFIED"; }
        else if (c.EndsWith("_id") || c is "poi_id" or "region_id" or "scene_id" or "monster_id" or "areapart_id" or "item_id" or "castle_id" or "skill_id" or "buff_id" or "effect_id" or "hero")
        { spec.Role = "外键"; spec.Semantics = "引用其它表的主键"; spec.Scope = "VALIDATION_ONLY"; spec.Verification = "LOADER_VERIFIED"; }
        else if (c is "name" or "label" or "description" or "theme" or "icon" or "color" or "displayname" or "boss_name" or "exit_name")
        { spec.Role = "展示"; spec.Semantics = "仅界面展示文本"; spec.Scope = "DISPLAY_ONLY"; spec.Verification = "LOADER_VERIFIED"; }
        else if (c.EndsWith("_seconds")) { spec.ValueType = "float"; spec.Unit = "s"; spec.Role = "业务数值"; spec.Semantics = "秒"; spec.Scope = "UNVERIFIED"; }
        else if (c.EndsWith("_ticks")) { spec.ValueType = "int"; spec.Unit = "tick(0.05s)"; spec.Role = "业务数值"; spec.Semantics = "逻辑 Tick"; }
        else if (c.EndsWith("_pct") || c.Contains("percent")) { spec.ValueType = "int"; spec.Unit = "%"; spec.Role = "业务数值"; spec.Semantics = "百分比"; }
        else if (c.StartsWith("is_") || c is "blocks" or "auto_sell" or "ui_visible" or "ranged" or "enterable" or "optional")
        { spec.ValueType = "bool"; spec.Role = "开关"; spec.Semantics = "布尔开关"; }
        else { spec.ValueType = "text"; spec.Semantics = "未分类字段"; }
        return spec;
    }

    /// <summary>规格行：column|valueType|unit|semantics|role|consumer|default|bounds|stage|aggregation|scope|verification|tests|notes</summary>
    private static Dictionary<string, ColumnSpec> SpecFor(string group, string[] header)
    {
        var map = new Dictionary<string, ColumnSpec>(StringComparer.Ordinal);
        // 章节表按“章内相对路径”命中同一份规格，保证五章共用一个 schema_group 仍逐文件覆盖。
        string key = group.StartsWith("chapters/act*/", StringComparison.Ordinal) ? group["chapters/act*/".Length..] : group;
        if (!Specs.TryGetValue(key, out string? block))
        {
            Specs.TryGetValue(Path.GetFileNameWithoutExtension(group), out block);
        }
        if (block == null) return map;
        foreach (string raw in block.Split('\n'))
        {
            string line = raw.Trim();
            if (line == "" || line.StartsWith("#", StringComparison.Ordinal)) continue;
            var f = line.Split('|');
            var spec = new ColumnSpec
            {
                Column = f[0], ValueType = f[1], Unit = f[2], Semantics = f[3], Role = f[4], Consumer = f[5],
                Default = f[6], Bounds = f[7], Stage = f[8], Aggregation = f[9], Scope = f[10], Verification = f[11],
                Tests = f.Length > 12 ? f[12] : "", Notes = f.Length > 13 ? f[13] : ""
            };
            map[spec.Column] = spec;
        }
        return map;
    }

    // ---------- 公式依赖 ----------

    private static List<string> FormulaRows()
    {
        string Row(string id, string mech, string inputs, string formula, string def, string clamp, string rnd, string trig, string output, string write, string affected, string evidence, string check, string level, string risk)
            => string.Join(',', Q(id), Q(mech), Q(inputs), Q(formula), Q(def), Q(clamp), Q(rnd), Q(trig), Q(output), Q(write), Q(affected), Q(evidence), Q(check), Q(level), Q(risk));
        return new List<string>
        {
            Row("F-EXP-01","英雄经验","monster.reward_xp; hero.Level; monster.Level; hero/experience_level_rate.percent; hero/herolevel.exp",
                "percent=rate[clamp(heroLv-monsterLv, min,max)]；hundredths=Experience*100+Fraction+basis*percent；Experience=hundredths/100，Fraction=hundredths%100；while Experience>=HeroLevels[Lv].exp → Experience-=exp, Lv++",
                "零差100%；首版边界外取首/末行","经验用整数百分之一存储，避免倍率取整丢失；升级循环无上限但受 MaxHeroLevel 限","无随机","每次击杀结算（GainMonsterExperience）与宝箱（GainExperience 8）",
                "Actor.Experience/ExperienceFraction/Level/MaxHp/Attack","GrantHeroExperience","英雄成长、战斗强度、天赋点",
                "HeroExperience.cs:23-26,41-53","--hero-experience-checks；--simulation 经验对账（unexplained=0）","STATIC_VERIFIED+RUNTIME（对账）",
                "经验表若被改小，同一 Tick 可连升多级，属性按 before/after 差值累加"),
            Row("F-EXP-02","等级差倍率边界","hero/experience_level_rate.csv（负/零/正连续覆盖）",
                "Clamp(gap) 到表边界后查表，超出表范围沿用首/末行","—","倍率必须连续、零差=100%、随角色相对等级上升不递增","无随机","每次经验结算","percent(0..n)","Config.LoadExperienceRates","经验、击杀价值、Lv.7 平台期",
                "HeroExperience.cs:11-26","--hero-experience-checks","STATIC_VERIFIED","倍率为 0 时击杀仍发生但经验为 0（设计平台，不是 Bug）"),
            Row("F-EXP-03","英雄等级曲线","hero/herolevel.csv（id,exp,attack_bonus,hp_bonus）",
                "升级消耗 exp；每级增量 Attack+=attack_bonus[Lv+1]-attack_bonus[Lv]；MaxHp 同理，存活者按差值回血",
                "—","级别必须 1..N 连续（加载器校验）","无随机","升级瞬间","Actor.Attack/MaxHp/Hp","GrantHeroExperience","英雄战力、模拟等级",
                "HeroExperience.cs:46-53；LevelConfiguration.cs:16","--hero-experience-checks","STATIC_VERIFIED","攻击/生命为累加增量，不是绝对值"),
            Row("F-GOLD-01","金币产出与消耗","monster_drop.chance/quantity; item.sell_price; itemtype.auto_sell; star.cost; talent_reset_* 规则",
                "击杀→RollDrops(chance×drop_chance_multiplier)→拾取时 Gold+=loot.Gold；回村自动售卖 junk→Gold+=Σ(sell_price×count)；星图 Gold-=Costs[level]；洗点 Gold-=ceil((base+invested*per_point)*(1-min(StarValue(discount),1)))",
                "drop_chance_multiplier 默认 1；洗点需星图解锁","售卖与购买均先校验再提交，失败不扣费","掉落按怪物体内 rng","击杀/拾取/回村/购买时","GameSession.Gold / 账本",
                "GameSession.RollDrops/CollectLoot/SellJunk；Stars.BuyStar；Talents.ResetTalents","经济、星图、洗点",
                "GameSession.cs:567-620；Stars.cs:11-19；Talents.cs:115-124","--simulation ledger（diff=0）","STATIC_VERIFIED+RUNTIME（账本）",
                "刷图金币只来自掉落；没有击杀直接给金币的路径"),
            Row("F-DMG-01","伤害结算","atk/armor/属性修饰; skill.coefficient; damage_rule; armor_curve.k; 抗性; 闪避/格挡/暴击",
                "k=interp(armor_curve, attackerLevel)；basis=Attack；每分量 damage=basis*weight*(coef+Σskill)*(1+Σgeneral)*(1+Σpower)*(1+Σvuln)*(crit?baseCrit+critDamage+Σcrit:1)*defense*blocked；physical defense=k/(armor+k)，元素 defense=1-clamp(resist,min,max)；合计=Σ分量",
                "base_critical_multiplier=1.5；resistance_min<0、max<1（允许负抗、不允许免疫）","每分量与合计均检查 finite 且 <float.MaxValue；命中判定 rate>=1 直接命中","闪避/格挡/暴击各一次 NextDouble（暴击可快照）",
                "每次伤害结算（技能/Buff/DoT/召唤）","HP、死亡、掉落、经验","SettleDamage→DamagePipeline.Resolve","战斗、掉落、经验、Boss 门槛",
                "DamagePipeline.cs:65-92","--formula-checks（含 207 楼层预算等）","STATIC_VERIFIED","分量权重必须合计 1；Power 加算后不得为负"),
            Row("F-DMG-02","护甲 K 曲线","combat/armor_curve.csv（id=等级,k）",
                "按攻击者等级在控制点间线性插值；等级超出曲线末端直接拒绝","起点必须 1 级，末端覆盖最大等级","k>0 且控制点等级递增、K 不倒退","无随机","每次伤害","physical defense 因子","CombatRules.ArmorK","战斗数值",
                "DamagePipeline.cs:42-48","--formula-checks","STATIC_VERIFIED","改用目标等级或英雄实时升级会破坏在途快照"),
            Row("F-SKILL-01","主动技能提交","skill.cooldown_ticks/windup_ticks/recovery_ticks/coefficient/effect_ids; talents 装配; ability_settings",
                "AI 选合法目标→Action(Windup)→Commit→按 effect_ids 依次 DrainEffects；冷却=cooldown_ticks/20×(1+attack_speed_pct)，受 cooldown_reduction_cap 限制",
                "—","技能未学习/重复/超槽位拒绝；换技能在战斗中加 swap lock","部分效果按其行为（投射物/连锁）调用 rng","Commit 时","SkillActions/Effects/HP",
                "SkillRuntime.Commit/DrainEffects","战斗节奏与伤害",
                "SkillRuntime.cs:160-374；Talents.cs:86,134-142","--skill-bd-*-checks","STATIC_VERIFIED","逐技能施放事实仍 UNAVAILABLE（M3）"),
            Row("F-EFFECT-01","效果链","effect.kind（10 类）; summon/buff/ability/chain 引用",
                "ApplyBuff→BuffState；Damage→DamagePipeline；Heal→HP；AreaPulse→周期脉冲；Chain→ChainEffect（Chains）；Summon→SummonEffect；SpawnProjectile→跳跃/投射；CorpseExplosion→尸体半径；AbilityCast→AbilityRuntime；MaintainAura→光环维持",
                "—","引用不存在即抛错；空 trigger/proc 表属未投放，不是未实现","按 kind 决定","效果结算时","Buff/HP/召唤/伤害",
                "SkillRuntime.DrainEffects；ChainRuntime；SummonRuntime","技能表现与战斗结果",
                "SkillRuntime.cs:100-374；ChainRuntime.cs:1-60","--skill-bd-*-checks","STATIC_VERIFIED","Effect 种类与 Buff/召唤耦合，新增 kind 必须改 Core"),
            Row("F-BUFF-01","Buff 属性贡献","buff.duration_ticks/stacking_policy/attributes/periodic_effect/trigger_ids/mechanic/value",
                "属性声明 (attribute:op:value) 经 FightAttributeCatalog.Aggregate 聚合成最终值；周期效果按 periodic_interval_ticks 结算；触发按 trigger_ids 反应",
                "—","操作必须匹配聚合方式；ratio 类不得为负（general_damage/skill_damage/vulnerable）","触发反应可调 rng","施加减益/增益时","BattleAttributes、DoT、触发",
                "SkillRuntime.ApplyBuff/React/EffectiveAttributes","全部战斗数值",
                "SkillRuntime.cs:55-115；FightAttributes.cs:44-59","--skill-bd-*-checks","STATIC_VERIFIED","trigger_ids 当前全部为空（零投放）"),
            Row("F-SUMMON-01","召唤","summon.max_count/hp_ratio/attack_ratio/defense_ratio/speed/range/cooldown_seconds/engagement_capacity/duration_seconds",
                "召唤物属性=施法者基础值×ratio；同时存活≤max_count；engagement_capacity 决定接敌名额；duration_seconds=0 永续",
                "—","容量/上限在生成时校验","无随机","施法/入场","Summons 列表、接敌名额",
                "SummonRuntime.SummonEffect/PruneSummons","战斗与入场",
                "SummonRuntime.cs:36-235","--skill-bd-summon-checks","STATIC_VERIFIED","召唤生命周期事件仍 UNAVAILABLE（M3）"),
            Row("F-STAR-01","星图购买与生效","star.cost（逐级）; star_layout.prereq; attr_id/operation/value_per_level; gold",
                "CanBuy：节点存在→已配置效果→任一前置点亮→未满级→金币≥Costs[level]；成功后 Gold-=cost、ApplyStarTotals 按 (totals-old) 差量改全队 Attack/MaxHp",
                "—","节点最多 2000；等级 1..10000；costs 数量必须等于 max_level；洗点折扣总和≤100%","无随机","手动购买/编辑器刷新","星图等级、全队属性、席位、技能槽、洗点",
                "Stars.BuyStar/ApplyStarTotals；StarProgression.CanBuy/Totals","成长与编队",
                "Stars.cs:11-38；StarConfiguration.cs:28-96","默认全量检查(StarChecks)","STATIC_VERIFIED","属性只支持 attack/hp，功能性节点仅 4 类且各有消费者"),
            Row("F-TALENT-01","天赋学习与洗点","talent.row/col/prerequisite/required_rank/required_points/max_rank; system/config 规则; star talent_reset",
                "CanLearn：同职业→未满级→树投入≥required_points→前置等级→点数>0；点数=initial+(Lv-1)*per_level-invested；洗点费用=ceil((base+invested*per_point)*(1-discount))",
                "initial=1；per_level/row_points 等来自 system/config.csv","前置只统计手动点；洗点必须城镇且传送结束","无随机","手动学习/洗点","天赋投入、被动属性、主动装配",
                "Actor.Talents/SkillIds；Gold（洗点）","成长、被动属性、主动装配","Talents.cs:88-125","--talent-checks","STATIC_VERIFIED","23 个主动节点无 skill_id，走旧 CastTalent 简化结算"),
            Row("F-MON-01","刷怪规模与等级","dungeon_rule.count_min/max; spawn_rule.level; dungeon.level_min/max; poi_challenge.challenge; floor_plan.MonsterLevel; difficulty_bands",
                "等级权威：floor_plan.MonsterLevel > poi_challenge.challenge > dungeon.level_min/max，统一 +spawn_rule.level-1+modifiers.LevelBonus；数量=rng.Next(count_min+bonus,count_max+bonus+1)+GM 修正，clamp 1..1000",
                "—","Boss 占总数最后一个名额；入口内侧通行区避让最多 32 次后确定性回退","按房间种子 new Random(dungeonSeed*397^floor*7919^room*104729)","进入房间 Reveal 时","房间怪物、难度、经验",
                "GameSession.Reveal；MonsterPools","战斗难度与节奏",
                "GameSession.cs:459-510","--dungeon-travel-checks；--simulation","STATIC_VERIFIED","预算字段 ActiveCellTarget/PlannedKill 不参与生成"),
            Row("F-MON-02","区域投放池","chapters/*/monster/region_pool.csv（areapart_id,monster_id,weight）",
                "权重和必须=100；按累计权重抽取；无池时回退全局 monster/monsters.csv",
                "回退全局怪物表","权重 1..1000 且每组合计 100","按房间 rng","Reveal 时","怪物身份","MonsterPools.PickSpawnMonster","怪物内容",
                "MonsterPools.cs:44-90；GameSession.cs:494","--act-monster-checks","STATIC_VERIFIED","池按统治区（areapart）而非章节"),
            Row("F-MON-03","精英词缀","elite.difficulty/affix_id/buff_id/policy/weight/chance_pct",
                "同难度出现率必须一致；rng.Next(100)<chance_pct 才掷；再按 weight 选词缀；对随机一个存活非 Boss 施加 Buff/策略；每房最多一个",
                "—","出现率 0..100；词缀必须有 buff 或非 Ordinary 策略","两次 Next(100)/Next(total)","进入房间（非 Boss 房、仅世界远征）","目标策略与 Buff",
                "GameSession.TryApplyElite","战斗难度","MonsterContent.cs:105-119","--act-content-checks","STATIC_VERIFIED","单地牢模拟与 legacy 夹具不掷骰"),
            Row("F-MON-04","Boss 技能包与行为","boss_skill.poi_id/skill_id; act_behaviors.monster_id/policy/skill_id",
                "生成时按 poi_id 附加 Boss 技能；按 monster_id 覆盖 TargetPolicy 并追加技能；不改等级/奖励/预算",
                "无配置则沿用默认策略","skill_id 必须存在于 skill.csv","无随机","Reveal 生成时","Boss/怪物技能与索敌","MonsterContent.ApplyMonsterContent","战斗策略",
                "MonsterContent.cs:86-95","--act-content-checks","STATIC_VERIFIED","行为只有 area/hunter/support 三类分支"),
            Row("F-LEVEL-01","楼层与房间规模","dungeon_rule.floors_min/max, rooms_min/max, room_width/height_min/max; poi_profile.floors; floor_plan.FloorCount",
                "有 POI 档案时楼层数=profile.floors，否则 rng.Next(floors_min,floors_max+1)；房间数=rng.Next(rooms_min,rooms_max+1)；房间半宽/半高=rng.Next(min,max+1)*0.5；最深层最高 id 为 Boss 房",
                "—","楼层 1..20；房间尺寸 4..10","Dungeon.Generate 使用独立 rng","进入地牢","房间拓扑与尺寸",
                "Dungeon.Generate；GameSession.cs:138","关卡结构","Dungeon.cs:71-99","--dungeon-travel-checks","STATIC_VERIFIED","floor_plan 的 LayoutRows/Cols/ActiveCellTarget 不参与"),
            Row("F-EVENT-01","房间事件","dungeon_rule.event_chance",
                "非 Boss 房：rng.NextDouble()<event_chance 时 rng.Next(2)==0?\"营火\":\"宝箱\"；营火存活者回复 30% MaxHp；宝箱按房间等级结算基础 8 经验",
                "—","event_chance 0..1","两次 rng 调用","清房后","HP 或 经验","GameSession.TickDungeon","战斗续航与经验",
                "GameSession.cs:208-209,506","--simulation room_event","STATIC_VERIFIED","仅两种硬编码事件，无事件表"),
            Row("F-FAIL-01","失败返村与回刷","world/recovery.failure_threshold; poi_challenge.challenge; teamLevel",
                "仅当 challenge>小队最低等级时计数；达到阈值→暂停目标，改选已首通且可达、challenge≤teamLevel 的副本（否则最低 challenge），同级集合用固定种子 731913 抽取",
                "threshold=2","阈值 1..N 由校验限制；无可用副本则沿用正常开拓","recoveryRandom(731913)","全灭返村后","目标地牢、重试","FailureRecovery.TryRecovery","挂机循环",
                "FailureRecovery.cs:29-62","--simulation C/D（重试）","STATIC_VERIFIED","城堡失败不会自动重发，只暂停"),
            Row("F-WORLD-01","世界移动与交易","world.world/travel_speed,village_speed,trade_seconds,trade_range; portal_rule; town_scene.service_seconds",
                "旅行速度=travel_speed；村内速度=village_speed；交易/服务按 trade_seconds/service_seconds 计时；传送分阶段按 phase_seconds",
                "—","速度>0、交易时间>0、范围>0","无随机","世界阶段机","移动耗时与循环收益","WorldCampaign.Tick","挂机节奏",
                "WorldConfig.cs:38-40；TownScene.cs","--campaign-package-checks","STATIC_VERIFIED","根目录旧 world.csv（0.65）不参与正式章节"),
            Row("F-CASTLE-01","城堡与章节解锁","castle_region; areapart.initially_unlocked/unlock_mode; region_prerequisite; poi_challenge.challenge",
                "统治区解锁：初始解锁→任一/全部前置统治区；区域前置需对应地牢已通关；城堡只能由玩家手动请求，排队后仍需完成当前地牢与返村交易；通关→章节推进",
                "unlock_mode=any","不可进入地块不得配城堡；每区域最多一个城堡","无随机","请求/通关/返村后","可挑战集合、章节",
                "AreaParts；RegionPrerequisites；WorldCampaign.RequestCastle/CompleteDungeon","进度推进",
                "AreaParts.cs:56-72；RegionPrerequisites.cs；WorldCampaign.cs:158-260","--campaign-package-checks；--simulation castle_*","STATIC_VERIFIED","模拟只调用正式请求接口，不代替通关"),
            Row("F-SAVE-01","存档与自动保存","system/client.autosave_min_seconds,checkpoint_seconds",
                "自动保存按 autosave_min_seconds 计时写盘；checkpoint_seconds 无消费者",
                "—","—","无随机","定时/关键节点","存档文件","ClientSettings/SaveStore","持久化",
                "ClientSettings.cs；SaveStore.cs","默认全量检查(SaveChecks)","STATIC_VERIFIED","checkpoint_seconds 为无效旋钮（M1 ISS-009）"),
            Row("F-CAMERA-01","镜头跟随","system/camera.follow_rate,transition_rate,dungeon_max_zoom,world_zoom,world_units",
                "跟随插值=follow_rate；场景切换=transition_rate；世界坐标→像素用 world_units","—","均>0；world_units≥12","无随机","每帧表现层","镜头位置","Main/SceneRenderer","表现",
                "WorldNavigation.cs:61-66","--smoke-test","STATIC_VERIFIED","仅表现层，不参与战斗判定")
        };
    }

    // ---------- 属性 / Effect 矩阵 ----------

    private static List<string> AttributeRows(string configRoot, Config config)
    {
        // 投放(has_content_deployment)、消费(has_code_consumer)、运行验证级别三者独立成列，避免再出现“是/否”与状态混写。
        string Row(string cat, string key, string source, string def, string supplied, string agg, string consumer, string deployed, bool consumed, string level, string status, string notes)
            => string.Join(',', Q(cat), Q(key), Q(source), Q(def), Q(supplied), Q(agg), Q(consumer), Q(deployed), Q(consumed ? "是" : "否"), Q(level), Q(status), Q(notes));

        // key|config_supplied_by|consumer|has_content_deployment|status
        var attr = new (string key, string supplied, string consumer, string deployed, string status)[]
        {
            ("atk","heroes.csv/monsters.csv 的 attack 作为 basis；无 buff 投放","SkillRuntime.AttackSnapshot(EffectiveValue atk)","是","ACTIVE"),
            ("hp","heroes/monsters.hp；星图 hp 节点（星光加成走 MaxHp 增量）；无 buff 投放","SkillRuntime.MaxHp(EffectiveValue hp)","是","ACTIVE"),
            ("armor","heroes/monsters.defense 作为 basis；buff armor 有投放","DamagePipeline 的 k/(armor+k)","是","ACTIVE"),
            ("move_speed","heroes/monsters.speed；buff move_speed 有投放","GameSession.TravelWalkSpeed/跟随/撤退","是","ACTIVE"),
            ("attack_range","heroes/monsters.range；无 buff 投放","CombatTargeting 索敌半径、GameSession 接敌判定","是","ACTIVE"),
            ("basic_cooldown","heroes/monsters.cooldown；无 buff 投放","GameSession 普攻冷却重置","是","ACTIVE"),
            ("dodge_rate","unit_attributes.dodge_rate","DamagePipeline.Roll(Dodge)","是","ACTIVE"),
            ("crit_rate","unit_attributes.crit_rate","DamagePipeline.Roll(CriticalChance)","是","ACTIVE"),
            ("crit_damage","unit_attributes.crit_damage","DamagePipeline 暴击因子附加项","是","ACTIVE"),
            ("block_rate","unit_attributes.block_rate","DamagePipeline.Roll(BlockChance)","是","ACTIVE"),
            ("block_reduction","unit_attributes.block_reduction","DamagePipeline blocked=1-reduction","是","ACTIVE"),
            ("attack_speed_pct","buff attack_speed_pct（haste）","GameSession 普攻计时 (1+AttackSpeed)","是","ACTIVE"),
            ("cooldown_reduction_pct","无任何配置投放","SkillRuntime 冷却缩减（受 cooldown_reduction_cap 限制）","否","EMPTY_DEPLOYMENT"),
            ("all_damage_pct","无投放","SkillRuntime 转换为 General(无类型=全类型)","否","EMPTY_DEPLOYMENT"),
            ("skill_damage_pct","无投放","SkillRuntime 转换为 General(按 skillId)","否","EMPTY_DEPLOYMENT"),
            ("basic_attack_damage_pct","无投放","SkillRuntime 转换为 General(basic_player/basic_monster)","否","EMPTY_DEPLOYMENT"),
            ("physical_damage_pct","无投放","SkillRuntime 转换为 General(Type=Physical)","否","EMPTY_DEPLOYMENT"),
            ("fire_damage_pct","无投放","同上（Fire）","否","EMPTY_DEPLOYMENT"),
            ("cold_damage_pct","无投放","同上（Cold）","否","EMPTY_DEPLOYMENT"),
            ("lightning_damage_pct","无投放","同上（Lightning）","否","EMPTY_DEPLOYMENT"),
            ("poison_damage_pct","无投放","同上（Poison）","否","EMPTY_DEPLOYMENT"),
            ("ranged_damage_pct","无投放","General(RangedOnly)","否","EMPTY_DEPLOYMENT"),
            ("boss_damage_pct","无投放","General(BossOnly)","否","EMPTY_DEPLOYMENT"),
            ("skill_coefficient_bonus","无投放","DamageBucket.Skill 加算","否","EMPTY_DEPLOYMENT"),
            ("power_damage_pct","无投放","DamageBucket.Power 加算","否","EMPTY_DEPLOYMENT"),
            ("vulnerable_damage_pct","无投放","目标 Debuff 侧构造 Vulnerable(无类型=全类型)","否","EMPTY_DEPLOYMENT"),
            ("engagement_capacity","unit_attributes.engagement_capacity","CombatTargeting 接敌名额","是","ACTIVE"),
            ("fire_resistance","unit_attributes/buff","元素 defense=1-clamp(resist)","是","ACTIVE"),
            ("cold_resistance","unit_attributes/buff","同上","是","ACTIVE"),
            ("lightning_resistance","unit_attributes/buff","同上","是","ACTIVE"),
            ("poison_resistance","unit_attributes/buff","同上","是","ACTIVE"),
            ("physical_vulnerable_damage_pct","buff 有投放","Vulnerable(Type=Physical,DebuffId)","是","ACTIVE"),
            ("fire_vulnerable_damage_pct","无投放","Vulnerable(Type=Fire)","否","EMPTY_DEPLOYMENT"),
            ("cold_vulnerable_damage_pct","无投放","Vulnerable(Type=Cold)","否","EMPTY_DEPLOYMENT"),
            ("lightning_vulnerable_damage_pct","无投放","Vulnerable(Type=Lightning)","否","EMPTY_DEPLOYMENT"),
            ("poison_vulnerable_damage_pct","无投放","Vulnerable(Type=Poison)","否","EMPTY_DEPLOYMENT")
        };
        // 在 M3 模拟/定向检查中被实际观察到的 Key；其余已投放 Key 只做静态确认。
        var runtimeKeys = new HashSet<string>(StringComparer.Ordinal)
        { "atk","hp","armor","move_speed","attack_range","basic_cooldown","attack_speed_pct","engagement_capacity" };
        var rows = new List<string>();
        foreach (var a in attr)
        {
            var def = config.Attributes.Definitions.TryGetValue(a.key, out var d) ? d : null;
            var rule = config.Attributes.Rules.TryGetValue(a.key, out var r) ? r : null;
            string rate = rule == null ? "—" : $"{rule.Baseline}（min {rule.Minimum?.ToString(CultureInfo.InvariantCulture) ?? "—"} / max {rule.Maximum?.ToString(CultureInfo.InvariantCulture) ?? "—"}）";
            bool deployed = a.deployed == "是";
            string level = deployed ? (runtimeKeys.Contains(a.key) ? "RUNTIME_VERIFIED" : "STATIC_VERIFIED") : "STATIC_VERIFIED";
            string extra = deployed ? "" : "无内容投放，运行验证标 UNAVAILABLE；";
            rows.Add(Row("attribute", a.key, "combat/fightattr.csv + fightattr_rules.csv",
                $"default={def?.Default.ToString(CultureInfo.InvariantCulture) ?? "?"}；baseline={rate}",
                a.supplied, def?.Aggregation ?? "?", a.consumer, a.deployed, true, level, a.status,
                extra + "value_type=" + (def?.ValueType ?? "?") + "；category=" + (def?.Category ?? "?")));
        }
        // 10 类 Effect
        var kinds = new (string kind, string chain, string consumer, string status, string note)[]
        {
            ("Damage","effect.kind=Damage → SettleDamage→DamagePipeline","SkillRuntime.DrainEffects","ACTIVE","最常用；支持 damage_parts 多分量"),
            ("SpawnProjectile","SpawnProjectile→Flights→命中后 impact_effect","SkillRuntime/SkillSnapshot","ACTIVE","可带穿透/弧线/齐射"),
            ("ApplyBuff","ApplyBuff→BuffState（含 DoT periodic_effect）","SkillRuntime.ApplyBuff","ACTIVE","5 个效果使用"),
            ("Heal","直接加 HP（上限 MaxHp）","SkillRuntime","ACTIVE","1 个效果使用"),
            ("AreaPulse","周期脉冲（windup/recovery 期间的持续范围伤害）","PulseRuntime/SkillRuntime","ACTIVE","旋风斩类"),
            ("Chain","Chains[mechanic_id] 逐跳，伤害按 damage_decay","ChainRuntime","ACTIVE","1 个效果使用"),
            ("Summon","SummonEffect（cap/ratio/时长）","SummonRuntime","ACTIVE","2 个效果使用"),
            ("CorpseExplosion","以尸体为圆心结算","CorpseRuntime","ACTIVE","1 个效果使用"),
            ("MaintainAura","光环维持（离开/死亡则移除）","SkillSupportRuntime","ACTIVE","3 个效果使用"),
            ("AbilityCast","交给 AbilityRuntime（form=projectile/buff…）","AbilityRuntime","ACTIVE","1 个效果使用")
        };
        foreach (var k in kinds) rows.Add(Row("effect_kind", k.kind, "skill/effect.csv 的 kind 枚举", "由 effect 行配置", "按 kind 分支", "—", k.consumer, "是", true, "RUNTIME_VERIFIED", k.status, k.note));
        // 代码存在但零投放的分支标签：与 effect_kind 分开登记，避免把「未投放」写成「未实现」。
        var undeployedKinds = new (string kind, string consumer, string note)[]
        {
            ("AbilityAttack","AbilityRuntime.HitAbility（经 ability_effect.csv 行驱动）","ability.csv 仅 1 行 projectile form；buff form 与 ability_effect 零投放"),
            ("AbilityDot","AbilityRuntime.BuffEffect→HitAbility","同上；周期伤害需 ability_effect 行"),
            ("AbilityCooldown","AbilityRuntime.BuffEffect→冷却改写","同上；零投放"),
            ("AbilityMirror","AbilityRuntime.BuffEffect→镜像投射物","同上；零投放"),
            ("SpawnEntity","SkillRuntime.SpawnGeneric（与 SpawnProjectile 同分支）","effect.csv 无 SpawnEntity 行；分支可用但无内容")
        };
        foreach (var k in undeployedKinds)
            rows.Add(Row("effect_branch_undeployed", k.kind, "SkillRuntime.DrainEffects 的 case 标签", "无配置行", "需 ability_effect/effect 行显式引用", "—", k.consumer, "否", true, "STATIC_VERIFIED", "CODE_ONLY_NO_CONTENT", k.note + "；运行验证 UNAVAILABLE"));
        rows.Add(Row("buff_mechanic", "shield/forced/haste/crit_reduce/纯属性", "skill/buff.csv 的 mechanic", "13 行（9 行 mechanic 为空=纯属性）", "按 mechanic 分支", "—", "SkillRuntime/SummonRuntime", "是", true, "RUNTIME_VERIFIED", "ACTIVE", "空 mechanic 只提供 attributes 声明"));
        rows.Add(Row("buff_trigger", "trigger", "skill/trigger.csv（0 行）", "—", "—", "—", "SkillRuntime.React", "否", true, "STATIC_VERIFIED", "CODE_ONLY_NO_CONTENT", "链已实现但零投放；运行验证 UNAVAILABLE"));
        rows.Add(Row("buff_proc", "proc_icd/proc_limit", "两张空表", "—", "—", "—", "SkillRuntime.ProcAllowed", "否", true, "STATIC_VERIFIED", "CODE_ONLY_NO_CONTENT", "功能可用、零配置；运行验证 UNAVAILABLE"));
        return rows;
    }

    // ---------- 运行消费链 ----------

    private static List<string> ChainRows()
    {
        string Row(string id, string system, string cfg, string loader, string rule, string runtime, string result, string evidence, string level)
            => string.Join(',', Q(id), Q(system), Q(cfg), Q(loader), Q(rule), Q(runtime), Q(result), Q(evidence), Q(level));
        return new List<string>
        {
            Row("C-01","英雄经验","hero/herolevel.csv, hero/experience_level_rate.csv","LevelConfiguration.LoadLevels / HeroExperience.LoadExperienceRates","等级差倍率 + 整数百分之一累积 + while 升级","GameSession.GrantHeroExperience","Actor.Level/Experience/Attack/MaxHp","events.csv level_up/xp_mismatch；--simulation 对账","RUNTIME_VERIFIED"),
            Row("C-02","怪物经验","monster/spawn_rule.xp_base, boss_reward; 怪物等级","LoadSpawnRule/LoadMonsterContent","reward_xp 由等级与 xp_base/value_per_level 计算","GainMonsterExperience","每名上阵者各自结算","kill 事件 reward_xp 字段","RUNTIME_VERIFIED"),
            Row("C-03","金币产出","monster_drop, item.sell_price, itemtype.auto_sell","LoadWorld(shared)","RollDrops→loot.Gold；拾取入账；回村售卖 junk","RollDrops/CollectLoot/SellJunk","GameSession.Gold","ledger.csv（diff=0）","RUNTIME_VERIFIED"),
            Row("C-04","金币消耗","star.cost, talent_reset_* 规则","LoadStars / TalentConfiguration","CanBuy 预演全部属性→一次提交；洗点前校验城镇/解锁/余额","BuyStar/ResetTalents","Gold 下降、星图等级/天赋清零","events.csv star_purchase/talent_reset；ledger","RUNTIME_VERIFIED"),
            Row("C-05","战斗伤害","combat/fightattr*, armor_curve, damage_rule; unit_attributes","LoadCombat/FightAttributeCatalog.Load","Aggregate→Modifiers→Resolve 乘区","DamagePipeline.Resolve","HP/死亡","--formula-checks；HitCount","RUNTIME_VERIFIED（聚合计数）/STATIC（逐技能）"),
            Row("C-06","技能动作","skill/*, talent.active 绑定","SkillConfiguration.Load / TalentConfiguration","AI 合法性→Windup→Commit→Effects","SkillRuntime.Commit/DrainEffects","伤害/治疗/Buff/召唤","--skill-bd-*-checks","STATIC_VERIFIED"),
            Row("C-07","Buff 与触发","skill/buff.csv, trigger.csv(空), proc_*.csv(空)","SkillConfiguration.Load","属性聚合 + 周期 DoT + 触发反应","ApplyBuff/React","BattleAttributes/DoT","--skill-bd-proc-checks（含空表）","STATIC_VERIFIED"),
            Row("C-08","召唤","skill/summon.csv","SkillConfiguration.Load","cap/ratio/duration","SummonRuntime","Summons、接敌名额","--skill-bd-summon-checks","STATIC_VERIFIED"),
            Row("C-09","天赋树","skill/talent.csv, system/config 规则","TalentConfiguration.Load","点数/前置/树投入 + 被动属性","LearnTalent/SetTalentLoadout/HeroTalentAttribute","TalentBonuses、主动槽","--talent-checks","RUNTIME_VERIFIED"),
            Row("C-10","星图属性","star.csv(attack/hp)","LoadStars","Totals 累加 + 差量应用","ApplyStarTotals","全队 Attack/MaxHp","默认全量检查(StarChecks)","RUNTIME_VERIFIED"),
            Row("C-11","星图席位","star.csv(party_slots)","LoadStars","1+Σ等级，上限 5，且不低于 floor","FormationCapacity/ValidateFormationCapacity","编队上限","默认全量检查(StarChecks)；--simulation E 扩编","RUNTIME_VERIFIED"),
            Row("C-12","星图技能槽","star.csv(skill_slots) + system/config 规则","LoadStars","min(max_slots, initial+Σ)","ActiveSkillSlots/ValidateTalentCapacity","装配槽位","--talent-checks","RUNTIME_VERIFIED"),
            Row("C-13","星图洗点","star.csv(talent_reset, talent_reset_discount)","LoadStars","解锁=等级≥1；折扣总和≤100%","CanResetTalents/TalentResetCost","洗点可用性与价格","--talent-checks","RUNTIME_VERIFIED"),
            Row("C-14","地牢规模","dungeon_rule, poi_profile.floors","Config.Load / LoadPoiConfiguration","楼层=profile.floors 或随机；房间数随机","Dungeon.Generate","Floors/Rooms","--dungeon-travel-checks","RUNTIME_VERIFIED"),
            Row("C-15","房间刷怪","spawn_rule.count_*, region_pool.weight, dungeon/poi_challenge/floor_plan.MonsterLevel","LoadSpawnRule/LoadMonsterPools/LoadFloorPlans","等级权威优先级 + 加权抽身份 + Boss 占位","GameSession.Reveal","房间怪物与等级","events.csv state/kill；FloorReports","RUNTIME_VERIFIED"),
            Row("C-16","精英","monster/elite.csv","LoadMonsterContent","出现率→权重→目标","TryApplyElite","目标策略/Buff","--act-content-checks","STATIC_VERIFIED"),
            Row("C-17","Boss","monster/boss_skill.csv, poi_profile.boss_name","LoadMonsterContent/LoadPoiConfiguration","按 poi 附技能包与名字","ApplyMonsterContent/Reveal","Boss 行为","--act-content-checks","STATIC_VERIFIED"),
            Row("C-18","房间事件","dungeon_rule.event_chance","Config.Load","非 Boss 房掷选，营火/宝箱 50/50","TickDungeon","HP 或经验","--simulation room_event（event_ticks）","RUNTIME_VERIFIED"),
            Row("C-19","失败恢复","world/recovery.failure_threshold","LoadRecovery","阈值计数→暂停→选同级副本","FailureRecovery.TryRecovery","目标与重试","--simulation C/D retries","RUNTIME_VERIFIED"),
            Row("C-20","城堡请求","castle_region, areapart, region_prerequisite, poi_challenge","LoadWorld/LoadAreaParts/LoadPoiConfiguration","解锁→请求排队→完成当前地牢→返村→开战","WorldCampaign.RequestCastle/CompleteDungeon","章节推进","events.csv castle_*；--simulation","RUNTIME_VERIFIED"),
            Row("C-21","传输/传送","waypoint.csv, portal_rule.csv, town_scene","LoadWaypoints/LoadSceneConfig","使用距离→阶段计时","WorldCampaign.BeginPortal/Tick","阶段与位置","--town-layout-checks","RUNTIME_VERIFIED"),
            Row("C-22","自动保存","system/client.csv","LoadClientConfig","autosave_min_seconds 计时","ClientSettings/SaveStore","存档文件","默认全量检查(SaveChecks)","RUNTIME_VERIFIED（checkpoint_seconds 无消费者）"),
            Row("C-23","模拟隔离","tests/Simulation/**","--simulation 入口","GM/策略开关分离；不写玩家存档","SimulationRunner","artifacts/simulation","M3 报告","RUNTIME_VERIFIED")
        };
    }

    // ---------- 扩容接口 ----------

    private static List<string> ExpansionRows()
    {
        string Row(string system, string capability, string candidate, string cls, string schema, string consumer, string touch, string test, string missing, string decision)
            => string.Join(',', Q(system), Q(capability), Q(candidate), Q(cls), Q(schema), Q(consumer), Q(touch), Q(test), Q(missing), Q(decision));
        return new List<string>
        {
            Row("天赋/技能","61 节点、23 列 schema、行列/前置/等级/点数校验齐全","增节点、连线、被动属性、主动技能绑定（skill_id）",
                "DATA_ONLY（绑定现有 skill）/ 新被动 Key 需补属性注册与消费（REQUIRES_EXISTING_SYSTEM_EXTENSION）","talent.csv 支持任意节点数与 skill_id","被动走天赋值；主动需已存在 skill 或旧 effect 值",
                "无（纯数据）；若新增 effect 值需 Talents.CastTalent 分支","--talent-checks 可覆盖学习/前置/洗点","新 effect 值的运行时语义","P2-3 先补节点，不改 Core"),
            Row("属性/Effect","36 Key（18 已投放 / 18 零投放但有消费分支）、10 Effect kind、5 伤害桶、聚合规则表","元素/技能/职业增伤与通用及元素易伤等 18 Key 零投放，消费者已就绪（物理易伤已投放）",
                "DATA_ONLY（现有 Key，仅需 skill/buff/effect 配置引用）","buff.attributes 只接受已注册 Key","SkillRuntime/DamagePipeline 已有分支",
                "无（用现有 Key）；新增 Key 需改 fightattr/聚合/消费","--formula-checks + 模拟","新增 Key 的最小改动面","P2-3 优先用现有 Key 做内容"),
            Row("属性/Effect（新增种类）","Effect kind 为代码分支而非枚举表","新效果类型（如吸血、反弹）","REQUIRES_EXISTING_SYSTEM_EXTENSION",
                "skill/effect.csv 的 kind 由 Core 分支消费","需新 Consumer","SkillRuntime.DrainEffects 增加分支 + 校验 + 快照字段","--skill-bd-*-checks","新 kind 的状态与可观测性","P2-3 若需要须单独立项"),
            Row("星图（属性）","17 节点、逐级价格、任一前置、差量应用","增加攻击/生命节点与等级","DATA_ONLY","star.csv 仅接受 attack/hp + flat","ApplyStarTotals","无","默认全量检查(StarChecks)","—","可直接扩充"),
            Row("星图（现有功能节点）","party_slots/skill_slots/talent_reset/talent_reset_discount 各有消费者","增加同类节点（如第二个技能槽节点）",
                "DATA_ONLY（受校验约束）","party_slots/skill_slots/talent_reset 必须单级+1；折扣总和≤100%","FormationCapacity/ActiveSkillSlots/ResetTalents","无（但校验会拒绝违规）","默认全量检查(StarChecks)","扩展上限被 formula 硬编码","可直接扩充但受上限约束"),
            Row("星图（全新功能）","节点效果类型是白名单（6 种）","新功能解锁（如第二编队、装备槽、自动售卖升级）","REQUIRES_EXISTING_SYSTEM_EXTENSION",
                "新增 attr_id 需同时改 StarConfiguration 校验 + 消费者","需新 Consumer","StarConfiguration:33/35 白名单 + 新消费点 + 存档字段","默认全量检查(StarChecks)","新功能的存档与回放","P2-3 需逐项评估"),
            Row("地牢事件","仅宝箱/营火两种硬编码；event_chance 全局","事件表（类型/条件/奖励/风险）","REQUIRES_EXISTING_SYSTEM_EXTENSION",
                "无事件表 schema","需新 Consumer","GameSession.Reveal 抽选 + TickDungeon 执行 + 新校验","--simulation room_event","事件与房间/怪物的条件表达","P2-3 建议立项"),
            Row("怪物身份","49 身份；身份与行为分离；加权池","新增身份/组合（同区域不同权重）","DATA_ONLY","act_monsters + region_pool 即可","PickSpawnMonster","无","--act-monster-checks","—","可直接扩充"),
            Row("怪物行为","3 类行为分支（area/hunter/support）+ 默认","新 AI 行为（逃跑、召唤援军、阶段转换）","REQUIRES_EXISTING_SYSTEM_EXTENSION",
                "act_behaviors.policy 为枚举白名单","需新 Consumer","MonsterContent.ParseMonsterPolicy + CombatTargeting/GameSession 分支","--act-content-checks","新行为的状态机与观测","P2-3 排期区分"),
            Row("精英/Boss","10 词缀、34 Boss 技能包、每房最多一个精英","更多词缀与 Boss 技能组合、Boss 阶段","DATA_ONLY（词缀/技能包）/ NEEDS_M2（阶段）",
                "elite 同难度出现率一致；boss_skill 按 poi","TryApplyElite/ApplyMonsterContent","阶段脚本需新 Core","--act-content-checks","Boss 阶段状态","先补数据，阶段另议"),
            Row("关卡/POI","231 地牢、1515 楼层预算、415 区域","新增 POI/区域/挑战档","DATA_ONLY",
                "dungeon/poi_profile/poi_challenge/floor_plan 四表联动","Reveal/ExpeditionPlanner","无","--dungeon-travel-checks","—","可直接扩充"),
            Row("楼层预算","Planned*/ActiveCellTarget 只进报表","让预算驱动真实生成","REQUIRES_EXISTING_SYSTEM_EXTENSION",
                "字段齐备但无消费者","需新 Consumer","Dungeon.Generate 接预算 + 校验","--dungeon-travel-checks","预算→拓扑映射","P2-3 决定是否接入"),
            Row("经济/物品","4 物品、2 类型、自动售卖、星图出口","增加物品与掉落","DATA_ONLY","item/itemtype/monster_drop","RollDrops","无","--simulation ledger","—","可直接扩充"),
            Row("装备/商店","无表、无消费者","装备、商店、强化","OUT_OF_SCOPE_NEW_SYSTEM","—","—","—","—","—","P2-3 明确不做"),
            Row("章节","恰好 5 章 × 3 难度（加载器硬约束）","新增第 6 章","REQUIRES_EXISTING_SYSTEM_EXTENSION",
                "chapters/actN + world/chapter.csv","LoadChapters 校验 5 章","章数/难度枚举与存档兼容","--campaign-package-checks","跨章节存档","P2-3 不做新章")
        };
    }

    private static void Write(string dir, string name, string header, IEnumerable<string> rows)
    {
        var all = new List<string> { header };
        all.AddRange(rows);
        File.WriteAllLines(Path.Combine(dir, name), all, Utf8);
    }

    // ---------- 列规格（人工核对） ----------
    private static readonly Dictionary<string, string> Specs = new(StringComparer.Ordinal)
    {
        // 章节通用表
        ["dungeon/dungeon.csv"] = @"
id|text|-|地牢唯一标识（A01-D01）|主键|WorldCampaign/ExpeditionPlanner|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
name|text|-|地牢展示名|展示|HUD|-|-|加载时|-|DISPLAY_ONLY|LOADER_VERIFIED||
region_id|int|-|所属区域 ID，必须存在且可进入|外键|ValidateWorld|-|区域必须存在|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
kind|text|enum:normal/castle|地牢类型；castle 仅玩家手动请求|枚举|ValidateWorld/RequestCastle|-|仅 normal/castle|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--campaign-package-checks|
level_min|int|级|怪物等级下限（无 POI 挑战档时权威）|业务数值|Reveal|-|≥1 且 ≤level_max|进房时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--simulation|被 poi_challenge/floor_plan 覆盖
level_max|int|级|怪物等级上限（同上）|业务数值|Reveal|-|≥level_min|进房时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--simulation|同上
x|float|U|地牢在区域格内的归一化 X（0..1）|业务数值|WorldNavigation|-|0<x<1|旅行时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
y|float|U|同上 Y|业务数值|WorldNavigation|-|0<y<1|旅行时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["dungeon/difficulty_bands.csv"] = @"
id|int|-|行号（1..15）|主键|FloorPlanning|-|15 行且 (难度,章节) 唯一|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
DifficultyId|text|enum|难度名（NORMAL/NIGHTMARE/HELL）|枚举|DifficultyBands|-|与 world/difficulty 对齐|加载时|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
ChapterId|text|enum|章节 ID（ACT01..）|枚举|DifficultyBands|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
MonsterLevelMin|int|级|该难度章节的怪物等级下限|规划|无直接消费者|-|≥1|—|-|LOADED_ONLY|LOADER_VERIFIED||不参与 Reveal 等级计算
MonsterLevelMax|int|级|上限|规划|无直接消费者|-|≥Min|—|-|LOADED_ONLY|LOADER_VERIFIED||同上
RecommendedPlayerLevelMin|int|级|推荐玩家等级下限|规划|无直接消费者|-|≥1|—|-|LOADED_ONLY|LOADER_VERIFIED||
RecommendedPlayerLevelMax|int|级|推荐玩家等级上限|规划|无直接消费者|-|≥Min|—|-|LOADED_ONLY|LOADER_VERIFIED||
Status|text|-|人读备注（已确认范围/数值可调）|展示|无|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
",
        ["dungeon/poi_challenge.csv"] = @"
id|text|-|挑战档 ID（POI_难度序号）|主键|LoadPoiConfiguration|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
poi_id|text|-|所属 POI|外键|LoadPoiConfiguration|-|必须存在|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
difficulty|int|索引|难度索引 0/1/2|枚举|Config.Challenge|-|-|查询时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--simulation|
challenge|int|挑战等级|该 POI 在某难度下的挑战等级；决定刷怪等级与失败计数门槛|业务数值|Reveal/RecordFailure|-|≥1|进房/失败时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--simulation 城堡/失败|
recommended|int|级|推荐玩家等级|规划|无直接消费者（HUD 可读）|-|≥1|—|-|LOADED_ONLY|LOADER_VERIFIED||
",
        ["dungeon/poi_profile.csv"] = @"
id|text|-|POI ID|主键|LoadPoiConfiguration|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
optional|bool|0/1|是否支线（不影响主通）|开关|ExpeditionPlanner|-|0/1|规划时|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
required_for_castle|bool|0/1|是否为城堡解锁必需|开关|AreaParts/RegionPrerequisites|-|0/1|解锁判定|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
floors|int|层|该 POI 的楼层数；有档案时覆盖随机楼层|业务数值|Dungeon.Generate(floorCount)|-|1..20|进入地牢|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--dungeon-travel-checks|
auto_order|int|-|自动远征排序值|业务数值|ExpeditionPlanner|-|-|选目标时|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
boss_name|text|-|Boss 展示名（附加“（原型）”）|展示|Reveal|-|-|进房时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
interior|text|-|内部场景标记|展示|无|-|-|—|-|LOADED_ONLY|LOADER_VERIFIED||当前为空
status|text|-|人读状态（prototype）|展示|无|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
",
        ["dungeon/floor_plan.csv"] = @"
id|text|-|楼层配置 ID（POI_难度_层）|主键|LoadFloorPlans|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
ChapterId|text|enum|章节|枚举|LoadFloorPlans|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
DifficultyId|text|enum|难度名|枚举|LoadFloorPlans|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
PoiId|text|-|所属 POI|外键|LoadFloorPlans/Reveal|-|POI 必须存在|加载时|-|ACTIVE_CONSUMED|LOADER_VERIFIED|
FloorIndex|int|层|层序号（1 起），必须连续补齐 Floors|主键|LoadFloorPlans|-|与 poi.floors 完全一致|加载时|-|ACTIVE_CONSUMED|LOADER_VERIFIED|
FloorCount|int|层|该层所在 POI 的总层数（校验用）|规划|LoadFloorPlans|-|= profile.floors|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
FloorDisplayName|text|-|层展示名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
LayoutRows|int|格|规划布局行数|规划|无直接消费者|-|>0|—|-|PLANNED_ONLY|LOADER_VERIFIED||生成器不使用
LayoutCols|int|格|规划布局列数|规划|无直接消费者|-|>0|—|-|PLANNED_ONLY|LOADER_VERIFIED||
ActiveCellTarget|int|格|规划活跃格数（报表口径）|规划|FloorReports|-|>0|—|-|PLANNED_ONLY|STATIC_VERIFIED||与 Rooms.Count 并列，不驱动生成
ScaleTier|text|enum|规模档（XS..）|规划|无|-|-|—|-|PLANNED_ONLY|LOADER_VERIFIED||
EntryCellCount|int|格|规划入口模块数|规划|无|-|≥0|—|-|PLANNED_ONLY|LOADER_VERIFIED||
CorridorCellCount|int|格|规划走廊模块数|规划|无|-|≥0|—|-|PLANNED_ONLY|LOADER_VERIFIED||
CombatCellCount|int|格|规划战斗模块数|规划|无|-|≥0|—|-|PLANNED_ONLY|LOADER_VERIFIED||
EliteCellCount|int|格|规划精英模块数|规划|无|-|≥0|—|-|PLANNED_ONLY|LOADER_VERIFIED||
EventCellCount|int|格|规划事件模块数|规划|无|-|≥0|—|-|PLANNED_ONLY|LOADER_VERIFIED||
ExitCellCount|int|格|规划出口模块数|规划|无|-|≥0|—|-|PLANNED_ONLY|LOADER_VERIFIED||
BossArenaCellCount|int|格|规划 Boss  Arena 格数|规划|无|-|≥0|—|-|PLANNED_ONLY|LOADER_VERIFIED||
MonsterLevel|int|级|该层怪物等级（**实际生效**，覆盖 POI 挑战等级）|业务数值|Reveal|-|≥1|进房时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--simulation|等级权威链最高优先
PoiChallengeLevel|int|级|规划的 POI 挑战等级（与 poi_challenge 冗余）|规划|无|-|≥1|—|-|LOADED_ONLY|LOADER_VERIFIED||不一致时以 poi_challenge 为准
RecommendedPlayerLevel|int|级|推荐玩家等级|规划|无|-|≥1|—|-|LOADED_ONLY|LOADER_VERIFIED||
CombatMonstersPerCellMin|int|只/格|每战斗格怪物下限|规划|无|-|≥0|—|-|PLANNED_ONLY|LOADER_VERIFIED||
CombatMonstersPerCellTarget|int|只/格|目标值|规划|无|-|≥Min|—|-|PLANNED_ONLY|LOADER_VERIFIED||
CombatMonstersPerCellMax|int|只/格|上限|规划|无|-|≥Target|—|-|PLANNED_ONLY|LOADER_VERIFIED||
ElitePerCell|int|只/格|每格精英数|规划|无|-|≥0|—|-|PLANNED_ONLY|LOADER_VERIFIED||
EliteEscortPerCellMin|int|只|精英护卫下限|规划|无|-|≥0|—|-|PLANNED_ONLY|LOADER_VERIFIED||
EliteEscortPerCellTarget|int|只|精英护卫目标|规划|无|-|≥Min|—|-|PLANNED_ONLY|LOADER_VERIFIED||
EliteEscortPerCellMax|int|只|精英护卫上限|规划|无|-|≥Target|—|-|PLANNED_ONLY|LOADER_VERIFIED||
BossActorCount|int|只|Boss 数量（0/1）；1 时占总数最后一格|业务数值|Reveal(plan.BossActors)|-|0/1|进房时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
BossEscortMin|int|只|Boss 护卫下限|规划|无|-|≥0|—|-|PLANNED_ONLY|LOADER_VERIFIED||
BossEscortTarget|int|只|Boss 护卫目标|规划|无|-|≥Min|—|-|PLANNED_ONLY|LOADER_VERIFIED||
BossEscortMax|int|只|Boss 护卫上限|规划|无|-|≥Target|—|-|PLANNED_ONLY|LOADER_VERIFIED||
FixedUniqueActorCount|int|只|固定唯一单位数；参与 PlannedKill 预算校验，不参与生成|规划|LoadFloorPlans(OptionalN)+KillBudget 校验|-|≥0|加载校验|-|VALIDATION_ONLY|STATIC_VERIFIED||仅 act4/act5 存在
ScriptedWaveCount|int|波|脚本化波次数；参与预算校验，不参与生成|规划|LoadFloorPlans(OptionalN)+KillBudget 校验|-|≥0|加载校验|-|VALIDATION_ONLY|STATIC_VERIFIED||仅 act5 存在
ScriptedWaveMonsterTarget|int|只|脚本波次怪物数；参与预算校验|规划|LoadFloorPlans(OptionalN)+KillBudget 校验|-|≥0|加载校验|-|VALIDATION_ONLY|STATIC_VERIFIED||仅 act5 存在
StageMode|text|enum|阶段模式（默认 DUNGEON_FLOOR）；只写入 FloorPlan，无运行时消费者|规划|LoadFloorPlans(OptionalN)|DUNGEON_FLOOR|-|加载校验|-|LOADED_ONLY|STATIC_VERIFIED||仅 act5 存在；无消费方
FloorMonsterLevel|int|级|另一套“层怪物等级”字段；**FloorPlanning 未读取**，实际等级来自 MonsterLevel|规划|无|-|-|—|-|NO_CONSUMER|STATIC_VERIFIED||act2+ 新增，命名与 MonsterLevel 冲突
EliteGroupMonsterTarget|int|只|精英组怪物数；未被读取|规划|无|-|≥0|—|-|NO_CONSUMER|STATIC_VERIFIED||act2+ 存在
SpawnProfileId|text|-|刷怪档 ID；未被读取|规划|无|-|-|—|-|NO_CONSUMER|STATIC_VERIFIED||act3+ 存在
SealActivationCount|int|个|封印激活数；未被读取|规划|无|-|≥0|—|-|NO_CONSUMER|STATIC_VERIFIED||act4/act5 存在
ContentStatus|text|-|人读内容状态|展示|无|-|-|—|-|DISPLAY_ONLY|STATIC_VERIFIED||act2+ 存在
PlannedKillMin|int|只|规划击杀下限（报表口径）|规划|FloorReports(plan.TargetKills 用 Target)|-|≥0|—|-|PLANNED_ONLY|STATIC_VERIFIED||与实际 ActualSpawned 并列
PlannedKillTarget|int|只|规划击杀目标（FloorReports 使用）|规划|FloorReports|-|≥Min|—|-|PLANNED_ONLY|STATIC_VERIFIED||
PlannedKillMax|int|只|规划击杀上限|规划|无|-|≥Target|—|-|PLANNED_ONLY|LOADER_VERIFIED||
CompletionCondition|text|enum|完成条件（ALL_REQUIRED_ENEMIES_DEFEATED）|规划|无|-|-|—|-|PLANNED_ONLY|LOADER_VERIFIED||当前运行恒为清房
RequiredCombatFullCoverage|text|-|是否要求全清|规划|无|-|-|—|-|PLANNED_ONLY|LOADER_VERIFIED||
FirstClearExplorationCoverageAssumption|text|-|首通探索覆盖率假设|规划|无|-|-|—|-|PLANNED_ONLY|LOADER_VERIFIED||
SourceSeedPolicy|text|enum|种子策略（DETERMINISTIC_PENDING_RUNTIME_AUDIT）|规划|无|-|-|—|-|PLANNED_ONLY|LOADER_VERIFIED||
ConfigStatus|text|-|配置状态（PROPOSED_NOT_BALANCED）|展示|无|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
",
        ["monster/region_pool.csv"] = @"
areapart_id|text|-|统治区 ID（apN）|外键|LoadMonsterPools|-|必须存在于 areapart|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
monster_id|text|-|怪物身份 ID|外键|PickSpawnMonster|-|必须存在于 act_monsters|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
weight|int|权重|组内权重，每组合计必须 100|业务数值|PickSpawnMonster|-|1..1000，组和=100|抽签时|按权重累计|ACTIVE_CONSUMED|STATIC_VERIFIED|--act-monster-checks|
",
        ["village/village.csv"] = @"
id|text|-|村庄 ID|主键|ValidateWorld|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
name|text|-|展示名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
region_id|int|-|所在区域|外键|ValidateWorld|-|区域必须存在且可进入|加载时|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
x|float|U|区域内归一化 X|业务数值|WorldNavigation|-|0<x<1|旅行时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
y|float|U|归一化 Y|业务数值|WorldNavigation|-|0<y<1|旅行时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
npc_id|text|-|关联村庄 NPC|外键|ValidateWorld|-|必须存在|加载时|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
",
        ["world/areapart.csv"] = @"
id|text|-|统治区 ID|主键|LoadAreaParts|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
castle_id|text|-|该统治区城堡地牢|外键|AreaPartUnlocked|-|必须是 castle 地牢|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--campaign-package-checks|
initially_unlocked|bool|0/1|是否初始解锁|开关|AreaPartUnlocked|-|0/1|解锁判定|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
unlock_mode|text|enum:any/all|前置满足方式（任一/全部）|枚举|AreaPartUnlocked|-|any/all|解锁判定|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["world/areapart_prerequisite.csv"] = @"
areapart_id|text|-|目标统治区|外键|LoadAreaParts|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
required_areapart_id|text|-|前置统治区|外键|LoadAreaParts|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
",
        ["world/area_style.csv"] = @"
id|text|-|城堡 ID（作为样式键）|主键|AreaStyles|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
region_index|int|-|区域序号|展示|渲染|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
name|text|-|地形名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
color|int|RGB|区域底色整数|展示|渲染|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
",
        ["world/castle_region.csv"] = @"
region_id|int|-|区域 ID|外键|ValidateWorld|-|区域必须存在|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
castle_id|text|-|统治该区域的城堡|外键|Rulers/AreaPart|-|必须是 castle 地牢|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--campaign-package-checks|
",
        ["world/gate.csv"] = @"
id|text|-|门 ID|主键|LoadPoiConfiguration|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
from_region|int|-|起点区域|外键|WorldNavigation|-|必须存在|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
to_region|int|-|终点区域|外键|WorldNavigation|-|必须存在|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
kind|text|enum|门类型（ROAD/SIDE_*/…），无 kind 列时回退 ROAD|枚举|WorldNavigation|-|任意字符串|加载时|-|ACTIVE_CONSUMED|LOADER_VERIFIED||仅影响表现与寻路权重
",
        ["world/map_policy.csv"] = @"
id|text|-|策略键|主键|LoadPoiConfiguration|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
value|text|-|策略值|业务数值|LoadPoiConfiguration|-|-|加载时|-|LOADED_ONLY|LOADER_VERIFIED||当前仅 4 行，未发现读取点
",
        ["world/obstacle.csv"] = @"
id|text|-|障碍 ID|主键|LoadSceneConfig|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
kind|text|enum:water/rock|障碍类型|枚举|WorldNavigation|-|water/rock|寻路时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
x|float|U|左边界|业务数值|WorldNavigation|-|≥0 且 x+w≤世界宽|寻路时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
y|float|U|上边界|业务数值|WorldNavigation|-|≥0 且 y+h≤世界高|寻路时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
width|float|U|宽|业务数值|WorldNavigation|-|>0|寻路时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
height|float|U|高|业务数值|WorldNavigation|-|>0|寻路时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["world/region.csv"] = @"
id|int|-|区域 ID|主键|ValidateWorld|-|唯一|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
x|int|格|世界格 X|业务数值|WorldNavigation|-|0..width-1|寻路时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
y|int|格|世界格 Y|业务数值|WorldNavigation|-|0..height-1|寻路时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
name|text|-|区域名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
terrain|text|enum|地形（grass/hell/…），mountain/ocean 不可进入|枚举|ValidateWorld|-|8 种之一|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
enterable|bool|0/1|是否可进入|开关|ValidateWorld|-|不可进入不得配村庄/地牢/统治|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["world/region_prerequisite.csv"] = @"
id|text|-|前置记录 ID|主键|LoadRegionPrerequisites|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
region_id|int|-|目标区域|外键|RegionPrerequisites|-|必须存在|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
required_dungeon_id|text|-|解锁该区域所需地牢（通关）|外键|RegionPrerequisites|-|必须存在|解锁判定|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--campaign-package-checks|
",
        ["world/waypoint.csv"] = @"
id|text|-|传送点 ID|主键|LoadWaypoints|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
castle_id|text|-|关联城堡|外键|Waypoints|-|必须存在|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
region_id|int|-|所在区域|外键|Waypoints|-|必须存在|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
x|float|U|区域内归一化 X|业务数值|Waypoints|-|0..1|传送时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
y|float|U|归一化 Y|业务数值|Waypoints|-|0..1|传送时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["world/world.csv"] = @"
width|int|格|世界宽（章节为 8）|业务数值|WorldNavigation|-|>0 且 ≤64|寻路时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||根目录旧表为 16
height|int|格|世界高|业务数值|WorldNavigation|-|>0 且 ≤64|寻路时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
start_region_id|int|-|出生区域|业务数值|CreateWorld|-|必须存在且可进入|建图时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
travel_speed|float|格/秒|世界旅行速度（章节 0.31，旧表 0.65）|业务数值|WorldCampaign.Tick|-|>0|旅行时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--simulation|误读根目录会得到错误节奏
village_speed|float|格/秒|村内移动速度|业务数值|WorldCampaign.Tick|-|>0|村内|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
trade_seconds|float|s|交易耗时|业务数值|WorldCampaign.Tick|-|>0|交易时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--simulation village_trade|
trade_range|float|U|交易交互距离|业务数值|WorldCampaign.Tick|-|>0|交易时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        // 共享表
        ["hero/heroes.csv"] = @"
id|text|-|职业 ID|主键|HeroCreation|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
name|text|-|职业名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
hp|float|点|1 级基础生命|业务数值|Actor.MaxHp|-|>0|建号|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--hero-checks|
attack|float|点|1 级基础攻击|业务数值|Actor.Attack|-|>0|建号|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
defense|float|点|防御（=armor 基础）|业务数值|DamagePipeline|-|≥0|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
speed|float|U/秒|移动速度|业务数值|Move|-|>0|每 Tick|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
range|float|U|攻击距离|业务数值|CombatTargeting|-|>0|索敌|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
cooldown|float|s|普攻冷却|业务数值|GameSession|-|>0|普攻|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["monster/monsters.csv"] = @"
id|text|-|怪物 ID|主键|Spawn|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
name|text|-|展示名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
hp|float|点|1 级生命|业务数值|Actor.MaxHp|-|>0|生成|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
attack|float|点|1 级攻击|业务数值|Actor.Attack|-|>0|生成|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
defense|float|点|防御|业务数值|DamagePipeline|-|≥0|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
speed|float|U/秒|移动速度|业务数值|Move|-|>0|每 Tick|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
range|float|U|攻击距离|业务数值|CombatTargeting|-|>0|索敌|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
cooldown|float|s|攻击冷却|业务数值|GameSession|-|>0|攻击|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["hero/herolevel.csv"] = @"
id|int|级|等级|主键|LoadLevels|-|1..N 连续|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
exp|int|点|升到下一级所需经验|业务数值|GrantHeroExperience|-|递增|升级|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--hero-experience-checks|
attack_bonus|float|点|该等级累计攻击增量|业务数值|GrantHeroExperience|-|-|升级|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
hp_bonus|float|点|该等级累计生命增量|业务数值|GrantHeroExperience|-|-|升级|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["monster/monsterlevel.csv"] = @"
id|int|级|等级|主键|LoadLevels|-|1..N 连续|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
hp_multiplier|float|倍率|生命倍率|业务数值|SpawnRule.Create|-|>0|生成|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
attack_multiplier|float|倍率|攻击倍率|业务数值|SpawnRule.Create|-|>0|生成|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["hero/experience_level_rate.csv"] = @"
id|int|级差|等级差（英雄-怪物，-N..+N）|主键|LoadExperienceRates|-|连续、含 0|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
percent|int|%|经验倍率（100=全额）|业务数值|MonsterExperiencePercent|-|0..10000，零差=100，单调不增|经验结算|查表|ACTIVE_CONSUMED|STATIC_VERIFIED|--hero-experience-checks|
",
        ["hero/hero_unlocks.csv"] = @"
id|text|-|职业 ID|主键|LoadHeroUnlocks|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
kills|int|次|解锁所需累计击杀|业务数值|HeroUnlocked|-|≥0|解锁判定|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--hero-checks|
",
        ["monster/spawn_rule.csv"] = @"
count_min|int|只|每房怪物数下限|业务数值|Reveal|-|≥1|进房|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--simulation|
count_max|int|只|上限|业务数值|Reveal|-|≥min|进房|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
level|int|级|等级偏移（-1 表示按地牢等级）|业务数值|Reveal|-|-|进房|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
boss_hp|float|倍率|Boss 生命倍率|业务数值|Create|-|>0|生成 Boss|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
boss_attack|float|倍率|Boss 攻击倍率|业务数值|Create|-|>0|生成 Boss|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
xp_base|int|点|基础经验|业务数值|RewardXp|-|≥0|生成|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--simulation kill.reward_xp|
boss_reward|int|点|Boss 额外经验|业务数值|RewardXp|-|≥0|生成 Boss|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
drop_chance_multiplier|float|倍率|掉落概率倍率|业务数值|RollDrops|-|>0|掉落|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--simulation ledger|
value_per_level|float|点/级|每级经验增量系数|业务数值|RewardXp|-|≥0|生成|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["monster/monster_drop.csv"] = @"
monster_id|text|-|怪物 ID|外键|RollDrops|-|存在|掉落|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
item_id|text|-|物品 ID|外键|RollDrops|-|存在|掉落|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
chance|float|0..1|掉落概率（×drop_chance_multiplier）|业务数值|RollDrops|-|0..1|掉落|概率|ACTIVE_CONSUMED|STATIC_VERIFIED|--simulation ledger|
quantity_min|int|个|数量下限|业务数值|RollDrops|-|≥1|掉落|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
quantity_max|int|个|数量上限|业务数值|RollDrops|-|≥min|掉落|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["monster/act_monsters.csv"] = @"
id|text|-|身份 ID|主键|PickSpawnMonster|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
name|text|-|展示名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
hp|float|点|基础生命|业务数值|Actor.MaxHp|-|>0|生成|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
attack|float|点|基础攻击|业务数值|Actor.Attack|-|>0|生成|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
defense|float|点|防御|业务数值|DamagePipeline|-|≥0|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
speed|float|U/秒|速度|业务数值|Move|-|>0|每 Tick|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
range|float|U|攻击距离|业务数值|CombatTargeting|-|>0|索敌|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
cooldown|float|s|攻击冷却|业务数值|GameSession|-|>0|攻击|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["monster/act_behaviors.csv"] = @"
monster_id|text|-|怪物身份|主键|LoadMonsterContent|-|存在|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--act-content-checks|
policy|text|enum|目标策略（Ordinary/BacklinePriority/Hunter/Support）|枚举|ApplyMonsterContent|-|枚举值|生成时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
skill_id|text|-|追加技能|外键|ApplyMonsterContent|-|可空，须存在|生成时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["monster/elite.csv"] = @"
difficulty|text|enum|难度名|枚举|TryApplyElite|-|同难度出现率一致|进房|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
affix_id|text|-|词缀 ID|主键|TryApplyElite|-|-|进房|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
buff_id|text|-|施加的 Buff|外键|TryApplyElite|-|可空，须存在|进房|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
policy|text|enum|覆盖后的目标策略|枚举|TryApplyElite|-|可空|进房|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
weight|int|权重|同难度内权重|业务数值|TryApplyElite|-|≥1|进房|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
chance_pct|int|%|每房出现率|业务数值|TryApplyElite|-|0..100|进房|概率|ACTIVE_CONSUMED|STATIC_VERIFIED|--act-content-checks|
",
        ["monster/boss_skill.csv"] = @"
poi_id|text|-|POI ID|主键|ApplyMonsterContent|-|存在|生成 Boss|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
skill_id|text|-|Boss 技能|外键|ApplyMonsterContent|-|必须存在|生成 Boss|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["combat/fightattr.csv"] = @"
attr_id|text|-|属性 Key|主键|FightAttributeCatalog.Load|-|36 个，且必须含 30 个必备项|加载时|-|VALIDATION_ONLY|STATIC_VERIFIED|--formula-checks|
name|text|-|展示名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
category|text|enum|分类（base/event/tempo/general_damage/skill_damage/power/vulnerable/defense）|枚举|ValidateContribution|-|枚举|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
value_type|text|enum:number/ratio|数值类型|枚举|Aggregate|-|-|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
aggregation|text|enum|聚合方式（base_flat_pct/add/add_clamp/multiply）|枚举|Aggregate|-|与 allowed_ops 匹配|聚合|见公式|ACTIVE_CONSUMED|STATIC_VERIFIED||
allowed_ops|text|enum 列表|允许操作（base/flat/pct/add/multiply）|枚举|ValidateContribution|-|去重且匹配聚合|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
default_value|float|-|默认值，必须等于 rules.baseline|业务数值|ValidateValue|-|-|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
display_format|text|enum:number/integer/percentage|展示格式|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
ui_visible|bool|0/1|是否在 UI 展示|开关|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
",
        ["combat/fightattr_rules.csv"] = @"
id|text|-|属性 Key|外键|Load|-|必须与 fightattr 一一对应|加载时|-|VALIDATION_ONLY|STATIC_VERIFIED||
baseline|float|-|基线（= default_value）|业务数值|Aggregate|-|在 min..max 内|聚合|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
min_value|float|-|下限（可空）|业务数值|Aggregate|-|≤baseline|聚合|Clamp|ACTIVE_CONSUMED|STATIC_VERIFIED||
max_value|float|-|上限（可空）|业务数值|Aggregate|-|≥baseline|聚合|Clamp|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["combat/unit_attributes.csv"] = @"
id|text|-|单位 ID（职业/怪物/召唤）|主键|LoadCombat|-|唯一|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
side|text|enum:hero/monster|阵营|枚举|ApplyCombatProfile|-|hero/monster|加载时|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
dodge_rate|float|0..1|闪避率|业务数值|DamagePipeline|-|0..1|结算|加算|ACTIVE_CONSUMED|STATIC_VERIFIED||
crit_rate|float|0..1|暴击率|业务数值|DamagePipeline|-|0..1|结算|加算|ACTIVE_CONSUMED|STATIC_VERIFIED||
crit_damage|float|比例|暴击额外倍率|业务数值|DamagePipeline|-|≥0|结算|加算|ACTIVE_CONSUMED|STATIC_VERIFIED||
block_rate|float|0..1|格挡率|业务数值|DamagePipeline|-|0..1|结算|加算|ACTIVE_CONSUMED|STATIC_VERIFIED||
block_reduction|float|0..1|格挡减伤|业务数值|DamagePipeline|-|0..1|结算|加算|ACTIVE_CONSUMED|STATIC_VERIFIED||
ranged|bool|0/1|是否远程|开关|CombatTargeting|-|0/1|索敌|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
engagement_capacity|float|个|接敌容量|业务数值|CombatTargeting|-|≥1|索敌|加算|ACTIVE_CONSUMED|STATIC_VERIFIED||
fire_resistance|float|比例|火焰抗性（可为负）|业务数值|DamagePipeline|-|min..max|结算|加算|ACTIVE_CONSUMED|STATIC_VERIFIED||
cold_resistance|float|比例|冰霜抗性|业务数值|DamagePipeline|-|min..max|结算|加算|ACTIVE_CONSUMED|STATIC_VERIFIED||
lightning_resistance|float|比例|闪电抗性|业务数值|DamagePipeline|-|min..max|结算|加算|ACTIVE_CONSUMED|STATIC_VERIFIED||
poison_resistance|float|比例|毒素抗性|业务数值|DamagePipeline|-|min..max|结算|加算|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["combat/damage_rule.csv"] = @"
id|text|-|常量名|主键|CombatRules.Load|-|必须 3 行|加载时|-|VALIDATION_ONLY|STATIC_VERIFIED||
value|float|-|常量值|业务数值|DamagePipeline|-|暴击≥1；抵抗区间 min<0≤max<1|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--formula-checks|
",
        ["combat/armor_curve.csv"] = @"
id|int|级|等级控制点|主键|CombatRules.Load|-|从 1 起且覆盖最大等级|加载时|-|VALIDATION_ONLY|STATIC_VERIFIED||
k|float|-|护甲曲线系数|业务数值|ArmorK|-|>0 且不倒退|结算|线性插值|ACTIVE_CONSUMED|STATIC_VERIFIED|--formula-checks|
",
        ["skill/skill.csv"] = @"
id|text|-|技能 ID|主键|SkillConfiguration.Load|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
role|text|enum|角色（主动/普攻）|枚举|SkillRuntime|-|-|加载时|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
kind|text|enum:ActiveSkill/BasicAttack|技能种类|枚举|SkillRuntime|-|枚举|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
cooldown_ticks|int|tick|冷却|业务数值|SkillCooldown|-|≥0|施放后|按 tick/20|ACTIVE_CONSUMED|STATIC_VERIFIED||
windup_ticks|int|tick|前摇|业务数值|Commit|-|≥0|施放|按 tick/20|ACTIVE_CONSUMED|STATIC_VERIFIED||
recovery_ticks|int|tick|后摇|业务数值|Commit|-|≥0|施放|按 tick/20|ACTIVE_CONSUMED|STATIC_VERIFIED||
coefficient|float|倍率|伤害系数（进入 Skill 桶）|业务数值|DamagePipeline|-|≥0|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--skill-bd-*-checks|
range|float|U|施法距离|业务数值|SkillRuntime|-|≥0|选目标|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
effect_ids|text|; 列表|效果链|外键|DrainEffects|-|必须存在|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
name|text|-|展示名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
cast_sound_id|text|-|施法音效|外键|Audio|-|可空|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
hit_sound_id|text|-|命中音效|外键|Audio|-|可空|命中|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
target_policy|text|enum|目标策略|枚举|SkillRuntime|-|-|选目标|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
cast_condition|text|enum|施放条件|枚举|SkillRuntime|-|-|选目标|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["skill/effect.csv"] = @"
id|text|-|效果 ID|主键|SkillConfiguration.Load|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
role|text|enum|效果角色|枚举|DrainEffects|-|-|结算|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
kind|text|enum|10 类效果种类|枚举|DrainEffects|-|枚举|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--skill-bd-*-checks|
damage_type|text|enum|伤害类型|枚举|DamagePipeline|-|5 类|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
share|float|0..1|多分量权重|业务数值|DamagePipeline|-|0..1|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
extent|float|U|范围半径|业务数值|SettleDamage|-|≥0|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
speed|float|U/秒|投射物速度|业务数值|SkillSnapshot|-|≥0|飞行|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
impact_effect|text|-|命中后效果|外键|SpawnProjectile|-|可空|命中|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
behavior|text|enum|行为（projectile/pulse/…）|枚举|SkillRuntime|-|-|结算|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
spawn_tick|int|tick|效果触发延迟|业务数值|DrainEffects|-|≥0|结算|按 tick|ACTIVE_CONSUMED|STATIC_VERIFIED||
buff_id|text|-|关联 Buff|外键|ApplyBuff|-|可空|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
entity_kind|text|enum|实体类型（summon/…）|枚举|SummonEffect|-|-|结算|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
lifetime_ticks|int|tick|持续时长|业务数值|DrainEffects|-|≥0|结算|按 tick/20|ACTIVE_CONSUMED|STATIC_VERIFIED||
resolve_effect_ids|text|; 列表|结算时串联的效果|外键|DrainEffects|-|可空|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
mechanic_id|text|-|机制 ID（chain/summon/ability）|外键|ChainEffect/SummonEffect|-|可空且须可解析|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
damage_parts|text|; 列表|多伤害分量（type:weight）|业务数值|DamagePipeline|-|权重合计 1|结算|分桶|ACTIVE_CONSUMED|STATIC_VERIFIED||
projectile_count|int|个|齐射数|业务数值|SpawnProjectile|-|≥1|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
spread_degrees|float|度|扇形角|业务数值|SpawnProjectile|-|≥0|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["skill/buff.csv"] = @"
id|text|-|Buff ID|主键|SkillConfiguration.Load|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
duration_ticks|int|tick|持续时长|业务数值|ApplyBuff|-|≥0（0=永续）|施加|按 tick/20|ACTIVE_CONSUMED|STATIC_VERIFIED||
stacking_policy|text|enum|叠加策略|枚举|ApplyBuff|-|枚举|施加|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
attributes|text|; 声明|属性贡献（key:op:value）|业务数值|FightAttributeCatalog.Aggregate|-|Key 必须注册、op 匹配|施加|flat/pct/add|ACTIVE_CONSUMED|STATIC_VERIFIED|--formula-checks|
periodic_effect|text|-|周期效果|外键|DrainEffects|-|可空|周期|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
periodic_interval_ticks|int|tick|周期间隔|业务数值|DrainEffects|-|≥1|周期|按 tick/20|ACTIVE_CONSUMED|STATIC_VERIFIED||
trigger_ids|text|; 列表|触发链|外键|React|-|可空（当前全空）|事件|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
mechanic|text|enum|机制（shield/forced/haste/crit_reduce）|枚举|ApplyBuff|-|可空|施加|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
value|float|-|机制数值|业务数值|ApplyBuff|-|-|施加|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
extra|float|-|额外参数|业务数值|ApplyBuff|-|-|施加|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
target|text|enum|作用目标|枚举|ApplyBuff|-|-|施加|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
stack_group|text|-|叠组|业务数值|ApplyBuff|-|可空|施加|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
stack_strength|float|-|叠加强度|业务数值|ApplyBuff|-|-|施加|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
",
        ["skill/trigger.csv"] = @"
id|text|-|触发 ID|主键|SkillConfiguration.Load|-|当前 0 行|加载时|-|VALIDATION_ONLY|STATIC_VERIFIED||
event_kind|text|enum|事件种类|枚举|React|-|-|事件|-|LOADED_ONLY|STATIC_VERIFIED||零投放
effect_id|text|-|触发效果|外键|React|-|-|事件|-|LOADED_ONLY|STATIC_VERIFIED||
owner_buff|text|-|所属 Buff|外键|React|-|-|事件|-|LOADED_ONLY|STATIC_VERIFIED||
origin_filter|text|enum|来源过滤|枚举|React|-|-|事件|-|LOADED_ONLY|STATIC_VERIFIED||
",
        ["skill/summon.csv"] = @"
id|text|-|召唤 ID|主键|SkillConfiguration.Load|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
name|text|-|展示名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
max_count|int|个|同时存在上限|业务数值|SummonEffect|-|≥1，且受施法者容量影响|召唤|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--skill-bd-summon-checks|
hp_ratio|float|倍率|生命比例|业务数值|SummonEffect|-|>0|召唤|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
attack_ratio|float|倍率|攻击比例|业务数值|SummonEffect|-|>0|召唤|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
defense_ratio|float|倍率|防御比例|业务数值|SummonEffect|-|≥0|召唤|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
speed|float|U/秒|速度|业务数值|SummonEffect|-|>0|召唤|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
range|float|U|攻击距离|业务数值|SummonEffect|-|>0|召唤|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
cooldown_seconds|float|s|攻击冷却|业务数值|SummonEffect|-|>0|召唤|×20|ACTIVE_CONSUMED|STATIC_VERIFIED||
engagement_capacity|float|个|接敌容量|业务数值|CombatTargeting|-|≥1|召唤|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
role|text|enum|召唤角色|枚举|SummonRuntime|-|-|召唤|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
duration_seconds|float|s|存活时长（0=永续）|业务数值|SummonRuntime|-|≥0|召唤|×20|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["skill/chain.csv"] = @"
id|text|-|连锁 ID|主键|SkillConfiguration.Load|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
name|text|-|展示名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
max_targets|int|个|最大跳跃目标数|业务数值|ChainEffect|-|≥1|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
jump_range|float|U|跳跃距离|业务数值|ChainEffect|-|>0|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
damage_decay|float|倍率|每跳衰减|业务数值|ChainEffect|-|0..1|结算|累乘|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["skill/proc_icd.csv"] = @"
id|text|-|Proc ID|主键|SkillConfiguration.Load|-|当前 0 行|加载时|-|VALIDATION_ONLY|STATIC_VERIFIED||
icd_seconds|float|s|内部冷却|业务数值|ProcAllowed|-|>0|触发|×20|LOADED_ONLY|STATIC_VERIFIED||零投放
",
        ["skill/proc_limit.csv"] = @"
id|text|-|Proc ID|主键|SkillConfiguration.Load|-|当前 0 行|加载时|-|VALIDATION_ONLY|STATIC_VERIFIED||
max_per_cast|int|次|每次施放上限|业务数值|ProcAllowed|-|≥1|触发|-|LOADED_ONLY|STATIC_VERIFIED||
max_per_target|int|次|每目标上限|业务数值|ProcAllowed|-|≥1|触发|-|LOADED_ONLY|STATIC_VERIFIED||
",
        ["skill/runtime.csv"] = @"
id|text|-|参数名|主键|SkillConfiguration.Load|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
value|int|-|参数值|业务数值|SkillConfig.Setting|-|-|加载时|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
",
        ["skill/ability.csv"] = @"
id|text|-|Ability ID|主键|SkillConfiguration.Load|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
source_id|text|-|来源技能|外键|AbilityRuntime|-|-|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
form|text|enum|形态（projectile/buff）|枚举|AbilityRuntime|-|枚举|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
targeting|text|enum|目标方式|枚举|AbilityRuntime|-|-|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
hits|int|次|命中次数|业务数值|AbilityRuntime|-|≥1|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
cast_root|int|bool|是否根节点|开关|AbilityRuntime|-|-|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
level|int|级|当前等级|业务数值|AbilityRuntime|-|-|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
max_level|int|级|最大等级|业务数值|AbilityRuntime|-|≥level|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
effect_ids|text|; 列表|关联效果|外键|AbilityRuntime|-|可空|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
buff_ids|text|; 列表|关联 Buff|外键|AbilityRuntime|-|可空|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
",
        ["skill/ability_effect.csv"] = @"
id|text|-|Ability 效果 ID|主键|SkillConfiguration.Load|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
name|text|-|展示名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
effect_type|text|enum|效果类型|枚举|AbilityRuntime|-|-|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
condition|text|enum|触发条件|枚举|AbilityRuntime|-|-|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
condition_value|float|-|条件阈值|业务数值|AbilityRuntime|-|-|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
power|float|倍率|强度|业务数值|AbilityRuntime|-|≥0|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
skill_flat|float|点|固定加成|业务数值|AbilityRuntime|-|-|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
duration|float|s|持续|业务数值|AbilityRuntime|-|≥0|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
delay|float|s|延迟|业务数值|AbilityRuntime|-|≥0|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
tick_interval|float|s|周期|业务数值|AbilityRuntime|-|≥0|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
range|float|U|范围|业务数值|AbilityRuntime|-|≥0|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
trajectory|text|enum|轨迹|枚举|AbilityRuntime|-|-|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
projectile_count|int|个|投射物数|业务数值|AbilityRuntime|-|≥1|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
hover_time|float|s|悬停|业务数值|AbilityRuntime|-|≥0|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
arc_min|float|U|弧线最小|业务数值|AbilityRuntime|-|-|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
arc_max|float|U|弧线最大|业务数值|AbilityRuntime|-|-|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
speed|float|U/秒|速度|业务数值|AbilityRuntime|-|≥0|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
spread|float|度|散布|业务数值|AbilityRuntime|-|≥0|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
volley_interval|float|s|齐射间隔|业务数值|AbilityRuntime|-|≥0|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
volley_jitter|float|s|齐射抖动|业务数值|AbilityRuntime|-|≥0|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
spawn_jitter|float|U|出生抖动|业务数值|AbilityRuntime|-|≥0|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
pierce_chance|float|0..1|穿透概率|业务数值|AbilityRuntime|-|0..1|施放|概率|ACTIVE_CONSUMED|LOADER_VERIFIED||
aoe_radius|float|U|范围半径|业务数值|AbilityRuntime|-|≥0|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
aoe_all|bool|0/1|是否全体|开关|AbilityRuntime|-|-|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
band|text|enum|档位|枚举|AbilityRuntime|-|-|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
knockback|float|U|击退|业务数值|AbilityRuntime|-|≥0|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
gather|float|U|聚拢|业务数值|AbilityRuntime|-|≥0|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
buff_id|text|-|关联 Buff|外键|AbilityRuntime|-|可空|施放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
",
        ["skill/ability_settings.csv"] = @"
id|text|-|参数名|主键|SkillConfiguration.Load|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
value|float|-|参数值|业务数值|SkillConfig.Setting|-|-|加载时|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
",
        ["skill/talent.csv"] = @"
id|text|-|节点 ID|主键|TalentConfiguration.Load|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
hero|text|-|所属职业|外键|CanLearnTalent|-|必须存在|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--talent-checks|
tree|int|索引|树索引 0/1/2|业务数值|TreeInvestment|-|0..2|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
tree_name|text|-|树名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
row|int|行|行号（0 起），决定 required_points 默认值|业务数值|TalentConfiguration.Required|-|≥0|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
col|int|列|列号，用于顺序与布局|业务数值|UI/排序|-|≥0|加载时|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
name|text|-|节点名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
description|text|-|描述|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
max_rank|int|级|最高等级|业务数值|CanLearnTalent|-|≥1|学习|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
bonus_allowed|bool|0/1|是否允许外部加成提高等级|开关|TalentLevel|-|-|学习|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
kind|text|enum:active/passive|主动/被动|枚举|LoadTalent|-|枚举|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
prerequisite|text|-|前置节点|外键|CanLearnTalent|-|可空且须存在|学习|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
required_rank|int|级|前置所需等级|业务数值|CanLearnTalent|-|≥1|学习|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
required_points|int|点|该树累计投入要求（-1 表示按行自动）|业务数值|TalentConfiguration.Required|-|≥0 或 -1|学习|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
effect|text|enum|旧路径效果名（shot/leap/nova/shout/weaken/永续属性名）|枚举|CastTalent/PassiveTalentValue|-|-|结算/属性|-|ACTIVE_CONSUMED|STATIC_VERIFIED|23 个 active 节点依赖此列|
value|float|-|基础值|业务数值|TalentValue|-|-|属性/伤害|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
per_rank|float|-|每级增量|业务数值|TalentValue|-|-|属性/伤害|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
cooldown|float|s|冷却|业务数值|CastTalent|-|≥0|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
range|float|U|距离|业务数值|CastTalent|-|≥0|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
radius|float|U|作用半径|业务数值|CastTalent|-|≥0|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
damage_type|text|enum|伤害类型|枚举|CastTalent|-|5 类|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
projectile_speed|float|U/秒|投射物速度|业务数值|SpawnGeneric|-|≥0|结算|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
skill_id|text|-|绑定的正式技能（空=旧 CastTalent 路径）|外键|SkillRuntime/CastTalent|-|可空且须存在|施放|-|ACTIVE_CONSUMED|STATIC_VERIFIED|16 绑定 / 23 旧路径|
",
        ["skill/hero_starting_skill.csv"] = @"
id|text|-|职业 ID|主键|StartingSkills|-|-|建号|-|VALIDATION_ONLY|LOADER_VERIFIED||
skill_id|text|-|初始主动技能|外键|GrantStarterSkill|-|必须存在|建号|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["star/star.csv"] = @"
id|text|-|节点 ID|主键|LoadStars|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
name|text|-|节点名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
max_level|int|级|最高等级（价格项数必须相等）|业务数值|CanBuy|-|1..10000|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|默认全量检查(StarChecks)|
cost_currency|text|enum:gold|货币（仅 gold）|枚举|CanBuy|-|必须 gold|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
cost|int|金币|逐级价格（; 分隔）|业务数值|BuyStar|-|项数=max_level，≥0|购买|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
attr_id|text|enum|效果（attack/hp/party_slots/skill_slots/talent_reset/talent_reset_discount）|枚举|ApplyStarTotals/各消费者|-|白名单|购买|-|ACTIVE_CONSUMED|STATIC_VERIFIED|默认全量检查(StarChecks)|空=占位节点不可购买
operation|text|enum:flat|运算（仅 flat）|枚举|Totals|-|必须 flat|购买|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
value_per_level|float|-|每级增量|业务数值|Totals|-|≥0；节点类型有额外约束|购买|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
icon|text|enum:attack/defense/utility|图标分组|展示|HUD|-|枚举|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
",
        ["star/star_layout.csv"] = @"
id|text|-|节点 ID|主键|LoadStars|-|必须与 star.csv 一一对应|加载时|-|VALIDATION_ONLY|STATIC_VERIFIED||
col|int|格|列坐标|规划|UI|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
row|int|格|行坐标|规划|UI|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
prereq|text|; 列表|前置节点（任一满足即可点亮）|外键|PrerequisitesMet|-|必须存在且无环|购买|-|ACTIVE_CONSUMED|STATIC_VERIFIED|默认全量检查(StarChecks)|
label|text|-|展示标签|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
",
        ["star/star_plan.csv"] = @"
id|text|-|节点 ID|主键|无消费者|-|与 star.csv 对应|—|-|PLANNED_ONLY|LOADER_VERIFIED||设计意图文本
intent|text|-|设计意图说明|展示|无|-|-|—|-|PLANNED_ONLY|LOADER_VERIFIED||
",
        ["item/item.csv"] = @"
id|text|-|物品 ID|主键|LoadWorld|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
name|text|-|展示名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
type_id|text|-|类型|外键|ValidateWorld|-|必须存在|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
sell_price|int|金币|出售价|业务数值|SellJunk|-|≥0|售卖|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--simulation ledger|
",
        ["item/itemtype.csv"] = @"
id|text|-|类型 ID|主键|LoadWorld|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
name|text|-|展示名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
kind|text|enum:currency/junk|类型（金币/杂物）|枚举|ValidateWorld|-|currency/junk|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
auto_sell|bool|0/1|是否自动售卖（junk 必须为 1）|开关|SellJunk|-|与 kind 一致|售卖|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["village/npc.csv"] = @"
id|text|-|NPC ID|主键|LoadWorld|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
name|text|-|展示名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
kind|text|enum:trade|职能（仅 trade）|枚举|ValidateWorld|-|必须 trade|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
x|float|U|村内位置|业务数值|WorldNavigation|-|-|交互|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
y|float|U|村内位置|业务数值|WorldNavigation|-|-|交互|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["village/portal_rule.csv"] = @"
phase_seconds|float|s|传送阶段时长|业务数值|WorldCampaign.PortalPhase|-|>0|传送|×20|ACTIVE_CONSUMED|STATIC_VERIFIED||
town_x|float|格|城镇坐标|业务数值|WorldCampaign|-|-|传送|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
town_y|float|格|城镇坐标|业务数值|WorldCampaign|-|-|传送|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
waypoint_x|float|格|传送阵坐标|业务数值|WorldCampaign|-|-|传送|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
waypoint_y|float|格|传送阵坐标|业务数值|WorldCampaign|-|-|传送|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
use_range|float|U|使用距离|业务数值|TownScene|-|>0|交互|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["village/town_scene.csv"] = @"
id|text|-|场景 ID|主键|LoadTown|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
chapter_id|text|-|所属章节（actN）|外键|LoadTown|-|每章恰一条|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
name|text|-|场景名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
width|float|U|场景宽|业务数值|TownScene|-|>0|渲染/寻路|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
height|float|U|场景高|业务数值|TownScene|-|>0|同上|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
entry_x|float|U|入口 X|业务数值|TownScene|-|-|进入|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
entry_y|float|U|入口 Y|业务数值|TownScene|-|-|进入|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
exit_x|float|U|出口 X|业务数值|TownScene|-|-|离场|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
exit_y|float|U|出口 Y|业务数值|TownScene|-|-|离场|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
interaction_range|float|U|交互半径|业务数值|TownScene|-|>0|交互|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
service_seconds|float|s|服务耗时（修理/洗点等）|业务数值|TownScene|-|>0|交互|×20|ACTIVE_CONSUMED|STATIC_VERIFIED||
theme|text|-|主题|展示|渲染|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
layout_version|int|-|布局版本|业务数值|TownLayoutMigration|-|≥0|迁移|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
return_x|float|U|返回点 X|业务数值|TownScene|-|-|离场|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
return_y|float|U|返回点 Y|业务数值|TownScene|-|-|离场|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
waypoint_x|float|U|传送阵 X|业务数值|TownScene|-|-|传送|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
waypoint_y|float|U|传送阵 Y|业务数值|TownScene|-|-|传送|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
exit_name|text|-|出口名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
walk_speed|float|U/秒|村内速度|业务数值|TownScene|-|>0|移动|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
",
        ["village/town_npc.csv"] = @"
scene_id|text|-|场景|外键|LoadTown|-|存在|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
id|text|-|NPC ID（场景内唯一）|主键|TownScene.Npc|-|-|加载时|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
name|text|-|展示名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
role|text|enum|职能（trader/smith/healer/…）|枚举|TownScene.Npc(role)|-|必须存在所需角色|交互|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--town-layout-checks|
x|float|U|位置|业务数值|TownScene|-|-|交互|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
y|float|U|位置|业务数值|TownScene|-|-|交互|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
description|text|-|描述|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
",
        ["village/town_prop.csv"] = @"
scene_id|text|-|场景|外键|LoadTown|-|存在|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
id|text|-|道具 ID|主键|TownScene|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
kind|text|enum|类型（forge/anvil/…）|枚举|渲染|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
x|float|U|位置|业务数值|TownScene|-|-|碰撞|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
y|float|U|位置|业务数值|TownScene|-|-|碰撞|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
width|float|U|宽|业务数值|TownScene|-|>0|碰撞|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
height|float|U|高|业务数值|TownScene|-|>0|碰撞|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
blocks|bool|0/1|是否阻挡|开关|TownScene|-|-|移动|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
",
        ["village/town_zone.csv"] = @"
scene_id|text|-|场景|外键|LoadTown|-|存在|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
id|text|-|区域 ID|主键|TownScene|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
kind|text|enum|区域类型|枚举|TownScene|-|-|判定|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
x|float|U|左边界|业务数值|TownScene|-|-|判定|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
y|float|U|上边界|业务数值|TownScene|-|-|判定|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
width|float|U|宽|业务数值|TownScene|-|>0|判定|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
height|float|U|高|业务数值|TownScene|-|>0|判定|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
",
        ["world/chapter.csv"] = @"
id|text|-|章节 ID（act1..act5）|主键|LoadChapters|-|恰好 5 条|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--campaign-package-checks|
name|text|-|章节名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
terrain|text|enum|主题地形|展示|渲染|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
width|int|格|章节世界宽|业务数值|WorldNavigation|-|>0|寻路|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
height|int|格|章节世界高|业务数值|WorldNavigation|-|>0|寻路|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
directory|text|-|章节目录（chapters/actN）|外键|LoadChapters|-|目录必须存在|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["world/difficulty.csv"] = @"
id|text|enum:normal/nightmare/hell|难度稳定 ID|主键|LoadClientConfig|-|恰好 3 条|加载时|-|VALIDATION_ONLY|STATIC_VERIFIED||
name|text|-|难度名|展示|HUD|-|-|—|-|DISPLAY_ONLY|LOADER_VERIFIED||
",
        ["world/recovery.csv"] = @"
id|text|enum|参数名（failure_threshold）|主键|LoadRecovery|-|必须含 failure_threshold|加载时|-|VALIDATION_ONLY|STATIC_VERIFIED||
value|int|次|连续失败阈值|业务数值|RecordFailure|-|≥1|失败返村|计数|ACTIVE_CONSUMED|STATIC_VERIFIED|--simulation C/D|
",
        ["dungeon/dungeon_rule.csv"] = @"
floors_min|int|层|随机楼层下限|业务数值|Dungeon.Generate|-|1..20|进入地牢|-|ACTIVE_CONSUMED|STATIC_VERIFIED|--dungeon-travel-checks|有 POI 档案时被 profile.floors 覆盖
floors_max|int|层|随机楼层上限|业务数值|Dungeon.Generate|-|≥min|进入地牢|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
rooms_min|int|间|每层房间下限|业务数值|Dungeon.Generate|-|3..20|生成|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
rooms_max|int|间|每层房间上限|业务数值|Dungeon.Generate|-|≥min|生成|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
event_chance|float|0..1|非 Boss 房事件概率|业务数值|Reveal|-|0..1|进房|概率|ACTIVE_CONSUMED|STATIC_VERIFIED|--simulation room_event|
revive_seconds|float|s|复活读条时长|业务数值|Rescue/Revive|-|>0|清房救援|×20|ACTIVE_CONSUMED|STATIC_VERIFIED||
revive_range|float|U|救援距离|业务数值|Rescue|-|>0|救援|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
revive_hp_ratio|float|0..1|复活后生命比例|业务数值|Revive|-|(0,1]|救援|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
room_width_min|int|格|房间宽下限|业务数值|Dungeon.Generate|-|4..10|生成|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
room_width_max|int|格|房间宽上限|业务数值|Dungeon.Generate|-|≤10|生成|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
room_height_min|int|格|房间高下限|业务数值|Dungeon.Generate|-|4..10|生成|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
room_height_max|int|格|房间高上限|业务数值|Dungeon.Generate|-|≤10|生成|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["gm/gm.csv"] = @"
gold_grant|int|金币|GM 发放金币|业务数值|GmCommands|-|≥0|GM 操作|-|ACTIVE_CONSUMED|LOADER_VERIFIED||仅 GM
experience_grant|int|点|GM 发放经验|业务数值|GmCommands|-|≥0|GM 操作|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
kill_grant|int|次|GM 增加击杀|业务数值|GmCommands|-|≥0|GM 操作|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
monster_scale_steps|text|; 列表|怪物缩放档位|业务数值|GmBattleSettings|-|-|GM 操作|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
count_step|int|只|数量步进|业务数值|GmBattleSettings|-|≥1|GM 操作|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
count_limit|int|只|数量上限|业务数值|GmBattleSettings|-|≥1|GM 操作|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
",
        ["audio/audio.csv"] = @"
id|text|-|音频 ID|主键|LoadClientConfig|-|-|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
path|text|-|资源路径|外键|Audio|-|文件必须存在|播放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
kind|text|enum|音频分类|枚举|Audio|-|-|播放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
volume_db|float|dB|音量|展示|Audio|-|-|播放|-|DISPLAY_ONLY|LOADER_VERIFIED||
min_gap|float|s|最小间隔|业务数值|Audio|-|≥0|播放|-|ACTIVE_CONSUMED|LOADER_VERIFIED||
",
        ["system/window.csv"] = @"
width|int|px|窗口宽|展示|ClientSettings.BuildSettings|-|>0|启动|-|DISPLAY_ONLY|LOADER_VERIFIED|--smoke-test|
height|int|px|窗口高|展示|ClientSettings.BuildSettings|-|>0|启动|-|DISPLAY_ONLY|LOADER_VERIFIED||
",
        ["system/camera.csv"] = @"
follow_rate|float|1/s|跟随插值速度|表现|Main/SceneRenderer|-|>0|每帧|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
transition_rate|float|1/s|场景切换速度|表现|Main|-|>0|切换|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
dungeon_max_zoom|float|缩放|地牢最大缩放|表现|Main|-|>0|渲染|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
world_zoom|float|缩放|世界缩放|表现|Main|-|>0|渲染|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
world_units|float|单位|世界投影单位数|表现|WorldNavigation|-|≥12|渲染|-|ACTIVE_CONSUMED|STATIC_VERIFIED||
",
        ["system/client.csv"] = @"
autosave_min_seconds|float|s|自动保存最小间隔|业务数值|ClientSettings|-|>0|定时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|默认全量检查(SaveChecks)|
checkpoint_seconds|float|s|检查点间隔|业务数值|无消费者|-|—|—|-|NO_CONSUMER|STATIC_VERIFIED||仅检查断言“无周期写盘”
",
        ["system/config.csv"] = @"
id|text|-|规则名|主键|TalentConfiguration/CombatRules|-|必须含必备规则|加载时|-|VALIDATION_ONLY|LOADER_VERIFIED||
value|int|-|规则值|业务数值|Talents.Rule/CombatRules|-|-|加载时|-|ACTIVE_CONSUMED|STATIC_VERIFIED|34 条；--talent-checks/--room-entry-checks|
"
    };
}
