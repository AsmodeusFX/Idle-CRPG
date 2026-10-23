using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public record RegionSpec(int Id, int X, int Y, string Name, string Terrain,bool Enterable=true);
public record DungeonSpec(string Id, string Name, int RegionId, string Kind, int LevelMin, int LevelMax, float X, float Y)
{
    public bool Castle => Kind == "castle";
}
public record VillageSpec(string Id, string Name, int RegionId, float X, float Y, string NpcId);
public record NpcSpec(string Id, string Name, string Kind, Vector2 Position);
public record ItemTypeSpec(string Id, string Name, string Kind, bool AutoSell);
public record ItemSpec(string Id, string Name, string TypeId, int SellPrice);
public record DropSpec(string MonsterId, string ItemId, float Chance, int Min, int Max);

public sealed partial class Config
{
    public int WorldWidth, WorldHeight, StartRegionId;
    public float TravelSpeed, VillageSpeed, TradeSeconds, TradeRange;
    public SpawnRule Spawn = null!;
    public List<RegionSpec> Regions = new();
    public List<DungeonSpec> Dungeons = new();
    public List<VillageSpec> Villages = new();
    public List<NpcSpec> Npcs = new();
    public List<ItemTypeSpec> ItemTypes = new();
    public List<ItemSpec> Items = new();
    public List<DropSpec> Drops = new();
    public Dictionary<int,string> Rulers = new();

    private void LoadWorld(string directory,string? sharedDirectory=null)
    {
        List<Dictionary<string,string>> Table(string p) => Read(Path.Combine(sharedDirectory!=null&&(p.StartsWith("item/")||p.StartsWith("monster/")||p=="village/npc.csv")?sharedDirectory:directory,p));
        Dictionary<string,string> One(string p) { var rows=Table(p); return rows.Count==1?rows[0]:throw new InvalidDataException(p+" 必须恰有一行"); }
        var w=One("world/world.csv");
        WorldWidth=Integer(w,"width"); WorldHeight=Integer(w,"height"); StartRegionId=Integer(w,"start_region_id");
        TravelSpeed=Number(w,"travel_speed"); VillageSpeed=Number(w,"village_speed"); TradeSeconds=Number(w,"trade_seconds"); TradeRange=Number(w,"trade_range");
        var s=One("monster/spawn_rule.csv");
        Spawn=new SpawnRule(Integer(s,"count_min"),Integer(s,"count_max"),Integer(s,"level"),Number(s,"boss_hp"),Number(s,"boss_attack"),Integer(s,"xp_base"),Integer(s,"boss_reward"),Number(s,"drop_chance_multiplier"),Number(s,"value_per_level"));
        Regions=Table("world/region.csv").Select(r=>new RegionSpec(Integer(r,"id"),Integer(r,"x"),Integer(r,"y"),r["name"],r["terrain"],Boolean(r,"enterable"))).ToList();
        Dungeons=Table("dungeon/dungeon.csv").Select(r=>new DungeonSpec(r["id"],r["name"],Integer(r,"region_id"),r["kind"],Integer(r,"level_min"),Integer(r,"level_max"),Number(r,"x"),Number(r,"y"))).ToList();
        Villages=Table("village/village.csv").Select(r=>new VillageSpec(r["id"],r["name"],Integer(r,"region_id"),Number(r,"x"),Number(r,"y"),r["npc_id"])).ToList();
        Npcs=Table("village/npc.csv").Select(r=>new NpcSpec(r["id"],r["name"],r["kind"],new Vector2(Number(r,"x"),Number(r,"y")))).ToList();
        ItemTypes=Table("item/itemtype.csv").Select(r=>new ItemTypeSpec(r["id"],r["name"],r["kind"],Boolean(r,"auto_sell"))).ToList();
        Items=Table("item/item.csv").Select(r=>new ItemSpec(r["id"],r["name"],r["type_id"],Integer(r,"sell_price"))).ToList();
        Drops=Table("monster/monster_drop.csv").Select(r=>new DropSpec(r["monster_id"],r["item_id"],Number(r,"chance"),Integer(r,"quantity_min"),Integer(r,"quantity_max"))).ToList();
        var rulers=Table("world/castle_region.csv");
        if(rulers.Select(r=>Integer(r,"region_id")).Distinct().Count()!=rulers.Count) throw new InvalidDataException("区域统治重复");
        Rulers=rulers.ToDictionary(r=>Integer(r,"region_id"),r=>r["castle_id"]);
        LoadPoiConfiguration(directory);
        LoadAreaParts(directory);
        ValidateWorld();
    }

    private static bool Boolean(Dictionary<string,string> r,string key) => r[key] switch { "0"=>false,"1"=>true,_=>throw new InvalidDataException("非法布尔值 "+key) };
    /// <summary>配置完整性检查：严格拒绝缺区域、重复归属和失效引用。</summary>
    public void ValidateWorld()
    {
        static void Require(bool ok,string message) { if(!ok)throw new InvalidDataException(message); }
        static bool Unique<T>(IEnumerable<T> values) => values.Distinct().Count()==values.Count();
        Require(WorldWidth>0&&WorldWidth<=64&&WorldHeight>0&&WorldHeight<=64&&Regions.Count==WorldWidth*WorldHeight,"世界尺寸/区域数量错误");
        Require(TravelSpeed>0&&VillageSpeed>0&&TradeSeconds>0&&TradeRange>0,"世界移动/交互参数非法");
        Require(Unique(Regions.Select(r=>r.Id))&&Unique(Regions.Select(r=>(r.X,r.Y)))&&Regions.All(r=>r.Id>0&&r.X>=0&&r.X<WorldWidth&&r.Y>=0&&r.Y<WorldHeight&&new[]{"snow","tropical","desert","coast","grass","hell","mountain","ocean"}.Contains(r.Terrain)&&(!(r.Terrain is "mountain" or "ocean")||!r.Enterable)),"区域 ID/坐标/地形非法");
        Require(Regions.Any(r=>r.Id==StartRegionId&&r.Enterable),"出生区域不存在或不可进入");
        IndexWorld();
        bool Region(int id)=>Regions.Any(r=>r.Id==id&&r.Enterable);
        Require(Unique(Dungeons.Select(d=>d.Id))&&Dungeons.All(d=>!string.IsNullOrWhiteSpace(d.Id)&&Region(d.RegionId)&&new[]{"normal","castle"}.Contains(d.Kind)&&d.LevelMin>=1&&d.LevelMax>=d.LevelMin&&d.X>0&&d.X<1&&d.Y>0&&d.Y<1),"地牢 ID/引用/等级/位置非法");
        Require(Unique(Villages.Select(v=>v.Id))&&Unique(Npcs.Select(n=>n.Id))&&Npcs.All(n=>n.Kind=="trade")&&Villages.All(v=>Region(v.RegionId)&&Npcs.Any(n=>n.Id==v.NpcId)&&v.X>0&&v.X<1&&v.Y>0&&v.Y<1),"村庄/NPC 配置非法");
        foreach(var r in Regions)
        {
            if(!r.Enterable){Require(!Villages.Any(v=>v.RegionId==r.Id)&&!Dungeons.Any(d=>d.RegionId==r.Id)&&!Rulers.ContainsKey(r.Id),"不可进入地块不得配置村庄、地牢或统治归属");continue;}
            if(IsTown(r.Id)){Require(!Rulers.ContainsKey(r.Id)&&!Dungeons.Any(d=>d.RegionId==r.Id),"营地不得关联统治归属或地牢");continue;}
            Require(TownRegionId>0||Villages.Count(v=>v.RegionId==r.Id)==1,"每区域必须一个村庄");
            Require(Dungeons.Count(d=>d.RegionId==r.Id&&d.Castle)<=1,"区域城堡数量错误");
            Require(Rulers.TryGetValue(r.Id,out var castle)&&Dungeons.Any(d=>d.Id==castle&&d.Castle),"区域必须恰好归属一个有效城堡");
        }
        Require(Rulers.Count==Regions.Count(r=>r.Enterable&&!IsTown(r.Id))&&Dungeons.Where(d=>d.Castle).All(d=>Rulers[d.RegionId]==d.Id),"统治范围必须包含城堡所在区且不得多余");
        Require(TownRegionId==0||Villages.Count==1&&Villages[0].RegionId==TownRegionId,"独立营地地图必须且仅在营地配置一个村庄");
        ValidatePoiConfiguration();
        ValidateAreaParts();
        Require(Unique(ItemTypes.Select(t=>t.Id))&&ItemTypes.All(t=>t.Kind is "currency" or "junk"&&t.AutoSell==(t.Kind=="junk")),"物品类型非法");
        Require(Unique(Items.Select(i=>i.Id))&&Items.All(i=>i.SellPrice>=0&&ItemTypes.Any(t=>t.Id==i.TypeId))&&Items.Count(i=>ItemTypes.Single(t=>t.Id==i.TypeId).Kind=="currency")==1&&Items.Any(i=>i.Id=="gold"&&ItemTypes.Single(t=>t.Id==i.TypeId).Kind=="currency"),"物品引用/金币配置非法");
        Require(Drops.Count>0&&Drops.All(d=>(Monsters.Any(m=>m.Id==d.MonsterId)||ActMonsters.Any(m=>m.Id==d.MonsterId))&&Items.Any(i=>i.Id==d.ItemId)&&d.Chance>=0&&d.Chance<=1&&d.Min>=1&&d.Max>=d.Min),"掉落引用/概率/数量非法");
        Spawn.Validate(new SpawnModifiers());
    }
}
