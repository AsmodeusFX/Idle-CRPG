using Godot;
using IdleCrpg.Core;
using System;
using System.IO;
using System.Linq;
using V=System.Numerics.Vector2;

public partial class Main
{
    /// <summary>召唤物跟随/入场 QA 截图：只构造隔离会话与内存定义，不写配置或玩家进度。
    /// follow/fast 验证非战斗追赶，entry/pair 验证入场分类展开与多召唤物车道，pre 验证永续召唤脱战补员。</summary>
    private void PrepareSummonCapture(string scene)
    {
        if(scene=="pre"){PreparePreSummonCapture();return;}
        // QA 远程定义复用同一骷髅原型，只改 role/range，证明展开分类来自定义而不是显示名。
        var skeleton=config.Skills.Summons["nec_skeleton"];
        config.Skills.Summons["nec_skeleton"]=skeleton with {MaxCount=scene=="d2"?3:scene=="pair"?2:1,Role="melee"};
        var ranged=skeleton with {Id="summon_qa_ranged",Name="QA远程骷髅",MaxCount=1,Role="ranged",Range=3.4f};
        config.Skills.Summons[ranged.Id]=ranged;
        var raise=config.Skills.Skills["nec_raise_skeleton"];
        var summonEffect=config.Skills.Effects[raise.Effects[0]];
        config.Skills.Effects["summon_qa_ranged_effect"]=summonEffect with {Id="summon_qa_ranged_effect",MechanicId=ranged.Id};
        config.Skills.Skills["nec_raise_ranged_qa"]=raise with {Id="nec_raise_ranged_qa",Effects=new[]{"summon_qa_ranged_effect"}};
        session=GameSession.CreateWorld(config,787,scene=="d2"?new[]{"warrior","necromancer"}:new[]{"necromancer"});
        for(int i=0;i<10000&&(session.World!.Stage!=CampaignStage.Dungeon||session.CurrentRoom==0);i++)session.Tick(.05f);
        var room=session.Floor.Rooms[session.CurrentRoom];var state=session.Capture().SkillState;
        state.Targeting=new();state.Actions.Clear();state.Pending.Clear();state.Buffs.Clear();state.Flights.Clear();room.ContentCleared=false;
        var hero=session.Heroes[0];hero.Position=room.Center;hero.Hp=hero.MaxHp=10000;hero.Attack=10;hero.Timer=999;hero.Range=100;
        hero.SkillIds=scene=="entry"?new[]{"nec_raise_skeleton","nec_raise_ranged_qa"}:new[]{"nec_raise_skeleton"};
        for(int i=0;i<room.Monsters.Count;i++){var m=room.Monsters[i];m.Position=room.Center+new V(0,2.6f+(i-room.Monsters.Count/2f)*.4f);m.Hp=m.MaxHp=100000;m.Range=100;m.Attack=0;m.Timer=999;}
        int expected=scene=="d2"?3:scene=="pair"?2:scene=="entry"?2:1;
        for(int i=0;i<900&&session.Summons.Count(s=>s.Alive)<expected;i++)session.Tick(.05f);
        if(session.Summons.Count(s=>s.Alive)<expected)throw new InvalidOperationException("QA 召唤物未按预期生成");
        // 等待召唤期间怪物会自行接近，截图前恢复同一排布，保证场景可读。
        for(int i=0;i<room.Monsters.Count;i++)room.Monsters[i].Position=room.Center+new V(0,2.6f+(i-room.Monsters.Count/2f)*.4f);
        float startOwner=0,startSummon=0;
        if(scene is "follow" or "fast")
        {
            // 清空当前房后让小队自然走向下一房间；召唤物放在前进方向后方5单位，按主人速度倍率追赶。
            foreach(var m in room.Monsters)m.Hp=0;
            room.ContentCleared=true;room.EventUsed=true;room.Loot.Clear();
            var summon=session.Summons.First(s=>s.Alive);
            session.Tick(.05f);
            int next=session.Capture().Destination;
            var travel=next>=0?V.Normalize(session.Floor.Rooms[next].Center-room.Center):V.UnitX;
            hero.Position=room.Center;summon.Position=room.Center-travel*5;
            if(scene=="fast")hero.Speed=9;
            var ownerStart=hero.Position;var summonStart=summon.Position;
            for(int i=0;i<(scene=="fast"?12:30);i++)session.Tick(.05f);
            startOwner=V.Distance(ownerStart,hero.Position);startSummon=V.Distance(summonStart,summon.Position);
            GD.Print($"SUMMON_QA {scene}: ownerSpeed={hero.Speed:0.00}; ownerMoved={startOwner:0.00}; summonMoved={startSummon:0.00}; distance={V.Distance(hero.Position,summon.Position):0.00}");
        }
        else
        {
            // 与正式跨房一致的入口方向；召唤物从入口内侧出发，由入场部署推进到各自站位。
            var inward=new V(0,1);var entry=room.Center-new V(0,room.HalfHeight);
            var targeting=session.Capture().SkillState.Targeting;
            targeting.EntryPrepared=true;targeting.EntryElapsed=0;targeting.EntryAnchor=entry;targeting.EntryDirection=inward;targeting.DeployedMask=0;
            var summons=session.Summons.Where(s=>s.Alive).ToArray();
            for(int i=0;i<summons.Length;i++)summons[i].Position=entry+inward*.45f+new V((i-.5f)*.2f,0);
            hero.Position=entry+inward*1f;
            for(int i=0;i<9;i++)session.Tick(.05f);
            GD.Print($"SUMMON_QA {scene}: summons={summons.Length}; roles={string.Join(',',summons.Select(s=>config.Skills.Summons[s.SummonDefinitionId].Role))}; depth={string.Join(',',summons.Select(s=>V.Dot(s.Position-entry,inward).ToString("0.00")))}; entry={session.CombatEntryRemaining:0.00}");
        }
        FinishSummonCapture("summon-"+scene);
    }

    /// <summary>永续召唤脱战补员 QA：空地牢内不借助敌人补齐骷髅上限，只使用隔离会话与内存定义。</summary>
    private void PreparePreSummonCapture()
    {
        config.Skills.Summons["nec_skeleton"]=config.Skills.Summons["nec_skeleton"] with {MaxCount=3,Duration=0,Role="melee"};
        session=GameSession.CreateWorld(config,791,new[]{"necromancer"});
        for(int i=0;i<10000&&(session.World!.Stage!=CampaignStage.Dungeon||session.CurrentRoom==0);i++)session.Tick(.05f);
        var room=session.Floor.Rooms[session.CurrentRoom];var state=session.Capture().SkillState;
        state.Targeting=new();state.Actions.Clear();state.Pending.Clear();state.Buffs.Clear();state.Flights.Clear();
        var hero=session.Heroes[0];hero.Position=room.Center;hero.Hp=hero.MaxHp=10000;hero.Timer=999;hero.SkillIds=new[]{"nec_raise_skeleton"};
        // 每一步都清空当前房，证明补员发生在无敌人状态，而不是由战斗AI完成。
        for(int i=0;i<400&&session.Summons.Count(s=>s.Alive)<3;i++)
        {
            var current=session.Floor.Rooms[session.CurrentRoom];
            foreach(var monster in current.Monsters)monster.Hp=0;
            current.ContentCleared=true;current.EventUsed=true;current.Loot.Clear();
            session.Tick(.05f);
        }
        if(session.Summons.Count(s=>s.Alive)!=3)throw new InvalidOperationException("QA 永续召唤未在无敌人房间补满");
        room=session.Floor.Rooms[session.CurrentRoom];hero.Position=room.Center;
        // 队列排成一行便于截图核对数量；不修改任何战斗数值。
        var live=session.Summons.Where(s=>s.Alive).ToArray();
        for(int i=0;i<live.Length;i++)live[i].Position=room.Center+new V((i-(live.Length-1)/2f)*.9f,.9f);
        var remaining=live.Select(s=>config.Skills.Summons[s.SummonDefinitionId].Permanent?"permanent":s.SummonRemaining.ToString("0.0"));
        GD.Print($"SUMMON_QA pre: summons={live.Length}; roles={string.Join(',',live.Select(s=>config.Skills.Summons[s.SummonDefinitionId].Role))}; lifetime={string.Join(',',remaining)}");
        FinishSummonCapture("summon-pre");
    }

    /// <summary>QA截图统一收尾：Mini窗口尺寸、暂停、镜头就绪与面板刷新。</summary>
    private void FinishSummonCapture(string view)
    {
        bool mini=OS.GetCmdlineUserArgs().Contains("--qa-mini");
        if(mini)GetWindow().Size=new Vector2I(960,600);
        motion.UpdateCombatVisuals(session!,.1);viewMode=0;paused=true;cameraReady=false;motionAlpha=1;captureView=view+(mini?"-mini":"");Refresh();TickStarPanels();
    }

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
