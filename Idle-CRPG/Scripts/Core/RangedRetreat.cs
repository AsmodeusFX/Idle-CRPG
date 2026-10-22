using System;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    /// <summary>受近战直接伤害后在前摇/恢复结束的首个逻辑步退让；只移动自身，不触碰锁敌与容量。</summary>
    private bool RunRetreat(Actor actor,float dt,Room room)
    {
        var action=skillState.Actions.FirstOrDefault(a=>a.Owner==CombatId(actor));
        if(action==null)return false;
        if(!actor.Ranged){action.RetreatThreat=-1;action.RetreatMoving=false;return false;}
        if(action.Active!=""||action.Recovery>0)return false;
        float speed=(float)EffectiveValue(actor,"move_speed",actor.Speed)*MoveMultiplier(actor);
        if(speed<=0)return false;
        if(!action.RetreatMoving&&action.RetreatThreat>=0)
        {
            var threat=CombatActor(action.RetreatThreat);action.RetreatThreat=-1;
            if(action.RetreatCooldown>0||threat is not {Alive:true}||!InCombatRoom(threat))return false;
            var away=actor.Position-threat.Position;
            if(away.LengthSquared()<.000001f)return false;
            away=Vector2.Normalize(away);
            float distance=Config.Talents.Rule("ranged_retreat_distance_milli")/1000f;
            var min=room.Center-room.HalfSize+new Vector2(.3f);var max=room.Center+room.HalfSize-new Vector2(.3f);
            // 固定候选顺序保证重放一致；沿远离方向的扇形寻找完整距离，禁止夹墙后原地消耗CD。
            bool found=false;
            foreach(float angle in new[]{0f,.3926991f,-.3926991f,.7853982f,-.7853982f})
            {
                var direction=new Vector2(away.X*MathF.Cos(angle)-away.Y*MathF.Sin(angle),away.X*MathF.Sin(angle)+away.Y*MathF.Cos(angle));
                var goal=actor.Position+direction*distance;
                if(goal.X<min.X||goal.Y<min.Y||goal.X>max.X||goal.Y>max.Y)continue;
                action.RetreatGoal=goal;found=true;break;
            }
            if(!found)return false;
            action.RetreatMoving=true;action.RetreatCooldown=Config.Talents.Rule("ranged_retreat_cooldown_milli")/1000d;
        }
        if(!action.RetreatMoving)return false;
        Move(actor,action.RetreatGoal,dt,speed);
        if(Vector2.DistanceSquared(actor.Position,action.RetreatGoal)<.000001f)action.RetreatMoving=false;
        return true;
    }
}
