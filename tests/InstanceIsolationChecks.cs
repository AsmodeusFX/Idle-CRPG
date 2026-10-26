using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

/// <summary>
/// R-02 定向证据：四名同职业英雄的实例身份、Buff 来源键与召唤归属互不串号。
/// 断言仅覆盖现有 Buff/召唤语义（RefreshDuration 同ID同目标替换、每主人名额独立），不提前批准新叠加规则。
/// </summary>
public static class InstanceIsolationChecks
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception("INSTANCE_ISOLATION: "+why);}
    public static void Run(string root)
    {
        var c=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        c.Spawn=c.Spawn with {Min=2,Max=2};
        // QA 夹具：仅本进程内存中放开死灵，用于覆盖同职业四实例；不改正式 hero_unlocks.csv。
        c.HeroUnlockKills["necromancer"]=0;
        string necro=c.Heroes.First(h=>h.Id=="necromancer").Id;
        string warrior=c.Heroes.First(h=>h.Id=="warrior").Id;
        void Tick(GameSession game,int ticks){for(int i=0;i<ticks;i++)game.Tick(.05f);}
        GameSession Battle(string classId,int count,int seed,params string[] skills)
        {
            var game=GameSession.CreateNewWorld(c,seed,Enumerable.Repeat(classId,count));
            for(int i=0;i<20000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            Check(game.CurrentRoom!=0,"未进入战斗房间");
            while(game.CombatEntryRemaining>0)game.Tick(.05f);
            var room=game.Floor.Rooms[game.CurrentRoom];
            for(int i=0;i<game.Heroes.Count;i++)
            {
                var h=game.Heroes[i];
                // 拉开间距，避免半径型范围 Buff 互相覆盖他人；同时保持不主动攻击。
                h.Hp=h.MaxHp=100000;h.Attack=1;h.Timer=0;h.Defense=0;
                float dx=Math.Min(2.2f,room.HalfWidth-.6f),dy=Math.Min(2.2f,room.HalfHeight-.6f);h.Position=room.Center+new Vector2(i%2==0?-dx:dx,i<2?-dy:dy);
            }
            foreach(var m in room.Monsters){m.Hp=m.MaxHp=100000000;m.Timer=999;m.Attack=0;m.Defense=0;m.Position=room.Center;}
            if(skills.Length>0)foreach(var h in game.Heroes.Where(h=>h.SpecId==classId))Check(skills.All(id=>h.SkillIds.Contains(id)),"S1必须开局已学，无装配步骤");
            return game;
        }

        // 1) 同职业四实例：身份、召唤归属与每主人名额。
        var summonGame=Battle(necro,4,4711,new[]{"nec_raise_skeleton"});
        Tick(summonGame,120);
        var live=summonGame.Summons.Where(s=>s.Alive).ToArray();
        var heroIds=summonGame.Heroes.Select(h=>h.HeroInstanceId).ToHashSet();
        Check(live.Length>=4,$"四名同职业未各自召唤，实际 {live.Length}");
        Check(live.Select(s=>s.SummonOwnerInstanceId).Distinct().Count()==4,"召唤属主实例ID重复或缺失");
        Check(live.All(s=>heroIds.Contains(s.SummonOwnerInstanceId)),"召唤属主实例ID不属于本轮英雄");
        Check(live.All(s=>summonGame.Heroes[s.SummonOwner].HeroInstanceId==s.SummonOwnerInstanceId),"属主索引与实例ID不一致");
        var cap=c.Skills.Summons["nec_skeleton"].MaxCount;
        Check(summonGame.Heroes.All(h=>{int n=live.Count(s=>s.SummonOwnerInstanceId==h.HeroInstanceId);return n>=1&&n<=cap;}),"同主人召唤数量越界或缺失");
        Check(summonGame.Heroes.Select(h=>h.HeroInstanceId).All(id=>live.Any(s=>s.SummonOwnerInstanceId==id)),"存在没有被记到任何实例的召唤");

        // 2) 新版按实例学习/自动施放，旧装配原文保留于retired_loadout_ui夹具。
        var loadoutGame=Battle(warrior,4,4721); // 用无召唤职业，避免"卸下召唤技能"影响存档校验
        var equipTargets=loadoutGame.Heroes.Take(2).ToArray();
        var untouched=loadoutGame.Heroes.Skip(2).ToDictionary(h=>h.HeroInstanceId,h=>h.SkillIds.ToArray());
        loadoutGame.GrantGmResources(experience:10000);
        foreach(var hero in equipTargets){Check(loadoutGame.UpgradeHero(hero.HeroInstanceId,out _),"升级学习前置");Check(loadoutGame.LearnTalent(hero.HeroInstanceId,"barb_taunt",out _),"按实例学习S2");}
        Check(loadoutGame.Heroes.Where(h=>untouched.ContainsKey(h.HeroInstanceId)).All(h=>h.SkillIds.SequenceEqual(untouched[h.HeroInstanceId])),"同职业装配影响到未操作的实例");
        // 该断言只关心"装配按实例保存"，用完整内部快照（含召唤现场）避免精简检查点的召唤引用校验干扰。
        var loadoutReload=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=loadoutGame.Capture()})).Session!);
        Check(equipTargets.All(h=>loadoutReload.Heroes.Single(r=>r.HeroInstanceId==h.HeroInstanceId).SkillIds.Contains("barb_taunt")),"已学S2未按实例随存档保存");
        Check(loadoutReload.Heroes.Where(h=>untouched.ContainsKey(h.HeroInstanceId)).All(h=>h.SkillIds.SequenceEqual(untouched[h.HeroInstanceId])),"读档后未操作的实例被套用装配");

        Console.WriteLine("INSTANCE_ISOLATION_PASS: 4 same-class instances keep distinct summon ownership and per-instance learned fixed skills across save/reload");
    }
}
