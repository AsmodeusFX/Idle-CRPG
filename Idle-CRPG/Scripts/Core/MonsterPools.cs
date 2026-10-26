using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

/// <summary>区域投放池条目：同一统治区按权重抽取怪物身份；权重为整数百分比，同区合计必须为100。</summary>
public record MonsterPoolEntry(string MonsterId,int Weight);

/// <summary>五章怪物内容身份与区域投放池。只做“身份选择层”，不改既有怪物行为、等级或奖励路径。</summary>
public sealed partial class Config
{
    /// <summary>正式怪物之外的章节内容身份；不并入 Monsters，旧单地牢模拟与 legacy 回归的随机集合保持不变。</summary>
    public List<UnitSpec> ActMonsters { get; private set; }=new();
    /// <summary>统治区 ID → 加权池；没有该表的章节与旧地图回落到全局怪物表。</summary>
    public Dictionary<string,List<MonsterPoolEntry>> MonsterPools { get; private set; }=new();
    /// <summary>
    /// 身份→职能档位（melee/ranged/heavy/support）。与身份ID分开登记，只用于"从池里抽哪一档"，
    /// 不参与等级、数量、奖励或伤害；缺表时按射程/生命相对分档回落（A1 旧口径）。
    /// </summary>
    public Dictionary<string,string> MonsterArchetypes { get; private set; }=new(StringComparer.Ordinal);

    /// <summary>章节身份先加载，职能档位才能校验引用；文件缺失表示沿用相对分档。</summary>
    private void LoadMonsterArchetypes(string directory)
    {
        MonsterArchetypes=new(StringComparer.Ordinal);
        string path=Path.Combine(directory,"monster/archetypes.csv");
        if(!File.Exists(path))return;
        foreach(var r in CsvTable.Load(path,File.ReadAllText(path),"monster_id","archetype").Rows)
        {
            string archetype=r.Required("archetype");
            if(FindMonsterSpec(r.Id)==null)throw r.Error("monster_id","职能档位引用未知怪物身份");
            if(archetype is not ("melee" or "ranged" or "heavy" or "support"))throw r.Error("archetype","职能档位仅支持 melee/ranged/heavy/support");
            if(!MonsterArchetypes.TryAdd(r.Id,archetype))throw r.Error("monster_id","职能档位重复");
        }
    }

    /// <summary>读取章节内容身份表；缺失视为未投放，不报错。数值只做范围与重复校验，不重算平衡。</summary>
    private void LoadActMonsters(string directory)
    {
        ActMonsters=new();
        string path=Path.Combine(directory,"monster/act_monsters.csv");
        if(!File.Exists(path))return;
        var table=CsvTable.Load(path,File.ReadAllText(path),"id","name","hp","attack","defense","speed","range","cooldown");
        foreach(var r in table.Rows)
        {
            var unit=new UnitSpec(r.Id,r.Required("name"),(float)r.Number("hp"),(float)r.Number("attack"),(float)r.Number("defense"),(float)r.Number("speed"),(float)r.Number("range"),(float)r.Number("cooldown"));
            if(string.IsNullOrWhiteSpace(unit.Id)||string.IsNullOrWhiteSpace(unit.Name)||unit.Hp<=0||unit.Attack<=0||unit.Defense<0||unit.Speed<=0||unit.Range<=0||unit.Cooldown<=0)throw r.Error("hp","章节怪物身份字段非法");
            ActMonsters.Add(unit);
        }
        if(ActMonsters.Select(u=>u.Id).Distinct().Count()!=ActMonsters.Count)throw new InvalidDataException("章节怪物身份ID重复");
        if(ActMonsters.Any(u=>Monsters.Any(m=>m.Id==u.Id)))throw new InvalidDataException("章节怪物身份与正式怪物ID冲突，必须使用独立ID");
    }

    /// <summary>读取本章统治区投放池；文件缺失表示该章沿用全局随机怪物表。</summary>
    private void LoadMonsterPools(string mapRoot)
    {
        MonsterPools=new();
        string path=Path.Combine(mapRoot,"monster/region_pool.csv");
        if(!File.Exists(path))return;
        // 同一统治区会有多行，不能用“首列为唯一键”的 CsvTable.Load；这里按无键记录逐行读取。
        var records=CsvTable.ReadRecords(path,File.ReadAllText(path));
        string[] expected={"areapart_id","monster_id","weight"};
        if(records.Count==0||!records[0].Cells.SequenceEqual(expected,StringComparer.Ordinal))
            throw new InvalidDataException("怪物投放池："+path+" 表头必须为 areapart_id,monster_id,weight");
        for(int i=1;i<records.Count;i++)
        {
            var (line,cells)=records[i];
            if(cells.Length!=expected.Length)throw new InvalidDataException($"怪物投放池：{path}:{line} 列数错误");
            string part=cells[0].Trim(),id=cells[1].Trim();
            if(part==""||id=="")throw new InvalidDataException($"怪物投放池：{path}:{line} 统治区或怪物为空");
            if(!int.TryParse(cells[2],System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out int weight))
                throw new InvalidDataException($"怪物投放池：{path}:{line} 权重非法");
            if(!MonsterPools.TryGetValue(part,out var pool))MonsterPools[part]=pool=new();
            pool.Add(new MonsterPoolEntry(id,weight));
        }
    }

    /// <summary>池只允许引用本工程已知身份，且每个统治区都要有完整权重；缺项应报错而不是静默回落。</summary>
    private void ValidateMonsterPools()
    {
        if(MonsterPools.Count==0)return;
        void Need(bool ok,string why){if(!ok)throw new InvalidDataException("怪物投放池："+why);}
        Need(MonsterPools.Keys.All(k=>AreaParts.Any(p=>p.Id==k)),"统治区引用无效");
        Need(AreaParts.All(p=>MonsterPools.ContainsKey(p.Id)),"存在未配置投放池的统治区");
        foreach(var pair in MonsterPools)
        {
            var pool=pair.Value;
            Need(pool.Count>0,"池为空："+pair.Key);
            Need(pool.Select(e=>e.MonsterId).Distinct().Count()==pool.Count,"同区怪物重复："+pair.Key);
            Need(pool.All(e=>Monsters.Any(m=>m.Id==e.MonsterId)||ActMonsters.Any(m=>m.Id==e.MonsterId)),"怪物身份引用无效："+pair.Key);
            Need(pool.All(e=>e.Weight>=1&&e.Weight<=1000),"权重越界："+pair.Key);
            Need(pool.Sum(e=>e.Weight)==100,"权重合计必须为100："+pair.Key);
        }
    }

    /// <summary>按统治区加权抽取怪物身份；poolBias 只在该池内放大同类身份的权重（3倍），不改池成员与权重合计。</summary>
    public UnitSpec PickSpawnMonster(Random rng,string areaPartId,string poolBias="mixed")
    {
        if(MonsterPools.TryGetValue(areaPartId,out var pool)&&pool.Count>0)
        {
            // 内容形态偏好：优先用显式职能档位，缺表回落到池内相对射程/生命分档（A1 旧口径）。
            bool biased=poolBias is "melee" or "ranged" or "heavy" or "support";
            double maxRange=pool.Max(e=>FindMonsterSpec(e.MonsterId)?.Range??0);
            double maxHp=pool.Max(e=>FindMonsterSpec(e.MonsterId)?.Hp??0);
            var weighted=biased
                ?pool.Select(e=>(e.MonsterId,Weight:e.Weight*(MatchesBias(e.MonsterId,poolBias,maxRange,maxHp)?3:1))).ToArray()
                :pool.Select(e=>(e.MonsterId,e.Weight)).ToArray();
            int total=weighted.Sum(e=>e.Weight),roll=rng.Next(total),accumulated=0;
            foreach(var entry in weighted)
            {
                accumulated+=entry.Weight;
                if(roll<accumulated)return FindSpawnSpec(entry.MonsterId);
            }
            return FindSpawnSpec(weighted[^1].MonsterId);
        }
        return Monsters[rng.Next(Monsters.Count)];
    }

    /// <summary>
    /// 按职能档位抽取身份：城堡威胁组合的每一格只决定"这一格要哪一档"，
    /// 同档内仍按池权重随机，因此总数与池成员不被改写。池内没有该档时报错而不是静默改名。
    /// </summary>
    public UnitSpec PickSpawnMonsterOfArchetype(Random rng,string areaPartId,string archetype)
    {
        if(archetype is not ("melee" or "ranged" or "heavy" or "support"))throw new ArgumentOutOfRangeException(nameof(archetype));
        if(!MonsterPools.TryGetValue(areaPartId,out var pool)||pool.Count==0)return Monsters[rng.Next(Monsters.Count)];
        var candidates=pool.Where(e=>ArchetypeOf(e.MonsterId)==archetype).ToArray();
        if(candidates.Length==0)throw new InvalidDataException($"统治区 {areaPartId} 的投放池没有 {archetype} 档身份");
        int total=candidates.Sum(e=>e.Weight),roll=rng.Next(total),accumulated=0;
        foreach(var entry in candidates)
        {
            accumulated+=entry.Weight;
            if(roll<accumulated)return FindSpawnSpec(entry.MonsterId);
        }
        return FindSpawnSpec(candidates[^1].MonsterId);
    }

    /// <summary>显式职能档位优先；没有登记时按池内相对分档，保证旧图与旧档行为不变。</summary>
    public string ArchetypeOf(string monsterId)
    {
        if(MonsterArchetypes.TryGetValue(monsterId,out var declared))return declared;
        var spec=FindMonsterSpec(monsterId);
        return spec==null?"melee":spec.Hp>=55?"heavy":spec.Range>=.85?"ranged":"melee";
    }

    /// <summary>身份分类优先读显式档位；缺表时相对本池最高射程/生命档判定，不引入第二套行为标签。</summary>
    private bool MatchesBias(string monsterId,string poolBias,double maxRange,double maxHp)
    {
        var spec=FindMonsterSpec(monsterId);if(spec==null)return false;
        if(MonsterArchetypes.TryGetValue(monsterId,out var declared))return declared==poolBias;
        return poolBias switch
        {
            "ranged"=>maxRange>0&&spec.Range>=maxRange-.0001,
            "melee"=>maxRange>0&&spec.Range<maxRange-.0001,
            "heavy"=>maxHp>0&&spec.Hp>=maxHp-.0001,
            _=>false
        };
    }

    /// <summary>按稳定ID查怪物模板：先正式怪物，再章节内容身份；读档重建出生现场共用此入口。</summary>
    public UnitSpec? FindMonsterSpec(string id)=>Monsters.FirstOrDefault(m=>m.Id==id)??ActMonsters.FirstOrDefault(m=>m.Id==id);

    private UnitSpec FindSpawnSpec(string id)=>FindMonsterSpec(id)!;
}
