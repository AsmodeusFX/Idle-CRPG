using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class SkillBdSummonChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("BD_SUMMON: "+why);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        string qa=Path.Combine(root,"artifacts/qa/bd-summon-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(qa,"skill"));
        foreach(var source in Directory.EnumerateFiles(Path.Combine(root,"Idle-CRPG/config/skill"),"*.csv"))File.Copy(source,Path.Combine(qa,"skill",Path.GetFileName(source)));
        foreach(var file in Directory.EnumerateFiles(Path.Combine(root,"tests/fixtures/skill_bd_slice_b2"),"*.csv"))
        {string destination=Path.Combine(qa,"skill",Path.GetFileName(file));File.WriteAllText(destination,File.ReadAllText(destination).TrimEnd()+"\n"+string.Join('\n',File.ReadAllLines(file).Skip(1))+"\n");}
        var definitions=SkillConfiguration.Load(qa,c.Attributes);
        foreach(var entry in definitions.Skills.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Skills.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.Effects.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Effects.Add(entry.Key,entry.Value);
        foreach(var entry in definitions.Summons.Where(e=>e.Key.StartsWith("QA_")))c.Skills.Summons.Add(entry.Key,entry.Value);

        GameSession Battle()
        {
            var game=new GameSession(c,932,new[]{"priest","warrior","mage"});
            for(int i=0;i<10000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            var room=game.Floor.Rooms[game.CurrentRoom];var state=game.Capture().SkillState;
            state.Targeting=new();state.Actions.Clear();state.Pending.Clear();state.Buffs.Clear();state.Flights.Clear();
            for(int i=0;i<game.Heroes.Count;i++){var h=game.Heroes[i];h.Position=room.Center+new Vector2(i*.35f,0);h.Hp=h.MaxHp=200;h.Attack=10;h.Defense=0;h.Range=100;h.Timer=999;h.SkillIds=Array.Empty<string>();h.StartingSkillGranted=true;}
            for(int i=0;i<room.Monsters.Count;i++){var m=room.Monsters[i];m.Position=room.Center+new Vector2(1.6f,i*.1f);m.Hp=m.MaxHp=100000;m.Defense=0;m.Range=100;m.Attack=0;m.Timer=999;}
            return game;
        }
        void Tick(GameSession game,int count=1){for(int i=0;i<count;i++)game.Tick(.05f);}
        void Summon(GameSession game){game.Heroes[0].SkillIds=new[]{"QA_SUMMON_SKELETON"};Tick(game,8);}
        Actor[] Live(GameSession game)=>game.Summons.Where(s=>s.Alive).ToArray();
        string Encode(GameSession game)=>SaveStore.Encode(new(){Session=game.Capture()});

        var g=Battle();Summon(g);
        var skeleton=Live(g).Single();
        Need(skeleton.SummonOwner==0&&skeleton.SummonSkill=="QA_SUMMON_SKELETON"&&skeleton.SummonDefinitionId=="QA_SKELETON","召唤来源记录错误");
        Need(Math.Abs(skeleton.MaxHp-100)<.0001f&&Math.Abs(skeleton.Attack-5)<.0001f&&Math.Abs(skeleton.Defense)<.0001f,"召唤没有按主人基础值固定比例继承");
        Need(Vector2.Distance(skeleton.Position,g.Heroes[0].Position)<1,"召唤没有在主人附近落位");
        Tick(g,150);Need(Live(g).Length==1,"重复召唤超过上限1只");
        var copy=GameSession.Restore(c,SaveStore.Decode(c,Encode(g)).Session!);Need(Live(copy).Length==1&&Encode(g)==Encode(copy),"完整召唤快照不能确定性恢复");

        // 骷髅击杀进入既有全队经验与击杀统计，不产生尸体或独立奖励路径。
        var killer=Battle();Summon(killer);var killerRoom=killer.Floor.Rooms[killer.CurrentRoom];var killerSummon=Live(killer)[0];killerSummon.Attack=1000;killerSummon.Timer=0;
        var victim=killerRoom.Monsters[0];victim.Position=killerSummon.Position;victim.Hp=victim.MaxHp=1;victim.Defense=0;victim.Timer=999;
        int beforeKills=killer.Kills;long beforeXp=killer.Experience;int beforeLoot=killerRoom.Loot.Count;
        for(int i=0;i<40&&victim.Alive;i++)Tick(killer);
        Need(!victim.Alive&&killer.Kills>beforeKills&&killer.Experience>beforeXp&&killerRoom.Loot.Count>=beforeLoot,"骷髅击杀没有沿用全队经验/击杀奖励");

        // 召唤物死亡只撤销自身，不进入怪物奖励或清房统计。
        var doomed=Battle();Summon(doomed);var doomedRoom=doomed.Floor.Rooms[doomed.CurrentRoom];doomedRoom.HalfWidth=doomedRoom.HalfHeight=5;var summon=Live(doomed)[0];
        foreach(var h in doomed.Heroes){h.Position=doomedRoom.Center+new Vector2(4.4f,4.4f);h.Hp=h.MaxHp=100000;h.Timer=999;}
        foreach(var m in doomedRoom.Monsters){m.Position=summon.Position+new Vector2(.4f,0);m.Attack=100000;m.Range=100;m.Cooldown=.05f;m.Timer=0;}
        doomed.Capture().SkillState.Targeting=new();
        int doomedKills=doomed.Kills;long doomedXp=doomed.Experience;int doomedLoot=doomedRoom.Loot.Count;
        for(int i=0;i<20&&Live(doomed).Length>0;i++)Tick(doomed);
        Need(Live(doomed).Length==0&&doomed.Kills==doomedKills&&doomed.Experience==doomedXp&&doomedRoom.Loot.Count==doomedLoot,"召唤物死亡产生尸体/经验/掉落或未撤销");

        // 跨房保留实际 HP；未清房正式检查点从进房快照恢复，不保留房内新召唤。
        var crossing=Battle();crossing.SetGmBattle(new(Invincible:true));Summon(crossing);
        var crossingRoom=crossing.Floor.Rooms[crossing.CurrentRoom];Live(crossing)[0].Hp=20;
        var inRoomCheckpoint=crossing.CaptureProgress();Need(inRoomCheckpoint.Summons.Count==0&&inRoomCheckpoint.RoomEntrySummons.Count==0,"房内新召唤错误进入正式检查点");
        foreach(var m in crossingRoom.Monsters)m.Hp=0;crossingRoom.ContentCleared=true;crossingRoom.EventUsed=true;crossingRoom.Loot.Clear();
        int oldRoom=crossing.CurrentRoom;
        bool followed=false;
        for(int i=0;i<20000&&crossing.CurrentRoom==oldRoom;i++)
        {
            var before=Live(crossing)[0].Position;Tick(crossing);var after=Live(crossing)[0].Position;
            bool switched=crossing.CurrentRoom!=oldRoom;
            followed|=Vector2.Distance(before,after)>.0001f;
            // 落后时允许按 max(基础, 主人速度×配置追赶倍率) 追赶；更大的位移只能是闪现或正式跨房同步。
            float limit=Math.Max(Live(crossing)[0].Speed,crossing.Heroes[0].Speed*c.Talents.Rule("summon_catchup_speed_pct")/100f)*.05f+.001f;
            bool blinked=Vector2.Distance(before,after)>limit;
            if(blinked&&!switched)Need(Vector2.Distance(after,crossing.Heroes[0].Position)<=1,"脱队闪现落点不在主人附近");
            if(switched)
            {
                // 跨房切换不依赖原脱队距离，存活召唤物须直接落在新房间内。
                var now=crossing.Floor.Rooms[crossing.CurrentRoom];
                Need(Math.Abs(after.X-now.Center.X)<=now.HalfWidth+.001f&&Math.Abs(after.Y-now.Center.Y)<=now.HalfHeight+.001f,"跨房切换后召唤物未同步到新房间");
            }
            bool onFloor=crossing.Floor.Rooms.Any(r=>Math.Abs(after.X-r.Center.X)<=r.HalfWidth+.001f&&Math.Abs(after.Y-r.Center.Y)<=r.HalfHeight+.001f);
            bool onPassage=crossing.Floor.Passages.Any(p=>
            {
                var a=crossing.Floor.Rooms[p.A];var b=crossing.Floor.Rooms[p.B];
                var min=Vector2.Min(a.DoorTowards(b),b.DoorTowards(a))-new Vector2(.001f);
                var max=Vector2.Max(a.DoorTowards(b),b.DoorTowards(a))+new Vector2(.001f);
                return after.X>=min.X&&after.X<=max.X&&after.Y>=min.Y&&after.Y<=max.Y;
            });
            Need(onFloor||onPassage,"召唤物穿墙或偏离走廊");
        }
        Need(followed,"召唤物未随小队连续移动");
        Need(crossing.CurrentRoom!=oldRoom&&Live(crossing).Length==1&&Math.Abs(Live(crossing)[0].Hp-20)<.0001f,"跨房丢失召唤或补满实际HP");
        var compact=crossing.CaptureProgress();Need(compact.Summons.Count==1&&Math.Abs(compact.Summons[0].Hp-20)<.0001f,"进房检查点未保留召唤实际HP");
        var reopened=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=compact})).Session!);
        Need(Live(reopened).Length==1&&Math.Abs(Live(reopened)[0].Hp-20)<.0001f,"正式检查点重开丢失召唤");

        // 跟随三档：正常距离内不动、超距按主人速度×1.2追赶、持续脱队0.5秒才闪现；闪现保留HP/数量且无奖励。
        var trail=Battle();Summon(trail);
        var trailRoom=trail.Floor.Rooms[trail.CurrentRoom];
        foreach(var monster in trailRoom.Monsters)monster.Hp=0;
        trailRoom.ContentCleared=true;trailRoom.EventUsed=true;trailRoom.Loot.Clear();
        trailRoom.HalfWidth=trailRoom.HalfHeight=20;
        var follower=Live(trail)[0];var trailOwner=trail.Heroes[0];
        trailOwner.Position=trailRoom.Center;follower.Position=trailOwner.Position+new Vector2(2,0);
        var parked=follower.Position;Tick(trail);
        Need(Vector2.Distance(follower.Position,parked)<.0001f,"跟随距离内召唤物仍在移动");
        trailOwner.Position=trailRoom.Center;follower.Position=trailOwner.Position+new Vector2(6,0);
        var chaseFrom=follower.Position;Tick(trail);
        float chase=Vector2.Distance(chaseFrom,follower.Position);
        float chaseLimit=Math.Max(follower.Speed,trailOwner.Speed*c.Talents.Rule("summon_catchup_speed_pct")/100f)*.05f+.0001f;
        Need(chase>follower.Speed*.05f+.0001f&&chase<=chaseLimit,"脱队追赶未提速或超出速度上限");
        trailOwner.Position=trailRoom.Center;follower.Position=trailOwner.Position+new Vector2(14,0);
        Need(Vector2.Distance(follower.Position,trailOwner.Position)>8,"闪现用例起始距离不足");
        follower.Hp=12;int trailCount=Live(trail).Length;int trailKills=trail.Kills;long trailXp=trail.Experience;int trailLoot=trailRoom.Loot.Count;
        bool teleported=false;
        for(int i=0;i<40&&!teleported;i++)
        {
            var from=follower.Position;Tick(trail);
            teleported=Vector2.Distance(from,follower.Position)>Math.Max(follower.Speed,trailOwner.Speed*c.Talents.Rule("summon_catchup_speed_pct")/100f)*.05f+.001f;
        }
        Need(teleported&&Vector2.Distance(follower.Position,trailOwner.Position)<=1,"持续脱队0.5秒后未闪现或落点不在主人附近");
        Need(Live(trail).Length==trailCount&&Math.Abs(follower.Hp-12)<.0001f&&trail.Kills==trailKills&&trail.Experience==trailXp&&trailRoom.Loot.Count==trailLoot,"闪现改变了HP/数量或产生了击杀经验掉落");

        // 未清房且召唤物在本房时不跟随、不闪现；战斗结束后才重新按距离判定。
        var combat=Battle();Summon(combat);
        var combatRoom=combat.Floor.Rooms[combat.CurrentRoom];
        combatRoom.HalfWidth=combatRoom.HalfHeight=20;
        var combatSummon=Live(combat)[0];var combatOwner=combat.Heroes[0];
        combatOwner.Position=combatRoom.Center;combatSummon.Position=combatOwner.Position+new Vector2(14,0);
        bool escaped=false;
        for(int i=0;i<30;i++)
        {
            var from=combatSummon.Position;Tick(combat);
            // 战斗 AI 自身走位仍受基础速度限制，超过该预算只能来自跟随闪现。
            if(Vector2.Distance(from,combatSummon.Position)>combatSummon.Speed*.05f+.001f)escaped=true;
        }
        Need(!combatRoom.Clear&&!escaped,"未清房时召唤物离队闪现");

        // 入场部署：近战前压、远程靠后、支援贴身；同角色多只使用不同车道，入场加速不超过独立临时上限。
        (GameSession Game,Vector2 Anchor,Actor[] Live) DeployScene(string role,int count=1)
        {
            c.Skills.Summons["QA_SKELETON"]=c.Skills.Summons["QA_SKELETON"] with {MaxCount=count,Role=role};
            var game=Battle();game.Heroes[0].SkillIds=new[]{"QA_SUMMON_SKELETON"};
            for(int i=0;i<1200&&Live(game).Length<count;i++)Tick(game);
            Need(Live(game).Length==count,"入场部署用例未召出足量召唤物："+role);
            var room=game.Floor.Rooms[game.CurrentRoom];room.HalfWidth=room.HalfHeight=6;
            foreach(var monster in room.Monsters){monster.Attack=0;monster.Hp=monster.MaxHp=100000;monster.Position=room.Center+new Vector2(0,4.5f);monster.Timer=999;}
            var anchor=room.Center+new Vector2(0,-5);
            var state=game.Capture().SkillState.Targeting;
            state.EntryPrepared=true;state.EntryElapsed=0;state.EntryAnchor=anchor;state.EntryDirection=new(0,1);state.DeployedMask=0;
            var summons=Live(game);
            for(int i=0;i<summons.Length;i++)summons[i].Position=anchor+new Vector2((i-.5f)*.15f,.45f);
            game.Heroes[0].Position=anchor+new Vector2(0,1f);
            return (game,anchor,summons);
        }
        void DeployCheck((GameSession Game,Vector2 Anchor,Actor[] Live) scene)
        {
            float cap=Math.Max(scene.Live[0].Speed*c.Talents.Rule("summon_entry_speed_pct")/100f,scene.Game.Heroes[0].Speed*c.Talents.Rule("summon_entry_owner_pct")/100f)*.05f+.001f;
            for(int i=0;i<10;i++)
            {
                var from=scene.Live.Select(s=>s.Position).ToArray();Tick(scene.Game);
                for(int s=0;s<scene.Live.Length;s++)Need(Vector2.Distance(from[s],scene.Live[s].Position)<=cap,"入场部署超出临时加速上限");
            }
        }
        float Depth((GameSession Game,Vector2 Anchor,Actor[] Live) scene,Actor actor)=>Vector2.Dot(actor.Position-scene.Anchor,scene.Game.Capture().SkillState.Targeting.EntryDirection);
        var meleeScene=DeployScene("melee");DeployCheck(meleeScene);
        var rangedScene=DeployScene("ranged");DeployCheck(rangedScene);
        Need(Depth(meleeScene,meleeScene.Live[0])>Depth(rangedScene,rangedScene.Live[0])+.5f,"近战召唤物没有比远程展开得更远");
        var supportScene=DeployScene("support");DeployCheck(supportScene);
        Need(Vector2.Distance(supportScene.Live[0].Position,supportScene.Game.Heroes[0].Position)<.7f,"支援型召唤物未贴近主人");
        var pairScene=DeployScene("melee",2);DeployCheck(pairScene);
        Need(Math.Abs(pairScene.Live[0].Position.X-pairScene.Live[1].Position.X)>.5f&&pairScene.Live.All(s=>Depth(pairScene,s)>.8f),"多只近战召唤物入场落点重叠或未前压");
        c.Skills.Summons["QA_SKELETON"]=c.Skills.Summons["QA_SKELETON"] with {MaxCount=1,Role="melee"};

        // 敌人 AI 启动时，未到位且未接敌的召唤物兜底同步到站位；不延长警觉、不改 HP/数量、不产生奖励。
        c.Skills.Summons["QA_SKELETON"]=c.Skills.Summons["QA_SKELETON"] with {MaxCount=1,Role="melee"};
        var snap=Battle();Summon(snap);
        var snapRoom=snap.Floor.Rooms[snap.CurrentRoom];snapRoom.HalfWidth=snapRoom.HalfHeight=6;
        foreach(var monster in snapRoom.Monsters){monster.Attack=0;monster.Hp=monster.MaxHp=100000;monster.Position=snapRoom.Center+new Vector2(0,4.5f);monster.Timer=999;}
        var snapAnchor=snapRoom.Center+new Vector2(0,-5);
        var snapState=snap.Capture().SkillState.Targeting;
        snapState.EntryPrepared=true;snapState.EntryElapsed=c.Talents.Rule("combat_entry_delay_milli")/1000f-.05f;snapState.EntryAnchor=snapAnchor;snapState.EntryDirection=new(0,1);snapState.DeployedMask=0;snapState.Locks.Clear();
        snap.Capture().SkillState.Actions.Clear();
        var snapSummon=Live(snap)[0];snapSummon.Position=snapRoom.Center+new Vector2(-5,5);snapSummon.Hp=7;
        int snapKills=snap.Kills;long snapXp=snap.Experience;int snapLoot=snapRoom.Loot.Count;
        Tick(snap);
        Need(Math.Abs(Vector2.Dot(snapSummon.Position-snapAnchor,snapState.EntryDirection)-c.Talents.Rule("combat_entry_melee_depth_milli")/1000f)<.35f,"入场超时未兜底同步到站位");
        Need(Math.Abs(snapSummon.Hp-7)<.0001f&&Live(snap).Length==1&&snap.Kills==snapKills&&snap.Experience==snapXp&&snapRoom.Loot.Count==snapLoot,"兜底同步改变HP/数量或产生奖励");

        // 同主人多只召唤物闪现时使用不同虚拟站位，不能全部叠在同一点。
        c.Skills.Summons["QA_SKELETON"]=c.Skills.Summons["QA_SKELETON"] with {MaxCount=2};
        var pair=Battle();pair.Heroes[0].SkillIds=new[]{"QA_SUMMON_SKELETON"};
        for(int i=0;i<400&&Live(pair).Length<2;i++)Tick(pair);
        Need(Live(pair).Length==2,"上限2只时未召唤出第二只");
        var pairRoom=pair.Floor.Rooms[pair.CurrentRoom];
        foreach(var monster in pairRoom.Monsters)monster.Hp=0;
        pairRoom.ContentCleared=true;pairRoom.EventUsed=true;pairRoom.Loot.Clear();
        pairRoom.HalfWidth=pairRoom.HalfHeight=20;
        var pairOwner=pair.Heroes[0];pairOwner.Position=pairRoom.Center;
        var pairSummons=Live(pair);
        for(int i=0;i<pairSummons.Length;i++)pairSummons[i].Position=pairOwner.Position+new Vector2(12+i*.1f,0);
        for(int i=0;i<40&&Vector2.Distance(pairSummons[0].Position,pairOwner.Position)>1;i++)Tick(pair);
        Need(pairSummons.All(s=>Vector2.Distance(s.Position,pairOwner.Position)<=1)&&Vector2.Distance(pairSummons[0].Position,pairSummons[1].Position)>.2f,"多只召唤物闪现落点重叠或未同步");

        // 永续召唤脱战补员：没有敌人也按原前摇/冷却逐只补齐，到达上限后不再施放。
        // 每一步都强制清空当前房，确保补员发生在非战斗状态，而不是由战斗AI完成。
        void ClearCurrent(GameSession game)
        {
            var room=game.Floor.Rooms[game.CurrentRoom];
            foreach(var monster in room.Monsters)monster.Hp=0;
            room.ContentCleared=true;room.EventUsed=true;room.Loot.Clear();
        }
        c.Skills.Summons["QA_SKELETON"]=c.Skills.Summons["QA_SKELETON"] with {MaxCount=3,Role="melee",Duration=0};
        var refill=Battle();refill.Heroes[0].SkillIds=new[]{"QA_SUMMON_SKELETON"};
        for(int i=0;i<400&&Live(refill).Length<3;i++){ClearCurrent(refill);Tick(refill);}
        Need(Live(refill).Length==3,"永续召唤未在无敌人房间自动补满上限");
        for(int i=0;i<320;i++){ClearCurrent(refill);Tick(refill);}
        Need(Live(refill).Length==3,"永续召唤超过数量上限");
        // 战斗外损失一只后继续补员。
        Live(refill)[0].Hp=0;ClearCurrent(refill);Tick(refill);
        Need(Live(refill).Length==2,"阵亡召唤物未从存活列表移除");
        for(int i=0;i<400&&Live(refill).Length<3;i++){ClearCurrent(refill);Tick(refill);}
        Need(Live(refill).Length==3,"非战斗阶段阵亡召唤物未补员");

        // 限时召唤：按固定逻辑步到期撤销，不产生奖励，也不会在脱战时重新施放。
        c.Skills.Summons["QA_SKELETON"]=c.Skills.Summons["QA_SKELETON"] with {MaxCount=1,Duration=.5};
        var timed=Battle();Summon(timed);
        var timedRoom=timed.Floor.Rooms[timed.CurrentRoom];
        foreach(var monster in timedRoom.Monsters)monster.Hp=0;
        timedRoom.ContentCleared=true;timedRoom.EventUsed=true;timedRoom.Loot.Clear();
        int timedKills=timed.Kills;long timedXp=timed.Experience;
        Need(Live(timed).Length==1,"限时召唤用例未生成召唤物");
        for(int i=0;i<30&&Live(timed).Length>0;i++){ClearCurrent(timed);Tick(timed);}
        Need(Live(timed).Length==0,"限时召唤到期未撤销");
        for(int i=0;i<20;i++){ClearCurrent(timed);Tick(timed);}
        Need(Live(timed).Length==0&&timed.Kills==timedKills&&timed.Experience==timedXp,"限时召唤到期后脱战重新施放或产生奖励");
        c.Skills.Summons["QA_SKELETON"]=c.Skills.Summons["QA_SKELETON"] with {MaxCount=1,Duration=0};
        Need(c.Skills.Summons["nec_skeleton"] is {Permanent:true,MaxCount:3},"生产骷髅未配置为上限3的永续召唤");
        Need(c.Skills.Summons["ran_valkyrie"] is {Permanent:false}&&Math.Abs(c.Skills.Summons["ran_valkyrie"].Duration-5)<.0001f,"生产女武神未配置为5秒限时召唤");

        // 主人卸槽/死亡立即撤销；全灭清除全部召唤记录。
        var unslot=Battle();Summon(unslot);unslot.EquipSkills("priest",Array.Empty<string>());Need(Live(unslot).Length==0,"主人卸槽后召唤仍存活");
        var ownerDead=Battle();Summon(ownerDead);ownerDead.Heroes[0].Hp=0;Tick(ownerDead);Need(Live(ownerDead).Length==0,"主人死亡后召唤仍存活");
        var wipe=Battle();Summon(wipe);foreach(var h in wipe.Heroes)h.Hp=0;Tick(wipe);Need(wipe.Summons.Count==0&&wipe.Capture().Summons.Count==0,"全灭没有清除召唤记录");

        // 正常通关回城必须死亡并清理召唤；下一轮进入副本清普攻、技能及换入冷却。
        var world=GameSession.CreateWorld(c,932,new[]{"priest","warrior","mage"});world.SetGmBattle(new(Invincible:true));
        for(int i=0;i<20000&&(world.World!.Stage!=CampaignStage.Dungeon||world.CurrentRoom==0);i++)Tick(world);
        foreach(var h in world.Heroes){h.Hp=h.MaxHp=100000;h.Timer=999;h.SkillIds=Array.Empty<string>();}
        world.Heroes[0].SkillIds=new[]{"QA_SUMMON_SKELETON"};
        for(int i=0;i<100&&Live(world).Length==0;i++)Tick(world);
        var townSummon=Live(world).Single();
        for(int i=0;i<50000&&world.World!.Stage!=CampaignStage.Village;i++)
        {
            if(world.World!.Stage==CampaignStage.Dungeon)foreach(var monster in world.Floor.Rooms[world.CurrentRoom].Monsters)monster.Hp=0;
            Tick(world);
        }
        Need(world.World!.Stage==CampaignStage.Village&&!townSummon.Alive&&world.Summons.Count==0&&world.Capture().RoomEntrySummons.Count==0,"正常回城未死亡清除召唤及进房副本");
        foreach(var h in world.Heroes){h.Timer=999;foreach(string id in h.SkillIds)h.SkillSwapLocks[id]=999;}
        for(int i=0;i<20000&&world.World.Stage!=CampaignStage.Dungeon;i++)Tick(world);
        Need(world.World.Stage==CampaignStage.Dungeon&&world.Heroes.All(h=>h.Timer==0&&h.SkillSwapLocks.Count==0&&h.SkillIds.All(id=>world.SkillCooldown(h,id)==0)),"进副本未重置全部战斗CD");

        Need(!LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config")).Skills.Summons.ContainsKey("QA_SKELETON"),"QA召唤定义泄漏生产配置");
        Console.WriteLine("PASS: B2 summon stats/cap/rewards, permanent out-of-combat refill to cap and timed expiry without recast, production nec_skeleton permanent x3 / ran_valkyrie 5s, continuous door/corridor following, normal/catch-up/blink follow tiers with HP/count/reward preservation, room-switch sync, melee/ranged/support entry deployment with capped acceleration and timeout snap, combat no-blink and distinct blink slots, cross-room HP/checkpoint, owner/wipe/town death cleanup and new-dungeon combat cooldown reset");
    }
}
