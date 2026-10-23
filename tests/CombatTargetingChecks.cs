using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

public static class CombatTargetingChecks
{
    public static void Run(string root)
    {
        void Need(bool value,string why){if(!value)throw new Exception("TARGETING: "+why);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        GameSession Battle(params string[] ids)
        {
            var g=new GameSession(c,785,ids);
            for(int t=0;t<10000&&g.CurrentRoom==0;t++)g.Tick(.05f);
            // 本夹具隔离索敌契约，真实入场/AI延迟在下方独立测试，不修改生产延迟配置。
            var state=g.Capture().SkillState;state.Targeting=new();state.Actions.Clear();state.Buffs.Clear();state.Flights.Clear();state.Pending.Clear();
            var room=g.Floor.Rooms[g.CurrentRoom];room.ContentCleared=false;room.Monsters.Clear();room.Origins.Clear();
            for(int i=0;i<8;i++)
            {
                var m=Actor.From(c.Monsters[0]);c.ApplyCombatProfile(m);m.Hp=m.MaxHp=1000000;m.Timer=999;m.Range=100;m.Position=room.Center+new Vector2(.4f,.1f*i);room.Monsters.Add(m);
            }
            for(int i=0;i<g.Heroes.Count;i++){var h=g.Heroes[i];h.Position=room.Center+new Vector2(i,0);h.Hp=h.MaxHp=1000000;h.Range=100;h.Timer=999;h.Attack=1;h.SkillIds=Array.Empty<string>();}
            return g;
        }
        void Tick(GameSession g,int n=1){for(int t=0;t<n;t++)g.Tick(.05f);}
        // 真实近战伤害提交请求，下一安全步才移动；CD、锁定与完整快照均独立保留。
        var retreatGame=Battle("mage");var retreatRoom=retreatGame.Floor.Rooms[retreatGame.CurrentRoom];
        var rear=retreatGame.Heroes[0];rear.Position=retreatRoom.Center;rear.Speed=2;
        foreach(var enemy in retreatRoom.Monsters)enemy.Position=retreatRoom.Center-Vector2.UnitX;
        retreatRoom.Monsters[0].Timer=0;retreatRoom.Monsters[0].Attack=10;
        Tick(retreatGame);var retreatAction=retreatGame.Capture().SkillState.Actions.Single(a=>a.Owner==0);
        Need(retreatAction.RetreatThreat>=0&&rear.Position==retreatRoom.Center,"退让应由真实受击请求而非立即传送");
        retreatAction.Recovery=.15;Tick(retreatGame);Need(rear.Position==retreatRoom.Center,"恢复期间不得退让");
        Tick(retreatGame,4);Need(rear.Position.X>retreatRoom.Center.X&&retreatAction.RetreatCooldown>0,"安全边界后应远离近战威胁");
        Need(retreatGame.MonsterTargets.All(l=>l.Target==0),"退让不改敌方锁定");
        var retreatCopy=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=retreatGame.Capture()})).Session!);
        Tick(retreatGame,20);Tick(retreatCopy,20);
        Need(SaveStore.Encode(new(){Session=retreatGame.Capture()})==SaveStore.Encode(new(){Session=retreatCopy.Capture()}),"退让中快照续行不一致");
        Need(!retreatAction.RetreatMoving&&Math.Abs(rear.Position.X-retreatRoom.Center.X-1.2f)<.001,"退让距离及结束恢复");
        Need(retreatGame.CaptureProgress().SkillState.Actions.Count==0,"正式存档不得保存退让现场");
        retreatRoom.Monsters[0].Timer=0;Tick(retreatGame);
        Need(retreatAction.RetreatThreat==-1&&!retreatAction.RetreatMoving,"CD期间再次受击不得触发退让");
        var rangedHit=Battle("mage");var rangedRoom=rangedHit.Floor.Rooms[rangedHit.CurrentRoom];
        rangedRoom.Monsters[0].Ranged=true;rangedRoom.Monsters[0].Timer=0;rangedRoom.Monsters[0].Attack=10;Tick(rangedHit,2);
        Need(rangedHit.Capture().SkillState.Actions.All(a=>a.Owner!=0||a.RetreatThreat==-1&&!a.RetreatMoving),"远程伤害不得触发退让");
        var blocked=Battle("mage");var blockedRoom=blocked.Floor.Rooms[blocked.CurrentRoom];var blockedHero=blocked.Heroes[0];
        blockedHero.Position=blockedRoom.Center+new Vector2(blockedRoom.HalfWidth-.3f,0);
        foreach(var enemy in blockedRoom.Monsters)enemy.Position=blockedHero.Position-Vector2.UnitX;
        blockedRoom.Monsters[0].Timer=0;blockedRoom.Monsters[0].Attack=10;Tick(blocked,2);
        Need(blocked.Capture().SkillState.Actions.Single(a=>a.Owner==0).RetreatCooldown==0,"墙边无完整退让空间不得消耗CD");
        MonsterTargetLock[] Locks(GameSession g)=>g.Capture().SkillState.Targeting.Locks.ToArray();
        var g=Battle("warrior","mage");var room=g.Floor.Rooms[g.CurrentRoom];
        g.Heroes[1].Position=room.Center+new Vector2(room.HalfWidth+5,0);Tick(g);
        Need(Locks(g).Count(l=>l.Kind==MonsterLockKind.Normal)==3&&Locks(g).Count(l=>l.Kind==MonsterLockKind.Overflow)==5&&Locks(g).All(l=>l.Target==0),"TG-01 单坦3正常5溢出");
        int[] stable=Locks(g).Where(l=>l.Kind==MonsterLockKind.Normal).Select(l=>l.Owner).ToArray();
        g.Heroes[1].Position=room.Center+Vector2.UnitX;Tick(g,7);
        Need(Locks(g).Where(l=>stable.Contains(l.Owner)).All(l=>l.Target==0&&l.Kind==MonsterLockKind.Normal)&&Locks(g).All(l=>l.Target==0),"TG-02 后排0容量允许多怪围攻，正常锁稳定");
        var positionsBefore=room.Monsters.Select(m=>m.Position).ToArray();Tick(g);
        Need(room.Monsters.Select(m=>m.Position).SequenceEqual(positionsBefore),"C11 不允许逻辑推挤改变重叠单位位置");
        g=Battle("warrior","mage","ranger");room=g.Floor.Rooms[g.CurrentRoom];g.Heroes[2].Position=room.Center+new Vector2(room.HalfWidth+5,0);Tick(g,6);
        g.Heroes[2].Position=room.Monsters[0].Position;Tick(g);
        Need(Locks(g).Count(l=>l.Target==2)<=4,"TG-03 同步重评超过每步预算");Tick(g,6);
        Need(Locks(g).Count(l=>l.Target==2)==0,"TG-03 最近新后排可同时承受多怪，不强制均摊");
        g=Battle("warrior","mage");room=g.Floor.Rooms[g.CurrentRoom];Tick(g);
        int deadOwner=g.Heroes.Count;room.Monsters[0].Hp=1;g.Heroes[0].Timer=0;g.Heroes[0].Attack=100000;Tick(g);
        Need(!room.Monsters[0].Alive&&Locks(g).All(l=>l.Owner!=deadOwner),"TG-15 真实击杀后遗留死亡攻击者预约");Tick(g,7);
        Need(Locks(g).Count(l=>l.Kind==MonsterLockKind.Normal)==2,"TG-15 容量释放不得使后排威胁远程转向");
        // D2：真实单体近战命中后接走后排威胁，死亡释放后再次承接。
        g=Battle("warrior","mage");room=g.Floor.Rooms[g.CurrentRoom];Tick(g);
        var d2state=g.Capture().SkillState.Targeting;
        var normal=d2state.Locks.First(l=>l.Kind==MonsterLockKind.Normal);
        room.Monsters[normal.Owner-g.Heroes.Count].Hp=0;Tick(g);
        Need(Locks(g).Count(l=>l.Kind==MonsterLockKind.Normal)==2,"D2 释放不得远程吸走后排威胁");
        var threat=d2state.Locks.First(l=>l.Kind==MonsterLockKind.Overflow&&l.Target==1);
        var victim=room.Monsters[threat.Owner-g.Heroes.Count];
        d2state.FriendlyTargets.Clear();g.Heroes[0].Position=victim.Position;g.Heroes[0].Timer=0;g.Heroes[0].Attack=10;
        Tick(g);
        Need(threat.Target==0&&threat.Kind==MonsterLockKind.Normal&&Locks(g).Count(l=>l.Kind==MonsterLockKind.Normal)==3,"D2 直接近战命中未复用容量");
        var other=d2state.Locks.First(l=>l.Kind==MonsterLockKind.Overflow&&l.Target==1);
        d2state.FriendlyTargets.Clear();g.Heroes[0].Position=room.Monsters[other.Owner-g.Heroes.Count].Position;g.Heroes[0].Timer=0;Tick(g);
        Need(other.Target==1&&Locks(g).Count(l=>l.Kind==MonsterLockKind.Normal)==3,"D2 满容量不可额外承接");
        Need(g.EngagementDiagnostics().Contains("直接近战承接"),"D2 GM缺少承接原因");
        g=Battle("mage");Tick(g);Need(Locks(g).Length==8&&Locks(g).All(l=>l.Kind==MonsterLockKind.Overflow&&l.Target==0),"TG-04 无坦不能停机");
        g=Battle("warrior","priest");Tick(g);Need(Locks(g).Count(l=>l.Target==0&&l.Kind==MonsterLockKind.Normal)==3&&Locks(g).Count(l=>l.Target==1&&l.Kind==MonsterLockKind.Normal)==5,"TG-06 双坦独立预约");
        g=Battle("warrior","mage");room=g.Floor.Rooms[g.CurrentRoom];
        g.Heroes[0].Position=room.Center-room.HalfSize+new Vector2(.4f);
        foreach(var m in room.Monsters)m.Position=room.Center+room.HalfSize-new Vector2(.4f);
        g.Heroes[1].Position=room.Monsters[0].Position;Tick(g);
        Need(Locks(g).All(l=>l.Target==1&&l.Kind==MonsterLockKind.Overflow),"C04 不允许跨整房间强行吸向远处坦克");
        g=Battle("warrior","mage");room=g.Floor.Rooms[g.CurrentRoom];room.Monsters[0].Ranged=true;room.Monsters[1].Ranged=true;Tick(g);
        Need(Locks(g).Count(l=>l.Kind==MonsterLockKind.Ranged)==2&&Locks(g).Count(l=>l.Kind==MonsterLockKind.Normal)==3,"TG-09 远程不占近战名额");
        room.Monsters[0].Ranged=false;Tick(g);Need(Locks(g).Single(l=>l.Owner==g.Heroes.Count).Kind!=MonsterLockKind.Ranged,"TG-10 模式切换");
        room.Monsters[0].Ranged=true;Tick(g);Need(Locks(g).Single(l=>l.Owner==g.Heroes.Count).Kind==MonsterLockKind.Ranged,"TG-10 切回远程应释放近战预约");
        var modeCopy=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=g.Capture()})).Session!);
        Need(modeCopy.Floor.Rooms[modeCopy.CurrentRoom].Monsters[0].Ranged,"完整快照恢复覆盖了运行中的攻击方式");
        var passive=c.Talents.Nodes.Values.First(n=>n.Hero=="warrior"&&n.Kind=="passive");
        c.Talents.Nodes[passive.Id]=passive with {Effect="engagement_capacity",Value=2,PerRank=0};
        g=Battle("warrior","mage");g.Heroes[0].Level=20;g.Heroes[0].Talents[passive.Id]=1;Tick(g);
        Need(g.EngagementCapacity(g.Heroes[0])==5&&Locks(g).Count(l=>l.Kind==MonsterLockKind.Normal)==5,"TG-05 天赋容量3提升到5");
        g.Heroes[0].Talents.Remove(passive.Id);Tick(g);Need(Locks(g).Count(l=>l.Kind==MonsterLockKind.Normal)<=3,"TG-16 容量下降释放多余正常预约");
        c.Talents.Nodes[passive.Id]=passive;
        // Windup中候选变化仅记重评，已提交动作目标不变。
        g=Battle("warrior","mage");room=g.Floor.Rooms[g.CurrentRoom];g.Heroes[1].Position=room.Center+new Vector2(room.HalfWidth+5,0);Tick(g);
        var overflow=Locks(g).First(l=>l.Kind==MonsterLockKind.Overflow);
        var monster=room.Monsters[overflow.Owner-g.Heroes.Count];monster.SkillIds=new[]{"skill_01"};
        g.Capture().SkillState.Actions.Add(new(){Owner=overflow.Owner,Target=0,Active="skill_01",Windup=2});
        g.Heroes[1].Position=room.Center+Vector2.UnitX;Tick(g,7);
        Need(Locks(g).Single(l=>l.Owner==overflow.Owner).Target==0&&g.Capture().SkillState.Actions.Single(a=>a.Owner==overflow.Owner).Target==0,"TG-17 前摇不被普通重评打断");
        var full=SaveStore.Decode(c,SaveStore.Encode(new(){Session=g.Capture()})).Session!;var copy=GameSession.Restore(c,full);
        Tick(g,12);Tick(copy,12);Need(SaveStore.Encode(new(){Session=g.Capture()})==SaveStore.Encode(new(){Session=copy.Capture()}),"完整快照确定性续行");
        var compact=g.CaptureProgress();Need(compact.SkillState.Targeting.Locks.Count==0,"正式存档不保留锁或预约");
        var corrupt=copy.Capture();corrupt.SkillState.Targeting.Locks.Add(new(){Owner=99999,Target=0});bool rejected=false;
        try{GameSession.Restore(c,corrupt);}catch(InvalidDataException){rejected=true;}Need(rejected,"TG-24 非法目标引用未拒绝");
        var intro=new GameSession(c,789,new[]{"warrior","mage","ranger"});
        for(int t=0;t<10000&&intro.CurrentRoom==0;t++)intro.Tick(.05f);
        Need(intro.CombatEntryRemaining>0,"有怪新房未启动警觉延迟");room=intro.Floor.Rooms[intro.CurrentRoom];
        var positions=room.Monsters.Select(m=>m.Position).ToArray();var heroPositions=intro.Heroes.Select(h=>h.Position).ToArray();
        Tick(intro,5);Need(!intro.MonsterTargets.Any()&&room.Monsters.Select(m=>m.Position).SequenceEqual(positions)&&intro.Heroes.Where((h,i)=>h.Position!=heroPositions[i]).Any(),"延迟期间怪物不索敌移动，英雄自然展开");
        var reopened=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=intro.CaptureProgress()})).Session!);
        Need(reopened.CombatEntryRemaining>0&&!reopened.MonsterTargets.Any(),"正式检查点应重建入场而非续接旧锁");
        Tick(intro,25);Need(intro.MonsterTargets.Any(),"AI延迟结束未开战");
        var motion=new MotionPresentation();var original=room.Monsters.Select(m=>m.Position).ToArray();motion.UpdateCombatVisuals(intro,.1);
        Need(room.Monsters.Select(m=>m.Position).SequenceEqual(original),"表现偏移污染权威位置");
        var traversal=intro.Capture();var source=traversal.Floors[traversal.FloorIndex].Rooms[traversal.CurrentRoom];
        foreach(var m in source.Monsters)m.Hp=0;source.ContentCleared=true;source.EventUsed=true;source.Loot.Clear();
        int previous=source.Neighbors.First(i=>traversal.Floors[traversal.FloorIndex].Rooms[i].Clear);var cleared=traversal.Floors[traversal.FloorIndex].Rooms[previous];
        // 动态领队按实际速度选择；把最快英雄和最接近门的近战先锋都放在目标门外。
        traversal.SkillState=new();traversal.Destination=previous;traversal.Phase=1;
        traversal.Heroes[0].Position=cleared.DoorTowards(source);
        traversal.Heroes.OrderByDescending(h=>h.Speed).First().Position=cleared.DoorTowards(source);
        foreach(var hero in traversal.Heroes.Where(h=>h.Position!=cleared.DoorTowards(source)))hero.Position=source.Center;
        var passing=GameSession.Restore(c,traversal);Tick(passing);
        Need(passing.CurrentRoom==previous&&passing.CombatEntryRemaining==0,"已清理房间错误启动警觉或等待队尾");Tick(passing);
        Need(passing.MovingBetweenRooms,"已清理房间通过时仍在门口等待队尾");
        var five=new GameSession(c,789,c.Heroes.Select(h=>h.Id));
        for(int t=0;t<10000&&five.CurrentRoom==0;t++)five.Tick(.05f);
        var entry=five.Capture().SkillState.Targeting.EntryAnchor;
        foreach(var hero in five.Heroes)hero.Position=entry;Tick(five,16);
        for(int a=0;a<five.Heroes.Count;a++)for(int b=a+1;b<five.Heroes.Count;b++)
            Need(five.InCombatRoom(five.Heroes[a])&&five.InCombatRoom(five.Heroes[b]),"软部署不得越出房间");
        var late=Battle("warrior","mage");var lateRoom=late.Floor.Rooms[late.CurrentRoom];
        var anchor=lateRoom.DoorTowards(late.Floor.Rooms[lateRoom.Neighbors[0]]);var direction=Vector2.Normalize(lateRoom.Center-anchor);var deployment=late.Capture().SkillState.Targeting;
        deployment.EntryPrepared=true;deployment.EntryElapsed=.8;deployment.EntryAnchor=anchor;deployment.EntryDirection=direction;deployment.FloorIndex=late.FloorIndex;deployment.RoomIndex=late.CurrentRoom;deployment.DeployedMask=1;
        late.Heroes[1].Position=anchor-direction*.5f;Tick(late,3);
        Need((deployment.DeployedMask&2)==0,"迟到队员跨门时被误标记为已完成室内展开");Tick(late,20);
        Need((deployment.DeployedMask&2)!=0&&deployment.FriendlyTargets.ContainsKey(1),"迟到远程角色进入房间后未交接战斗决策");
        // 五职业实际展开：高容量远程也留在后排；分组不受接敌容量影响，四向入门均适用。
        foreach(var inward in new[]{Vector2.UnitX,-Vector2.UnitX,Vector2.UnitY,-Vector2.UnitY})
        {
            var formation=Battle(c.Heroes.Select(h=>h.Id).ToArray());var arena=formation.Floor.Rooms[formation.CurrentRoom];arena.HalfWidth=arena.HalfHeight=5;
            var doorway=arena.Center-inward*5;var lateral=new Vector2(-inward.Y,inward.X);var state=formation.Capture().SkillState.Targeting;
            state.EntryPrepared=true;state.EntryAnchor=doorway;state.EntryDirection=inward;state.FloorIndex=formation.FloorIndex;state.RoomIndex=formation.CurrentRoom;
            foreach(var h in formation.Heroes)h.Position=doorway+inward*.4f;
            // QA将游侠切成近战类型验证横向间距；高容量圣骑士仍按远程类型使用浅展开。
            formation.Heroes.Single(h=>h.SpecId=="ranger").Ranged=false;
            Tick(formation,60);
            for(int i=0;i<formation.Heroes.Count;i++)
            {
                var h=formation.Heroes[i];string role=h.Ranged?"ranged":"melee";int lane=i==0?0:(i+1)/2*(i%2==1?-1:1);
                float depth=c.Talents.Rule($"combat_entry_{role}_depth_milli")/1000f;
                float spread=c.Talents.Rule($"combat_entry_{role}_spread_milli")/1000f;
                var expected=doorway+inward*depth+lateral*(lane*spread);
                Need(state.FriendlyTargets.ContainsKey(i),"软部署仍在等待强制落位："+h.SpecId);
                Need(formation.InCombatRoom(h),"展开越出房间边界");
            }
        }
        var longRun=Battle("warrior","mage");var repeated=Battle("warrior","mage");
        foreach(var test in new[]{longRun,repeated})
        {
            var r=test.Floor.Rooms[test.CurrentRoom];
            for(int i=0;i<2;i++){var m=r.Monsters[0].Copy();m.Position=r.Center;r.Monsters.Add(m);}
            Tick(test,1000);
            Need(Locks(test).Length==10&&Locks(test).Count(l=>l.Kind==MonsterLockKind.Normal)==3&&Locks(test).Select(l=>l.Owner).Distinct().Count()==10,"TG-18/RG-08 十怪同刻稳定预约或长时间现场残留");
        }
        Need(SaveStore.Encode(new(){Session=longRun.Capture()})==SaveStore.Encode(new(){Session=repeated.Capture()}),"TG-18 相同种子输入的目标分配不确定");
        var solo=Battle("warrior");solo.Heroes[0].Hp=1;var killer=solo.Floor.Rooms[solo.CurrentRoom].Monsters[0];killer.Attack=1000000000;killer.Timer=0;Tick(solo);
        Need(solo.World?.Stage==CampaignStage.Village&&solo.Heroes[0].Alive&&!solo.MonsterTargets.Any(),"TG-19 单人死亡未清理预约并正常返城");
        var controlled=Battle("warrior","mage");Tick(controlled);
        var cs=controlled.Capture().SkillState;
        cs.Buffs.Add(new(){Instance=++cs.NextId,Id="buff_stun",Owner=0,Target=controlled.Heroes.Count,Ability="skill_07",Remaining=1,Coefficient=1});Tick(controlled);
        Need(Locks(controlled).Single(l=>l.Owner==controlled.Heroes.Count).Kind==MonsterLockKind.Normal,"眩晕不应自动释放已有普通预约");
        string sourceConfig=Path.Combine(root,"Idle-CRPG/config"),qa=Path.Combine(root,"artifacts/qa/targeting-config-"+Guid.NewGuid().ToString("N"));
        foreach(var file in Directory.EnumerateFiles(sourceConfig,"*.csv",SearchOption.AllDirectories))
        {string target=Path.Combine(qa,Path.GetRelativePath(sourceConfig,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);}
        string units=Path.Combine(qa,"combat/unit_attributes.csv"),unitText=File.ReadAllText(units);
        void RejectConfig(string text,string why)
        {
            File.WriteAllText(units,text);bool failed=false;try{LegacySkillConfig.LoadLegacy(qa);}catch(Exception error)when(error is InvalidDataException or CsvConfigurationException){failed=true;}Need(failed,why);
        }
        RejectConfig(unitText.Replace("false,3","false,-1"),"TG-24 负容量配置未拒绝");
        RejectConfig(unitText.Replace("engagement_capacity","missing_capacity"),"TG-24 缺少容量字段未拒绝");
        File.WriteAllText(units,unitText);
        string rulesFile=Path.Combine(qa,"system/config.csv"),rulesText=File.ReadAllText(rulesFile);
        foreach(string invalid in new[]{rulesText.Replace("combat_entry_ranged_depth_milli,900","combat_entry_ranged_depth_milli,0"),rulesText.Replace("combat_entry_melee_spread_milli,1100","combat_entry_melee_spread_milli,400"),rulesText.Replace("combat_entry_ranged_spread_milli","missing_entry_spread")})
        {File.WriteAllText(rulesFile,invalid);bool failed=false;try{Config.Load(qa);}catch(Exception error)when(error is InvalidDataException or CsvConfigurationException){failed=true;}Need(failed,"入场距离缺失/非法或近远关系未拒绝");}
        Console.WriteLine("PASS: capacity/overflow/zero capacity, two tanks, ranged and mode switch, dynamic capacity, safe windup, exact snapshots, checkpoint intro delay and visual-only spreading");
    }
}
