using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;
using V=System.Numerics.Vector2;

public partial class Main
{
    /// <summary>只在隔离QA入口构造战场；远程混编是测试Actor，不投放新怪物或写玩家进度。</summary>
    private void PrepareTargetingCapture(string scene)
    {
        session=GameSession.CreateWorld(config,785,scene is "intro5" or "intro-roles"?config.Heroes.Select(h=>h.Id):new[]{"warrior","mage","ranger"});
        for(int i=0;i<10000&&(session.World!.Stage!=CampaignStage.Dungeon||session.CurrentRoom==0);i++)session.Tick(.05f);
        var s=session.Capture();var room=s.Floors[s.FloorIndex].Rooms[s.CurrentRoom];
        var entry=room.Neighbors.Select(i=>room.DoorTowards(s.Floors[s.FloorIndex].Rooms[i])).OrderBy(p=>V.DistanceSquared(p,s.Heroes[0].Position)).First();
        var inward=V.Normalize(room.Center-entry);var side=new V(-inward.Y,inward.X);
        s.SkillState=new();room.Monsters.Clear();room.Origins.Clear();room.ContentCleared=false;room.Event="";
        for(int i=0;i<(scene=="crowd20"?20:8);i++)
        {
            var m=config.Spawn.Create(config.Monsters[i%config.Monsters.Count],Math.Max(1,room.SnapshotLevel),false,new());config.ApplyCombatProfile(m);
            m.Hp=m.MaxHp=100000;m.Attack=1;m.Timer=999;m.Position=room.Center+side*((i%4-1.5f)*.5f)+inward*((i/4)*.5f);room.Monsters.Add(m);
            room.Origins.Add(new(m.SpecId,m.Level,false,m.Position,new()));
        }
        for(int i=0;i<s.Heroes.Count;i++)
        {var h=s.Heroes[i];h.Position=room.Center-inward*1.6f+side*(i*.8f);h.Hp=h.MaxHp=100000;h.Timer=999;h.SkillIds=Array.Empty<string>();}
        session=GameSession.Restore(config,s);
        room=session.Floor.Rooms[session.CurrentRoom];
        if(scene is "intro" or "intro5" or "intro-roles")
        {
            var state=session.Capture().SkillState.Targeting;state.EntryPrepared=true;state.EntryAnchor=entry;state.EntryDirection=inward;state.FloorIndex=session.FloorIndex;state.RoomIndex=session.CurrentRoom;
            for(int i=0;i<session.Heroes.Count;i++)session.Heroes[i].Position=entry-inward*(i*.55f);
            // 参数对照截图隔离展开与后续索敌走位，展示五职业完成入场后的真实坐标。
            if(scene=="intro-roles")foreach(var actor in session.Heroes.Concat(room.Monsters))actor.Range=100;
            for(int i=0;i<(scene=="intro-roles"?40:9);i++)session.Tick(.05f);
        }
        else
        {
            if(scene is "tank-first" or "rear-entry")
            {
                var state=session.Capture().SkillState.Targeting;state.EntryPrepared=true;state.EntryElapsed=config.Talents.Rule("combat_entry_delay_milli")/1000d;state.EntryAnchor=entry;state.EntryDirection=inward;state.FloorIndex=session.FloorIndex;state.RoomIndex=session.CurrentRoom;state.DeployedMask=1;
                for(int i=1;i<session.Heroes.Count;i++)session.Heroes[i].Position=entry-inward*(5+i);
            }
            if(scene=="ranged")for(int i=0;i<3;i++){room.Monsters[i].Ranged=true;room.Monsters[i].Range=5;}
            for(int i=0;i<(scene=="tank-first"?5:scene=="rear-entry"?75:45);i++)session.Tick(.05f);
        }
        motion.UpdateCombatVisuals(session,.1);viewMode=0;paused=true;cameraReady=false;captureView="targeting-"+scene;
        Refresh();TickStarPanels();
        GD.Print("TARGETING_QA "+scene+": "+string.Join(",",session.MonsterTargets.Select(l=>$"{l.Owner}>{l.Target}:{l.Kind}"))+"; entry="+session.CombatEntryRemaining.ToString("0.00"));
    }
}
