using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;
using System.Collections.Generic;
public partial class Main
{
    /// <summary>480×240原生试场视图，位置/血量/目标均读Core；形状与文字同时编码，不只依赖颜色。</summary>
    private void DrawMonsterMini()
    {
        var room=session!.Floor.Rooms[session.CurrentRoom];var state=session.Capture().SkillState;
        var area=new Rect2(574,390,480,240);DrawRect(area,new Color("0a1520"));DrawRect(area,new Color("73929c"),false,1);
        var actors=room.Monsters.Where(a=>a.Alive).Concat(session.Heroes.Where(a=>a.Alive)).ToArray();
        var points=actors.ToDictionary(a=>a,a=>area.Position+new Vector2(240+a.Position.X*36,125+a.Position.Y*15));
        // 仅Mini图标避让，不写回Core坐标；所有目标与来源仍引用同一Actor。
        for(int pass=0;pass<30;pass++)for(int i=0;i<actors.Length;i++)for(int j=i+1;j<actors.Length;j++)
        {var delta=points[actors[j]]-points[actors[i]];float length=delta.Length();if(length>=30)continue;var offset=(length<.01f?Vector2.Right:delta/length)*(30-length)*.5f;points[actors[i]]-=offset;points[actors[j]]+=offset;}
        foreach(var actor in actors)points[actor]=points[actor].Clamp(area.Position+new Vector2(18,65),area.End-new Vector2(18,40));
        Vector2 Point(Actor actor)=>points.TryGetValue(actor,out var point)?point:area.Position;
        Text("MINI 480×240 · 主锁 / 前摇 / 来源",area.Position+new Vector2(10,19),13,"d9e7e8");
        foreach(var locked in session.MonsterTargets)
        {
            var actor=room.Monsters[locked.Owner-session.Heroes.Count];var target=locked.Target<session.Heroes.Count?session.Heroes[locked.Target]:session.Summons.ElementAtOrDefault(locked.Target-session.Heroes.Count);
            if(!actor.Alive||target==null)continue;
            var color=locked.Kind==MonsterLockKind.Forced?new Color("ffffff"):locked.Kind==MonsterLockKind.Special?new Color("ef78ae"):new Color("436071");
            DrawLine(Point(actor),Point(target),color,1);
        }
        foreach(var actor in room.Monsters.Where(a=>a.Alive).Concat(session.Heroes.Where(a=>a.Alive)))
        {
            bool hero=session.Heroes.Contains(actor);string id=actor.Name.Split(' ')[0];
            var def=config.MonsterPreviews.FirstOrDefault(d=>d.Id==id);
            if(actor.EnemySummonSource>=0)def=config.MonsterPreviews.First(d=>d.Id=="M01");
            var p=Point(actor);var tint=new Color(def?.Color??"d1ebff");
            if(hero)DrawCircle(p,7,tint);
            else if(actor.Ranged)DrawColoredPolygon(new[]{p+new Vector2(0,-8),p+new Vector2(8,0),p+new Vector2(0,8),p+new Vector2(-8,0)},tint);
            else DrawRect(new Rect2(p-new Vector2(7,7),new Vector2(14,14)),tint);
            DrawRect(new Rect2(p+new Vector2(-12,11),new Vector2(24,3)),new Color("422c39"));DrawRect(new Rect2(p+new Vector2(-12,11),new Vector2(24*actor.Hp/actor.MaxHp,3)),new Color("a4d994"));
            string label=hero?"友":""+(actor.EnemySummonSource>=0?"仆":def?.Symbol??"怪");
            var locked=session.MonsterTargets.FirstOrDefault(l=>l.Owner==session.Heroes.Count+room.Monsters.IndexOf(actor));
            string kind=hero?"":locked.Kind switch {MonsterLockKind.Normal=>"N",MonsterLockKind.Overflow=>"O",MonsterLockKind.Ranged=>"R",MonsterLockKind.Forced=>"F",MonsterLockKind.Special=>"S",_=>""};
            Text(label+kind,p+new Vector2(-10,-12),12,"ffffff");
            if(actor.EnemySummonSource>=0)
            {var owner=room.Monsters[actor.EnemySummonSource];if(owner.Alive)painter.DrawDashedLine(p,Point(owner),new Color("91d99c"),1,4);}
            int actorId=hero?session.Heroes.IndexOf(actor):session.Heroes.Count+room.Monsters.IndexOf(actor);
            if(state.Actions.Any(a=>a.Owner==actorId&&a.Active!=""))DrawArc(p,12,0,Mathf.Tau,24,new Color("ffd779"),2);
            if(state.Buffs.Any(b=>b.Target==actorId))Text("+",p+new Vector2(10,-5),16,"7ce1d6");
        }
        Text("N正常 O溢出 R远程 F强制 S猎手 · ○蓄力 +强化",area.Position+new Vector2(8,229),12,"abc3c9");
        string legend=string.Join("  ",room.Monsters.Where(a=>a.Alive&&a.EnemySummonSource<0).Select(a=>a.Name).Distinct());
        Text(legend,area.Position+new Vector2(8,40),12,"edf1db");
    }
}
