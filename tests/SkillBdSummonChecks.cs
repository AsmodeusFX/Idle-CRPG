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
        var c=Config.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
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
        for(int i=0;i<20000&&crossing.CurrentRoom==oldRoom;i++)Tick(crossing);
        Need(crossing.CurrentRoom!=oldRoom&&Live(crossing).Length==1&&Math.Abs(Live(crossing)[0].Hp-20)<.0001f,"跨房丢失召唤或补满实际HP");
        var compact=crossing.CaptureProgress();Need(compact.Summons.Count==1&&Math.Abs(compact.Summons[0].Hp-20)<.0001f,"进房检查点未保留召唤实际HP");
        var reopened=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new(){Session=compact})).Session!);
        Need(Live(reopened).Length==1&&Math.Abs(Live(reopened)[0].Hp-20)<.0001f,"正式检查点重开丢失召唤");

        // 主人卸槽/死亡立即撤销；全灭清除全部召唤记录。
        var unslot=Battle();Summon(unslot);unslot.EquipSkills("priest",Array.Empty<string>());Need(Live(unslot).Length==0,"主人卸槽后召唤仍存活");
        var ownerDead=Battle();Summon(ownerDead);ownerDead.Heroes[0].Hp=0;Tick(ownerDead);Need(Live(ownerDead).Length==0,"主人死亡后召唤仍存活");
        var wipe=Battle();Summon(wipe);foreach(var h in wipe.Heroes)h.Hp=0;Tick(wipe);Need(wipe.Summons.Count==0&&wipe.Capture().Summons.Count==0,"全灭没有清除召唤记录");

        Need(!Config.LoadLegacy(Path.Combine(root,"Idle-CRPG/config")).Skills.Summons.ContainsKey("QA_SKELETON"),"QA召唤定义泄漏生产配置");
        Console.WriteLine("PASS: B2 independent skeleton summon, owner-relative stats, single cap, auto-attack XP, no death rewards, cross-room HP, room checkpoint restore, unslot/death/wipe cleanup");
    }
}
