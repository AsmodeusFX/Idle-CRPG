using Godot;
using IdleCrpg.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public partial class Main
{
    private bool performanceRun;
    private readonly List<double> processTimes=new(),drawTimes=new(),frameTimes=new();
    private static double Milliseconds(long start)=>(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
    // 专用 QA 存档、真实窗口计时；不以固定 FPS 参数伪造帧间隔。
    private async void RunPerformanceSmoke()
    {
        performanceRun=true;
        try
        {
            foreach(string scene in new[]{"travel","combat","map"})
            {
                session=GameSession.CreateWorld(config,20261017,new[]{"warrior","ranger","mage"});
                if(scene!="travel")
                    for(int i=0;i<50000;i++)
                    {
                        session.Tick(.05f);
                        if(session.World!.Stage==CampaignStage.Dungeon&&!session.MovingBetweenRooms&&session.Floor.Rooms[session.CurrentRoom].Monsters.Count(m=>m.Alive)>=3)break;
                    }
                viewMode=scene=="map"?1:0;paused=false;speed=1;accumulator=0;cameraReady=false;
                ResetSoundBaseline();
                for(int i=0;i<30;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                processTimes.Clear();drawTimes.Clear();frameTimes.Clear();
                for(int i=0;i<240;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                string Stats(List<double> values)
                {
                    var a=values.Order().ToArray();return a.Length==0?"none":$"avg={a.Average():F2},p95={a[(int)((a.Length-1)*.95)]:F2},max={a[^1]:F2}ms";
                }
                GD.Print($"PERF {scene}: process {Stats(processTimes)}; draw {Stats(drawTimes)}; frame {Stats(frameTimes)}");
            }
            GD.Print("PERFORMANCE_SMOKE_OK");QuitSafely();
        }
        catch(Exception e){GD.PushError(e.ToString());QuitSafely(1);}
    }
}
