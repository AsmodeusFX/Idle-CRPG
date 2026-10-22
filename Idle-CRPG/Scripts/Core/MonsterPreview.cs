using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
namespace IdleCrpg.Core;

/// <summary>GM隔离试场原型；配置只供显式预览使用，不注册正式刷怪池。</summary>
public record MonsterPreviewDefinition(string Id,string Name,string Template,float Hp,float Attack,float Speed,float Range,float Cooldown,bool Ranged,MonsterTargetPolicy Policy,string Effect,int SkillCooldown,int Windup,int Recovery,string Color,string Symbol,int EscortCount);
public sealed partial class Config
{
    public List<MonsterPreviewDefinition> MonsterPreviews=new();
    /// <summary>加载GM预览内容字典，数值全部为测试占位。</summary>
    public void LoadMonsterPreviews(string directory)
    {
        string path=Path.Combine(directory,"monster/preview_archetypes.csv");
        var table=CsvTable.Load(path,File.ReadAllText(path),"id","name","template","hp","attack","speed","range","cooldown","ranged","policy","effect","skill_cooldown_ticks","windup_ticks","recovery_ticks","color","symbol","escort_count");
        MonsterPreviews.Clear();
        foreach(var r in table.Rows)
        {
            var d=new MonsterPreviewDefinition(r.Id,r.Required("name"),r.Required("template"),(float)r.Number("hp"),(float)r.Number("attack"),(float)r.Number("speed"),(float)r.Number("range"),(float)r.Number("cooldown"),r.Boolean("ranged"),r.Enum<MonsterTargetPolicy>("policy"),r["effect"],r.Integer("skill_cooldown_ticks"),r.Integer("windup_ticks"),r.Integer("recovery_ticks"),r.Required("color"),r.Required("symbol"),r.Integer("escort_count"));
            if(MonsterPreviews.Any(x=>x.Id==d.Id)||!Monsters.Any(x=>x.Id==d.Template)||new[]{d.Hp,d.Attack,d.Speed,d.Range,d.Cooldown}.Any(x=>!float.IsFinite(x)||x<=0)||d.Effect!=""&&!Skills.Effects.ContainsKey(d.Effect)||d.SkillCooldown<0||d.Windup<0||d.Recovery<0||d.EscortCount<0||d.EscortCount+40>1000)throw new InvalidDataException("GM怪物原型无效："+d.Id);
            MonsterPreviews.Add(d);
        }
    }
}
public sealed partial class GameSession
{
    /// <summary>仅独立试场调用；保留真实技能调度，特殊怪群以同一Actor/索敌/奖励实现运行。</summary>
    public void SetPreviewMonsters(string prototype,int count,int level,string directory)
    {
        if(!skillPreviewMode)throw new InvalidOperationException("只能修改独立技能试场");
        if(count<1||count>40||level<1||level>Config.MaxMonsterLevel)throw new ArgumentOutOfRangeException(nameof(count));
        var definition=Config.MonsterPreviews.Single(d=>d.Id==prototype);
        var room=Floor.Rooms[CurrentRoom];room.Monsters.Clear();room.Origins.Clear();room.ContentCleared=false;Summons.Clear();ResetSkillRoom();
        // M07加入普通近战陪同，M08加入受强化目标，单人预览也能观察动态单位与支援。
        int guards=definition.EscortCount;
        for(int i=0;i<count+guards;i++)
        {
            var d=i<guards?Config.MonsterPreviews.Single(x=>x.Id=="M01"):definition;
            var a=Config.Spawn.Create(Config.Monsters.Single(x=>x.Id==d.Template),level,false,new());Config.ApplyCombatProfile(a);
            a.Name=d.Id+" "+d.Name;a.Hp=a.MaxHp=d.Hp;a.Attack=d.Attack;a.Speed=d.Speed;a.Range=d.Range;a.Cooldown=d.Cooldown;a.Ranged=d.Ranged;a.TargetPolicy=d.Policy;
            a.Position=new Vector2(1+(i%4)*.7f,((i/4)-(count+guards)/8f)*.7f);
            if(d.Effect!="")
            {
                string skill="preview_"+d.Id;
                Config.Skills.Skills[skill]=new(skill,"ActiveSkill",d.SkillCooldown,d.Windup,d.Recovery,1,d.Range,new[]{d.Effect},d.Name,d.Id is "M07" or "M08"?"self":"enemy");a.SkillIds=new[]{skill};
            }
            room.Monsters.Add(a);
        }
        Config.EnemySummons.Clear();
        if(prototype=="M07")
        {
            string path=Path.Combine(directory,"monster/preview_enemy_summon.csv");
            var row=CsvTable.Load(path,File.ReadAllText(path),"id","caster_template","skill","child_template","alive_cap","total_cap","persist_after_owner_death","status").Rows.Single();
            Config.EnemySummons[row.Id]=new(row.Id,definition.Template,"preview_M07",row.Required("child_template"),row.Integer("alive_cap"),row.Integer("total_cap"),row.Boolean("persist_after_owner_death"));
            PrepareEnemySummons(Config,room);
            foreach(var child in room.Monsters.Where(a=>a.EnemySummonSource>=0))child.Name="仆从·M01";
        }
        // 五职业中的远程/近战可同场观察猎手，第二名固定为法师；不写玩家编队。
        if(Heroes.Count==1&&Heroes[0].SpecId!="mage")
        {var rear=Actor.From(Config.Heroes.Single(x=>x.Id=="mage"));Config.ApplyCombatProfile(rear);rear.Position=new Vector2(-3,1);Heroes.Add(rear);}
        StartCombatEntry(room,new Vector2(-room.HalfWidth,0));CaptureRoomEntry();
    }
}
