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
        float entryStep=Live(crossing)[0].Speed*c.Talents.Rule("summon_entry_speed_pct")/100f;
        for(int i=0;i<20000&&crossing.CurrentRoom==oldRoom;i++)
        {
            var before=Live(crossing)[0].Position;Tick(crossing);var after=Live(crossing)[0].Position;
            followed|=Vector2.Distance(before,after)>.0001f;
            // 走廊/已清空房间按追赶倍率，进入有怪房间后按入场倍率；超过两者上限的位移只能是异常闪现。
            float limit=Math.Max(entryStep,crossing.Heroes[0].Speed*c.Talents.Rule("summon_catchup_speed_pct")/100f)*.05f+.01f;
            Need(Vector2.Distance(before,after)<=limit,"召唤物跨房移动超出入场/追赶速度上限（疑似瞬移）");
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
        // 新房间有怪：召唤物不得跨房瞬移，必须沿真实房门在入场部署中走进房间。
        bool entered=false;
        for(int i=0;i<200&&!entered;i++)
        {
            Tick(crossing);
            var now=crossing.Floor.Rooms[crossing.CurrentRoom];var p=Live(crossing)[0].Position;
            entered=Math.Abs(p.X-now.Center.X)<=now.HalfWidth+.001f&&Math.Abs(p.Y-now.Center.Y)<=now.HalfHeight+.001f;
        }
        Need(entered&&Live(crossing).Length==1&&Math.Abs(Live(crossing)[0].Hp-20)<.0001f,"跨房后召唤物未沿真实路径进入新房间或丢失召唤");
        var compact=crossing.CaptureProgress();Need(compact.Summons.Count==1&&Math.Abs(compact.Summons[0].Hp-20)<.0001f,"进房检查点未保留召唤实际HP");
        var reopened=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=compact})).Session!);
        Need(Live(reopened).Length==1&&Math.Abs(Live(reopened)[0].Hp-20)<.0001f,"正式检查点重开丢失召唤");

        // 非战斗跟随锚点为当前跑图领队，不再跟随召唤者；超距追赶与异常闪现同样以领队为锚点。
        var trail=Battle();Summon(trail);
        var trailRoom=trail.Floor.Rooms[trail.CurrentRoom];
        foreach(var monster in trailRoom.Monsters)monster.Hp=0;
        trailRoom.ContentCleared=true;trailRoom.EventUsed=true;trailRoom.Loot.Clear();
        trailRoom.HalfWidth=trailRoom.HalfHeight=20;
        var follower=Live(trail)[0];var trailOwner=trail.Heroes[0];var trailLeader=trail.Heroes.Single(h=>h.SpecId=="warrior");
        Need(trailLeader.Speed>trailOwner.Speed,"跟随锚点用例前提：领队不是召唤者");
        // 主人远在后方，召唤物紧贴领队：移动后应继续靠近领队而不是折返主人。
        trailOwner.Position=trailRoom.Center+new Vector2(10,0);
        trailLeader.Position=trailRoom.Center;follower.Position=trailLeader.Position+new Vector2(0,-1f);
        Tick(trail);
        Need(ReferenceEquals(trail.TravelLeader,trailLeader),"跟随锚点没有使用当前跑图领队");
        Need(Vector2.Distance(follower.Position,trailLeader.Position)<2.5f,"召唤物没有跟随领队");
        Need(Vector2.Distance(follower.Position,trailOwner.Position)>6f,"召唤物仍在跟随召唤者而不是领队");
        trailLeader.Position=trailRoom.Center;follower.Position=trailLeader.Position+new Vector2(6,0);
        var chaseFrom=follower.Position;Tick(trail);
        float chase=Vector2.Distance(chaseFrom,follower.Position);
        float chaseLimit=Math.Max(follower.Speed,trailLeader.Speed*c.Talents.Rule("summon_catchup_speed_pct")/100f)*.05f+.001f;
        Need(chase>follower.Speed*.05f+.0001f&&chase<=chaseLimit,"脱队追赶未提速或超出速度上限");
        trailLeader.Position=trailRoom.Center;follower.Position=trailLeader.Position+new Vector2(14,0);
        Need(Vector2.Distance(follower.Position,trailLeader.Position)>8,"闪现用例起始距离不足");
        follower.Hp=12;int trailCount=Live(trail).Length;int trailKills=trail.Kills;long trailXp=trail.Experience;int trailLoot=trailRoom.Loot.Count;
        bool teleported=false;
        for(int i=0;i<40&&!teleported;i++)
        {
            var from=follower.Position;Tick(trail);
            teleported=Vector2.Distance(from,follower.Position)>Math.Max(follower.Speed,trailLeader.Speed*c.Talents.Rule("summon_catchup_speed_pct")/100f)*.05f+.001f;
        }
        Need(teleported&&Vector2.Distance(follower.Position,trailLeader.Position)<=1,"持续脱队0.5秒后未闪现或落点不在领队附近");
        Need(Live(trail).Length==trailCount&&Math.Abs(follower.Hp-12)<.0001f&&trail.Kills==trailKills&&trail.Experience==trailXp&&trailRoom.Loot.Count==trailLoot,"闪现改变了HP/数量或产生了击杀经验掉落");

        // 领队锚点与角色站位：近战在领队前方、远程在后方；多只近战横向分散；换领队不瞬移，归属仍为原召唤者。
        c.Skills.Summons["QA_RANGED"]=c.Skills.Summons["QA_SKELETON"] with {Id="QA_RANGED",Role="ranged",MaxCount=1};
        c.Skills.Effects["QA_RANGED_EFFECT"]=c.Skills.Effects["QA_SUMMON_EFFECT"] with {Id="QA_RANGED_EFFECT",MechanicId="QA_RANGED"};
        c.Skills.Skills["QA_SUMMON_RANGED"]=c.Skills.Skills["QA_SUMMON_SKELETON"] with {Id="QA_SUMMON_RANGED",Effects=new[]{"QA_RANGED_EFFECT"}};
        var roles=Battle();roles.Heroes[0].SkillIds=new[]{"QA_SUMMON_SKELETON","QA_SUMMON_RANGED"};
        for(int i=0;i<800&&!(Live(roles).Any(s=>s.SummonDefinitionId=="QA_SKELETON")&&Live(roles).Any(s=>s.SummonDefinitionId=="QA_RANGED"));i++)Tick(roles);
        Need(Live(roles).Any(s=>s.SummonDefinitionId=="QA_SKELETON")&&Live(roles).Any(s=>s.SummonDefinitionId=="QA_RANGED"),"近战/远程召唤未同时生成");
        var rolesRoom=roles.Floor.Rooms[roles.CurrentRoom];
        foreach(var monster in rolesRoom.Monsters)monster.Hp=0;
        rolesRoom.ContentCleared=true;rolesRoom.EventUsed=true;rolesRoom.Loot.Clear();rolesRoom.HalfWidth=rolesRoom.HalfHeight=20;
        roles.Tick(.05f);
        var rolesLeader=roles.TravelLeader!;var rolesForward=Vector2.Normalize(roles.Floor.Rooms[roles.Capture().Destination].Center-rolesRoom.Center);
        var melee=Live(roles).Single(s=>s.SummonDefinitionId=="QA_SKELETON");var ranged=Live(roles).Single(s=>s.SummonDefinitionId=="QA_RANGED");
        rolesLeader.Position=rolesRoom.Center;
        melee.Position=rolesRoom.Center+rolesForward*.55f;ranged.Position=rolesRoom.Center-rolesForward*.65f;
        Tick(roles);
        Need(Vector2.Dot(melee.Position-rolesLeader.Position,rolesForward)>0&&Vector2.Dot(ranged.Position-rolesLeader.Position,rolesForward)<0,"近战/远程跟随位置没有前后区分");
        Need(melee.SummonOwner==0&&ranged.SummonOwner==0,"跟随领队改变了召唤归属");
        // 三只近战召唤物沿不同横向车道分散在领队前方。
        c.Skills.Summons["QA_SKELETON"]=c.Skills.Summons["QA_SKELETON"] with {MaxCount=3,Role="melee"};
        var spread=Battle();spread.Heroes[0].SkillIds=new[]{"QA_SUMMON_SKELETON"};
        for(int i=0;i<1200&&Live(spread).Length<3;i++)Tick(spread);
        Need(Live(spread).Length==3,"三只近战召唤未生成");
        var spreadRoom=spread.Floor.Rooms[spread.CurrentRoom];
        foreach(var monster in spreadRoom.Monsters)monster.Hp=0;
        spreadRoom.ContentCleared=true;spreadRoom.EventUsed=true;spreadRoom.Loot.Clear();spreadRoom.HalfWidth=spreadRoom.HalfHeight=20;
        spread.Tick(.05f);
        var spreadLeader=spread.TravelLeader!;var spreadForward=Vector2.Normalize(spread.Floor.Rooms[spread.Capture().Destination].Center-spreadRoom.Center);
        var spreadSide=new Vector2(-spreadForward.Y,spreadForward.X);
        spreadLeader.Position=spreadRoom.Center;
        var pack=Live(spread);
        for(int i=0;i<pack.Length;i++){int lane=i==0?0:(i+1)/2*(i%2==1?-1:1);pack[i].Position=spreadRoom.Center+spreadForward*.55f+spreadSide*(lane*.32f);}
        Tick(spread);
        for(int a=0;a<pack.Length;a++)
        {
            Need(Vector2.Dot(pack[a].Position-spreadLeader.Position,spreadForward)>-.1f,"多只近战没有分布在领队前方");
            for(int b=a+1;b<pack.Length;b++)Need(Vector2.Distance(pack[a].Position,pack[b].Position)>.2f,"多只近战跟随位置重叠");
        }
        // 有效速度变化换领队时，召唤物连续改追新领队，不瞬移。
        var beforeSwitch=melee.Position;var newLeader=roles.Heroes.Single(h=>h.SpecId=="mage");newLeader.Speed=10;
        Tick(roles);
        Need(ReferenceEquals(roles.TravelLeader,newLeader),"有效速度变化后召唤跟随锚点未切换");
        Need(Vector2.Distance(beforeSwitch,melee.Position)<=Math.Max(melee.Speed,newLeader.Speed*c.Talents.Rule("summon_catchup_speed_pct")/100f)*.05f+.01f,"换领队时召唤物瞬移");
        c.Skills.Summons["QA_SKELETON"]=c.Skills.Summons["QA_SKELETON"] with {MaxCount=1,Role="melee"};

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
            // 入场速度上限只由召唤物基础 Speed 的配置倍率决定，不继承主人或其他临时加速。
            float cap=scene.Live[0].Speed*c.Talents.Rule("summon_entry_speed_pct")/100f*.05f+.001f;
            for(int i=0;i<10;i++)
            {
                var from=scene.Live.Select(s=>s.Position).ToArray();Tick(scene.Game);
                for(int s=0;s<scene.Live.Length;s++)Need(Vector2.Distance(from[s],scene.Live[s].Position)<=cap,"入场部署超出临时加速上限");
            }
        }
        var meleeScene=DeployScene("melee");DeployCheck(meleeScene);
        var rangedScene=DeployScene("ranged");DeployCheck(rangedScene);
        Need(meleeScene.Game.Capture().SkillState.Targeting.FriendlyTargets.Count>0&&rangedScene.Game.Capture().SkillState.Targeting.FriendlyTargets.Count>0,"软部署进入房间后必须直接进行战斗索敌");
        var supportScene=DeployScene("support");DeployCheck(supportScene);
        Need(supportScene.Live.All(s=>s.Alive),"软部署不得损坏支援召唤实体");
        var pairScene=DeployScene("melee",2);DeployCheck(pairScene);
        Need(pairScene.Live.All(pairScene.Game.InCombatRoom),"软部署多召唤物必须连续进入房间");
        c.Skills.Summons["QA_SKELETON"]=c.Skills.Summons["QA_SKELETON"] with {MaxCount=1,Role="melee"};

        // 警觉窗口结束后不再强制闪现：未接敌的召唤物继续以入场速度沿真实路径走向落点，敌人 AI 无需等待；
        // 到达后立即恢复普通战斗速度，HP/数量/奖励不变。
        c.Skills.Summons["QA_SKELETON"]=c.Skills.Summons["QA_SKELETON"] with {MaxCount=1,Role="melee"};
        var march=Battle();Summon(march);
        var marchRoom=march.Floor.Rooms[march.CurrentRoom];marchRoom.HalfWidth=marchRoom.HalfHeight=6;
        foreach(var monster in marchRoom.Monsters){monster.Attack=0;monster.Hp=monster.MaxHp=100000;monster.Position=marchRoom.Center+new Vector2(0,4.5f);monster.Timer=999;}
        var marchAnchor=marchRoom.Center+new Vector2(0,-5);
        var marchState=march.Capture().SkillState.Targeting;
        marchState.EntryPrepared=true;marchState.EntryElapsed=c.Talents.Rule("combat_entry_delay_milli")/1000f-.05f;marchState.EntryAnchor=marchAnchor;marchState.EntryDirection=new(0,1);marchState.DeployedMask=0;marchState.Locks.Clear();
        march.Capture().SkillState.Actions.Clear();
        // V1.2入口通行：召唤物需先真正入房约1.5U才允许因远处目标停下；本用例验证的是通行之后的行为，
        // 因此把召唤物放在通行边界之内，避免与入口通行限制混淆。
        var marchSummon=Live(march)[0];marchSummon.Position=marchRoom.Center+new Vector2(-4,-2);marchSummon.Hp=7;
        int marchKills=march.Kills;long marchXp=march.Experience;int marchLoot=marchRoom.Loot.Count;
        float marchEntrySpeed=marchSummon.Speed*c.Talents.Rule("summon_entry_speed_pct")/100f;
        int marchLaneIndex=march.Heroes.Count;int marchLane=marchLaneIndex==0?0:(marchLaneIndex+1)/2*(marchLaneIndex%2==1?-1:1);
        var marchGoal=marchAnchor+marchState.EntryDirection*(c.Talents.Rule("combat_entry_melee_depth_milli")/1000f)
            +new Vector2(-marchState.EntryDirection.Y,marchState.EntryDirection.X)*(marchLane*c.Talents.Rule("combat_entry_melee_spread_milli")/1000f);
        var marchStart=marchSummon.Position;
        Tick(march); // 警觉窗口结束的这一步，敌人 AI 开始行动
        Need(Vector2.Distance(marchStart,marchSummon.Position)<=marchSummon.Speed*.05f+.001f,"房内发现合法目标后应直接按战斗速度行动");
        Need(marchState.FriendlyTargets.ContainsKey(march.Heroes.Count+marchRoom.Monsters.Count),"软部署未建立召唤物战斗目标");
        Need(Vector2.Distance(marchSummon.Position,marchGoal)>.35f,"软部署不允许闪现到旧落点");
        var marchMid=marchSummon.Position;Tick(march);
        Need(Vector2.Distance(marchMid,marchSummon.Position)<=marchSummon.Speed*.05f+.001f,"接敌后入场加速未停止");
        Need(Math.Abs(marchSummon.Hp-7)<.0001f&&Live(march).Length==1&&march.Kills==marchKills&&march.Experience==marchXp&&marchRoom.Loot.Count==marchLoot,"软部署改变HP/数量或产生奖励");
        // 部署途中被敌人锁定即结束入场部署，改由正式战斗 AI 以普通速度接管。
        c.Skills.Summons["QA_SKELETON"]=c.Skills.Summons["QA_SKELETON"] with {MaxCount=1,Role="melee"};
        var engage=Battle();Summon(engage);
        var engageRoom=engage.Floor.Rooms[engage.CurrentRoom];engageRoom.HalfWidth=engageRoom.HalfHeight=6;
        foreach(var monster in engageRoom.Monsters){monster.Attack=0;monster.Hp=monster.MaxHp=100000;monster.Position=engageRoom.Center+new Vector2(0,4.5f);monster.Timer=999;}
        var engageState=engage.Capture().SkillState.Targeting;
        engageState.EntryPrepared=true;engageState.EntryElapsed=c.Talents.Rule("combat_entry_delay_milli")/1000f-.05f;engageState.EntryAnchor=engageRoom.Center+new Vector2(0,-5);engageState.EntryDirection=new(0,1);engageState.DeployedMask=0;engageState.Locks.Clear();
        engage.Capture().SkillState.Actions.Clear();
        var engagedSummon=Live(engage)[0];engagedSummon.Position=engageRoom.Center+new Vector2(0,2.7f);engagedSummon.Hp=7;
        int engagedIndex=engage.Heroes.Count+engage.Summons.IndexOf(engagedSummon);
        for(int i=0;i<3&&!engage.MonsterTargets.Any(l=>l.Target==engagedIndex);i++)Tick(engage);
        Need(engage.MonsterTargets.Any(l=>l.Target==engagedIndex),"部署途中未被怪物锁定（用例前提不成立）");
        var engagedFrom=engagedSummon.Position;Tick(engage);
        Need(Vector2.Distance(engagedFrom,engagedSummon.Position)<=engagedSummon.Speed*.05f+.01f,"进入有效战斗后仍享受入场加速");

        // 同主人多只召唤物跟随同一领队并闪现时使用不同虚拟站位，不能全部叠在同一点。
        c.Skills.Summons["QA_SKELETON"]=c.Skills.Summons["QA_SKELETON"] with {MaxCount=2};
        var pair=Battle();pair.Heroes[0].SkillIds=new[]{"QA_SUMMON_SKELETON"};
        for(int i=0;i<400&&Live(pair).Length<2;i++)Tick(pair);
        Need(Live(pair).Length==2,"上限2只时未召唤出第二只");
        var pairRoom=pair.Floor.Rooms[pair.CurrentRoom];
        foreach(var monster in pairRoom.Monsters)monster.Hp=0;
        pairRoom.ContentCleared=true;pairRoom.EventUsed=true;pairRoom.Loot.Clear();
        pairRoom.HalfWidth=pairRoom.HalfHeight=20;
        pair.Tick(.05f);
        var pairLeader=pair.TravelLeader!;
        Need(pairLeader is {Alive:true},"未确定跑图领队");
        pairLeader.Position=pairRoom.Center;
        var pairSummons=Live(pair);
        for(int i=0;i<pairSummons.Length;i++)pairSummons[i].Position=pairLeader.Position+new Vector2(12+i*.1f,0);
        for(int i=0;i<40&&Vector2.Distance(pairSummons[0].Position,pairLeader.Position)>1;i++)Tick(pair);
        Need(pairSummons.All(s=>Vector2.Distance(s.Position,pairLeader.Position)<=1)&&Vector2.Distance(pairSummons[0].Position,pairSummons[1].Position)>.2f,"多只召唤物闪现落点重叠或未同步到领队");

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
        Console.WriteLine("PASS: B2 summon stats/cap/rewards, permanent out-of-combat refill to cap and timed expiry without recast, production nec_skeleton permanent x3 / ran_valkyrie 5s, non-combat follow anchored to the travel leader with melee-front/ranged-rear and owner attribution preserved, continuous door/corridor following, leader-anchored catch-up/blink tiers with HP/count/reward preservation, room entry without summon teleports and continuous door-path deployment, melee/ranged/support entry deployment at capped 150% acceleration without timeout snap, engagement handover to normal combat speed, combat no-blink and distinct blink slots, cross-room HP/checkpoint, owner/wipe/town death cleanup and new-dungeon combat cooldown reset");
    }
}
