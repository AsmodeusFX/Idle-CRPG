using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleCrpg.Core;

/// <summary>Steam D2 V1.2：入口通行、敌方启动、出生避让与召唤物独立通行的隔离检查。</summary>
public static class RoomEntryChecks
{
    public static void Run(string root)
    {
        void Need(bool value,string why){if(!value)throw new Exception("ROOM_ENTRY: "+why);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        float pass=c.Talents.Rule("combat_entry_pass_depth_milli")/1000f;
        float threat=c.Talents.Rule("combat_entry_threat_range_milli")/1000f;
        double alert=c.Talents.Rule("combat_entry_delay_milli")/1000d;
        double cap=c.Talents.Rule("combat_entry_alert_cap_milli")/1000d;
        void Tick(GameSession g,int n=1){for(int i=0;i<n;i++)g.Tick(.05f);}
        GameSession Entered(int party=0)
        {
            var ids=(party<=0?c.Heroes.Take(4):c.Heroes.Take(party)).Select(h=>h.Id).ToArray();
            var g=new GameSession(c,785,ids);
            for(int t=0;t<10000&&g.CurrentRoom==0;t++)g.Tick(.05f);
            return g;
        }
        // 隔离场景：重配当前房间尺寸与怪物，并把入口门线/朝内方向显式写入唯一入场现场。
        // doorGap<0 表示沿用“已入房”的前置态；>=0 表示英雄停在门线外 doorGap 处（AUD-001 边界用例）。
        (GameSession Game,Room Room,Vector2 Anchor,Vector2 Inward,CombatTargetingState State) Arena(Vector2 inward,int width,int height,int monsters=3,int party=0,float doorGap=-1f)
        {
            var g=Entered(party);var room=g.Floor.Rooms[g.CurrentRoom];
            room.HalfWidth=width/2f;room.HalfHeight=height/2f;
            room.Monsters.Clear();room.Origins.Clear();room.Loot.Clear();
            room.ContentCleared=false;room.Event="";room.EventUsed=true;
            var skin=g.Capture().SkillState;
            skin.Targeting=new();skin.Actions.Clear();skin.Buffs.Clear();skin.Flights.Clear();skin.Pending.Clear();
            var anchor=room.Center-inward*(Math.Abs(inward.X)>.5f?room.HalfWidth:room.HalfHeight);
            var state=skin.Targeting;
            state.EntryPrepared=true;state.EntryElapsed=0;state.EntryAnchor=anchor;state.EntryDirection=inward;
            state.FloorIndex=g.FloorIndex;state.RoomIndex=g.CurrentRoom;
            var lateral=new Vector2(-inward.Y,inward.X);
            for(int i=0;i<monsters;i++)
            {
                var m=Actor.From(c.Monsters[0]);c.ApplyCombatProfile(m);
                m.Hp=m.MaxHp=1000000;m.Attack=0;m.Timer=999;m.Speed=.0001f;m.Range=.2f;m.Cooldown=1;
                m.Position=anchor+inward*(Math.Min(room.HalfHeight,room.HalfWidth)*2-.8f)+lateral*((i-1)*.5f);
                room.Monsters.Add(m);
            }
            foreach(var h in g.Heroes){h.Hp=h.MaxHp=1000000;h.Timer=999;h.Attack=1;}
            for(int i=0;i<g.Heroes.Count;i++)
            {
                float offset=(i-(g.Heroes.Count-1)/2f)*.12f;
                g.Heroes[i].Position=doorGap>=0?anchor-inward*doorGap+lateral*offset:anchor+inward*.2f+lateral*offset;
            }
            return (g,room,anchor,inward,state);
        }
        int Index(GameSession g,Actor actor)=>g.Heroes.Contains(actor)?g.Heroes.IndexOf(actor):g.Heroes.Count+g.Floor.Rooms[g.CurrentRoom].Monsters.Count+g.Summons.IndexOf(actor);

        // 一、四方向 × 四种房间尺寸：不得越过门线即停战；交接时已达通行深度，且逐单位独立。
        foreach(var inward in new[]{Vector2.UnitX,-Vector2.UnitX,Vector2.UnitY,-Vector2.UnitY})
        foreach(var size in new[]{(6,5),(7,6),(8,7),(9,8)})
        {
            var scene=Arena(inward,size.Item1,size.Item2);
            var g=scene.Game;var state=scene.State;
            Tick(g);
            for(int i=0;i<g.Heroes.Count;i++)
                Need(!state.FriendlyTargets.ContainsKey(i),"单位刚越过门线就停下进入战斗");
            var handed=new bool[g.Heroes.Count];
            for(int t=0;t<200&&handed.Any(h=>!h);t++)
            {
                Tick(g);
                for(int i=0;i<g.Heroes.Count;i++)
                {
                    if(handed[i]||!state.FriendlyTargets.ContainsKey(i))continue;
                    handed[i]=true;
                    bool exempt=(state.EntryEngagedMask&(1<<i))!=0;
                    Need(exempt||g.EntryDepth(g.Heroes[i].Position)>=pass-.05f,"未通行就停止移动攻击");
                }
            }
            Need(handed.All(h=>h),"四方向入口未在有限步内完成通行交接");
            Need(g.Heroes.All(g.InCombatRoom),"通行过程中越出房间边界");
        }

        // 单单位独立：队首已接战，队尾仍在走廊时必须继续向门内推进，不被队首状态冻结。
        {
            var scene=Arena(Vector2.UnitX,9,8,2);
            var g=scene.Game;var state=scene.State;
            var lateral=new Vector2(-scene.Inward.Y,scene.Inward.X);
            g.Heroes[0].Position=scene.Anchor+scene.Inward*1.45f;
            g.Heroes[1].Position=scene.Anchor+scene.Inward*.2f+lateral*.5f;
            for(int i=2;i<g.Heroes.Count;i++)g.Heroes[i].Position=scene.Anchor-scene.Inward*(1f+i*.6f)+lateral*(i*.3f);
            var tail=g.Heroes[^1];
            Tick(g,3);
            Need(!state.FriendlyTargets.ContainsKey(Index(g,tail)),"队尾在走廊就参与了战斗");
            float before=g.EntryDepth(tail.Position);
            Tick(g,10);
            Need((state.EntryEngagedMask&(1<<0))!=0||(state.EntryPassMask&(1<<0))!=0,"队首未先行完成通行或接战豁免");
            Need(!state.FriendlyTargets.ContainsKey(Index(g,tail)),"队尾未通行就参与战斗");
            Need(g.EntryDepth(tail.Position)>before+.2f,"队首接战后队尾停在门口");
        }

        // 二、近距离威胁允许立即接战，且接战后不再补做通行。
        {
            var scene=Arena(Vector2.UnitY,7,6,1);
            var g=scene.Game;var state=scene.State;
            var monster=scene.Room.Monsters[0];
            var hero=g.Heroes[0];
            hero.Position=scene.Anchor+scene.Inward*.4f;
            monster.Position=hero.Position+scene.Inward*(threat*.8f);
            Tick(g);
            Need((state.EntryEngagedMask&1)!=0,"近距离威胁未允许立即接战");
            Need((state.EntryPassMask&1)==0,"提前接战被记成已完成通行");
            Need(g.EntryDepth(hero.Position)<pass,"用例前提：接战时仍在入口浅区");
            monster.Position=scene.Anchor+scene.Inward*6f;
            Tick(g,20);
            Need((state.EntryPassMask&1)==0,"接战后仍补做入口通行记录");
            Need((state.EntryEngagedMask&1)!=0,"接战豁免被撤销");
        }

        // 二之二：远程在通行边界即可覆盖远处目标时，应停下攻击而不是继续深入。
        {
            var scene=Arena(Vector2.UnitX,9,8,5);
            var g=scene.Game;var state=scene.State;
            var ranged=g.Heroes.First(h=>h.Ranged);
            ranged.Range=6;
            foreach(var m in scene.Room.Monsters)m.Position=scene.Anchor+scene.Inward*4.2f;
            bool handed=false;
            for(int t=0;t<200&&!handed;t++){Tick(g);handed=state.FriendlyTargets.ContainsKey(Index(g,ranged));}
            Need(handed,"远程未完成入口通行交接");
            float depth=g.EntryDepth(ranged.Position);
            Need(depth>=pass-.05f&&depth<pass+.6f,"远程未在通行边界停下攻击");
            Tick(g,20);
            Need(g.EntryDepth(ranged.Position)<pass+.8f,"远程在射程内仍继续深入");
        }

        // 三、敌方启动：基础警觉保留；参考前方入房进度；异常时按兜底上限启动。
        {
            var scene=Arena(Vector2.UnitX,9,8,2);
            var g=scene.Game;
            foreach(var h in g.Heroes)h.Speed=0;
            Tick(g,16);
            Need(Math.Abs(g.CombatEntryRemaining-(cap-alert))<.06,"基础警觉零点未正确保留");
            Need(!g.MonsterTargets.Any(),"前方未通行就提前启动敌人");
            int extra=0;
            while(g.CombatEntryRemaining>0&&extra<80){Tick(g);extra++;}
            Need(!g.MonsterTargets.Any()==false,"兜底上限后敌人仍未启动");
            Need(scene.State.EntryElapsed>=cap-.06,"敌人启动早于兜底上限");

            var normal=Arena(Vector2.UnitX,9,8,2);
            var ng=normal.Game;int steps=0;
            while(!ng.MonsterTargets.Any()&&steps<80){Tick(ng);steps++;}
            double at=normal.State.EntryElapsed;
            Need(ng.MonsterTargets.Any(),"正常入房后敌人未启动");
            Need(at>=alert-.06&&at<cap,"参考入房进度时敌人启动时机错误");
        }

        // 四、出生避让：四方向入口内侧通行区不得刷怪，四边0.8U留白保留，数量与随机可复现。
        {
            int DirectionKey(Vector2 d)=>Math.Abs(d.X)>.5f?(d.X>0?0:1):(d.Y>0?2:3);
            var seen=new HashSet<int>();
            int seed=20261023;
            for(int attempt=0;attempt<6&&seen.Count<4;attempt++)
            {
                var g=new GameSession(c,seed+attempt,c.Heroes.Take(4).Select(h=>h.Id));
                int last=g.CurrentRoom;
                for(int t=0;t<200000&&seen.Count<4;t++)
                {
                    foreach(var h in g.Heroes){h.Hp=h.MaxHp;}
                    g.Tick(.05f);
                    if(g.CurrentRoom==last)continue;
                    last=g.CurrentRoom;
                    var st=g.Capture().SkillState.Targeting;
                    if(!st.EntryPrepared)continue;
                    var room=g.Floor.Rooms[g.CurrentRoom];
                    Need(room.Monsters.Count is >=1 and <=5,"入口避让改变了每房数量");
                    foreach(var m in room.Monsters)
                    {
                        Need(Vector2.Dot(m.Position-st.EntryAnchor,st.EntryDirection)>=pass-.0001f,"怪物刷在入口通行区内");
                        var delta=Vector2.Abs(m.Position-room.Center);
                        Need(delta.X<=room.HalfWidth-.8f+.0001f&&delta.Y<=room.HalfHeight-.8f+.0001f,"出生点未保留四边留白");
                    }
                    seen.Add(DirectionKey(st.EntryDirection));
                }
            }
            Need(seen.Count==4,"未覆盖四个方向的入口出生避让");
            // 相同 seed 的出生随机不得因避让重试变得不确定。
            string Spawn(int s)
            {
                var g=new GameSession(c,s,c.Heroes.Take(4).Select(h=>h.Id));
                for(int t=0;t<20000&&g.CurrentRoom==0;t++)g.Tick(.05f);
                return string.Join(";",g.Floor.Rooms[g.CurrentRoom].Monsters.Select(m=>$"{m.SpecId}:{m.Position.X:F4},{m.Position.Y:F4}"));
            }
            Need(Spawn(4242)==Spawn(4242),"出生避让重试破坏了随机可复现");
        }

        // 五、召唤物独立通行：三只骷髅同样要先入房约1.5U；接敌容量仍取 summon.csv 定义值。
        {
            var scene=Arena(Vector2.UnitX,9,8,1);
            var g=scene.Game;var state=scene.State;
            var def=c.Skills.Summons["nec_skeleton"];
            var lateral=new Vector2(-scene.Inward.Y,scene.Inward.X);
            // 直接登记召唤实体：主人装配槽必须包含其来源技能，否则既有清理逻辑会当场撤销召唤。
            string sourceSkill=c.Skills.Skills.Keys.First(k=>c.Skills.Skills[k].Kind=="ActiveSkill");
            g.Heroes[0].SkillIds=new[]{sourceSkill};
            var summons=new List<Actor>();
            for(int i=0;i<3;i++)
            {
                var summon=Actor.From(c.Heroes[0]);
                summon.Name="骷髅"+i;summon.SummonDefinitionId="nec_skeleton";summon.SummonSkill=sourceSkill;summon.SummonOwner=0;
                summon.Hp=summon.MaxHp=1000;summon.Timer=999;summon.Attack=1;
                summon.Position=scene.Anchor+scene.Inward*.1f+lateral*(i*.3f);
                g.Summons.Add(summon);summons.Add(summon);
            }
            Tick(g);
            foreach(var summon in summons)Need(!state.FriendlyTargets.ContainsKey(Index(g,summon)),"召唤物越过门线就停下战斗");
            bool[] handed=new bool[summons.Count];
            for(int t=0;t<200&&handed.Any(h=>!h);t++)
            {
                Tick(g);
                for(int i=0;i<summons.Count;i++)
                {
                    if(handed[i]||!state.FriendlyTargets.ContainsKey(Index(g,summons[i])))continue;
                    handed[i]=true;
                    int index=Index(g,summons[i]);
                    bool exempt=(state.EntryEngagedMask&(1<<index))!=0;
                    Need(exempt||g.EntryDepth(summons[i].Position)>=pass-.05f,"召唤物未通行就停止移动攻击");
                }
            }
            Need(handed.All(h=>h),"召唤物未在有限步内完成通行交接");
            Need(g.EngagementCapacity(summons[0])==def.EngagementCapacity,"召唤物接敌容量来源与 summon.csv 不一致");
        }
        // 六、GM 诊断必须直接暴露门线、朝内方向、通行边界、单位坐标、停止原因与敌方警觉。
        {
            var scene=Arena(Vector2.UnitY,8,7,2);
            Tick(scene.Game,2);
            var text=string.Join("\n",scene.Game.EntryDiagnostics());
            foreach(var key in new[]{"门线","朝内","通行边界","敌方警觉","坐标","停止原因","通行中"})
                Need(text.Contains(key),"GM入场诊断缺少字段："+key);
            Need(string.Join("\n",scene.Game.EngagementDiagnostics()).Contains("通行边界"),"GM主诊断未包含入场通行信息");
        }
        // 七、AUD-001/AUD-002 边界带：门线外 0~0.0019U（覆盖位移到达阈值两侧）×
        //     四方向 × 四种房间尺寸 × 1/2/3 人。断言有限时间内入房或合法提前接战，
        //     且不存在“持续无位移、无目标、无战斗”的死锁。
        {
            Need(SpatialTolerance.RoomBoundary>=SpatialTolerance.MoveArrival,"房间归属容差必须不小于位移到达阈值");
            foreach(float gap in new[]{0f,0.00005f,0.0003f,0.0006f,0.0009f,0.0011f,0.0015f,0.0019f})
            foreach(var inward in new[]{Vector2.UnitX,-Vector2.UnitX,Vector2.UnitY,-Vector2.UnitY})
            foreach(var size in new[]{(6,5),(7,6),(8,7),(9,8)})
            foreach(int party in new[]{1,2,3})
            {
                string tag=$"gap={gap:F5}/{inward}/room={size.Item1}x{size.Item2}/{party}人";
                var scene=Arena(inward,size.Item1,size.Item2,2,party,gap);
                var g=scene.Game;
                var handed=new int[g.Heroes.Count];
                int idle=0;
                for(int t=0;t<80;t++)
                {
                    var before=g.Heroes.Select(h=>h.Position).ToArray();
                    Tick(g);
                    bool moved=false,hasTarget=scene.State.FriendlyTargets.Count>0;
                    for(int i=0;i<g.Heroes.Count;i++)
                    {
                        if(Vector2.Distance(before[i],g.Heroes[i].Position)>0.0005f)moved=true;
                        int bit=1<<i;
                        if((scene.State.EntryPassMask&bit)!=0||(scene.State.EntryEngagedMask&bit)!=0)handed[i]=1;
                        if(scene.State.FriendlyTargets.ContainsKey(i))handed[i]=2;
                    }
                    idle=!moved&&!hasTarget?idle+1:0;
                    Need(idle<40,$"{tag} 连续40步无位移且无目标（入口死区）");
                }
                for(int i=0;i<g.Heroes.Count;i++)
                {
                    Need(handed[i]>0,$"{tag} 4秒内未完成通行或合法提前接战（#{i}）");
                    Need(g.InCombatRoom(g.Heroes[i]),$"{tag} 4秒后仍未进入房间（#{i}）");
                }
            }
        }
        // 八、AUD-001 旧存档恢复：门线外 0.00063U（修复前的死区现场）与 0.19063U（真实旧档记录点）
        //     经正式 CaptureProgress→Restore 往返后必须自行入房恢复推进，不依赖 GM 移动。
        {
            foreach(float gap in new[]{0.00063f,0.19063f})
            {
                var scene=Arena(Vector2.UnitY,6,5,3);
                var g=scene.Game;
                // 正式检查点按 Origin 重建怪物，手工场景必须登记出生快照才能被读档还原。
                foreach(var m in scene.Room.Monsters)scene.Room.Origins.Add(new MonsterOrigin(m.SpecId,m.Level,m.Boss,m.Position,new SpawnModifiers()));
                var snapshot=g.CaptureProgress();
                // 模拟旧档写入的门外坐标：读档后入口门禁仍按门线解锁，只有几何推进能自救。
                snapshot.Heroes[0].Position=scene.Anchor-scene.Inward*gap;
                var resumed=GameSession.Restore(c,snapshot);
                // 0.00063U 落在新的房间归属容差内，0.19063U 才是真正的门外；后者必须保持“读档时仍在门外”的前提。
                if(gap>0.01f)Need(!resumed.InCombatRoom(resumed.Heroes[0]),$"旧档用例前提：{gap}U 应在门外");
                bool inRoom=false;
                for(int t=0;t<120&&!inRoom;t++){resumed.Tick(.05f);inRoom=resumed.InCombatRoom(resumed.Heroes[0]);}
                Need(inRoom,$"旧存档停滞现场（门外 {gap}U）未在6秒内自行入房");
                bool engaged=false;
                for(int t=0;t<120&&!engaged;t++){resumed.Tick(.05f);engaged=resumed.MonsterTargets.Any();}
                Need(engaged,$"旧存档停滞现场（门外 {gap}U）入房后未恢复接敌");
            }
        }
        Console.WriteLine("PASS: room entry passage in four directions and four room sizes, per-unit handover, near-threat early engagement without retro passage, alert base/progress/fallback, entrance-zone spawn avoidance with padding and reproducibility, independent summon passage, capacity source and GM entry diagnostics, AUD-001 boundary band (0~0.0019U × 4 directions × 4 room sizes × 1/2/3 party) without displacement/target deadlock, and stalled old-checkpoint self recovery");
    }
}
