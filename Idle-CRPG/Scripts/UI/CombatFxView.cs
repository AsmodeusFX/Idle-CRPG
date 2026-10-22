using Godot;
using IdleCrpg.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using V = System.Numerics.Vector2;

/// <summary>
/// 临时战斗表现层：只消费 Core 的只读表现事件和当前 Buff/尸体状态，用几何图元绘制。
/// 不修改 Actor、Buff、弹道、伤害或随机源；正式美术接入后可逐项替换，不影响战斗权威。
/// </summary>
public partial class Main
{
    private GameSession? fxSession;
    private long fxSequence;
    private int fxFloor=-1,fxRoom=-1;
    private readonly List<CombatVisualEvent> fxEvents=new();

    private void UpdateCombatFx()
    {
        if(session==null){fxEvents.Clear();fxSession=null;return;}
        if(!ReferenceEquals(fxSession,session)||fxFloor!=session.FloorIndex||fxRoom!=session.CurrentRoom)
        {
            fxSession=session;fxFloor=session.FloorIndex;fxRoom=session.CurrentRoom;
            fxSequence=0;fxEvents.Clear();
        }
        foreach(var e in session.VisualEvents)
            if(e.Sequence>fxSequence){fxEvents.Add(e);fxSequence=e.Sequence;}
        long now=CurrentFxTick();
        fxEvents.RemoveAll(e=>FxAge(e,now)>FxLife(e.Kind));
        // 高倍速/长时间挂机时只保留最近一段，避免临时表现反向拖慢模拟。
        if(fxEvents.Count>96)fxEvents.RemoveRange(0,fxEvents.Count-96);
    }
    private long CurrentFxTick()
    {
        try{return session==null?0:session.Floor.ElapsedTicks;}catch(ArgumentOutOfRangeException){return 0;}
    }
    private static double FxAge(CombatVisualEvent e,long now)=>Math.Max(0,(now-e.Tick)*.05);
    private static double FxLife(CombatVisualKind kind)=>kind switch
    {
        CombatVisualKind.Cast=>1.0,CombatVisualKind.Hit=>.65,CombatVisualKind.Heal=>.8,
        CombatVisualKind.Buff=>1.2,CombatVisualKind.Summon=>1.0,CombatVisualKind.Chain=>.5,
        CombatVisualKind.CorpseExplosion=>1.2,_=>.7
    };

    private void DrawCombatFx()
    {
        if(session==null||session.World?.InDungeonScene!=true)return;
        DrawPersistentCombatFx();
        long now=CurrentFxTick();
        foreach(var e in fxEvents)
        {
            float t=(float)Math.Clamp(FxAge(e,now)/FxLife(e.Kind),0,1);
            switch(e.Kind)
            {
                case CombatVisualKind.Cast:DrawCastFx(e,t);break;
                case CombatVisualKind.Hit:DrawHitFx(e,t);break;
                case CombatVisualKind.Heal:DrawHealFx(e,t);break;
                case CombatVisualKind.Buff:DrawBuffFx(e,t);break;
                case CombatVisualKind.Summon:DrawSummonFx(e,t);break;
                case CombatVisualKind.Chain:DrawChainFx(e,t);break;
                case CombatVisualKind.CorpseExplosion:DrawCorpseFx(e,t);break;
                case CombatVisualKind.Pulse:DrawPulseFx(e,t);break;
            }
        }
    }

    private void DrawPersistentCombatFx()
    {
        // 光环/诅咒/嘲讽/旋风窗口的持续状态直接读 Buff 列表，而不是依赖一次性的施放事件。
        foreach(var buff in session!.PresentationBuffs)
        {
            if(buff.Remaining<=0)continue;
            var target=session.ActorForPresentation(buff.Target);
            if(target is not {Alive:true})continue;
            Vector2 p=Project(VisualPosition(target))+new Vector2(0,6);
            Color color=BuffColor(buff.Id,buff.AuraSkill);
            if(buff.AuraSkill!=""||buff.Id is "barb_taunt_buff" or "barb_whirlwind_buff")
            {
                float radius=22+Math.Abs(buff.Id.GetHashCode()%3)*4;
                DrawCircle(p,radius,Alpha(color,.07f));
                DrawArc(p,radius,0,MathF.Tau,28,Alpha(color,.6f),2,true);
            }
            if(buff.Id=="barb_whirlwind_buff")DrawWhirlwind(p);
        }
        // 未消费尸体用地面标记显示，尸爆前的可观察目标不再只存在于Core列表。
        foreach(var corpse in session.PresentationCorpses)
        {
            Vector2 p=Project(corpse.Position);
            DrawCircle(p,9,new Color(.55f,.75f,.35f,.12f));
            DrawArc(p,9,0,MathF.Tau,16,new Color(.65f,.85f,.45f,.45f),1,true);
        }
        // 嘲讽的强制锁敌用短暂连线表达，不改变怪物实际锁定。
        foreach(var lockInfo in session.MonsterTargets.Where(l=>l.Kind==MonsterLockKind.Forced))
        {
            var monster=session.ActorForPresentation(lockInfo.Owner);
            var target=session.ActorForPresentation(lockInfo.Target);
            if(monster is not {Alive:true}||target is not {Alive:true})continue;
            Vector2 from=Project(VisualPosition(monster));
            Vector2 to=Project(VisualPosition(target));
            DrawLine(from,to,Alpha(new Color("ff6b6b"),.45f),2,true);
            DrawArc(from,16,0,MathF.Tau,18,Alpha(new Color("ff6b6b"),.65f),2,true);
        }
    }

    private void DrawCastFx(CombatVisualEvent e,float t)
    {
        Vector2 p=Project(e.From);
        Color color=CastColor(e.Skill,e.Type);
        float radius=14+22*t;
        DrawCircle(p,radius,Alpha(color,.07f*(1-t)));
        DrawArc(p,radius,0,MathF.Tau,24,Alpha(color,1-t),2,true);
        // 群体/光环类技能用世界坐标范围圈表达作用域，避免只看逻辑结果。
        float extent=e.Skill switch { "barb_taunt"=>4,"pal_prayer"=>3,"pal_fanaticism"=>3,"pal_conviction"=>4,"barb_whirlwind_window"=>2.4f,_=>0 };
        if(extent>0)DrawWorldRing(e.From,extent*(.5f+.5f*t),Alpha(color,.55f*(1-t)),2);
        if(V.Distance(e.From,e.To)>.4f)
            DrawLine(p,Project(e.To),Alpha(color,.2f*(1-t)),1,true);
        DrawString(font,p+new Vector2(-24,-86-12*t),SkillName(e.Skill),HorizontalAlignment.Left,-1,12,Alpha(color,1-t));
    }

    private void DrawHitFx(CombatVisualEvent e,float t)
    {
        Vector2 from=Project(e.From),to=Project(e.To);
        Color color=DamageColor(e.Type);
        if(V.Distance(e.From,e.To)>.8f)DrawLine(from,to,Alpha(color,.35f*(1-t)),1,true);
        float angle=V.Distance(e.From,e.To)>.001f?(to-from).Angle():0;
        DrawArc(to,13+8*t,angle-.8f,angle+.8f,10,Alpha(color,1-t*.7f),3,true);
        if(e.Critical)DrawArc(to,19+10*t,0,MathF.Tau,20,Alpha(new Color("ffe18a"),1-t),2,true);
        string text=e.Dodged?"闪避":e.Blocked?"格挡":$"{Math.Round(e.Amount):0}";
        if(e.Critical&&!e.Dodged&&!e.Blocked)text="暴击 "+text;
        DrawString(font,to+new Vector2(-14,-18-26*t),text,HorizontalAlignment.Left,-1,e.Critical?14:12,Alpha(e.Dodged?new Color("a9b4c0"):color,1-t));
    }

    private void DrawHealFx(CombatVisualEvent e,float t)
    {
        Vector2 p=Project(e.To);
        Color color=new("6ee7a8");
        DrawArc(p,12+14*t,0,MathF.Tau,20,Alpha(color,1-t),2,true);
        DrawLine(p+new Vector2(-7,0),p+new Vector2(7,0),Alpha(color,1-t),3,true);
        DrawLine(p+new Vector2(0,-7),p+new Vector2(0,7),Alpha(color,1-t),3,true);
        DrawString(font,p+new Vector2(-12,-24-24*t),$"+{Math.Round(e.Amount):0}",HorizontalAlignment.Left,-1,12,Alpha(color,1-t));
    }

    private void DrawBuffFx(CombatVisualEvent e,float t)
    {
        Vector2 p=Project(e.To);
        Color color=BuffColor(e.Detail,e.Skill);
        DrawArc(p,18+14*t,0,MathF.Tau,22,Alpha(color,1-t),2,true);
        if(t<.55f)DrawString(font,p+new Vector2(-20,-30-10*t),SkillName(e.Skill),HorizontalAlignment.Left,-1,11,Alpha(color,1-t*1.6f));
    }

    private void DrawSummonFx(CombatVisualEvent e,float t)
    {
        Vector2 p=Project(e.To);
        Color color=new("9fc7b4");
        DrawArc(p,12+24*t,0,MathF.Tau,24,Alpha(color,1-t),3,true);
        for(int i=0;i<6;i++){float a=i*MathF.Tau/6+t*2;DrawCircle(p+new Vector2(MathF.Cos(a),MathF.Sin(a))*(10+16*t),2,Alpha(color,1-t));}
    }

    private void DrawChainFx(CombatVisualEvent e,float t)
    {
        Vector2 from=Project(e.From),to=Project(e.To);Color color=DamageColor(DamageType.Lightning);
        var points=new Vector2[7];
        for(int i=0;i<points.Length;i++)
        {
            float f=i/(float)(points.Length-1);var q=from.Lerp(to,f);
            if(i>0&&i<points.Length-1)q+=new Vector2(((e.Sequence+i)%2==0?1:-1)*(6+6*MathF.Sin(MathF.PI*f)),((e.Sequence+i)%3-1)*3);
            points[i]=q;
        }
        DrawPolyline(points,Alpha(color,1-t*.6f),2,true);DrawCircle(to,4,Alpha(color,1-t));
    }

    private void DrawCorpseFx(CombatVisualEvent e,float t)
    {
        Vector2 p=Project(e.To);Color color=DamageColor(DamageType.Poison);
        DrawWorldRing(e.To,e.Radius*(.35f+.65f*t),Alpha(color,.75f*(1-t)),2);
        DrawCircle(p,10*(1-t*.4f),new Color(.35f,.55f,.2f,.35f*(1-t)));
        DrawString(font,p+new Vector2(-12,-24-18*t),"尸爆",HorizontalAlignment.Left,-1,12,Alpha(color,1-t));
    }

    private void DrawPulseFx(CombatVisualEvent e,float t)
    {
        Vector2 p=Project(e.To);
        DrawWorldRing(e.To,e.Radius*(.45f+.55f*t),Alpha(DamageColor(e.Type),.65f*(1-t)),2);
        float angle=(e.Sequence%20)*.31f+t*3;
        for(int i=0;i<3;i++)DrawArc(p,20+18*t,angle+i*2.09f,angle+i*2.09f+1.0f,10,Alpha(new Color("d7e6ee"),.8f*(1-t)),2,true);
    }

    private void DrawWhirlwind(Vector2 p)
    {
        long ticks=CurrentFxTick();
        float angle=(ticks%40)*.16f;
        for(int i=0;i<3;i++)DrawArc(p,30,angle+i*2.09f,angle+i*2.09f+1.1f,10,Alpha(new Color("d7e6ee"),.7f),3,true);
    }

    private void DrawWorldRing(V center,float radius,Color color,float width)
    {
        if(radius<=.01f)return;
        var points=new Vector2[29];
        for(int i=0;i<points.Length;i++)
        {
            float angle=i/(float)(points.Length-1)*MathF.Tau;
            points[i]=Project(center+new V(MathF.Cos(angle)*radius,MathF.Sin(angle)*radius));
        }
        DrawPolyline(points,color,width,true);
    }

    private string SkillName(string id)=>session!=null&&session.Config.Skills.Skills.TryGetValue(id,out var skill)?skill.Name:id;
    private static Color Alpha(Color color,float alpha)=>new(color.R,color.G,color.B,color.A*Math.Clamp(alpha,0,1));
    private static Color DamageColor(DamageType type)=>type switch
    {
        DamageType.Fire=>new("ff9b48"),DamageType.Cold=>new("83daff"),DamageType.Lightning=>new("fff08d"),
        DamageType.Poison=>new("b5e47d"),_=>new("e2dcc9")
    };
    private static Color CastColor(string skill,DamageType type)=>skill switch
    {
        "barb_taunt"=>new("ff6b6b"),
        "pal_prayer"=>new("6ee7a8"),
        "pal_fanaticism"=>new("f0c96a"),
        "pal_conviction"=>new("c99cff"),
        "nec_amplify_damage"=>new("ff7b72"),
        "nec_lower_resist"=>new("c99cff"),
        "nec_corpse_explosion"=>new("b5e47d"),
        "barb_whirlwind_window"=>new("d7e6ee"),
        "sor_chain_lightning"=>new("fff08d"),
        _=>DamageColor(type)
    };
    private static Color BuffColor(string buff,string skill)
    {
        if(buff=="pal_prayer_buff")return new("6ee7a8");
        if(buff=="pal_fanaticism_buff")return new("f0c96a");
        if(buff=="pal_conviction_buff"||buff=="nec_lower_resist_buff")return new("c99cff");
        if(buff=="nec_amplify_buff")return new("ff7b72");
        if(buff=="barb_taunt_buff")return new("ff6b6b");
        if(buff=="barb_whirlwind_buff")return new("d7e6ee");
        if(buff=="buff_chill")return new("83daff");
        if(buff=="buff_stun")return new("ffe18a");
        if(buff=="buff_burn")return new("ff9b48");
        return skill.StartsWith("pal_")?new("f0c96a"):skill.StartsWith("nec_")?new("c99cff"):new("a9c6d8");
    }
}
