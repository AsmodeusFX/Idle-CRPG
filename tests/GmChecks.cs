using System;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

public static class GmChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string message){if(!ok)throw new Exception("GM_CHECK: "+message);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        GameSession Battle()
        {
            var game=new GameSession(c,123,new[]{"warrior","ranger","mage"});
            for(int i=0;i<10000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            while(game.CombatEntryRemaining>0)game.Tick(.05f);
            game.Capture().SkillState.Targeting.DeployedMask=(1<<game.Heroes.Count)-1;
            // 隔离倍率测试，丢弃进入夹具前已经发生的受击退让请求。
            foreach(var action in game.Capture().SkillState.Actions){action.RetreatThreat=-1;action.RetreatMoving=false;action.RetreatCooldown=0;}
            var room=game.Floor.Rooms[game.CurrentRoom];
            Check(room.Monsters.Any(),"battle fixture");
            foreach(var hero in game.Heroes){hero.Hp=hero.MaxHp=10000;hero.Attack=10;hero.Defense=0;hero.Range=100;hero.Cooldown=.05f;hero.Timer=0;hero.Position=room.Center;}
            foreach(var monster in room.Monsters)monster.Hp=0;
            var enemy=room.Monsters[0];enemy.Hp=enemy.MaxHp=10000;enemy.Attack=20;enemy.Defense=0;enemy.Range=100;enemy.Cooldown=.05f;enemy.Timer=0;enemy.Position=room.Center;
            return game;
        }
        var normal=Battle();var scaled=Battle();var immune=Battle();
        scaled.SetGmBattle(new(MonsterHp:2,MonsterAttack:2));immune.SetGmBattle(new(Invincible:true));
        normal.Tick(.05f);scaled.Tick(.05f);immune.Tick(.05f);
        Check(normal.Heroes.Sum(h=>h.Hp)==29980&&scaled.Heroes.Sum(h=>h.Hp)==29960,"attack scale not applied immediately");
        Check(immune.Heroes.All(h=>h.Hp==h.MaxHp)&&immune.HitCount>0,"invincibility skipped combat or lost health");
        float normalDamage=10000-normal.Floor.Rooms[normal.CurrentRoom].Monsters[0].Hp;
        float scaledDamage=10000-scaled.Floor.Rooms[scaled.CurrentRoom].Monsters[0].Hp;
        Check(normalDamage==30&&scaledDamage==15,$"effective health scale {normalDamage}/{scaledDamage}");
        immune.SetGmBattle(new());immune.Tick(.05f);
        Check(immune.Heroes.Sum(h=>h.Hp)<30000,"invincibility did not switch off");

        GameSession FirstRoom(int bonus)
        {
            var game=new GameSession(c,456,new[]{"warrior","ranger","mage"});game.SetGmBattle(new(Invincible:true,RoomCountBonus:bonus));
            for(int i=0;i<10000&&game.CurrentRoom==0;i++)game.Tick(.05f);
            return game;
        }
        var baseline=FirstRoom(0);var extra=FirstRoom(3);var minimum=FirstRoom(-999);
        var room=extra.Floor.Rooms[extra.CurrentRoom];
        Check(room.Monsters.Count==baseline.Floor.Rooms[baseline.CurrentRoom].Monsters.Count+3&&minimum.Floor.Rooms[minimum.CurrentRoom].Monsters.Count==1,"future room count or lower bound");
        int count=room.Monsters.Count;extra.SetGmBattle(extra.Gm with {RoomCountBonus=99});
        Check(room.Monsters.Count==count&&room.Origins.Count==count,"existing room snapshot was respawned");
        extra.GrantGmResources(10000,10000);
        var restored=GameSession.Restore(c,SaveStore.Decode(c,SaveStore.Encode(new SaveDocument {Session=extra.CaptureProgress()})).Session!);
        Check(restored.Gm==new GmBattleSettings()&&restored.Gold==extra.Gold&&restored.Experience==extra.Experience&&restored.Heroes[0].Level==extra.Heroes[0].Level,"GM settings persisted or granted progress lost");
        Check(restored.Floor.Rooms[restored.CurrentRoom].Monsters.Count==count,"room reopen changed generated monster count");
        bool rejected=false;try{extra.SetGmBattle(new(MonsterHp:float.NaN));}catch(ArgumentOutOfRangeException){rejected=true;}
        Check(rejected&&extra.Gm.RoomCountBonus==99,"invalid settings mutated GM state");
        var goldGame=GameSession.CreateWorld(c,2,new[]{"warrior","ranger","mage"});goldGame.GrantGmResources(long.MaxValue);
        rejected=false;try{goldGame.GrantGmResources(1,10000);}catch(OverflowException){rejected=true;}
        Check(rejected&&goldGame.Gold==long.MaxValue&&goldGame.Heroes[0].Level==1,"resource overflow partially applied experience");
        // 使用正式地图验证进入边界通关，不以直接写进度代替真实命令和世界推进。
        var production=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        var skip=GameSession.CreateWorld(production,27,new[]{"warrior","ranger","mage"});
        skip.SetGmBattle(new(InstantDungeonClear:true));
        for(int i=0;i<30000&&skip.World!.Progress.Cleared.Count==0;i++)skip.Tick(.05f);
        string first=skip.World!.Target.Dungeon.Id;
        Check(skip.World.Progress.Cleared.Contains(first)&&skip.Won&&skip.World.Stage==CampaignStage.TravelVillage,"entry clear did not complete and return to village");
        Check(skip.Kills==0&&skip.Experience==0&&skip.Gold==0&&skip.Inventory.Count==0,"entry clear forged battle rewards");
        var skipRestored=GameSession.Restore(production,SaveStore.Decode(production,SaveStore.Encode(new(){Session=skip.CaptureProgress()})).Session!);
        Check(!skipRestored.Gm.InstantDungeonClear&&skipRestored.World!.Progress.Cleared.Contains(first),"clear switch persisted or progress lost");
        // 开关关闭后下一次进入必须正常战斗；不能因此前Won残留自动清下一关。
        skip.SetGmBattle(new());
        for(int i=0;i<30000&&skip.World.Stage!=CampaignStage.Dungeon;i++)skip.Tick(.05f);
        Check(skip.World.Stage==CampaignStage.Dungeon&&!skip.Won&&skip.World.Progress.Cleared.Count==1,"disabled switch still cleared next entry");
        skip.SetGmBattle(new(InstantDungeonClear:true));skip.Tick(.05f);
        Check(!skip.Won&&skip.World.Progress.Cleared.Count==1,"switch retroactively cleared current dungeon");
        var progression=GameSession.CreateWorld(production,28,new[]{"warrior","ranger","mage"});progression.SetGmBattle(new(InstantDungeonClear:true));
        var normals=production.Dungeons.Where(d=>!d.Castle&&production.Rulers[d.RegionId]=="A01-C01").Select(d=>d.Id).ToArray();
        for(int i=0;i<60000&&!normals.All(id=>progression.World!.Progress.Cleared.Contains(id));i++)progression.Tick(.05f);
        Check(normals.All(id=>progression.World!.Progress.Cleared.Contains(id))&&!progression.World!.Progress.Cleared.Contains("A01-C01"),"instant mode bypassed manual castle gate");
        Check(progression.World!.Progress.CastleUnlocked(production,"A01-C01")&&progression.RequestCastleChallenge("A01-C01"),"castle not unlocked/requestable after normal clears");
        for(int i=0;i<60000&&!progression.World.Progress.Cleared.Contains("A01-C01");i++)progression.Tick(.05f);
        Check(progression.World.Progress.Cleared.Contains("A01-C01")&&progression.World.Progress.AreaPartUnlocked(production,"ap2")&&progression.World.DungeonVisits>1,"authorized castle clear did not unlock region or resume departure after optional supplies");
        var chapter=GameSession.CreateWorld(production,29,new[]{"warrior","ranger","mage"});
        chapter.CompleteGmChapter();Check(chapter.World!.Progress.Cleared.Count==production.Dungeons.Count&&chapter.World.Stage==CampaignStage.Village&&chapter.Floors.Count==0&&chapter.Gold==0&&chapter.Experience==0&&chapter.Kills==0,"GM chapter clear did not clear all content or forged rewards");
        chapter=GameSession.Restore(production,SaveStore.Decode(production,SaveStore.Encode(new(){Session=chapter.CaptureProgress()})).Session!);
        for(int i=0;i<20000&&chapter.ChapterIndex==0;i++)chapter.Tick(.05f);
        Check(chapter.ChapterIndex==1&&chapter.MapProgress(0,0).Cleared.Count==production.Dungeons.Count,"GM chapter clear did not advance after town services");
        string qa=Path.Combine(root,"artifacts/qa/gm-config-"+Guid.NewGuid().ToString("N"));
        string source=Path.Combine(root,"Idle-CRPG/config");
        foreach(var file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories))
        {
            string target=Path.Combine(qa,Path.GetRelativePath(source,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);
        }
        string gmFile=Path.Combine(qa,"gm/gm.csv");
        File.WriteAllText(gmFile,"gold_grant,experience_grant,monster_scale_steps,count_step,count_limit\n10000,10000,0.5;1;1;2,1,999\n");
        rejected=false;try{LegacySkillConfig.LoadLegacy(qa);}catch(InvalidDataException){rejected=true;}
        Check(rejected,"duplicate scale steps accepted");
        File.WriteAllText(gmFile,"gold_grant,experience_grant,monster_scale_steps,count_step,count_limit\n10000,10000,0.5;1;2,1000,999\n");
        rejected=false;try{LegacySkillConfig.LoadLegacy(qa);}catch(InvalidDataException){rejected=true;}
        Check(rejected,"count step exceeds limit");
        Console.WriteLine("PASS: GM resources, shared growth, invincibility, immediate enemy scales, future room count, checkpoint reopen and transient debug settings");
    }
}
