using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class ClassFollowingChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("CLASS_FOLLOW: "+why);}
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        Need(c.Heroes.Single(h=>h.Id=="ranger").Name=="游侠","游侠名称");
        Need(c.Heroes.Single(h=>h.Id=="mage").Name=="法师","法师名称");
        var renamed=new GameSession(c,785,new[]{"mage"}).Capture();renamed.Heroes[0].Name="女法师";
        Need(GameSession.Restore(c,renamed).Heroes[0].Name=="法师","旧存档职业名称未同步");
        foreach(var spec in c.Heroes)
        {
            var nodes=c.Talents.Nodes.Values.Where(n=>n.Hero==spec.Id).ToArray();
            Need(nodes.All(n=>n.Tree is >=0 and <=2)&&nodes.Any(n=>n.Kind=="active")&&nodes.Any(n=>n.Kind=="passive"),"职业三系主动/被动："+spec.Id);
            var g=new GameSession(c,785,new[]{spec.Id});var h=g.Heroes.Single();string starter=c.Talents.StartingSkills[spec.Id];
            Need(h.Talents[starter]==1&&h.SkillIds.SequenceEqual(new[]{starter})&&g.TalentPoints(h)==0&&g.TreeInvestment(h,c.Talents.Nodes[starter].Tree)==1,"初始技能未消耗一级点数："+spec.Id);
            Need(!g.LearnTalent(spec.Id,starter,out _),"一级零剩余点可再升级");
            for(int i=0;i<10000&&g.CurrentRoom==0;i++)g.Tick(.05f);
            var room=g.Floor.Rooms[g.CurrentRoom];h.Position=room.Center-new Vector2(1,0);h.Timer=999;h.Hp=h.MaxHp=100000;
            foreach(var m in room.Monsters){m.Position=room.Center+new Vector2(1,0);m.Hp=m.MaxHp=100000;m.Timer=999;}
            var starterNode=c.Talents.Nodes[starter];
            // 绑定节点按 skill.csv 定义执行；召唤类初始技能不直接造成伤害，以召唤物存在作为生效判据。
            bool bound=starterNode.SkillId!="";
            bool summonStarter=bound&&c.Skills.Skills[starterNode.SkillId].Effects.Any(e=>c.Skills.Effects[e].Kind=="Summon");
            bool projectileStarter=bound?c.Skills.Skills[starterNode.SkillId].Effects.Any(e=>c.Skills.Effects[e].Kind is "SpawnProjectile" or "AbilityCast"):starterNode.Effect=="shot";
            bool projectile=false;for(int i=0;i<40;i++){g.Tick(.05f);projectile|=g.ActiveFlightCount>0;}
            bool acted=room.Monsters.Any(m=>m.Hp<100000)||(summonStarter&&g.Summons.Count>0);
            Need(acted&&g.Capture().SkillState.Actions.Any(a=>a.Cooldowns.ContainsKey(starter)),"初始主动未生效："+spec.Id);
            if(projectileStarter)Need(projectile,"远程初始攻击没有投射物");
        }
        GameSession Reopen(GameSession g)=>GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=g.CaptureProgress()})).Session!);
        var player=GameSession.CreateNewWorld(c,17,"warrior");player.GrantGmResources(10000,0);player.BuyStar("s_root",out _);player.BuyStar("s_respec",out _);
        player.SetGmBattle(new(InstantDungeonClear:true));for(int i=0;i<30000&&!(player.World!.Stage==CampaignStage.Village&&player.World.PortalPhase==PortalPhase.None);i++)player.Tick(.05f);
        Need(player.ResetTalents("warrior",out _)&&player.Heroes[0].Talents.Count==0&&player.Heroes[0].SkillIds.Length==0&&player.TalentPoints(player.Heroes[0])==1,"洗点保留初始技能或没有返还一级点数");
        player=Reopen(player);Need(player.Heroes[0].Talents.Count==0&&player.TalentPoints(player.Heroes[0])==1,"洗点读档自动补回初始技能");
        var old=player.CaptureProgress();old.Heroes[0].StartingSkillGranted=false;var migrated=GameSession.Restore(c,old);Need(migrated.Heroes[0].Talents["barb_bash"]==1&&migrated.TalentPoints(migrated.Heroes[0])==0,"旧角色有空余点未初始化");
        var party=GameSession.CreateWorld(c,20261017,c.Heroes.Take(4).Select(h=>h.Id));party.SetGmBattle(new(Invincible:true));
        bool spread=false,turn=false,gather=false;Vector2 direction=Vector2.Zero;int samples=0;
        var motion=new MotionPresentation();
        for(int t=0;t<12000&&!(spread&&turn&&gather);t++)
        {
            var w=party.World!;bool travel=w.Stage is CampaignStage.TravelDungeon or CampaignStage.TravelVillage&&w.PortalPhase==PortalPhase.None;
            var before=Enumerable.Range(0,4).Select(w.OutdoorPartyPosition).ToArray();var lead=w.Position;motion.BeforeTick(party);party.Tick(.05f);
            if(!travel||w.PortalPhase!=PortalPhase.None)continue;
            var after=Enumerable.Range(0,4).Select(w.OutdoorPartyPosition).ToArray();
            if(w.Stage is CampaignStage.TravelDungeon or CampaignStage.TravelVillage)
            {
                foreach(var pos in after)Need(WorldNavigation.Walkable(party.Config,pos),"跟随角色进入障碍");
                for(int i=0;i<4;i++)Need(Vector2.Distance(before[i],after[i])<=party.Config.TravelSpeed*.05f+.0001f,"跟随角色瞬移/超速");
                var delta=w.Position-lead;if(delta.LengthSquared()>.000001f){var next=Vector2.Normalize(delta);if(direction.LengthSquared()>0&&Vector2.Dot(direction,next)<.99f)turn=true;direction=next;}
                if(after.Distinct().Count()==4){spread=true;samples++;}
                var visual=motion.OutdoorPartyPosition(party,3,.5f);Need(Vector2.Distance(visual,Vector2.Lerp(before[3],after[3],.5f))<.00001f,"队尾未插值");
            }
            if(w.Stage==CampaignStage.Dungeon){Need(after.All(p=>Vector2.Distance(p,w.Position)<.0001f),"队尾未集合即进入地牢");gather=true;}
        }
        Need(spread&&turn&&gather&&samples>0,"缺少队列展开、转弯或入口集合样本");
        for(int t=0;t<20000&&!(party.World!.Stage==CampaignStage.TravelDungeon&&party.World.PortalPhase==PortalPhase.None&&Enumerable.Range(0,4).Select(party.World.OutdoorPartyPosition).Distinct().Count()==4);t++)party.Tick(.05f);
        var copy=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=party.Capture()})).Session!);copy.SetGmBattle(party.Gm);
        Need(Enumerable.Range(0,4).All(i=>party.World!.OutdoorPartyPosition(i)==copy.World!.OutdoorPartyPosition(i)),"读档丢失跟随位置");
        for(int i=0;i<20;i++){party.Tick(.05f);copy.Tick(.05f);}Need(SaveStore.Encode(new(){Session=party.Capture()})==SaveStore.Encode(new(){Session=copy.Capture()}),"跟随完整快照不确定");
        var bad=party.CaptureProgress();bad.World!.OutdoorTrail=new(){new Vector2(float.NaN,0)};bool rejected=false;try{GameSession.Restore(c,bad);}catch(InvalidDataException){rejected=true;}Need(rejected,"非法跟随轨迹未拒绝");
        Console.WriteLine("PASS: all five three-tree classes, typed starting attacks, level-one point consumption, removable starter/save migration, safe leader-trail following, turn/gather/interpolation and exact saves");
    }
}
