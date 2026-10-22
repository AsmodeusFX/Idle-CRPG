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
}
