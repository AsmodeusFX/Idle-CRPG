using Godot;
using IdleCrpg.Core;
using System;
using System.IO;
using System.Linq;
using V=System.Numerics.Vector2;

public partial class Main
{
    /// <summary>仅--capture显式入口加载TEST_ONLY参数；自动存档隔离在artifacts/qa，不替换正式树。</summary>
    private void PrepareSkillBdCapture()
    {
        string path=ProjectSettings.GlobalizePath("res://../tests/fixtures/skill_bd_slice_a/skill_cases.csv");
        var row=CsvTable.Load(path,File.ReadAllText(path),"id","prototype_id","projectile_count","spread_degrees").Require("QA_RAN_MULTISHOT");
        string id=row.Id,prototype=row.Required("prototype_id");var node=config.Talents.Nodes[prototype];
        var impact=config.Skills.Effects[prototype+"_impact"] with {Id=id+"_impact"};
        var projectile=config.Skills.Effects[prototype+"_projectile"] with {Id=id+"_projectile",Impact=impact.Id,ProjectileCount=row.Integer("projectile_count"),SpreadDegrees=(float)row.Number("spread_degrees")};
        config.Skills.Effects.Add(impact.Id,impact);config.Skills.Effects.Add(projectile.Id,projectile);
        config.Skills.Skills.Add(id,config.Skills.Skills[prototype] with {Id=id,Effects=new[]{projectile.Id},Coefficient=node.Value});
        session=GameSession.CreateWorld(config,785,new[]{"ranger"});
        for(int i=0;i<10000&&(session.World!.Stage!=CampaignStage.Dungeon||session.CurrentRoom==0);i++)session.Tick(.05f);
        var room=session.Floor.Rooms[session.CurrentRoom];var state=session.Capture().SkillState;
        state.Targeting=new();state.Actions.Clear();state.Pending.Clear();state.Buffs.Clear();state.Flights.Clear();room.ContentCleared=false;
        var hero=session.Heroes[0];hero.Position=room.Center-new V(1.7f,0);hero.Hp=hero.MaxHp=10000;hero.Attack=10;hero.Timer=999;hero.Range=100;hero.SkillIds=new[]{id};
        for(int i=0;i<room.Monsters.Count;i++){var m=room.Monsters[i];m.Position=room.Center+new V(1.7f,(i-(room.Monsters.Count-1)/2f)*.45f);m.Hp=m.MaxHp=10000;m.Range=100;m.Attack=1;m.Timer=999;}
        for(int i=0;i<9;i++)session.Tick(.05f);
        var flights=state.Flights.Where(f=>f.Skill==id).ToArray();
        if(flights.Length!=projectile.ProjectileCount||session.TalentProjectiles.Count()!=flights.Length||flights.Select(f=>f.Direction).Distinct().Count()!=flights.Length)throw new InvalidOperationException("A阶段扇形绘制没有对应全部权威弹道");
        bool mini=OS.GetCmdlineUserArgs().Contains("--qa-mini");
        if(mini)GetWindow().Size=new Vector2I(960,600);
        viewMode=0;paused=true;cameraReady=false;motionAlpha=1;captureView=mini?"bd-a-fan-mini":"bd-a-fan";Refresh();TickStarPanels();
        GD.Print($"BD_A_VISUAL_MODEL_OK: {flights.Length} authoritative/drawn arrows; shared cast={flights[0].OriginCast}");
    }

    /// <summary>正式绑定技能的视觉入口：只构造隔离QA战斗现场，不写入配置或玩家进度。</summary>
    private void PrepareSkillBdSkillCapture(string skillId)
    {
        if(!config.Skills.Skills.ContainsKey(skillId))throw new InvalidOperationException("未知视觉技能："+skillId);
        string hero=skillId.StartsWith("nec_")?"necromancer":skillId.StartsWith("ran_")?"ranger":skillId.StartsWith("barb_")?"warrior":skillId.StartsWith("mage_")?"mage":"priest";
        session=GameSession.CreateWorld(config,786,new[]{hero});
        for(int i=0;i<10000&&(session.World!.Stage!=CampaignStage.Dungeon||session.CurrentRoom==0);i++)session.Tick(.05f);
        var room=session.Floor.Rooms[session.CurrentRoom];var state=session.Capture().SkillState;
        state.Targeting=new();state.Actions.Clear();state.Pending.Clear();state.Buffs.Clear();state.Flights.Clear();room.ContentCleared=false;
        // 近战/短程光环需要把QA演员放进实际作用距离，否则逻辑未施放，截图只会是空场景。
        float gap=skillId switch
        {
            "barb_bash" or "paladin_smite"=>1.2f,
            "barb_whirlwind_window"=>2.0f,
            "barb_taunt" or "pal_conviction" or "nec_corpse_explosion"=>3.2f,
            _=>5.6f
        };
        var actor=session.Heroes[0];actor.Position=room.Center-new V(gap/2,0);actor.Hp=actor.MaxHp=10000;actor.Timer=999;actor.Range=100;actor.SkillIds=new[]{skillId};
        if(skillId=="pal_prayer")actor.Hp=5000;
        for(int i=0;i<room.Monsters.Count;i++){var m=room.Monsters[i];m.Position=room.Center+new V(gap/2,(i-(room.Monsters.Count-1)/2f)*.4f);m.Hp=m.MaxHp=10000;m.Range=100;m.Attack=1;m.Timer=999;}
        // 尸爆需要一具本房真实尸体权利和一个存活受害者；只改隔离QA现场，不写配置或玩家存档。
        if(skillId=="nec_corpse_explosion")
        {
            var corpse=room.Monsters[0];
            if(room.Monsters.Count==1)
            {
                var extra=corpse.Copy();extra.Position=room.Center+new V(1.6f,.4f);extra.Hp=extra.MaxHp=10000;
                room.Monsters.Add(extra);
            }
            corpse.Hp=0;
            state.Corpses.Add(new(){ActorId=session.Heroes.Count,Position=room.Center});
        }
        // 分别推进到召唤/尸体/弹道或技能事件出现；截屏画面必须有可观察表现，而不是只看逻辑结果。
        bool summonSkill=config.Skills.Skills[skillId].Effects.Any(e=>config.Skills.Effects[e].Kind=="Summon");
        for(int i=0;i<80;i++)
        {
            session.Tick(.05f);
            long now=session.Floor.ElapsedTicks;
            bool recent=session.VisualEvents.Any(e=>e.Skill==skillId&&now-e.Tick<=4);
            if(summonSkill&&session.Summons.Count(s=>s.Alive)>0)break;
            // 弹道至少离手一小段再截屏；Ability在命中当Tick即移除，不能只判断是否曾出现。
            if(session.TalentProjectiles.Any(f=>V.Distance(f.Position,actor.Position)>.5f))break;
            if(skillId=="barb_whirlwind_window")
            {
                if(session.VisualEvents.Any(e=>e.Skill==skillId&&e.Kind==CombatVisualKind.Pulse&&now-e.Tick<=4))break;
            }
            else if(skillId=="nec_corpse_explosion")
            {
                if(session.VisualEvents.Any(e=>e.Skill==skillId&&e.Kind==CombatVisualKind.CorpseExplosion&&now-e.Tick<=4))break;
            }
            else if(skillId=="pal_prayer")
            {
                if(session.VisualEvents.Any(e=>e.Skill==skillId&&e.Kind==CombatVisualKind.Heal&&now-e.Tick<=4))break;
            }
            else if(recent)break;
        }
        bool mini=OS.GetCmdlineUserArgs().Contains("--qa-mini");
        if(mini)GetWindow().Size=new Vector2I(960,600);
        viewMode=0;paused=true;cameraReady=false;motionAlpha=1;captureView="bd-skill-"+skillId+(mini?"-mini":"");Refresh();TickStarPanels();
        var actions=session.Capture().SkillState.Actions;
        var buffIds=session.Capture().SkillState.Buffs.Select(b=>b.Id+"@"+b.Target);
        var monsterInfo=room.Monsters.Select(m=>$"{m.Name}:{m.Hp:0}:{m.Position.X:0.0},{m.Position.Y:0.0}:{session.InCombatRoom(m)}");
        var effectInfo=config.Skills.Skills[skillId].Effects.Select(id=>{var f=config.Skills.Effects[id];return $"{id}:{f.Kind}:{f.Extent:0.0}:{f.BuffId}";});
        GD.Print($"BD_SKILL_VISUAL_OK: {skillId}; summons={session.Summons.Count(s=>s.Alive)}; flights={session.TalentProjectiles.Count()}; buffs={string.Join(',',buffIds)}; forced={session.MonsterTargets.Count(l=>l.Kind==MonsterLockKind.Forced)}; pending={session.Capture().SkillState.Pending.Count}; room={room.HalfWidth:0.0}x{room.HalfHeight:0.0}; monsters={string.Join(';',monsterInfo)}; effects={string.Join(';',effectInfo)}; cooldowns={string.Join(',',actions.SelectMany(a=>a.Cooldowns.Keys))}");
    }
}
