using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

/// <summary>
/// S15-C2-A / A2：四章末 Boss 阶段能力与 31 城堡威胁组合的真实消费。
/// 只写 artifacts/qa；不引入第二套怪物等级/刷怪/伤害/奖励权威。
/// </summary>
public static class C2ABossPhaseChecks
{
    private static void Need(bool ok,string why){if(!ok)throw new Exception("C2A-BOSS: "+why);}
    private static void Tick(GameSession game,int frames){for(int i=0;i<frames;i++)game.Tick(.05f);}
    private static Actor Boss(Room room)=>MaybeBoss(room)??throw new Exception("C2A-BOSS: 房间内没有存活Boss本体");
    private static Actor? MaybeBoss(Room room)=>room.Monsters.FirstOrDefault(m=>m.Boss&&!m.BossPhaseDormant&&m.Alive);
    private static int PhaseActors(Room room,string tag,bool aliveOnly)=>room.Monsters.Count(m=>m.BossPhaseId==tag&&(!aliveOnly||(m.Alive&&!m.BossPhaseDormant)));

    public static void Run(string root)
    {
        var config=Config.Load(Path.Combine(root,"Idle-CRPG/config"));
        string evidence=Path.Combine(root,"artifacts/qa","s15-c2a-boss-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
        var ledger=new List<string>{"chapter,poi,floor,room,index,spec,level,boss,elite,phase,stage"};

        // 1) 阶段表合同：四个章末首领、四种触发方式、接替身份唯一。
        {
            Need(config.BossPhases.Count==4,"阶段表应覆盖4个章末首领，实际"+config.BossPhases.Count);
            foreach(string key in new[]{"1:A08-C01","2:A07-C01","3:B07-C01","4:D04-C01"})
                Need(config.BossPhases.ContainsKey(key),"缺少阶段表 "+key);
            var all=config.BossPhases.Values.SelectMany(x=>x).ToArray();
            Need(all.Select(p=>p.Trigger).Distinct().Count()==4,"四种触发方式必须全部有真实内容");
            var b07=config.BossPhasesOf(3,"B07-C01");
            Need(b07.Count==2&&b07[^1].IsSuccessor&&b07[^1].Trigger==BossPhaseTrigger.Sequence&&b07[^1].EscortTemplate=="boss_mephisto",
                "议会→墨菲斯托必须是遭遇序列接替，且用独立Boss身份而不是给普通怪改名");
            foreach(var group in all.GroupBy(p=>p.TableKey))
                Need(group.Sum(p=>p.EscortCount)<=10,"阶段召援总名额超过上限："+group.Key);
            Need(all.Where(p=>p.SkillId!="").All(p=>config.Skills.Skills[p.SkillId].Cooldown>0),"阶段技能必须带冷却");
            Need(all.Where(p=>p.BuffId!="").All(p=>config.Skills.Buffs.ContainsKey(p.BuffId)),"阶段Buff引用无效");
        }

        // 2) 31 城堡威胁组合：每座城堡都有可抽出的身份档位循环，且普通地牢不被覆盖。
        {
            var threats=new List<string>();
            foreach(var map in config.ChapterMaps.Take(4))
            {
                foreach(var castle in map.Dungeons.Where(d=>d.Castle))
                {
                    var plan=map.EncounterFor(map.ChapterIndex+1,castle.Id,1);
                    Need(plan!=null&&plan.Composition!=""&&plan.Threat!="","城堡缺少威胁组合："+castle.Id);
                    var threat=plan!;
                    threats.Add(threat.Threat);
                    var pool=map.MonsterPools[map.AreaPart(castle.RegionId).Id];
                    foreach(string token in threat.Composition.Split(';'))
                    {
                        string archetype=token switch {"M"=>"melee","R"=>"ranged","H"=>"heavy",_=>"support"};
                        Need(pool.Any(e=>map.ArchetypeOf(e.MonsterId)==archetype),$"{castle.Id} 的组合需要池内没有的 {archetype} 档");
                    }
                }
            }
            Need(threats.Count==31,"城堡威胁组合应覆盖31座城堡，实际"+threats.Count);
            Need(threats.Distinct().Count()>=25,"威胁组合区分度不足："+threats.Distinct().Count());
            var ordinary=config.ChapterMaps[0].Dungeons.First(d=>!d.Castle);
            var ordinaryPlan=config.ChapterMaps[0].EncounterFor(1,ordinary.Id,1);
            Need(ordinaryPlan==null||ordinaryPlan.Composition=="","普通地牢不应被城堡威胁组合覆盖："+ordinary.Id);
            File.WriteAllLines(Path.Combine(evidence,"castle_threats.csv"),
                new[]{"chapter,poi,threat,composition,floors"}.Concat(config.ChapterMaps.Take(4).SelectMany(m=>
                    m.CastleThreats.Values.Select(p=>$"{p.Chapter},{p.Poi},{p.Threat},{p.Composition},{p.Floor}"))));
        }

        // 3) 威胁组合在同一生成事务里决定身份档位：31 座城堡逐座在运行时核对循环与怪级区间。
        {
            var compositionRows=new List<string>{"chapter,poi,floor,threat,composition,spawned,actual,ok"};
            int castlesChecked=0;
            foreach(var map in config.ChapterMaps.Take(4))
            {
                foreach(var castle in map.Dungeons.Where(d=>d.Castle))
                {
                    var game=new GameSession(map,8810+castlesChecked,new[]{"warrior","ranger","mage","priest"});
                    game.SetGmBattle(new GmBattleSettings(Invincible:true,MonsterHp:20));
                    game.QaEnterBossRoom(castle.Id,1);
                    var room=game.Floor.Rooms[game.CurrentRoom];
                    var plan=map.EncounterFor(map.ChapterIndex+1,castle.Id,1)!;
                    var tokens=plan.Composition.Split(';');
                    var regular=room.Monsters.Where(m=>!m.Boss&&m.BossPhaseId=="").ToArray();
                    Need(regular.Length>0,"城堡威胁组合未生成普通怪："+castle.Id);
                    var actual=new List<string>();
                    for(int i=0;i<regular.Length;i++)
                    {
                        string expected=tokens[i%tokens.Length] switch {"M"=>"melee","R"=>"ranged","H"=>"heavy",_=>"support"};
                        string got=map.ArchetypeOf(regular[i].SpecId);
                        actual.Add(got);
                        Need(got==expected,$"{castle.Id} 第{i}格身份档位不符：期望{expected} 实际{got}（{regular[i].SpecId}）");
                    }
                    Need(room.Monsters.Where(m=>!m.Boss).All(m=>m.Level>=game.MonsterLevelMin&&m.Level<=game.MonsterLevelMax),"城堡怪级超出 Run 区间："+castle.Id);
                    compositionRows.Add($"{map.ChapterIndex+1},{castle.Id},1,{plan.Threat},{plan.Composition},{regular.Length},{string.Join('|',actual)},true");
                    castlesChecked++;
                }
            }
            Need(castlesChecked==31,"运行时只覆盖了"+castlesChecked+"座城堡");
            File.WriteAllLines(Path.Combine(evidence,"c2a_a2_castle_composition.csv"),compositionRows);
        }

        // 4) 安达利尔：Boss 战斗状态切换（开场 + 50% 血量），随从有上限且只激活一次。
        {
            var map=config.ChapterMaps[0];
            var game=new GameSession(map,8811,new[]{"warrior","ranger","mage","priest"});
            game.SetGmBattle(new GmBattleSettings(Invincible:true,MonsterHp:20));
            game.QaEnterBossRoom("A08-C01");
            var room=game.Floor.Rooms[game.CurrentRoom];
            var boss=Boss(room);
            Need(boss.Name=="安达利尔","章末Boss不得沿用原型占位名："+boss.Name);
            Need(PhaseActors(room,"A08-C01:1",false)==2,"第1段随从必须预留2个，实际"+PhaseActors(room,"A08-C01:1",false));
            Tick(game,60);
            Need(game.BossPhaseLabel.Contains("毒雾领域"),"第1段未生效："+game.BossPhaseLabel);
            Need(boss.SkillIds.Contains("boss_poison_burst"),"第1段技能未附加");
            Need(PhaseActors(room,"A08-C01:1",true)==2,"第1段随从未激活");
            int spawnedAfterPhase1=game.Floor.ActualSpawned;
            Tick(game,120);
            Need(PhaseActors(room,"A08-C01:1",true)==2&&PhaseActors(room,"A08-C01:1",false)==2,"阶段随从重复生成");
            Need(game.BossPhaseLabel.Contains("毒雾领域"),"阶段重复推进");
            boss.Hp=boss.MaxHp*.45f;
            Tick(game,10);
            Need(game.BossPhaseLabel.Contains("毒压狂怒"),"第2段未在血量阈值生效："+game.BossPhaseLabel);
            Need(boss.SkillIds.Contains("boss_venom_press"),"第2段技能未附加");
            Need(game.Capture().SkillState.Buffs.Any(b=>b.Id=="boss_enrage"),"第2段Buff未生效");
            Tick(game,200);
            Need(game.BossPhaseLabel.Contains("毒压狂怒"),"第2段之后不应继续推进");
            Need(game.Floor.ActualSpawned>=spawnedAfterPhase1,"阶段激活未计入实际生成数");

            // 存档恢复：技能/随从/进度一致，且不重复生成、不重复发收益。
            long kp=game.Kp,xp=game.PublicExperience;int monsters=room.Monsters.Count;
            // 只验证房间/阶段进度恢复：world 置空走单地牢恢复路径，避免把QA强制入城写成"世界进度"。
            var snapshot=game.CaptureProgress();snapshot.World=null;
            var restored=GameSession.Restore(map,snapshot);
            Need(restored.BossPhaseState.Count==1&&restored.BossPhaseState.Values.Single()==2,"读档未恢复阶段进度");
            var rroom=restored.Floor.Rooms[restored.CurrentRoom];
            var rboss=Boss(rroom);
            Need(rboss.SkillIds.Contains("boss_poison_burst")&&rboss.SkillIds.Contains("boss_venom_press"),"读档未恢复阶段技能");
            Need(PhaseActors(rroom,"A08-C01:1",true)==2,"读档未恢复阶段随从");
            Need(rroom.Monsters.Count==monsters,"读档重复生成怪物："+rroom.Monsters.Count+"/"+monsters);
            Need(restored.Kp==kp&&restored.PublicExperience==xp,"读档重复发放收益");
            var twice=GameSession.Restore(map,restored.CaptureProgress());
            Need(twice.BossPhaseState.Values.Single()==2&&twice.Floor.Rooms[twice.CurrentRoom].Monsters.Count==monsters,"二次读档不幂等");
            File.WriteAllLines(Path.Combine(evidence,"andariel_phases.csv"),new[]
            {
                "state,label,boss_skills,escorts_alive,monsters",
                $"phase1,{game.BossPhaseLabel},{string.Join('|',boss.SkillIds)},{PhaseActors(room,"A08-C01:1",true)},{room.Monsters.Count}",
                $"restored,{restored.BossPhaseLabel},{string.Join('|',rboss.SkillIds)},{PhaseActors(rroom,"A08-C01:1",true)},{rroom.Monsters.Count}"
            });
        }

        // 5) 都瑞尔：第二章独立种子；阶段同样只按 Boss 状态推进。
        {
            var map=config.ChapterMaps[1];
            var game=new GameSession(map,8812,new[]{"warrior","ranger","mage","priest"});
            game.SetGmBattle(new GmBattleSettings(Invincible:true,MonsterHp:20));
            game.QaEnterBossRoom("A07-C01");
            var room=game.Floor.Rooms[game.CurrentRoom];
            var boss=Boss(room);
            Need(boss.Name.StartsWith("都瑞尔"),"第二章章末Boss名称错误："+boss.Name);
            Tick(game,60);
            Need(game.BossPhaseLabel.Contains("寒冰冲锋")&&boss.SkillIds.Contains("boss_charge"),"都瑞尔开场阶段未生效");
            boss.Hp=boss.MaxHp*.49f;Tick(game,10);
            Need(game.BossPhaseLabel.Contains("冰霜重压")&&boss.SkillIds.Contains("sor_frost_nova"),"都瑞尔血量阶段未生效");
        }

        // 6) 议会→墨菲斯托：必须是遭遇序列，接替者独立登场且房间在接替前不得结算。
        {
            var map=config.ChapterMaps[2];
            var game=new GameSession(map,8813,new[]{"warrior","ranger","mage","priest"});
            game.SetGmBattle(new GmBattleSettings(Invincible:true,MonsterHp:20));
            game.QaEnterBossRoom("B07-C01");
            var room=game.Floor.Rooms[game.CurrentRoom];
            var leader=Boss(room);
            Need(leader.Name=="议会成员","议会段首领名称错误："+leader.Name);
            Need(room.PhasePending,"遭遇序列接替者未待命时房间会在议会倒下时提前结算");
            Tick(game,60);
            Need(game.BossPhaseLabel.Contains("议会结阵"),"议会开场阶段未生效");
            Need(PhaseActors(room,"B07-C01:1",true)==2,"议会随从未入场");
            long kpBefore=game.Kp;
            foreach(var monster in room.Monsters.Where(m=>m.Alive&&!m.BossPhaseDormant).ToArray())game.QaResolveHit(monster);
            Need(!room.Clear,"议会全灭后房间不得直接结算");
            Need(MaybeBoss(room)==null,"接替前不应存在存活Boss本体");
            Tick(game,5);
            var mephisto=Boss(room);
            Need(mephisto.Name=="墨菲斯托","接替Boss名称错误："+mephisto.Name);
            Need(mephisto.SpecId=="boss_mephisto","接替Boss必须是独立身份而不是改名普通怪："+mephisto.SpecId);
            Need(mephisto.SkillIds.Contains("boss_chain_bolt"),"接替Boss未获得阶段技能");
            Need(!room.PhasePending,"接替完成后不应继续阻止结算");
            Need(game.Kp>kpBefore,"合法击杀未结算KP");
            Need(game.BossPhaseLabel.Contains("墨菲斯托"),"接替阶段标签未更新："+game.BossPhaseLabel);
            File.WriteAllLines(Path.Combine(evidence,"council_to_mephisto.csv"),new[]
            {
                "step,actor,spec,alive,boss,dormant",
                $"phase1,{leader.Name},{leader.SpecId},{leader.Alive},{leader.Boss},{leader.BossPhaseDormant}",
                $"phase2,{mephisto.Name},{mephisto.SpecId},{mephisto.Alive},{mephisto.Boss},{mephisto.BossPhaseDormant}"
            });
        }

        // 7) 混沌避难所：沿 P0 六层地图阶段推进，迪亚波罗再承载自身战斗变化。
        {
            var map=config.ChapterMaps[3];
            var seal=new GameSession(map,8814,new[]{"warrior","ranger","mage","priest"});
            seal.SetGmBattle(new GmBattleSettings(Invincible:true,MonsterHp:20));
            seal.QaEnterBossRoom("D04-C01",3);
            Tick(seal,10);
            var sealRoom=seal.Floor.Rooms[seal.CurrentRoom];
            Need(seal.BossPhaseLabel.Contains("封印西翼"),"第3层封印阶段未生效："+seal.BossPhaseLabel);
            Need(PhaseActors(sealRoom,"D04-C01:1",true)==2,"封印西翼守卫数量不符（P0 SealActivationCount=2）");
            Need(PhaseActors(sealRoom,"D04-C01:2",true)==0,"第3层不应提前激活第4层阶段");

            var throne=new GameSession(map,8815,new[]{"warrior","ranger","mage","priest"});
            throne.SetGmBattle(new GmBattleSettings(Invincible:true,MonsterHp:20));
            throne.QaEnterBossRoom("D04-C01",6);
            Tick(throne,40);
            var throneRoom=throne.Floor.Rooms[throne.CurrentRoom];
            var diablo=Boss(throneRoom);
            Need(diablo.Name=="迪亚波罗","终章Boss名称错误："+diablo.Name);
            int sealEscorts=PhaseActors(throneRoom,"D04-C01:1",true)+PhaseActors(throneRoom,"D04-C01:2",true)+PhaseActors(throneRoom,"D04-C01:3",true);
            Need(sealEscorts==5,"五处封印守卫应全部按楼层阶段登场，实际"+sealEscorts);
            Need(diablo.SkillIds.Contains("boss_fire_ring"),"迪亚波罗自身阶段技能未附加");
            diablo.Hp=diablo.MaxHp*.45f;Tick(throne,10);
            Need(throne.BossPhaseLabel.Contains("终末雷暴"),"迪亚波罗血量阶段未生效："+throne.BossPhaseLabel);
            Need(PhaseActors(throneRoom,"D04-C01:5",true)==2,"终末阶段召援数量不符");
            File.WriteAllLines(Path.Combine(evidence,"chaos_sanctuary_stages.csv"),new[]
            {
                "floor,label,seal_guardians,diablo_skills",
                $"3,{seal.BossPhaseLabel},2,",
                $"6,{throne.BossPhaseLabel},{sealEscorts},{string.Join('|',diablo.SkillIds)}"
            });
        }

        // 8) 固定种子复现：同种子同房间同阶段序列，不同种子不同。
        {
            string Signature(int seed)
            {
                var game=new GameSession(config.ChapterMaps[0],seed,new[]{"warrior","ranger","mage","priest"});
                game.SetGmBattle(new GmBattleSettings(Invincible:true,MonsterHp:20));
                game.QaEnterBossRoom("A08-C01");
                Tick(game,60);
                var room=game.Floor.Rooms[game.CurrentRoom];
                return string.Join(";",room.Monsters.Select(m=>$"{m.SpecId}@{m.Level}:{m.Boss}:{m.Position.X:0.000},{m.Position.Y:0.000}"))+"|"+game.BossPhaseLabel;
            }
            Need(Signature(8820)==Signature(8820),"同种子阶段现场不一致");
            Need(Signature(8820)!=Signature(8821),"不同种子现场不应相同");
        }

        // 9) 无阶段配置的地牢保持旧行为（仍带"（原型）"占位名）。
        {
            var map=config.ChapterMaps[0];
            var game=new GameSession(map,8830,new[]{"warrior","ranger","mage","priest"});
            game.SetGmBattle(new GmBattleSettings(Invincible:true,MonsterHp:20));
            game.QaEnterBossRoom("A06-C01");
            var room=game.Floor.Rooms[game.CurrentRoom];
            var boss=Boss(room);
            Need(boss.Name.EndsWith("（原型）"),"未投放阶段的Boss仍须显式标注原型："+boss.Name);
            Need(game.BossPhaseLabel=="","无阶段配置不应产生阶段标签");
            Tick(game,60);
            Need(room.Monsters.All(m=>m.BossPhaseId==""),"无阶段配置不得生成阶段单位");
        }

        // 10) 出处账本：真实生成的章/POI/楼层/房间/身份/等级/阶段。
        foreach(var map in config.ChapterMaps.Take(4))
        {
            var game=new GameSession(map,8850+map.ChapterIndex,new[]{"warrior","ranger","mage","priest"});
            game.SetGmBattle(new GmBattleSettings(Invincible:true,MonsterHp:20));
            var castle=map.Dungeons.First(d=>d.Castle);
            game.QaEnterBossRoom(castle.Id);
            Tick(game,40);
            var room=game.Floor.Rooms[game.CurrentRoom];
            for(int i=0;i<room.Monsters.Count;i++)
            {
                var m=room.Monsters[i];
                ledger.Add($"{map.ChapterIndex+1},{castle.Id},{game.FloorIndex+1},{room.Id},{i},{m.SpecId},{m.Level},{m.Boss},{m.Elite},{m.BossPhaseId},{game.BossPhaseLabel}");
            }
        }
        File.WriteAllLines(Path.Combine(evidence,"c2a_a2_spawn_ledger.csv"),ledger);

        Console.WriteLine($"C2A_A2_BOSS_PASS: 4 act-end boss phases (fight_start/hp_pct/sequence/floor_stage), 31 castle threat combos, save/load + fixed seed + receipts; evidence={evidence}");
    }
}
