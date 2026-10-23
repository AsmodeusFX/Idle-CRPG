using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

/// <summary>P1-4 角色自动技能 AI：按 AI01–AI30 的语义，在真实 PlayerPolicy/SkillRuntime 路径上做隔离检查。</summary>
public static class AutoSkillAiChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("AUTO_SKILL: "+why);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));

        GameSession Battle(Config config,params string[] ids)
        {
            var game=new GameSession(config,932,ids);
            for(int i=0;i<10000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            var room=game.Floor.Rooms[game.CurrentRoom];
            room.HalfWidth=room.HalfHeight=5;
            var state=game.Capture().SkillState;
            state.Targeting=new();state.Actions.Clear();state.Pending.Clear();state.Buffs.Clear();state.Flights.Clear();state.Corpses.Clear();
            for(int i=0;i<game.Heroes.Count;i++)
            {
                var h=game.Heroes[i];h.Position=room.Center+new Vector2(i*.3f,0);h.Hp=h.MaxHp=100000;
                h.Attack=10;h.Defense=0;h.Range=1.5f;h.Timer=999;h.SkillIds=Array.Empty<string>();h.StartingSkillGranted=true;
            }
            foreach(var m in room.Monsters)
            {
                m.Position=room.Center+new Vector2(1f,0);m.Hp=m.MaxHp=100000;m.Defense=0;
                m.Attack=0;m.Range=.2f;m.Timer=999;m.Speed=.0001f;
            }
            // 保证至少三只怪：单怪被击杀会清房并重置技能现场，无法验证“取消后无冷却”。
            while(room.Monsters.Count<3)
            {
                var extra=room.Monsters[0].Copy();
                extra.Position=room.Center+new Vector2(1f,.4f*room.Monsters.Count);extra.Hp=extra.MaxHp=100000;
                room.Monsters.Add(extra);
            }
            return game;
        }
        void Tick(GameSession g,int n=1){for(int i=0;i<n;i++)g.Tick(.05f);}
        SkillActionState ActionOf(GameSession g,int owner)=>g.Capture().SkillState.Actions.FirstOrDefault(a=>a.Owner==owner)!;
        string Diag(GameSession g)=>string.Join("\n",g.SkillAiDiagnostics());

        // AI01：槽序内首个可用技能被选中，Runtime 接受后进入既有前摇。
        var g=Battle(c,"warrior","mage");
        var room=g.Floor.Rooms[g.CurrentRoom];
        g.Heroes[0].SkillIds=new[]{"barb_bash"};
        Tick(g);
        var action=ActionOf(g,0);
        Need(action.Active=="barb_bash","AI01 可用技能未产生施法请求");
        Need(Diag(g).Contains("选择 猛击"),"AI01 缺少技能选择诊断");

        // AI02：最高优先技能冷却中时跳过并选择后续技能，不修改原冷却。
        g=Battle(c,"warrior","mage");
        g.Heroes[0].SkillIds=new[]{"mage_fireball","barb_bash"};
        var preset=new SkillActionState{Owner=0};preset.Cooldowns["mage_fireball"]=5;
        g.Capture().SkillState.Actions.Add(preset);
        Tick(g);
        action=ActionOf(g,0);
        Need(action.Active=="barb_bash","AI02 未跳过冷却中的高优先技能");
        Need(action.Cooldowns["mage_fireball"]>4.9&&action.Cooldowns["mage_fireball"]<=5,"AI02 改写了既有冷却");
        Need(Diag(g).Contains("冷却中"),"AI02 缺少冷却跳过诊断");

        // AI03：全部技能不可用时普通攻击兜底，不永久等待。
        g=Battle(c,"warrior","mage");
        g.Heroes[0].SkillIds=new[]{"barb_bash"};
        var groom=g.Floor.Rooms[g.CurrentRoom];
        var target=groom.Monsters[0];target.Position=g.Heroes[0].Position+new Vector2(1f,0);
        g.Heroes[0].Timer=0;
        float hp=target.Hp;
        Tick(g,12);
        Need(target.Hp<hp,"AI03 无可用技能时未以普通攻击兜底");

        // AI04：技能与普攻都在射程外时按既有移动追击，不凭空造成伤害。
        g=Battle(c,"warrior","mage");
        g.Heroes[0].SkillIds=new[]{"mage_fireball"};
        g.Heroes[0].Range=1.5f;
        g.Heroes[0].Position=g.Floor.Rooms[g.CurrentRoom].Center+new Vector2(-4.4f,-4.4f);
        foreach(var m in g.Floor.Rooms[g.CurrentRoom].Monsters)m.Position=g.Floor.Rooms[g.CurrentRoom].Center+new Vector2(4.4f,4.4f);
        var before=g.Heroes[0].Position;float farHp=g.Floor.Rooms[g.CurrentRoom].Monsters[0].Hp;
        Tick(g,4);
        Need(g.Heroes[0].Position!=before,"AI04 射程外未移动追击");
        Need(Math.Abs(g.Floor.Rooms[g.CurrentRoom].Monsters[0].Hp-farHp)<.0001f,"AI04 射程外造成了伤害");

        // AI05：忙 Recovery 时不抢断，也不额外普攻。
        g=Battle(c,"warrior","mage");
        g.Heroes[0].SkillIds=new[]{"barb_bash"};
        var busy=new SkillActionState{Owner=0,Recovery=.4};g.Capture().SkillState.Actions.Add(busy);
        g.Heroes[0].Timer=0;var busyPos=g.Heroes[0].Position;float busyHp=g.Floor.Rooms[g.CurrentRoom].Monsters[0].Hp;
        Tick(g,2);
        Need(busy.Active==""&&g.Heroes[0].Position==busyPos,"AI05 恢复期间被抢断动作");
        Need(Math.Abs(g.Floor.Rooms[g.CurrentRoom].Monsters[0].Hp-busyHp)<.0001f,"AI05 恢复期间仍输出普攻");

        // AI07：冷却只在 Commit 启动一次，前摇期间不重复计时。
        g=Battle(c,"warrior","mage");
        g.Heroes[0].SkillIds=new[]{"barb_bash"};
        Tick(g);
        Need(Math.Abs(ActionOf(g,0).Cooldowns.GetValueOrDefault("barb_bash"))<1e-9,"AI07 前摇阶段就启动了冷却");
        Tick(g,3);
        double cd=ActionOf(g,0).Cooldowns.GetValueOrDefault("barb_bash");
        Need(Math.Abs(cd-3.0)<1e-6,"AI07 冷却未在 Commit 按配置启动");
        Tick(g,3);
        double cdLater=ActionOf(g,0).Cooldowns.GetValueOrDefault("barb_bash");
        Need(cdLater<cd&&cdLater>0,"AI07 冷却被重复启动或未按时递减");

        // AI08：单体目标在 Commit 前死亡时取消，无冷却、无后续施法。
        g=Battle(c,"warrior","mage");
        g.Heroes[0].SkillIds=new[]{"barb_bash"};
        var bashTarget=g.Floor.Rooms[g.CurrentRoom].Monsters[0];
        bashTarget.Position=g.Heroes[0].Position+new Vector2(1f,0);
        Tick(g);
        Need(ActionOf(g,0).Active=="barb_bash","AI08 用例前提：未进入前摇");
        bashTarget.Hp=0;
        // 其余怪物移到近战射程外，避免取消后同一逻辑步内立刻改选新目标，掩盖“无冷却”的事实。
        foreach(var other in g.Floor.Rooms[g.CurrentRoom].Monsters.Where(m=>!ReferenceEquals(m,bashTarget)))
            other.Position=g.Floor.Rooms[g.CurrentRoom].Center+new Vector2(4.4f,4.4f);
        for(int i=0;i<6&&ActionOf(g,0).Active=="barb_bash";i++)Tick(g);
        Need(ActionOf(g,0).Active==""&&!ActionOf(g,0).Cooldowns.ContainsKey("barb_bash"),"AI08 目标失效仍提交或扣冷却");

        // AI13/AI14：附近无近战威胁时不空放防御技能；威胁进入范围后才进入候选。
        var nova=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        nova.Skills.Skills["mage_fireball"]=nova.Skills.Skills["mage_fireball"] with {Condition="nearby_melee"};
        g=Battle(nova,"warrior","mage");
        g.Heroes[0].SkillIds=new[]{"mage_fireball"};
        foreach(var m in g.Floor.Rooms[g.CurrentRoom].Monsters){m.Ranged=true;m.Position=g.Heroes[0].Position+new Vector2(1f,0);}
        Tick(g);
        Need(ActionOf(g,0).Active!="mage_fireball","AI13 无近战威胁仍空放条件技能");
        Need(!ActionOf(g,0).Cooldowns.ContainsKey("mage_fireball"),"AI13 条件不满足仍进入冷却");
        g.Floor.Rooms[g.CurrentRoom].Monsters[0].Ranged=false;
        Tick(g);
        Need(ActionOf(g,0).Active=="mage_fireball","AI14 近战威胁进入范围后未进入候选");

        // AI15：主动光环已维持时不再每 Tick 重施。
        g=Battle(c,"warrior","priest");
        g.Heroes[0].SkillIds=new[]{"pal_prayer"};
        Tick(g,5);
        Need(g.Heroes[0].ActiveAuraSkills.Contains("pal_prayer"),"AI15 光环未激活");
        Tick(g,40);
        Need(g.Heroes[0].ActiveAuraSkills.Count(id=>id=="pal_prayer")==1,"AI15 光环被反复重开");
        Need(ActionOf(g,0).Active!="pal_prayer","AI15 光环维持期间仍在施法");
        Need(Diag(g).Split("选择 祈祷光环").Length-1==1,"AI15 光环被重复选择施放");

        // AI18/AI19：不同主要诅咒可并存；已被同种覆盖时不再重复施放。
        g=Battle(c,"warrior","necromancer");
        g.Heroes[0].SkillIds=new[]{"nec_amplify_damage","nec_lower_resist"};
        var curseRoom=g.Floor.Rooms[g.CurrentRoom];
        g.Heroes[0].Position=curseRoom.Center+new Vector2(-4f,-4f);
        // 只留一只在诅咒射程内，避免第二种诅咒落到另一只怪身上让共存断言失去意义。
        foreach(var m in curseRoom.Monsters)m.Position=curseRoom.Center+new Vector2(4.4f,4.4f);
        var cursed=curseRoom.Monsters[0];
        cursed.Position=g.Heroes[0].Position+new Vector2(3f,0);
        Tick(g);Need(ActionOf(g,0).Active=="nec_amplify_damage","AI18 首选诅咒未按槽序选择");
        Tick(g,5);
        Tick(g);Need(ActionOf(g,0).Active=="nec_lower_resist","AI19 第二种诅咒被错误互斥");
        Tick(g,5);
        int cursedId=g.Heroes.Count+g.Floor.Rooms[g.CurrentRoom].Monsters.IndexOf(cursed);
        int curses=g.Capture().SkillState.Buffs.Where(b=>b.Target==cursedId).Select(b=>b.Id).Distinct().Count();
        Need(curses>=2,"AI18 两种不同诅咒未同时存在");
        Tick(g,20);
        Need(ActionOf(g,0).Active=="","AI19 已覆盖同种诅咒时仍重复施放");
        Need(Diag(g).Split("选择 伤害加深").Length-1==1,"AI19 同种诅咒被重复施放");

        // AI20：召唤名额已满时不发起施法，也不消耗冷却（本切片新增的AI侧资格）。
        g=Battle(c,"warrior","necromancer");
        var owner=g.Heroes[0];owner.SkillIds=new[]{"nec_raise_skeleton"};
        for(int i=0;i<3;i++)g.Summons.Add(new Actor{
            SpecId="summon:nec_skeleton",Name="骷髅",Level=1,Hp=1,MaxHp=1,Attack=1,Defense=0,Speed=2.5f,Range=.9f,Cooldown=3,Timer=999,
            SummonDefinitionId="nec_skeleton",SummonSkill="nec_raise_skeleton",SummonOwner=0,
            Position=owner.Position+new Vector2(.3f*i,0)});
        Tick(g,2);
        Need(ActionOf(g,0).Active==""&&!ActionOf(g,0).Cooldowns.ContainsKey("nec_raise_skeleton"),"AI20 召唤已满仍施法并消耗冷却");
        Need(Diag(g).Contains("召唤已满"),"AI20 缺少召唤已满诊断");

        // AI21：名额未满时可请求补召，不需要尸体，也不产生尸体。
        g=Battle(c,"warrior","necromancer");
        owner=g.Heroes[0];owner.SkillIds=new[]{"nec_raise_skeleton"};
        Tick(g);
        Need(ActionOf(g,0).Active=="nec_raise_skeleton","AI21 缺员时未请求补召");
        Tick(g,6);
        Need(g.Summons.Count(s=>s.Alive)>=1,"AI21 补召未在 Commit 后生成召唤物");
        Need(g.Capture().SkillState.Corpses.Count==0,"AI21 基础骷髅错误消耗尸体");

        // AI23：场上没有合法尸体时尸爆不入候选，普通行为继续。
        g=Battle(c,"warrior","necromancer");
        g.Heroes[0].SkillIds=new[]{"nec_corpse_explosion"};
        var plain=g.Floor.Rooms[g.CurrentRoom].Monsters[0];
        plain.Position=g.Heroes[0].Position+new Vector2(1f,0);
        g.Heroes[0].Timer=0;float plainHp=plain.Hp;
        Tick(g,12);
        Need(ActionOf(g,0).Active!="nec_corpse_explosion"&&!ActionOf(g,0).Cooldowns.ContainsKey("nec_corpse_explosion"),"AI23 无尸体仍施放尸爆");
        Need(plain.Hp<plainHp,"AI23 无尸体时未回到普通攻击");

        // AI29：同实例多英雄各自独立施法与冷却，状态不串。
        g=Battle(c,"warrior","mage");
        g.Heroes[0].SkillIds=new[]{"barb_bash"};
        g.Heroes[1].SkillIds=new[]{"mage_fireball"};
        g.Heroes[1].Position=g.Floor.Rooms[g.CurrentRoom].Center+new Vector2(-2f,0);
        Tick(g);
        Need(ActionOf(g,0).Active=="barb_bash"&&ActionOf(g,1).Active=="mage_fireball","AI29 多英雄动作状态互相串扰");

        // AI30：同一配置与输入重放时，决策与战斗事实一致。
        string Replay()
        {
            var replay=Battle(c,"warrior","mage");
            replay.Heroes[0].SkillIds=new[]{"barb_bash"};
            replay.Heroes[1].SkillIds=new[]{"mage_fireball"};
            Tick(replay,40);
            return SaveStore.Encode(new(){Session=replay.Capture()});
        }
        Need(Replay()==Replay(),"AI30 相同输入重放结果不一致");

        // 诊断接入 GM：技能AI记录与入场/接敌诊断同一只读窗口。
        Need(Diag(g).Length>0&&string.Join("\n",g.EngagementDiagnostics()).Contains("技能AI决策"),"GM缺少技能AI决策诊断");
        Console.WriteLine("PASS: slot-order skill selection, cooldown skip without mutation, basic fallback, out-of-range pursuit, busy recovery, commit-only cooldown, cancel-before-commit, nearby-threat condition, aura no-recast, multi-curse coexistence and coverage skip, summon cap skip, summon refill without corpse, no-corpse skip, multi-hero independence, deterministic replay and bounded GM diagnostics");
    }
}
