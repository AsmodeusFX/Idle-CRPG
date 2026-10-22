using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class SkillBdSliceAChecks
{
    public static void Run(string root)
    {
        void Need(bool value,string why){if(!value)throw new Exception("BD_SLICE_A: "+why);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        string fixture=Path.Combine(root,"tests/fixtures/skill_bd_slice_a/skill_cases.csv");
        var cases=CsvTable.Load(fixture,File.ReadAllText(fixture),"id","prototype_id","projectile_count","spread_degrees");
        foreach(var row in cases.Rows)
        {
            string id=row.Id,prototype=row.Required("prototype_id");var node=c.Talents.Nodes[prototype];
            var skill=c.Skills.Skills[prototype] with {Id=id};
            if(node.Effect=="shot")
            {
                var impact=c.Skills.Effects[prototype+"_impact"] with {Id=id+"_impact"};
                var projectile=c.Skills.Effects[prototype+"_projectile"] with {Id=id+"_projectile",Impact=impact.Id,ProjectileCount=row.Integer("projectile_count"),SpreadDegrees=(float)row.Number("spread_degrees")};
                c.Skills.Effects.Add(impact.Id,impact);c.Skills.Effects.Add(projectile.Id,projectile);
                skill=skill with {Effects=new[]{projectile.Id},Coefficient=node.Value};
            }
            c.Skills.Skills.Add(id,skill);
            // 多重箭是普通Skill/Effect的QA实例；其他三项复制天赋分支，不改正式节点及玩家数据。
            bool fan=row.Integer("projectile_count")>1;
            if(!fan)c.Talents.Nodes.Add(id,node with {Id=id});
            var g=new GameSession(c,785,new[]{node.Hero});
            for(int i=0;i<10000&&g.CurrentRoom==0;i++)g.Tick(.05f);
            var room=g.Floor.Rooms[g.CurrentRoom];var hero=g.Heroes[0];var state=g.Capture().SkillState;
            state.Targeting=new();state.Actions.Clear();state.Buffs.Clear();state.Pending.Clear();state.Flights.Clear();
            hero.Position=room.Center-Vector2.UnitX;hero.Attack=10;hero.Hp=hero.MaxHp=100000;hero.Level=7;hero.Timer=999;
            hero.SkillIds=new[]{id};if(!fan)hero.Talents[id]=1;
            for(int i=0;i<room.Monsters.Count;i++){var m=room.Monsters[i];m.Hp=m.MaxHp=100000;m.Defense=0;m.Timer=999;m.Position=room.Center+new Vector2(.2f,i*.1f);}
            // 火球至少两个合法目标，其他测试保留一个，避免把目标选择误当伤害次数。
            if(prototype!="mage_fireball")foreach(var m in room.Monsters.Skip(1))m.Hp=0;
            var victim=room.Monsters[0];float before=victim.Hp;long hits=g.HitCount;
            for(int t=0;t<5;t++)g.Tick(.05f);
            var action=g.Capture().SkillState.Actions.Single(a=>a.Owner==0);
            Need(action.Cooldowns.ContainsKey(id),id+"未从Commit开始CD");
            if(fan)
            {
                var arrows=state.Flights.Where(f=>f.Skill==id).ToArray();
                Need(arrows.Length==row.Integer("projectile_count")&&arrows.Select(f=>f.Direction).Distinct().Count()==arrows.Length,"扇形必须产生不同权威方向");
                Need(arrows.Select(f=>f.OriginCast).Distinct().Count()==1&&arrows[0].OriginCast>0,"同次施法各箭缺少共同来源");
                Need(arrows.All(f=>f.Snapshot.Level==7&&f.Snapshot.Attack==10),"泛用箭未快照等级和攻击");
                hero.Attack=10000;hero.Level=19;hero.SkillIds=Array.Empty<string>();
                string Encode(GameSession game)=>SaveStore.Encode(new(){Session=game.Capture()});
                var copy=GameSession.Restore(c,SaveStore.Decode(c,Encode(g)).Session!);
                for(int t=0;t<20;t++){g.Tick(.05f);copy.Tick(.05f);}
                Need(Encode(g)==Encode(copy),"多弹道完整快照续行不一致");
                Need(g.HitCount-hits==arrows.Length,"同一目标应允许被同次施法的每支箭命中一次");
                Need(Math.Abs(before-victim.Hp-10*node.Value*arrows.Length)<.01,"在途升级/加攻击改变了旧箭伤害或单箭重复命中");
                Need(g.LastDamage?.OriginCast==arrows[0].OriginCast,"伤害事实未继承箭矢施法来源");
            }
            else
            {
                hero.SkillIds=Array.Empty<string>();for(int t=0;t<30;t++)g.Tick(.05f);
                Need(victim.Hp<before,id+"没有命中并扣血");
                if(prototype=="mage_fireball")Need(room.Monsters.Count(m=>m.Hp<100000)>=2,"火球范围命中没有覆盖邻近目标");
            }
            Need(g.CaptureProgress().SkillState.Flights.Count==0,"QA弹道泄露正式战斗检查点");
        }
        Need(!LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config")).Skills.Skills.Keys.Any(id=>id.StartsWith("QA_")),"QA技能进入正式配置");
        string qa=Path.Combine(root,"artifacts/qa/bd-slice-a-config-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(qa,"skill"));
        foreach(string source in Directory.EnumerateFiles(Path.Combine(root,"Idle-CRPG/config/skill"),"*.csv"))File.Copy(source,Path.Combine(qa,"skill",Path.GetFileName(source)));
        string effects=Path.Combine(qa,"skill/effect.csv"),baseline=File.ReadAllText(effects);
        var columns=baseline.Split('\n')[0].TrimEnd('\r').Split(',');
        string Row(params (string Key,string Value)[] values)=>string.Join(',',columns.Select(name=>values.FirstOrDefault(v=>v.Key==name).Value??""))+"\n";
        string hit=Row(("id","QA_FAN_HIT"),("role","qa"),("kind","Damage"),("damage_type","Physical"),("share","1"));
        string Projectile(string count,string spread)=>Row(("id","QA_FAN_SPAWN"),("role","qa"),("kind","SpawnProjectile"),("extent","0"),("speed","8"),("impact_effect","QA_FAN_HIT"),("behavior","HitAllOnPathThenExpire"),("spawn_tick","HoldOnSpawnTick"),("projectile_count",count),("spread_degrees",spread));
        File.WriteAllText(effects,baseline.TrimEnd()+"\n"+hit+Projectile("5","20"));
        var loaded=SkillConfiguration.Load(qa,c.Attributes);Need(loaded.Effects["QA_FAN_SPAWN"].ProjectileCount==5&&loaded.Effects["QA_FAN_SPAWN"].SpreadDegrees==20,"CSV扇形定义未加载");
        foreach(var invalid in new[]{("0","20"),("65","20"),("5","181"),("1","20")})
        {
            File.WriteAllText(effects,baseline.TrimEnd()+"\n"+hit+Projectile(invalid.Item1,invalid.Item2));
            bool rejected=false;try{SkillConfiguration.Load(qa,c.Attributes);}catch(Exception error)when(error is InvalidDataException or CsvConfigurationException){rejected=true;}Need(rejected,"非法扇形弹数/夹角未拒绝");
        }
        File.WriteAllText(effects,baseline.TrimEnd()+"\n"+Row(("id","QA_MIXED"),("role","qa"),("kind","Damage"),("damage_type","Physical"),("share","1"),("damage_parts","Physical:0.6;Fire:0.4")));
        Need(SkillConfiguration.Load(qa,c.Attributes).Effects["QA_MIXED"].Portions?.Length==2,"混合分量CSV未加载");
        File.WriteAllText(effects,File.ReadAllText(effects).Replace("Physical:0.6;Fire:0.4","Physical:0.6;Fire:0.6"));
        bool invalidParts=false;try{SkillConfiguration.Load(qa,c.Attributes);}catch(Exception error)when(error is InvalidDataException or CsvConfigurationException){invalidParts=true;}Need(invalidParts,"混合权重和非1未拒绝");
        Console.WriteLine("PASS: isolated A-slice bash/smite/fireball/fan arrows, per-arrow repeated-target hits, cast provenance, launch snapshots/replay and no production loadout injection");
    }
}
