using Godot;
using IdleCrpg.Core;
using System;
using System.IO;
using System.Linq;

public partial class Main
{
    /// <summary>
    /// S15-C2-A / A2 真实窗口取证：四章末 Boss 的阶段前后必须可观察。
    /// 只在 automation 存档目录下运行；QA 强制入场与 GM 无敌/血量倍率都是显式夹具，不是正式现场。
    /// </summary>
    private async void RunC2ABossSmoke()
    {
        try
        {
            void Need(bool ok,string why){if(!ok)throw new Exception("C2A_BOSS_UI: "+why);}
            string output=QaOutput.NewDirectory(ProjectSettings.GlobalizePath("res://.."),"c2a-boss-ui");
            var rows=new System.Collections.Generic.List<string>{"scenario,step,label,boss,skills,escorts,status,entry,monsters_alive"};
            async System.Threading.Tasks.Task Frames()
            {
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            }
            async System.Threading.Tasks.Task Shot(string name)
            {
                Refresh();await Frames();
                var error=GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output,name+".png"));
                Need(error==Error.Ok,"截图写入失败："+name);
            }
            void Record(string scenario,string step)
            {
                var room=session!.Floor.Rooms[session.CurrentRoom];
                var boss=room.Monsters.FirstOrDefault(m=>m.Boss&&!m.BossPhaseDormant&&m.Alive);
                int escorts=room.Monsters.Count(m=>m.Alive&&!m.BossPhaseDormant&&m.BossPhaseId!="");
                rows.Add($"{scenario},{step},{session.BossPhaseLabel},{boss?.Name??""},{string.Join('|',boss?.SkillIds??Array.Empty<string>())},{escorts},{session.Status},{session.CombatEntryRemaining:0.00},{room.Monsters.Count(m=>m.Alive)}");
            }
            async System.Threading.Tasks.Task WaitCombat(int budget=200)
            {
                for(int i=0;i<budget&&session!.CombatEntryRemaining>0;i++)session.Tick(.05f);
                await System.Threading.Tasks.Task.CompletedTask;
            }
            async System.Threading.Tasks.Task<bool> WaitPhase(string keyword,int budget=240)
            {
                for(int i=0;i<budget;i++)
                {
                    if(session!.BossPhaseLabel.Contains(keyword))return true;
                    session.Tick(.05f);
                }
                return session!.BossPhaseLabel.Contains(keyword);
            }

            void Prepare(string poi,int chapter,int floor=0)
            {
                session=GameSession.CreateWorld(config,20261026+chapter,new[]{"warrior","ranger","mage","priest"},chapter);
                config=session.Config;
                paused=true;
                session.SetGmBattle(new GmBattleSettings(Invincible:true,MonsterHp:20));
                session.QaEnterBossRoom(poi,floor);
            }
            Actor BossActor()
            {
                var room=session!.Floor.Rooms[session.CurrentRoom];
                return room.Monsters.Single(m=>m.Boss&&!m.BossPhaseDormant&&m.Alive);
            }
            void DropBossTo(float ratio){var boss=BossActor();boss.Hp=boss.MaxHp*ratio;}

            // 1) 安达利尔：开场形态 + 50% 血量的战斗状态切换。
            Prepare("A08-C01",0);
            await Shot("a1-andariel-before");
            Need(session!.BossPhaseLabel=="","进入Boss房前不应已有阶段");
            await WaitCombat();Need(await WaitPhase("毒雾领域"),"安达利尔第1段未生效");
            await Shot("a1-andariel-phase1-poison");
            Record("andariel","phase1");
            Need(BossActor().SkillIds.Contains("boss_poison_burst"),"安达利尔第1段技能未附加");
            DropBossTo(.45f);
            Need(await WaitPhase("毒压狂怒",120),"安达利尔第2段未生效");
            await Shot("a1-andariel-phase2-enrage");
            Record("andariel","phase2");
            Need(BossActor().SkillIds.Contains("boss_venom_press"),"安达利尔第2段技能未附加");

            // 2) 都瑞尔：同一套"Boss 战斗状态切换"，但位于第二章独立地图。
            Prepare("A07-C01",1);
            await WaitCombat();Need(await WaitPhase("寒冰冲锋"),"都瑞尔第1段未生效");
            await Shot("a2-duriel-phase1-charge");
            Record("duriel","phase1");
            DropBossTo(.45f);
            Need(await WaitPhase("冰霜重压",120),"都瑞尔第2段未生效");
            await Shot("a2-duriel-phase2-frost");
            Record("duriel","phase2");

            // 3) 议会 → 墨菲斯托：遭遇序列，接替者必须是独立Boss身份。
            Prepare("B07-C01",2);
            await WaitCombat();Need(await WaitPhase("议会结阵"),"议会第1段未生效");
            await Shot("a3-council-phase1");
            Record("council","phase1");
            int councilEscorts=session!.Floor.Rooms[session.CurrentRoom].Monsters.Count(m=>m.Alive&&!m.BossPhaseDormant&&m.BossPhaseId=="B07-C01:1");
            Need(councilEscorts==2,"议会随从数量不符："+councilEscorts);
            session.QaKillRoomEnemies();
            Need(await WaitPhase("墨菲斯托",60),"议会全灭后墨菲斯托未登场");
            await Shot("a3-mephisto-phase2");
            Record("council","phase2");
            var mephisto=BossActor();
            Need(mephisto.SpecId=="boss_mephisto","接替Boss不是独立身份："+mephisto.SpecId);

            // 4) 混沌避难所：沿 P0 楼层阶段推进，迪亚波罗再承载自身战斗变化。
            Prepare("D04-C01",3,3);
            await WaitCombat();Need(await WaitPhase("封印西翼",40),"第3层封印阶段未生效");
            await Shot("a4-seal-floor3");
            Record("sanctuary","floor3");
            Prepare("D04-C01",3,6);
            await WaitCombat();Need(await WaitPhase("恐惧之王",80),"第6层迪亚波罗阶段未生效");
            await Shot("a4-diablo-phase1-fire");
            Record("diablo","phase1");
            var diablo=BossActor();
            Need(diablo.Name=="迪亚波罗","终章Boss名称错误："+diablo.Name);
            int sealGuards=session!.Floor.Rooms[session.CurrentRoom].Monsters.Count(m=>m.Alive&&!m.BossPhaseDormant&&m.BossPhaseId.StartsWith("D04-C01:")&&m.BossPhaseId!="D04-C01:4");
            Need(sealGuards==5,"五处封印守卫应全部登场："+sealGuards);
            DropBossTo(.45f);
            Need(await WaitPhase("终末雷暴",120),"迪亚波罗第5段未生效");
            await Shot("a4-diablo-phase2-storm");
            Record("diablo","phase2");

            File.WriteAllLines(Path.Combine(output,"phases.csv"),rows);
            GD.Print($"C2A_BOSS_UI_PASS window={GetWindow().Size} renderer={DisplayServer.GetName()} output={output} explicit_QA_fixture=true");
            QuitSafely();
        }
        catch(Exception e){GD.PushError(e.ToString());QuitSafely(1);}
    }
}
