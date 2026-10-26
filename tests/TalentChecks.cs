using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class TalentChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("TALENT: "+why);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        var auto=new GameSession(c,8,new[]{"warrior","mage"});var ah=auto.Heroes[0];
        Need(auto.LearnTalent("warrior","barb_bash",out _)&&ah.SkillIds.SequenceEqual(new[]{"barb_bash"}),"首次学习主动未自动填空槽");
        auto.GrantGmResources(10000,10000);TestPlayerActions.UpgradeWithClicks(auto);auto.SetTalentLoadout("warrior",Array.Empty<string>(),out _);auto.LearnTalent("warrior","barb_bash",out _);
        Need(ah.SkillIds.Length==0,"升级技能重新装配了手动卸下技能");
        auto.LearnTalent("warrior","barb_mastery",out _);Need(ah.SkillIds.Length==0,"被动占用主动槽");
        auto.LearnTalent("warrior","barb_shout",out _);Need(ah.SkillIds.SequenceEqual(new[]{"barb_shout"}),"第二个新技能未填入空槽");
        for(int i=0;i<4;i++)auto.LearnTalent("warrior","barb_bash",out _);
        auto.LearnTalent("warrior","barb_leap",out _);Need(ah.SkillIds.SequenceEqual(new[]{"barb_shout"}),"满槽学习挤掉原有技能");
        auto.BuyStar("s_root",out _);auto.BuyStar("s_skill_1",out _);
        for(int i=0;i<10000&&auto.CurrentRoom==0;i++)auto.Tick(.05f);
        Need(auto.LearnTalent("warrior","barb_whirlwind",out _)&&ah.SkillIds.SequenceEqual(new[]{"barb_shout","barb_whirlwind"})&&Math.Abs(auto.SkillCooldown(ah,"barb_whirlwind")-5)<.001,"战斗首次学习自动装配绕过完整冷却");
        var g=new GameSession(c,785,new[]{"warrior","mage"});var h=g.Heroes[0];
        Need(g.TalentPoints(h)==1&&g.ActiveSkillSlots==1,"初始点数/技能槽");
        Need(!g.SetTalentLoadout("warrior",new[]{"barb_bash"},out _),"未学习主动技能可装配");
        Need(g.LearnTalent("warrior","barb_mastery",out _)&&g.TalentPoints(h)==0,"首级点数消费");
        Need(!g.LearnTalent("warrior","barb_attack",out _)&&!g.LearnTalent("mage","barb_attack",out _),"不足点数/职业门禁");
        g.GrantGmResources(100000,100000);TestPlayerActions.UpgradeWithClicks(g);
        Need(g.TalentPoints(h)==h.Level-1,"等级补齐点数");
        Need(!g.LearnTalent("warrior","barb_leap",out _),"其他系投入解锁了战斗技能");
        for(int i=0;i<5;i++)Need(g.LearnTalent("warrior","barb_bash",out _),"基础猛击投入");
        Need(g.LearnTalent("warrior","barb_leap",out _)&&g.LearnTalent("warrior","barb_whirlwind",out _),"同系5点门禁");
        Need(g.TreeInvestment(h,2)==7&&g.TreeInvestment(h,1)==1,"各系投入独立");
        int spent=g.TreeInvestment(h,2);
        g.SetTalentBonuses("warrior","test-item",new[]{new TalentBonus("test-item","node","barb_bash",30),new TalentBonus("test-item","tree","2",2),new TalentBonus("test-item","all","",3)});
        Need(g.TalentLevel(h,"barb_bash")==40&&g.TalentLevel(h,"barb_shout")==0&&g.TreeInvestment(h,2)==spent,"额外等级突破/未学不激活/不计投入");
        g.SetTalentBonuses("warrior","test-item",new[]{new TalentBonus("test-item","node","barb_bash",1)});
        Need(g.TalentLevel(h,"barb_bash")==6,"同来源更新重复叠加");
        g.SetTalentBonuses("warrior","test-item",Array.Empty<TalentBonus>());Need(g.TalentLevel(h,"barb_bash")==5,"卸装贡献未撤销");
        for(int i=0;i<4;i++)g.LearnTalent("warrior","barb_mastery",out _);
        for(int i=0;i<5;i++)Need(g.LearnTalent("warrior","barb_defense",out _),"防御强化投入");
        Need(g.LearnTalent("warrior","barb_focus",out _),"单级被动学习");
        g.SetTalentBonuses("warrior","test-all",new[]{new TalentBonus("test-all","all","",10)});
        Need(g.TalentLevel(h,"barb_focus")==1&&!g.LearnTalent("warrior","barb_focus",out _),"单级节点受到额外等级或重复学习");
        Need(g.HeroTalentAttribute(h,"atk",h.Attack)>h.Attack&&g.HeroTalentAttribute(h,"armor",h.Defense)>h.Defense,"被动未生效");
        Need(g.BuyStar("s_root",out _),"星图根");for(int i=1;i<=4;i++)Need(g.BuyStar("s_skill_"+i,out _),"技能槽星图解锁");
        Need(g.ActiveSkillSlots==5,"全队主动槽扩展");
        Need(!g.SetTalentLoadout("warrior",new[]{"barb_bash","barb_bash"},out _)&&!g.SetTalentLoadout("warrior",new[]{"barb_mastery"},out _),"重复/被动装配");
        g.SetTalentLoadout("warrior",Array.Empty<string>(),out _);g.SetGmBattle(new(Invincible:true));
        for(int i=0;i<10000&&g.CurrentRoom==0;i++)g.Tick(.05f);
        var room=g.Floor.Rooms[g.CurrentRoom];Need(room.Monsters.Any(m=>m.Alive),"战斗测试房间");
        // 本段验证技能换入/移动/施放时间，隔离进房展开；展开规则由CombatTargetingChecks单独覆盖。
        g.Capture().SkillState.Targeting=new();
        foreach(var m in room.Monsters){m.Hp=m.MaxHp=100000;m.Position=room.Center+new Vector2(1,0);m.Attack=1;m.Range=100;m.Timer=999;}
        h.Position=room.Center;h.Hp=h.MaxHp=100000;
        Need(g.SetTalentLoadout("warrior",new[]{"barb_bash"},out _)&&Math.Abs(g.SkillCooldown(h,"barb_bash")-3)<.001,"战斗换入未完整冷却");
        g.Tick(.05f);Need(g.Capture().SkillState.Actions.All(a=>a.Owner!=0||a.Active!="barb_bash"),"换入技能立刻释放");
        g.SetTalentLoadout("warrior",Array.Empty<string>(),out _);g.SetTalentLoadout("warrior",new[]{"barb_bash"},out _);
        Need(Math.Abs(g.SkillCooldown(h,"barb_bash")-3)<.001,"反复换入绕过冷却");
        GameSession Reopen(GameSession game,bool progress)=>GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=progress?game.CaptureProgress():game.Capture()})).Session!);
        var reopened=Reopen(g,true);Need(reopened.TalentLevel(reopened.Heroes[0],"barb_bash")==15&&reopened.Heroes[0].SkillIds.SequenceEqual(new[]{"barb_bash"})&&Math.Abs(reopened.SkillCooldown(reopened.Heroes[0],"barb_bash")-3)<.001,"房间重开丢失成长/装配/换入冷却");
        Need(reopened.ActiveBuffCount==0&&reopened.ActiveFlightCount==0,"正式存档保留战斗现场");
        for(int i=0;i<66;i++)g.Tick(.05f);
        Need(g.Capture().SkillState.Actions.Any(a=>a.Owner==0&&a.Cooldowns.ContainsKey("barb_bash")),"猛击未自动施放");
        Need(g.SetTalentLoadout("warrior",new[]{"barb_leap","barb_whirlwind"},out _),"范围技能装配");
        h.Position=room.Center-new Vector2(3,0);var before=h.Position;
        for(int i=0;i<160;i++)g.Tick(.05f);
        Need(Vector2.Distance(h.Position,before)>1&&g.Capture().SkillState.Actions.Any(a=>a.Owner==0&&a.Cooldowns.ContainsKey("barb_leap")&&a.Cooldowns.ContainsKey("barb_whirlwind")),"跳斩连续移动/旋风释放："+before+" -> "+h.Position+"; "+string.Join(";",g.Capture().SkillState.Actions.Where(a=>a.Owner==0).Select(a=>string.Join(',',a.Cooldowns.Keys))));
        for(int i=0;i<5;i++)Need(g.LearnTalent("warrior","barb_shout",out _),"战斗吼叫投入");
        Need(g.LearnTalent("warrior","barb_weaken",out _),"挫锐吼叫学习");
        g.SetTalentLoadout("warrior",new[]{"barb_shout","barb_weaken"},out _);
        for(int i=0;i<290;i++)g.Tick(.05f);
        Need(g.Capture().SkillState.Buffs.Any(b=>b.Id=="talent_battle_shout")&&g.Capture().SkillState.Buffs.Any(b=>b.Id=="talent_weaken"),"吼叫增益/减益未生效");
        var copied=Reopen(g,false);copied.SetGmBattle(g.Gm);for(int i=0;i<10;i++){g.Tick(.05f);copied.Tick(.05f);}
        Need(SaveStore.Encode(new(){Session=g.Capture()})==SaveStore.Encode(new(){Session=copied.Capture()}),"天赋战斗完整快照不确定");
        var bad=g.CaptureProgress();bad.Heroes[0].Talents["barb_bash"]=999;bool rejected=false;try{GameSession.Restore(c,bad);}catch(InvalidDataException){rejected=true;}Need(rejected,"非法学习等级存档未拒绝");
        var formal=LegacySkillConfig.Load(Path.Combine(root,"Idle-CRPG/config"));var town=GameSession.CreateNewWorld(formal,3,"warrior");town.GrantGmResources(10000,5000);TestPlayerActions.UpgradeWithClicks(town);
        var th=town.Heroes[0];town.LearnTalent("warrior","barb_mastery",out _);town.LearnTalent("warrior","barb_bash",out _);town.SetTalentLoadout("warrior",new[]{"barb_bash"},out _);
        Need(!town.ResetTalents("warrior",out _),"未解锁洗点可重置");town.BuyStar("s_root",out _);town.BuyStar("s_respec",out _);town.BuyStar("s_respec_discount",out _);
        long cost=town.TalentResetCost(th);Need(cost==36,"洗点原型费用与折扣向上取整");
        town.SetGmBattle(new(InstantDungeonClear:true));for(int i=0;i<50000&&!(town.World!.Stage==CampaignStage.Village&&town.World.PortalPhase==PortalPhase.None);i++)town.Tick(.05f);
        long gold=town.Gold;Need(town.ResetTalents("warrior",out _)&&town.Gold==gold-cost&&th.Talents.Count==0&&th.SkillIds.Length==0&&town.TalentPoints(th)==th.Level,"洗点返还/卸技/扣费");
        var path=Path.Combine(root,"artifacts/qa/talent-save-"+Guid.NewGuid().ToString("N"),"progress.json");var store=new SaveStore(path);store.Save(town,1,false,0,-1);town.LearnTalent("warrior","barb_bash",out _);store.Save(town,1,false,0,-1);File.WriteAllText(path,"invalid");Need(store.Load(formal)?.Session?.Heroes[0].Talents.Count==0,"天赋备份恢复");
        var qa=Path.Combine(root,"artifacts/qa/talent-config-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(qa,"skill"));Directory.CreateDirectory(Path.Combine(qa,"system"));
        string nodes=File.ReadAllText(Path.Combine(LegacySkillConfig.DirectoryFor(Path.Combine(root,"Idle-CRPG/config")),"skill/talent.csv"));string rules=File.ReadAllText(Path.Combine(root,"Idle-CRPG/config/system/config.csv"));
        void Reject(string nodeText,string ruleText,string why)
        {
            File.WriteAllText(Path.Combine(qa,"skill/talent.csv"),nodeText);File.WriteAllText(Path.Combine(qa,"system/config.csv"),ruleText);
            bool failed=false;try{TalentConfiguration.Load(qa,c);}catch(InvalidDataException){failed=true;}Need(failed,why);
        }
        Reject(nodes.Replace("0,1,战斗吼叫","0,3,战斗吼叫"),rules,"超出三列布局未拒绝");
        Reject(nodes.Replace("active,,0,-1,shout","active,barb_weaken,1,-1,shout"),rules,"循环/向后前置未拒绝");
        Reject(nodes,rules.Replace("active_skill_max_slots,5","active_skill_max_slots,6"),"超过五槽未拒绝");
        Reject(nodes.Replace("10,1,active,,0,-1,shout","10,2,active,,0,-1,shout"),rules,"非法加成开关未拒绝");
        Console.WriteLine("PASS: talent points, independent trees/prerequisites, bonus scopes/caps, passive stats, five active skills, swap cooldown/checkpoint, exact combat snapshots, star slots and paid discounted respec/backup");
    }
}
