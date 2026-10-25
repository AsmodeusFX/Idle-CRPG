using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Collections.Generic;
using IdleCrpg.Core;

public static class ProcIcdChecks
{
    public static void Run(string root)
    {
        void Need(bool ok,string why){if(!ok)throw new Exception("PROC_ICD: "+why);}
        string qa=LegacySkillConfig.DirectoryFor(Path.Combine(root,"Idle-CRPG/config"));
        foreach(var file in Directory.GetFiles(Path.Combine(root,"tests/fixtures/proc_icd_v1"),"*.csv"))File.Copy(file,Path.Combine(qa,"skill",Path.GetFileName(file)),true);
        var c=Config.LoadLegacy(qa);
        Need(c.Skills.ProcIcdSeconds["tr_crit_shorten"]==.75&&c.Skills.ProcIcdSeconds["tr_skill_mirror"]==.75&&c.Skills.ProcLimits["tr_crit_shorten"].MaxPerCast==1,"首批测试参数未加载");
        const string A="QA_ICD_A",B="QA_ICD_B",Buff="QA_ICD_BUFF";
        c.Skills.Buffs.Add(Buff,c.Skills.Buffs["buff_crit"] with {Id=Buff,Attributes=Array.Empty<BuffAttributeSpec>(),Triggers=new[]{A},Mechanic="",Value=0});
        c.Skills.Triggers.Add(A,new(A,"OnHit","hit_player",Buff,"direct"));c.Skills.Triggers.Add(B,new(B,"OnHit","hit_player",Buff,"direct"));
        c.Skills.ProcIcdSeconds[A]=.75;c.Skills.ProcIcdSeconds[B]=.75;
        GameSession Battle(params string[] triggers)
        {
            var game=new GameSession(c,932,new[]{"necromancer","ranger","mage"});
            for(int i=0;i<10000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            var room=game.Floor.Rooms[game.CurrentRoom];var state=game.Capture().SkillState;
            state.Actions.Clear();state.Buffs.Clear();state.Flights.Clear();state.Pending.Clear();state.Corpses.Clear();state.ProcFires.Clear();state.ProcIcds.Clear();state.ProcTick=0;state.Targeting=new();
            while(room.Monsters.Count<3)room.Monsters.Add(room.Monsters[0].Copy());
            for(int i=0;i<game.Heroes.Count;i++)
            {var h=game.Heroes[i];h.Position=room.Center+new Vector2(-1,i*.2f);h.Hp=h.MaxHp=10000;h.Attack=10;h.Timer=999;h.SkillIds=Array.Empty<string>();h.Battle.CriticalChance=0;}
            for(int i=0;i<room.Monsters.Count;i++)
            {var m=room.Monsters[i];m.Position=room.Center+new Vector2(1,i*.2f);m.Hp=m.MaxHp=100000;m.Defense=0;m.Attack=0;m.Timer=999;m.Battle.Dodge=m.Battle.BlockChance=0;}
            c.Skills.Buffs[Buff]=c.Skills.Buffs[Buff] with {Triggers=triggers.Length==0?new[]{A}:triggers};
            AddBuff(game,Buff,0);AddBuff(game,Buff,1);return game;
        }
        long AddBuff(GameSession g,string id,int owner)
        {var s=g.Capture().SkillState;long instance=++s.NextId;s.Buffs.Add(new(){Instance=instance,Id=id,Owner=owner,Target=owner,Ability="basic_player",Remaining=100,Value=c.Skills.Buffs[id].Value});return instance;}
        long RootHit(GameSession g,int owner=0,int victim=0,long origin=0)
        {var s=g.Capture().SkillState;if(origin==0)origin=++s.NextId;s.Pending.Add(new(){Owner=owner,Target=g.Heroes.Count+victim,Effect="hit_player",Skill="basic_player",OriginCast=origin});return origin;}
        void Proc(GameSession g,string trigger=A,int owner=0,int victim=0,double coefficient=1,long origin=0,string skill="basic_player",long instance=0,SkillFlightState? flight=null)
        {var s=g.Capture().SkillState;s.Pending.Add(new(){Owner=owner,Target=g.Heroes.Count+victim,Effect=c.Skills.Triggers[trigger].Effect,Trigger=trigger,Skill=skill,Coefficient=coefficient,Depth=1,OriginCast=origin==0?++s.NextId:origin,BuffInstance=instance==0?s.Buffs.Single(b=>b.Id==Buff&&b.Owner==owner).Instance:instance,Flight=flight});}
        void Tick(GameSession g,int n=1){for(int i=0;i<n;i++)g.Tick(.05f);}
        long Count(GameSession g,string id=A)=>g.Capture().SkillState.ProcIcds.Count(p=>p.Trigger==id);

        var same=Battle();long before=same.HitCount;RootHit(same);RootHit(same);Tick(same);
        Need(same.HitCount-before==3&&Count(same)==1,"同Tick多命中未按队列共享ICD");
        long expires=same.Capture().SkillState.ProcIcds.Single().ReadyTick;
        Need(expires==16,"0.75秒未量化为成功Tick之后15Tick");
        Tick(same,14);before=same.HitCount;RootHit(same);Tick(same);
        Need(same.HitCount-before==2,"ICD到期边界未恢复触发");
        var chain=Battle();chain.Capture().SkillState.Pending.Add(new(){Owner=0,Target=chain.Heroes.Count,Skill="sor_chain_lightning",Effect="sor_chain_lightning_chain",OriginCast=++chain.Capture().SkillState.NextId});
        before=chain.HitCount;Tick(chain);Need(chain.HitCount-before==4,"三跳连锁没有共享同一ICD或真实伤害被删");
        var owners=Battle();RootHit(owners,0);RootHit(owners,1);Tick(owners);Need(Count(owners)==2,"多英雄错误共享ICD");
        var triggers=Battle(A,B);before=triggers.HitCount;RootHit(triggers);Tick(triggers);Need(triggers.HitCount-before==3&&triggers.Capture().SkillState.ProcIcds.Count==2,"不同TriggerID错误共享ICD");
        c.Skills.ProcLimits[A]=new(A,1,1);
        var limited=Battle();long cast=RootHit(limited);Tick(limited);Tick(limited,15);before=limited.HitCount;RootHit(limited,origin:cast);Tick(limited);
        Need(limited.HitCount-before==1&&limited.Capture().SkillState.ProcFires.Sum(p=>p.Count)==1,"ICD到期绕过根源施法/每目标限次");c.Skills.ProcLimits.Remove(A);
        c.Skills.ProcLimits[A]=new(A,0,1);var targetLimit=Battle();long targetCast=RootHit(targetLimit);Tick(targetLimit);Tick(targetLimit,15);
        before=targetLimit.HitCount;RootHit(targetLimit,origin:targetCast);RootHit(targetLimit,victim:1,origin:targetCast);Tick(targetLimit);
        Need(targetLimit.HitCount-before==3&&targetLimit.Capture().SkillState.ProcFires.Sum(p=>p.Count)==2,"每目标限次与ICD叠加失败或失败项占用ICD");c.Skills.ProcLimits.Remove(A);

        var invalid=Battle();Proc(invalid,coefficient:0);Tick(invalid);Need(Count(invalid)==0,"零效果占用ICD");
        var retry=Battle();Proc(retry,coefficient:0);Proc(retry);Tick(retry);Need(Count(retry)==1,"同Tick失败项阻断后续有效触发");
        invalid.Floor.Rooms[invalid.CurrentRoom].Monsters[0].Hp=0;Proc(invalid);Tick(invalid);Need(Count(invalid)==0,"非法目标占用ICD");
        c.Skills.Triggers[A]=c.Skills.Triggers[A] with {Chance=0};var probability=Battle();RootHit(probability);Tick(probability);Need(Count(probability)==0,"概率未通过占用ICD");c.Skills.Triggers[A]=c.Skills.Triggers[A] with {Chance=1};
        c.Skills.ProcIcdSeconds[A]=0;var disabled=Battle();before=disabled.HitCount;RootHit(disabled);RootHit(disabled);Tick(disabled);Need(disabled.HitCount-before==4&&Count(disabled)==0,"配置0仍限制Proc");
        c.Skills.ProcIcdSeconds.Remove(A);var missing=Battle();before=missing.HitCount;RootHit(missing);RootHit(missing);Tick(missing);Need(missing.HitCount-before==4,"缺配置改变旧行为");c.Skills.ProcIcdSeconds[A]=.75;
        c.Skills.ProcIcdSeconds[A]=.12;var configurable=Battle();RootHit(configurable);Tick(configurable);Need(configurable.Capture().SkillState.ProcIcds.Single().ReadyTick==4,"ICD时长被固定或未向上量化");c.Skills.ProcIcdSeconds[A]=.75;

        // 首批缩冷：仅真正缩短冷却才成功；触发一次根源施法限次与ICD并存。
        var shorten=Battle();long crit=AddBuff(shorten,"buff_crit",0);
        Proc(shorten,"tr_crit_shorten",instance:crit);Tick(shorten);Need(Count(shorten,"tr_crit_shorten")==0,"无可缩短技能仍消耗ICD");
        var ss=shorten.Capture().SkillState;ss.Actions.First(a=>a.Owner==0).Cooldowns["skill_01"]=.625;
        Proc(shorten,"tr_crit_shorten",instance:crit);Tick(shorten);Need(Count(shorten,"tr_crit_shorten")==0,"冷却保底无变化仍消耗ICD");
        ss.Actions.First(a=>a.Owner==0).Cooldowns["skill_01"]=10;long critCast=++ss.NextId;
        Proc(shorten,"tr_crit_shorten",origin:critCast,instance:crit);Proc(shorten,"tr_crit_shorten",origin:critCast,instance:crit);Tick(shorten);
        Need(Count(shorten,"tr_crit_shorten")==1&&ss.ProcFires.Where(p=>p.Trigger=="tr_crit_shorten").Sum(p=>p.Count)==1,"首批缩冷ICD/每Cast一次无效");

        // 数量上限失败后不占ICD，下一次出现空位可立即成功。
        var savedTrigger=c.Skills.Triggers[A];c.Skills.Triggers[A]=savedTrigger with {Effect="nec_raise_skeleton_summon"};
        // 用例只验证“上限失败”，与生产上限解耦：本地把骷髅上限压到1只，不能依赖生产数值。
        var savedSkeleton=c.Skills.Summons["nec_skeleton"];c.Skills.Summons["nec_skeleton"]=savedSkeleton with {MaxCount=1};
        var summon=Battle();summon.Heroes[0].SkillIds=new[]{"nec_raise_skeleton"};Proc(summon,skill:"nec_raise_skeleton");Tick(summon);summon.Capture().SkillState.ProcIcds.Clear();
        Proc(summon,skill:"nec_raise_skeleton");Tick(summon);Need(Count(summon)==0&&summon.Summons.Count(s=>s.Alive)==1,"召唤满额消耗ICD");c.Skills.Triggers[A]=savedTrigger;c.Skills.Summons["nec_skeleton"]=savedSkeleton;

        // 镜像沿用既有弹体全局预算与生命周期，未新增专属数值。
        var mirror=Battle();AddBuff(mirror,"buff_mirror",0);var ms=mirror.Capture().SkillState;
        for(int i=0;i<2;i++)ms.Pending.Add(new(){Owner=0,Target=mirror.Heroes.Count,Effect="cast_skill_01",Skill="skill_01",OriginCast=++ms.NextId});
        Tick(mirror);Need(ms.Flights.Count(f=>f.Mirrored)==1&&Count(mirror,"tr_skill_mirror")==1,"同Tick镜像未共享ICD");
        var first=ms.Flights.First(f=>!f.Mirrored);ms.ProcIcds.Clear();double capacity=c.Skills.Settings["max_flights"];c.Skills.Settings["max_flights"]=ms.Flights.Count;
        long mi=ms.Buffs.Single(b=>b.Id=="buff_mirror").Instance;
        Proc(mirror,"tr_skill_mirror",instance:mi,flight:first);Tick(mirror);Need(Count(mirror,"tr_skill_mirror")==0,"镜像全局弹体满额占用ICD");c.Skills.Settings["max_flights"]=capacity;
        Proc(mirror,"tr_skill_mirror",instance:mi,flight:first);Tick(mirror);Need(Count(mirror,"tr_skill_mirror")==1,"镜像恢复容量后无法立即触发");
        Tick(mirror,300);Need(ms.Flights.All(f=>!f.Mirrored),"镜像未沿原弹体生命周期销毁");

        // D4：连续施法与高频事件仍只按既有ICD产生镜像，不增加镜像专属限制或修改原型数值。
        void Cast(GameSession g)
        {var s=g.Capture().SkillState;s.Pending.Add(new(){Owner=0,Target=g.Heroes.Count,Effect="cast_skill_01",Skill="skill_01",OriginCast=++s.NextId});}
        var burst=Battle();AddBuff(burst,"buff_mirror",0);var burstState=burst.Capture().SkillState;var mirrorIds=new HashSet<long>();
        for(int t=0;t<90;t++)
        {
            for(int castIndex=0;castIndex<4;castIndex++)Cast(burst);
            Tick(burst);
            foreach(var f in burstState.Flights.Where(f=>f.Mirrored))mirrorIds.Add(f.Id);
            Need(burstState.Flights.Count<=c.Skills.Settings["max_flights"],"连续高频施法绕过全局弹体预算");
        }
        Need(mirrorIds.Count==6,"连续90Tick高频施法未按15Tick间隔触发6次镜像");
        Tick(burst,300);Need(burstState.Flights.Count==0,"连续施法停止后原/镜像弹体没有自然销毁");

        // 关闭QA镜像ICD单独验证原有不可递归规则，避免ICD掩盖递归错误。
        c.Skills.ProcIcdSeconds["tr_skill_mirror"]=0;
        var recursion=Battle();AddBuff(recursion,"buff_mirror",0);Cast(recursion);var recursionIds=new HashSet<long>();
        for(int t=0;t<300;t++){Tick(recursion);foreach(var f in recursion.Capture().SkillState.Flights.Where(f=>f.Mirrored))recursionIds.Add(f.Id);}
        Need(recursionIds.Count==1&&recursion.Capture().SkillState.Flights.Count==0,"关闭ICD后镜像递归生成镜像或未销毁");
        c.Skills.ProcIcdSeconds["tr_skill_mirror"]=.75;

        // 用实际全局预算填满QA现场，不能只依赖缩小预算的单元样本。
        var full=Battle();long fullBuff=AddBuff(full,"buff_mirror",0);Cast(full);Tick(full);var fullState=full.Capture().SkillState;
        var fullSource=fullState.Flights.First(f=>!f.Mirrored);fullState.Flights.RemoveAll(f=>f.Mirrored);fullState.ProcIcds.Clear();fullSource.Delay=1;
        while(fullState.Flights.Count<c.Skills.Settings["max_flights"])
        {var clone=JsonSerializer.Deserialize<SkillFlightState>(JsonSerializer.Serialize(fullSource,SaveStore.JsonOptions),SaveStore.JsonOptions)!;clone.Id=++fullState.NextId;fullState.Flights.Add(clone);}
        Proc(full,"tr_skill_mirror",instance:fullBuff,flight:fullSource);Tick(full);
        Need(fullState.Flights.Count==c.Skills.Settings["max_flights"]&&fullState.Flights.All(f=>!f.Mirrored)&&Count(full,"tr_skill_mirror")==0,"实际全局预算满额仍生成镜像或消耗ICD");
        fullState.Flights.RemoveAt(fullState.Flights.Count-1);Proc(full,"tr_skill_mirror",instance:fullBuff,flight:fullSource);Tick(full);
        Need(fullState.Flights.Count==c.Skills.Settings["max_flights"]&&fullState.Flights.Count(f=>f.Mirrored)==1,"仅余一个全局名额时镜像未共用同一预算");

        var mirrorRoom=Battle();AddBuff(mirrorRoom,"buff_mirror",0);Cast(mirrorRoom);Tick(mirrorRoom);var mirrorRoomState=mirrorRoom.Capture().SkillState;
        Need(mirrorRoomState.Flights.Any(f=>f.Mirrored),"换房检查未构造存活镜像");
        var cleared=mirrorRoom.Floor.Rooms[mirrorRoom.CurrentRoom];foreach(var m in cleared.Monsters)m.Hp=0;cleared.ContentCleared=true;cleared.EventUsed=true;cleared.Loot.Clear();int mirrorOld=mirrorRoom.CurrentRoom;
        for(int t=0;t<20000&&mirrorRoom.CurrentRoom==mirrorOld;t++)Tick(mirrorRoom);
        Need(mirrorRoom.CurrentRoom!=mirrorOld&&mirrorRoom.Capture().SkillState.Flights.Count==0&&mirrorRoom.Capture().SkillState.ProcIcds.Count==0,"换房残留镜像弹体或ICD");
        Console.WriteLine("MIRROR_D4_OK: 360 casts/90 ticks, ICD-off non-recursion, actual shared flight budget/full/recovery, natural expiry and room cleanup; original settings unchanged");

        // 完整内部快照确定性续跑；正式检查点重开房间，不保存ICD。
        var replay=Battle();RootHit(replay);Tick(replay);
        string Encode(GameSession g)=>SaveStore.Encode(new(){Session=g.Capture()});
        var copy=GameSession.Restore(c,SaveStore.Decode(c,Encode(replay)).Session!);Need(Encode(copy)==Encode(replay),"ICD完整快照不一致");
        for(int i=0;i<20;i++){RootHit(replay);RootHit(copy);Tick(replay);Tick(copy);}Need(Encode(copy)==Encode(replay),"ICD读回后续跑不确定");
        var compact=replay.CaptureProgress();Need(compact.SkillState.ProcTick==0&&compact.SkillState.ProcIcds.Count==0&&compact.SkillState.ProcFires.Count==0,"长期进度保存ICD/限次现场");
        var reopened=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=compact})).Session!);Need(reopened.Capture().SkillState.ProcIcds.Count==0,"重建房间继承ICD");
        var oldJson=JsonNode.Parse(Encode(copy))!;var oldState=oldJson["Run"]!["SkillState"]!.AsObject();oldState.Remove("ProcTick");oldState.Remove("ProcIcds");
        Need(SaveStore.Decode(c,oldJson.ToJsonString()).Session!.SkillState.ProcIcds.Count==0,"旧内部快照缺少ICD字段无法兼容");
        var invalidClock=copy.Capture();invalidClock.SkillState.ProcIcds[0].ReadyTick=invalidClock.SkillState.ProcTick+16;
        bool badClock=false;try{GameSession.Restore(c,invalidClock);}catch(InvalidDataException){badClock=true;}Need(badClock,"非法ICD未来Tick未拒绝");
        var level=Battle();RootHit(level);Tick(level);var floorSnap=level.Capture();Need(floorSnap.Floors.Count>1,"换层检查没有下一层");
        foreach(var r in floorSnap.Floors[0].Rooms){r.Generated=true;r.Revealed=true;r.ContentCleared=true;r.EventUsed=true;r.Loot.Clear();}
        floorSnap.CurrentRoom=floorSnap.Floors[0].BossRoom.Id;floorSnap.Regroup=.6f;
        foreach(var h in floorSnap.Heroes)h.Position=floorSnap.Floors[0].StairPosition(floorSnap.Floors[0].BossRoom);
        floorSnap.SkillState.Actions.Clear();floorSnap.SkillState.Buffs.Clear();floorSnap.SkillState.Flights.Clear();floorSnap.SkillState.Pending.Clear();floorSnap.SkillState.ProcFires.Clear();floorSnap.SkillState.Targeting=new();
        level=GameSession.Restore(c,floorSnap);Tick(level);Need(level.FloorIndex==1&&level.Capture().SkillState.ProcIcds.Count==0&&level.Capture().SkillState.ProcTick==0,"换层未清ICD");
        var room=replay.Floor.Rooms[replay.CurrentRoom];foreach(var m in room.Monsters)m.Hp=0;room.ContentCleared=true;room.EventUsed=true;room.Loot.Clear();int old=replay.CurrentRoom;
        for(int i=0;i<20000&&replay.CurrentRoom==old;i++)Tick(replay);Need(replay.CurrentRoom!=old&&replay.Capture().SkillState.ProcIcds.Count==0,"换房ICD未清理");

        string icdFile=Path.Combine(qa,"skill/proc_icd.csv");string original=File.ReadAllText(icdFile);
        foreach(string bad in new[]{"tr_crit_shorten,-1","tr_crit_shorten,NaN","QA_MISSING,0.75","tr_crit_shorten,Infinity"})
        {File.WriteAllText(icdFile,"id,icd_seconds\n"+bad+"\n");bool rejected=false;try{SkillConfiguration.Load(qa,c.Attributes);}catch(Exception e) when(e is InvalidDataException or CsvConfigurationException){rejected=true;}Need(rejected,"非法ICD配置未拒绝："+bad);}File.WriteAllText(icdFile,original);
        string triggerFile=Path.Combine(qa,"skill/trigger.csv"),triggerOriginal=File.ReadAllText(triggerFile);
        var rows=triggerOriginal.TrimEnd().Split('\n').Select((line,i)=>line.TrimEnd('\r')+(i==0?",proc_chance":",0.5"));File.WriteAllText(triggerFile,string.Join('\n',rows)+"\n");
        Need(SkillConfiguration.Load(qa,c.Attributes).Triggers["tr_crit_shorten"].Chance==.5,"可选概率字段加载失败");File.WriteAllText(triggerFile,triggerOriginal);
        File.Delete(icdFile);Need(SkillConfiguration.Load(qa,c.Attributes).ProcIcdSeconds.Count==0,"缺少ICD文件不兼容旧配置");File.WriteAllText(icdFile,original);
        Need(Config.Load(Path.Combine(root,"Idle-CRPG/config")).Skills.Triggers.Count==0,"首批退役触发器重新泄漏正式配置");
        Console.WriteLine("PROC_ICD_OK: same-tick queue/chain, owner/trigger isolation, 15-tick boundary, per-cast/target limits, probability/invalid/zero/cap failures, cooldown/mirror pilots, lifecycle and snapshot/progress regression");
    }
}
