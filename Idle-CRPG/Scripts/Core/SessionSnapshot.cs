using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Diagnostics.CodeAnalysis;

namespace IdleCrpg.Core;

public sealed class SessionSnapshot
{
    public int CampaignVersion,LatestMapIndex;
    public long Kp,KpEarned,KpSpent,DungeonEntrySerial;public int MonsterLevelMin=1,MonsterLevelMax=1;public List<string> KpKillReceipts=new();
    public long PublicExperience;public int PublicExperienceFraction,PublicExperienceVersion;
    public int RunVersion;public string RunId="";public bool TeamLocked=true;
    [System.Text.Json.Serialization.JsonIgnore] public AccountState Account=new();
    public BranchRequest? ActiveBranch;
    public List<BranchRequest> BranchQueue=new();
    public WorldSnapshot? SuspendedWorld;
    public int DifficultyIndex,ChapterIndex;
    public List<ChapterProgressSave> CompletedMaps=new();
    public int Seed, DungeonSeed, FloorIndex, CurrentRoom, Experience, Kills, Retries, Destination=-1, Phase;
    /// <summary>门前等待计时与先锋引用；只在Destination≥0且Phase==1时有效，其余状态必须为默认值。</summary>
    public float DoorWait;
    public bool DoorFallback;
    public int DoorOpenerHero=-1,DoorOpenerSummon=-1;
    public long Gold, Revision, HitCount;
    public uint LootRandomState;
    public uint RoomEntryLootState;
    public uint CombatRandomState,RoomEntryCombatState;
    public int CombatVersion;
    public int HeroExperienceVersion;
    /// <summary>0为旧三系布局；1为当前正式布局。迁移只做一次并按节点合法性退款，二次读档不再改键。</summary>
    public int SkillTreeVersion;
    public int FixedSkillsVersion;
    public long AssetEventSequence;
    public long AssetAuditTick;
    public SkillBattleState SkillState=new();
    public bool Won;
    public float Regroup, ReviveElapsed;
    public int ReviveTarget=-1, Rescuer=-1;
    public string Status="", ActiveDungeonId="";
    public List<Actor> ReserveHeroes=new();public string[] PendingFormation=Array.Empty<string>();public int FormationCapacityFloor;
    public List<Actor> Heroes=new();
    /// <summary>卷轴库存与药剂槽；旧档为空表示没有消耗品。</summary>
    public Dictionary<string,int> Scrolls=new();
    public List<PotionSlotSave> Potions=new();
    /// <summary>六部位装备（穿戴与候选），按英雄索引绑定；旧档为空表示无装备。</summary>
    public List<EquipmentSave> Equipment=new();
    /// <summary>存档时间戳（Unix 秒），仅用于有限离线结算锚点；不参与确定性哈希。</summary>
    public long SavedAtUnix;
    /// <summary>玩家侧独立召唤物；ID 追加在当前房怪物之后，正式检查点按进房快照恢复。</summary>
    public List<Actor> Summons=new();
    public List<Actor> RoomEntrySummons=new();
    public List<Floor> Floors=new();
    public List<InventoryEntry> Inventory=new();
    public List<string> Log=new();
    public SpawnModifiers Modifiers=new();
    public bool RoomCheckpoint;
    public List<PartyEntry> RoomEntry=new();
    public StarProgressSave Stars=new(Array.Empty<StarRankSave>());
    /// <summary>精英击杀收据令牌（C1-B B2 G02）；空数组=旧档，等同未领取任何精英奖励。</summary>
    public string[] EliteKillReceipts=Array.Empty<string>();
    /// <summary>
    /// Boss 阶段进度（C2-A A2）：<c>poi:floor:room:phase</c> 字符串。
    /// 空列表=旧档/未触发阶段，等同第0段；只用于"同一房间同一段不重复生效"，不保存怪物现场。
    /// </summary>
    public List<string> BossPhaseProgress=new();
    public double StarAttack,StarHp;
    public WorldSnapshot? World;
}
public record InventoryEntry(string ItemId,long Price,long Count);
public record PartyEntry(Vector2 Position,float Hp);
public sealed class WorldSnapshot
{
    public List<Vector2> OutdoorTrail=new();
    public float FollowGather;
    public int TownVersion;
    public int TownLayoutVersion;
    public TownVisitPhase TownPhase;
    public PortalPhase PortalPhase;
    public bool PortalReturning;
    public bool ReturnPortalVisible;
    public bool WaitingApproval;
    public long WaitApprovalTicks;
    public float PortalElapsed;
    public string PortalWaypoint="";
    public Vector2 PortalOrigin;
    public int MapVersion;
    public Dictionary<string,int> Failures=new(),PausedAtLevel=new();
    public List<string> RetryCastles=new();
    public int TeamLevel=1;
    public uint RecoveryRandomState;
    public string BranchCastle="";
    public int TerrainVersion;
    public CampaignStage Stage;
    public Vector2 Position;
    public string TargetId="",VillageId="",Status="";
    public string PendingCastle="";
    public List<string> PendingCastles=new();
    public int NavigationVersion;
    public bool Farming;
    public float TradeElapsed;
    public int Trades,DungeonVisits;
    public List<int> Explored=new();
    public List<string> Cleared=new();
    public List<Vector2> Route=new();
    public Dictionary<string,List<Vector2>> VillagePaths=new();
    public int AreaPartAccessVersion;
    public int LayoutVersion;
}

public sealed partial class GameSession
{
    /// <summary>快照包含房间生成结果、尸体、计时与随机源。序列化必须紧随快照，期间不推进模拟。</summary>
    public SessionSnapshot Capture() => new() {
        Kp=Kp,KpEarned=KpEarned,KpSpent=KpSpent,DungeonEntrySerial=dungeonEntrySerial,MonsterLevelMin=MonsterLevelMin,MonsterLevelMax=MonsterLevelMax,KpKillReceipts=kpKillReceipts.ToList(),PublicExperience=PublicExperience,PublicExperienceFraction=PublicExperienceFraction,PublicExperienceVersion=1,RunVersion=1,RunId=RunId,TeamLocked=TeamLocked,Account=Account.Copy(),CampaignVersion=2,LatestMapIndex=LatestMapIndex,ActiveBranch=ActiveBranch,BranchQueue=branchQueue.ToList(),SuspendedWorld=suspendedWorld?.Capture(),
        DifficultyIndex=DifficultyIndex,ChapterIndex=ChapterIndex,CompletedMaps=CaptureChapters(),
        Seed=Seed,DungeonSeed=dungeonSeed,FloorIndex=FloorIndex,CurrentRoom=CurrentRoom,Experience=0,Kills=Kills,Retries=Retries,RoomEntryLootState=roomEntryLootState,Stars=Stars.Capture(),StarAttack=starAttack,StarHp=starHp,EliteKillReceipts=eliteKillReceipts.OrderBy(x=>x,StringComparer.Ordinal).ToArray(),
        Gold=Gold,Revision=Revision,HitCount=HitCount,LootRandomState=lootRandom.State,HeroExperienceVersion=1,CombatVersion=3,SkillTreeVersion=1,CombatRandomState=combatRandom.State,RoomEntryCombatState=roomEntryCombatState,SkillState=skillState,Won=Won,Destination=destination,Phase=phase,DoorWait=destination>=0&&phase==1&&doorOpener is {Alive:true}?doorWait:0,DoorFallback=destination>=0&&phase==1&&doorOpener is {Alive:true}&&doorFallback,DoorOpenerHero=destination>=0&&phase==1&&doorOpener is {Alive:true}&&Heroes.Contains(doorOpener)?Heroes.IndexOf(doorOpener):-1,DoorOpenerSummon=destination>=0&&phase==1&&doorOpener is {Alive:true}&&Summons.Contains(doorOpener)?Summons.IndexOf(doorOpener):-1,Regroup=regroup,
        ReviveElapsed=ReviveElapsed,ReviveTarget=ReviveTarget==null?-1:Heroes.IndexOf(ReviveTarget),Rescuer=Rescuer==null?-1:Heroes.IndexOf(Rescuer),
        ReserveHeroes=reserveHeroes.Select(h=>h.Copy()).ToList(),PendingFormation=pendingFormation.ToArray(),FormationCapacityFloor=formationCapacityFloor,Status=Status,ActiveDungeonId=ActiveDungeon?.Id??"",Heroes=Heroes.ToList(),Summons=Summons.Select(a=>a.Copy()).ToList(),RoomEntrySummons=roomEntrySummons.Select(a=>a.Copy()).ToList(),Floors=Floors,
        AssetAuditTick=assetAuditTick,AssetEventSequence=assetEventSequence,FixedSkillsVersion=Config.LegacySkillRuntime?0:2,Inventory=inventory.Select(e=>new InventoryEntry(e.Key.ItemId,e.Key.Price,e.Value)).ToList(),Log=Log.ToList(),Modifiers=SpawnModifiers,World=World?.Capture(),RoomEntry=roomEntry.ToList(),BossPhaseProgress=CaptureBossPhases()
    };
    /// <summary>正式存档只保留房间检查点。未完成战斗从进房位置重开，已领取资源与奖励标志一并保留。</summary>
    public SessionSnapshot CaptureProgress()
    {
        // 直接复制持久字段，不先序列化整场战斗；后台不访问活跃模型。
        var s=Capture();s.Heroes=Heroes.Select(h=>h.Copy()).ToList();s.Equipment=CaptureGear();s.Scrolls=CaptureScrolls();s.Potions=CapturePotions();
        foreach(var hero in s.Heroes)hero.Hp=Math.Min(hero.Hp,hero.MaxHp);
        s.Floors=Floors.Select(f=>new Floor {
            Seed=f.Seed,ActualSpawned=f.ActualSpawned,ActualKillFacts=f.ActualKillFacts,ElapsedTicks=f.ElapsedTicks,BattleTicks=f.BattleTicks,WalkTicks=f.WalkTicks,
            Passages=f.Passages.Select(p=>new Passage {A=p.A,B=p.B,Revealed=p.Revealed}).ToList(),
            Rooms=f.Rooms.Select(r=>new Room {
                Id=r.Id,X=r.X,Y=r.Y,Depth=r.Depth,HalfWidth=r.HalfWidth,HalfHeight=r.HalfHeight,
                Boss=r.Boss,Revealed=r.Revealed,Generated=r.Generated,EventUsed=r.EventUsed,Event=r.Event,SnapshotLevel=r.SnapshotLevel,
                Neighbors=r.Neighbors.ToList(),ContentCleared=r.Clear,
                Origins=r.Clear?new():r.Origins.Count>0?r.Origins.ToList():r.Monsters.Where(m=>m.Alive).Select(m=>new MonsterOrigin(m.SpecId,m.Level,m.Boss,m.Position,s.Modifiers)).ToList(),
                Loot=!r.Clear?new():r.Loot.Where(l=>!l.Collected).Select(l=>new RoomLoot {Position=l.Position,Gold=l.Gold,Items=l.Items.ToList()}).ToList()
            }).ToList()
        }).ToList();
        s.RoomCheckpoint=true;
        bool inDungeon=s.World==null||s.World.Stage==CampaignStage.Dungeon;
        bool unclearedRoom=inDungeon&&!s.Floors[s.FloorIndex].Rooms[s.CurrentRoom].ContentCleared;
        // 未清房从进房快照恢复召唤；已清房保留存活召唤的实际 HP，死亡记录不写入正式存档。
        s.Summons=(unclearedRoom?roomEntrySummons:Summons.Where(a=>a.Alive)).Select(a=>a.Copy()).ToList();
        s.RoomEntrySummons=unclearedRoom?s.Summons.Select(a=>a.Copy()).ToList():new();
        if(unclearedRoom)
        {
            for(int i=0;i<s.Heroes.Count;i++)
            {
                var entry=s.RoomEntry.Count==s.Heroes.Count?s.RoomEntry[i]:new PartyEntry(s.Floors[s.FloorIndex].Rooms[s.CurrentRoom].Center,s.Heroes[i].MaxHp);
                s.Heroes[i].Position=entry.Position;s.Heroes[i].Hp=entry.Hp;s.Heroes[i].Timer=0;
            }
            s.Destination=-1;s.Phase=0;s.Regroup=0;s.ReviveTarget=s.Rescuer=-1;s.ReviveElapsed=0;
            if(s.RoomEntryLootState!=0)s.LootRandomState=s.RoomEntryLootState;
        }
        s.SkillState=new();
        if(inDungeon&&!s.Floors[s.FloorIndex].Rooms[s.CurrentRoom].ContentCleared&&s.RoomEntryCombatState!=0)s.CombatRandomState=s.RoomEntryCombatState;
        s.HitCount=0;
        return s;
    }
    /// <summary>校验后恢复；正式检查点重建未清房战斗，保留到账成长和待拾取奖励，不结算离线时间。</summary>
    public static GameSession Restore(Config config,SessionSnapshot s)
    {
        MigrateRunIdentity(s);
        if(s.DifficultyIndex<0||s.DifficultyIndex>2||s.ChapterIndex<0||s.ChapterIndex>4)throw new InvalidDataException("难度/章节越界");
        config=config.ChapterMaps[s.ChapterIndex];
        foreach(var oldWorld in new[]{s.World,s.SuspendedWorld})if(oldWorld!=null&&(oldWorld.AreaPartAccessVersion==0||oldWorld.NavigationVersion==0||oldWorld.LayoutVersion==0||oldWorld.TerrainVersion==0)){oldWorld.OutdoorTrail=new();oldWorld.FollowGather=0;}
        MigratePublicExperience(s);ValidateKpState(config,s);
        MigrateCampaignPatch(config,s);
        if(config.MapVersion>1&&s.World!=null&&s.World.MapVersion!=config.MapVersion)throw new InvalidDataException("第一章地图结构已更换，旧地图编号不能映射到新POI；需要重置不兼容存档");
        MigrateWorldLayout(config,s);
        MigrateTerrainRules(config,s);
        bool townMigrated=MigrateTownLayout(config,s);
        if(s.RoomCheckpoint)
        {
            for(int floorIndex=0;floorIndex<s.Floors.Count;floorIndex++)
            {
                var floor=s.Floors[floorIndex];
                foreach(var r in floor.Rooms)
                {
                    r.Monsters.Clear();
                    if(r.Generated&&!r.ContentCleared)
                    {
                        foreach(var o in r.Origins)
                        {
                            var spec=config.FindMonsterSpec(o.SpecId)??throw new InvalidDataException("出生模板怪物引用无效");
                            config.Spawn.Validate(o.Modifiers);
                            if(o.Level<1||o.Level>config.MaxMonsterLevel)throw new InvalidDataException("出生模板等级超过怪物等级表范围");
                            var monster=config.Spawn.Create(spec,o.Level,o.Boss,o.Modifiers);config.ApplyCombatProfile(monster);monster.Position=o.Position;r.Monsters.Add(monster);
                        }
                        // 阶段预留槽位必须与进房生成同序（基础怪→阶段单位→敌方召唤），否则击杀收据索引会漂移。
                        int fallbackLevel=r.Monsters.Count==0?r.SnapshotLevel:r.Monsters.Max(m=>m.Level);
                        PrepareBossPhaseActors(config,r,s.ActiveDungeonId,floorIndex,r.Id,fallbackLevel);
                    }
                }
            }
        }
        if(s.RoomCheckpoint)foreach(var floor in s.Floors)foreach(var room in floor.Rooms)if(room.Generated&&!room.ContentCleared)PrepareEnemySummons(config,room);
        MigrateCombatFormula(config,s);
        if(config.LegacySkillRuntime)MigrateSkillTree(config,s);else MigrateFixedSkills(config,s);
        ValidateSnapshot(config,s);
        var game=new GameSession(config,s.Seed,s.Heroes.Select(h=>h.SpecId));
        game.Kp=s.Kp;game.KpEarned=s.KpEarned;game.KpSpent=s.KpSpent;game.dungeonEntrySerial=s.DungeonEntrySerial;game.MonsterLevelMax=s.MonsterLevelMax;game.kpKillReceipts.UnionWith(s.KpKillReceipts);
        game.PublicExperience=s.PublicExperience;game.PublicExperienceFraction=s.PublicExperienceFraction;game.Account=s.Account.Copy();game.RunId=s.RunId;game.TeamLocked=s.TeamLocked;
        game.RestoreChapters(s);
        game.Heroes.Clear();game.Heroes.AddRange(s.Heroes);game.RestoreGear(s.Equipment??new());game.RestoreConsumables(s.Scrolls??new(),s.Potions??new());game.Summons.AddRange(s.Summons);game.roomEntrySummons.AddRange(s.RoomEntrySummons);game.reserveHeroes.AddRange(s.ReserveHeroes);game.pendingFormation=s.PendingFormation.ToArray();game.formationCapacityFloor=s.FormationCapacityFloor==0?s.Heroes.Count:s.FormationCapacityFloor;
        game.Floors=s.Floors;game.dungeonSeed=s.DungeonSeed;game.FloorIndex=s.FloorIndex;game.CurrentRoom=s.CurrentRoom;
        game.Kills=s.Kills;game.Retries=s.Retries;game.Gold=s.Gold;game.Revision=s.Revision;game.HitCount=s.HitCount;
        if(s.AssetEventSequence<0||s.AssetAuditTick<0)throw new InvalidDataException("资源事件序号/逻辑时刻无效");game.assetEventSequence=s.AssetEventSequence;game.assetAuditTick=s.AssetAuditTick;
        game.lootRandom.State=s.LootRandomState;game.Won=s.Won;game.destination=s.Destination;game.phase=s.Phase;game.regroup=s.Regroup;
        if(s.CombatRandomState!=0)game.combatRandom.State=s.CombatRandomState;
        game.roomEntryCombatState=s.RoomEntryCombatState==0?game.combatRandom.State:s.RoomEntryCombatState;game.skillState=s.SkillState;
        // 门前等待只在跑图相位的等待子状态保存；读档沿用同一先锋与计时，避免重新挑选造成分叉。
        if(game.destination>=0&&game.phase==1)
        {
            game.doorWait=s.DoorWait;game.doorFallback=s.DoorFallback;
            game.doorOpener=s.DoorOpenerHero>=0?game.Heroes[s.DoorOpenerHero]:s.DoorOpenerSummon>=0?game.Summons[s.DoorOpenerSummon]:null;
        }
        game.RefreshTravelLeader(game.destination>=0);
        foreach(var actor in game.Heroes.Concat(game.Summons).Concat(game.reserveHeroes).Concat(game.Floors.SelectMany(f=>f.Rooms).SelectMany(r=>r.Monsters)))
            config.ReconcileCombatMetadata(actor);
        game.ReviveElapsed=s.ReviveElapsed;game.ReviveTarget=s.ReviveTarget<0?null:game.Heroes[s.ReviveTarget];game.Rescuer=s.Rescuer<0?null:game.Heroes[s.Rescuer];
        game.Status=s.Status;game.ActiveDungeon=s.ActiveDungeonId==""?null:config.Dungeons.Single(d=>d.Id==s.ActiveDungeonId);
        game.SpawnModifiers=s.Modifiers;game.Log.Clear();game.Log.AddRange(s.Log);
        game.roomEntry=s.RoomEntry.ToList();
        game.roomEntryLootState=s.RoomEntryLootState;
        game.Stars=new StarProgression(config.Stars,s.Stars);game.starAttack=s.StarAttack;game.starHp=s.StarHp;
        game.eliteKillReceipts.Clear();game.eliteKillReceipts.UnionWith(s.EliteKillReceipts);
        game.ValidateStarTotals(game.Stars.Totals());game.ApplyStarTotals(game.Stars.Totals());
        foreach(var i in s.Inventory)game.inventory.Add((i.ItemId,i.Price),i.Count);
        game.World=s.World==null?null:WorldCampaign.Restore(config,s.World,s.DifficultyIndex);
        game.World?.SyncMonsterLevelRange(game);
        if(townMigrated)game.Note("主城布局更新：保留成长、背包、通关和补给阶段，移动到安全落点");
        if(config.Town!=null&&game.World?.Stage==CampaignStage.Village)
        {
            if(s.World!.TownVersion==0){game.World.SpawnTown(game,TownVisitPhase.Heal);game.Note("营地场景更新：保留进度，重新完成治疗与售卖");}
            else if(game.Heroes.Any(h=>!config.Town.Walkable(h.Position)))throw new InvalidDataException("营地角色位于不可走位置");
        }
        game.RestoreBranchState(s);
        if(game.World!=null&&s.World!.AreaPartAccessVersion==0&&!game.World.ReconcileAreaPartAccess(game))
        {
            game.Floors.Clear();game.ActiveDungeon=null;game.Won=false;game.destination=-1;game.phase=0;game.FloorIndex=game.CurrentRoom=0;game.regroup=0;game.ResetRescue();
            foreach(var h in game.Heroes){h.Hp=h.MaxHp;h.Timer=0;}
            game.Note("统治区前置规则更新：保留成长与资源，回初始村庄补给后继续冒险");
        }
        if(config.Town!=null)foreach(var h in game.Heroes.Concat(game.reserveHeroes))if(game.GrantStarterSkill(h))game.MarkChanged();
        if(s.RoomCheckpoint&&game.Floors.Count>0&&(game.World==null||game.World.Stage==CampaignStage.Dungeon)&&!game.Floor.Rooms[game.CurrentRoom].Clear)
        {
            game.skillState=new();
            var room=game.Floor.Rooms[game.CurrentRoom];
            var anchor=room.Neighbors.Select(i=>room.DoorTowards(game.Floor.Rooms[i])).OrderBy(p=>Vector2.DistanceSquared(p,game.Heroes[0].Position)).FirstOrDefault(room.Center-Vector2.UnitX*room.HalfWidth);
            game.StartCombatEntry(room,anchor);
        }
        // 阶段进度必须在技能现场重建之后重放：它会重新附上阶段技能与短期 Buff。
        game.RestoreBossPhases(s.BossPhaseProgress);
        return game;
    }
    /// <summary>旧档切换到正式三系布局：同 ID 保留等级；仅退款重排后前置/系内投入已不合法的节点，SkillTreeVersion 保证幂等。</summary>
    private static void MigrateSkillTree(Config c,SessionSnapshot s)
    {
        if(s.SkillTreeVersion is not (0 or 1))throw new InvalidDataException("不支持的天赋树版本");
        if(s.SkillTreeVersion==1)return;
        foreach(var hero in s.Heroes.Concat(s.ReserveHeroes))
        {
            if(hero.Talents==null)continue;
            hero.SkillIds??=Array.Empty<string>();hero.ActiveAuraSkills??=new();hero.SkillSwapLocks??=new();hero.TalentBonuses??=new();
            // 未知ID或越界等级说明存档被篡改或来自其他版本，直接拒绝而不是静默退款。
            foreach(var pair in hero.Talents)
                if(!c.Talents.Nodes.TryGetValue(pair.Key,out var node)||node.Hero!=hero.SpecId||pair.Value<1||pair.Value>node.MaxRank)
                    throw new InvalidDataException("天赋树迁移遇到未知或非法节点："+pair.Key);
            int refunded=0;
            while(!TalentLayoutLegal(c,hero,out string? offender,out bool refund))
            {
                if(offender==null)throw new InvalidDataException("天赋树迁移无法定位不兼容节点");
                if(refund&&hero.Talents.Remove(offender))refunded++;
                // 装备、换入冷却与激活光环都跟随被移除节点一起清理，避免二次校验失败。
                hero.SkillIds=hero.SkillIds.Where(id=>id!=offender).ToArray();
                hero.SkillSwapLocks.Remove(offender);
                hero.ActiveAuraSkills=hero.ActiveAuraSkills.Where(id=>id!=offender).ToList();
                hero.TalentBonuses.RemoveAll(b=>b.Scope=="node"&&b.Target==offender);
            }
            if(refunded>0)
            {
                if(s.Log.Count>=40)s.Log.RemoveAt(0);
                s.Log.Add($"天赋树迁移：{hero.Name} 退款 {refunded} 个与新布局不兼容的节点");
            }
        }
        s.SkillTreeVersion=1;
    }
    /// <summary>迁移用只读合法性检查；失败时优先返回行号更高的待退款节点，装备引用问题只清理装配不退款。</summary>
    private static bool TalentLayoutLegal(Config c,Actor h,out string? offender,out bool refund)
    {
        offender=null;refund=false;
        foreach(var pair in h.Talents)
            if(!c.Talents.Nodes.TryGetValue(pair.Key,out var node)||node.Hero!=h.SpecId||pair.Value<1||pair.Value>node.MaxRank){offender=pair.Key;refund=true;return false;}
        int budget=c.Talents.Rule("talent_initial_points")+(h.Level-1)*c.Talents.Rule("talent_points_per_level");
        if(h.Talents.Values.Sum()>budget){offender=HighestRowNode(c,h.Talents.Keys);refund=true;return false;}
        var pending=h.Talents.Keys.ToHashSet();var accepted=new HashSet<string>();var invested=new int[3];
        while(pending.Count>0)
        {
            string? ready=pending.FirstOrDefault(id=>{var node=c.Talents.Nodes[id];return invested[node.Tree]>=c.Talents.Required(node)&&(node.Prerequisite==""||accepted.Contains(node.Prerequisite)&&h.Talents[node.Prerequisite]>=node.RequiredRank);});
            if(ready==null){offender=HighestRowNode(c,pending);refund=true;return false;}
            var n=c.Talents.Nodes[ready];invested[n.Tree]+=h.Talents[ready];accepted.Add(ready);pending.Remove(ready);
        }
        foreach(string id in h.SkillIds)
            if(c.Talents.Nodes.TryGetValue(id,out var n)&&(n.Hero!=h.SpecId||h.Talents.GetValueOrDefault(id)==0||n.Kind!="active")){offender=id;refund=false;return false;}
        foreach(string id in h.ActiveAuraSkills)
            if(!h.SkillIds.Contains(id)||!c.Skills.Skills.TryGetValue(id,out var skill)||!skill.Effects.Any(e=>c.Skills.Effects[e].Kind=="MaintainAura")){offender=id;refund=false;return false;}
        return true;
    }
    private static string HighestRowNode(Config c,IEnumerable<string> ids)=>ids.Where(c.Talents.Nodes.ContainsKey).OrderByDescending(id=>c.Talents.Nodes[id].Row).ThenByDescending(id=>c.Talents.Nodes[id].Col).ThenBy(id=>id,StringComparer.Ordinal).First();
    private static void ValidateSnapshot(Config c,SessionSnapshot s)
    {
        static void Need([DoesNotReturnIf(false)] bool value,string reason) {if(!value)throw new InvalidDataException("存档无效："+reason);}
        static bool Finite(float n)=>float.IsFinite(n);
        static bool Point(Vector2 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y);
        Need(s.CombatVersion==3,"战斗存档版本");
        Need(s.CampaignVersion is 0 or 2,"远征存档版本");
        Need(s.ReserveHeroes!=null&&s.PendingFormation!=null&&s.Heroes!=null&&s.Summons!=null&&s.RoomEntrySummons!=null&&s.Floors!=null&&s.Inventory!=null&&s.Log!=null&&s.Modifiers!=null&&s.Equipment!=null,"缺少状态字段");
        ValidateGear(c,s.Equipment!,s.Heroes!.Count);
        ValidateConsumables(c,s.Scrolls,s.Potions);
        Need(s.Heroes!.Count is >=1 and <=4&&s.Heroes.Select(h=>h.HeroInstanceId).Distinct().Count()==s.Heroes.Count&&s.Heroes.All(h=>c.Heroes.Any(t=>t.Id==h.SpecId)),"队伍引用");
        Need(s.Gold>=0&&s.Experience>=0&&s.Kills>=0&&s.Retries>=0&&s.Revision>=0&&s.HitCount>=0&&s.LootRandomState!=0,"资源/随机源");
        Need(s.Log!.Count<=40&&s.ReviveTarget>=-1&&s.ReviveTarget<s.Heroes.Count&&s.Rescuer>=-1&&s.Rescuer<s.Heroes.Count&&Finite(s.ReviveElapsed)&&s.ReviveElapsed>=0&&s.ReviveElapsed<=c.ReviveSeconds&&Finite(s.Regroup)&&s.Regroup>=0,"行动计时");
        Need((s.ReviveTarget<0)==(s.Rescuer<0)&&(s.ReviveTarget<0||(!s.Heroes[s.ReviveTarget].Alive&&s.Heroes[s.Rescuer].Alive)),"救援引用");
        c.Spawn.Validate(s.Modifiers!);
        Need(s.Stars!=null&&double.IsFinite(s.StarAttack)&&double.IsFinite(s.StarHp)&&s.StarAttack>=0&&s.StarHp>=0,"星图贡献");
        // 精英击杀收据：数量有界、无重复、无非空字符串；旧档缺字段按空数组视为合法。
        Need(s.EliteKillReceipts!=null&&s.EliteKillReceipts.Length<=4096&&s.EliteKillReceipts.All(r=>!string.IsNullOrWhiteSpace(r)&&r.Length<=128)&&s.EliteKillReceipts.Distinct().Count()==s.EliteKillReceipts.Length,"精英击杀收据无效");
        // Boss 阶段进度：旧档缺字段按空列表合法；每条必须是 poi:floor:room:phase 且引用的阶段与楼层真实存在。
        Need(s.BossPhaseProgress!=null&&s.BossPhaseProgress.Count<=4096,"Boss阶段进度字段");
        foreach(string entry in s.BossPhaseProgress)
        {
            var parts=entry.Split(':');
            Need(parts.Length==4&&parts.All(p=>p!="")&&entry.Length<=128,"Boss阶段进度格式");
            bool parsedFloor=int.TryParse(parts[1],out int floor);
            bool parsedRoom=int.TryParse(parts[2],out int roomIndex);
            bool parsedPhase=int.TryParse(parts[3],out int phase);
            Need(parsedFloor&&parsedRoom&&parsedPhase,"Boss阶段进度数值");
            int applied=c.BossPhasesOf(c.ChapterIndex+1,parts[0]).Count;
            Need(applied==0||(phase>=1&&phase<=applied),"Boss阶段进度引用无效阶段");
            if(s.ActiveDungeonId==parts[0]&&s.Floors!=null&&s.Floors.Count>0)
                Need(floor>=0&&floor<s.Floors.Count&&roomIndex>=0&&roomIndex<s.Floors[floor].Rooms.Count,"Boss阶段进度引用无效房间");
        }
        var talentStars=new StarProgression(c.Stars,s.Stars);ValidateFormationSnapshot(c,s);
        if(!c.LegacySkillRuntime)
        {
            // C1-A 过渡：该职业已投放资格节点时必须购买；尚未投放资格节点的职业按学习期规则可持有T投入。
            bool TraitQualified(Actor hero)
            {
                if(!hero.Talents.Keys.Any(id=>c.Talents.Nodes.TryGetValue(id,out var node)&&node.ClassTrait))return true;
                var gates=c.Stars.Nodes.Values.Where(gate=>gate.Attribute=="class_trait_unlock"&&gate.TargetId==hero.SpecId).ToArray();
                if(gates.Length==0)return true;
                return gates.Any(gate=>talentStars.Level(gate.Id)>0);
            }
            Need(s.Heroes.Concat(s.ReserveHeroes).All(TraitQualified),"T投资缺少本职业星图资格");
        }
        Need(s.Heroes.Concat(s.ReserveHeroes).All(h=>h.SkillIds!=null&&h.SkillIds.Count(id=>c.Talents.Nodes.ContainsKey(id))<=TalentSlotCapacity(c,talentStars)),"主动技能槽超过星图解锁容量");
        Need(s.Inventory!.All(i=>c.Items.Any(t=>t.Id==i.ItemId)&&i.Price>=0&&i.Count>0)&&s.Inventory.Select(i=>(i.ItemId,i.Price)).Distinct().Count()==s.Inventory.Count,"背包引用/数量");
        Need((s.RoomEntry.Count==0||s.RoomEntry.Count==s.Heroes.Count)&&s.RoomEntry.All(e=>Point(e.Position)&&Finite(e.Hp)&&e.Hp>=0),"进房节点");
        Need(s.ActiveDungeonId==""||c.Dungeons.Any(d=>d.Id==s.ActiveDungeonId),"地牢引用");
        double AllowedMaximum(Actor actor)
        {
            if(s.SkillState?.Buffs==null||s.FloorIndex<0||s.FloorIndex>=s.Floors!.Count||s.CurrentRoom<0||s.CurrentRoom>=s.Floors[s.FloorIndex].Rooms.Count)return actor.MaxHp;
            var room=s.Floors[s.FloorIndex].Rooms[s.CurrentRoom];int index=s.Heroes.IndexOf(actor);
            if(index<0){int monster=room.Monsters.IndexOf(actor);if(monster>=0)index=s.Heroes.Count+monster;else {int summon=s.Summons!.IndexOf(actor);if(summon<0)return actor.MaxHp;index=s.Heroes.Count+room.Monsters.Count+summon;}}
            var buffs=s.SkillState.Buffs.Where(b=>b.Target==index&&b.Remaining>0&&c.Skills.Buffs.ContainsKey(b.Id)).GroupBy(b=>c.Skills.Buffs[b.Id].StackGroup==""?b.Id:c.Skills.Buffs[b.Id].StackGroup).SelectMany(group=>c.Skills.Buffs[group.First().Id].Stacking=="Strongest"?group.OrderByDescending(b=>c.Skills.Buffs[b.Id].StackStrength).ThenBy(b=>b.Owner).ThenBy(b=>b.Instance).Take(1):group);
            return c.Attributes.Aggregate("hp",actor.MaxHp,buffs.SelectMany(b=>c.Skills.Buffs[b.Id].Attributes).Where(a=>a.Attribute=="hp"));
        }
        foreach(var a in s.Heroes.Concat(s.ReserveHeroes).Concat(s.Summons).Concat(s.RoomEntrySummons).Concat(s.Floors!.SelectMany(f=>f.Rooms).SelectMany(r=>r.Monsters)))
        {
            Need(Point(a.Position)&&Finite(a.Hp)&&Finite(a.MaxHp)&&Finite(a.Attack)&&Finite(a.Defense)&&Finite(a.Speed)&&Finite(a.Range)&&Finite(a.Cooldown)&&Finite(a.Timer)&&Finite(a.DropMultiplier)&&Finite(a.ValueMultiplier)&&a.MaxHp>0&&a.Hp>=0&&a.Hp<=AllowedMaximum(a)&&a.Level>0&&a.Attack>=0&&a.Defense>=0&&a.Speed>0&&a.Range>0&&a.Cooldown>0&&a.Timer>=0&&a.DropMultiplier>=0&&a.ValueMultiplier>0,"角色属性");
            Need(a.Experience>=0&&a.ExperienceFraction is >=0 and <100,"角色经验无效");
            Need(Enum.IsDefined(a.TargetPolicy),"怪物索敌策略无效");
            Need(a.Battle!=null&&a.SkillIds!=null&&a.SkillLevels!=null,"战斗属性/技能缺失");a.Battle.Validate();
            bool summon=a.SummonDefinitionId!="";
            if(summon)Need(c.Skills.Summons.ContainsKey(a.SummonDefinitionId)&&a.SummonOwner>=0&&a.SummonOwner<s.Heroes.Count&&s.Heroes[a.SummonOwner].SkillIds.Contains(a.SummonSkill),"召唤定义/主人引用无效");
            else Need(a.SummonOwner<0&&a.SummonSkill=="","非召唤物不得携带召唤来源");
            // 职业特性T绑定的光环由已学T节点提供，不占S1～S4槽；其余激活光环仍必须已装配。
            bool talentAura(string id)=>c.Talents.Nodes.Values.Any(n=>n.ClassTrait&&n.SkillId==id&&a.Talents!=null&&a.Talents.GetValueOrDefault(n.Id)>0);
            Need(a.ActiveAuraSkills!=null&&a.ActiveAuraSkills.Count<=a.SkillIds.Length+c.Talents.Nodes.Values.Count(n=>n.ClassTrait)&&a.ActiveAuraSkills.Distinct().Count()==a.ActiveAuraSkills.Count&&a.ActiveAuraSkills.All(id=>(a.SkillIds.Contains(id)||talentAura(id))&&c.Skills.Skills.TryGetValue(id,out var skill)&&skill.Effects.Any(e=>c.Skills.Effects[e].Kind=="MaintainAura")),"激活光环缺失、重复或未装配");
            Need(a.Level<=(s.Heroes.Contains(a)||s.ReserveHeroes.Contains(a)||summon?c.MaxHeroLevel:c.MaxMonsterLevel),"等级超过等级表表尾；请补齐配置，禁止静默降级");
            ValidateTalents(c,a);
            Need(a.SkillIds.Distinct().Count()==a.SkillIds.Length&&a.SkillIds.All(id=>c.Skills.Skills.TryGetValue(id,out var skill)&&skill.Kind=="ActiveSkill")&&a.SkillLevels.All(p=>c.Skills.Abilities.TryGetValue(p.Key,out var ability)&&p.Value>=1&&p.Value<=ability.MaxLevel),"装备技能/等级引用");
        }
        ValidateSkillSnapshot(c,s);
        Need(s.Floors!.Count<=20,"楼层数量");
        foreach(var f in s.Floors)
        {
            Need(f.Rooms.Count>=3&&f.Rooms.Count<=20&&f.Rooms.Select((r,i)=>r.Id==i).All(v=>v)&&f.Rooms.Count(r=>r.Boss)==1,"房间编号/BOSS");
            Need(f.Rooms.Select(r=>(r.X,r.Y)).Distinct().Count()==f.Rooms.Count&&f.Passages.Count==f.Rooms.Count-1,"房间拓扑");
            Need(f.Passages.Select(p=>(Math.Min(p.A,p.B),Math.Max(p.A,p.B))).Distinct().Count()==f.Passages.Count&&f.Rooms.Sum(r=>r.Neighbors.Count)==f.Passages.Count*2,"重复走廊/连接数量");
            foreach(var r in f.Rooms)
            {
                Need(r.Neighbors.Distinct().Count()==r.Neighbors.Count&&r.Neighbors.All(n=>n>=0&&n<f.Rooms.Count&&n!=r.Id&&f.Rooms[n].Neighbors.Contains(r.Id)&&Math.Abs(r.X-f.Rooms[n].X)+Math.Abs(r.Y-f.Rooms[n].Y)==1),"房间连接");
                Need(float.IsFinite(r.HalfWidth)&&float.IsFinite(r.HalfHeight)&&r.HalfWidth>=2&&r.HalfWidth<=5&&r.HalfHeight>=2&&r.HalfHeight<=5,"房间尺寸");
                Need((!r.Generated||r.Revealed)&&(!r.EventUsed||r.Generated)&&new[]{"","营火","宝箱"}.Contains(r.Event)&&r.Monsters.Count<=1000&&(!r.Generated?r.Monsters.Count==0:true),"房间生成状态");
                Need(r.Monsters.All(m=>m.EnemySummonSource>=-1&&(m.EnemySummonSource<0||m.EnemySummonSource<r.Monsters.Count&&m.EnemySummonSource!=r.Monsters.IndexOf(m)&&c.EnemySummons.ContainsKey(m.EnemySummonRule)&&(!m.Alive||m.EnemySummonActivated))),"敌方召唤来源无效");
                Need(r.Monsters.All(m=>c.FindMonsterSpec(m.SpecId)!=null&&m.RewardXp>=0),"怪物引用");
                Need(r.Origins.Count<=1000&&r.Loot.Count<=1000&&(!r.ContentCleared||r.Generated),"房间检查点");
                Need(!s.RoomCheckpoint||!r.Generated||r.ContentCleared||r.Origins.Count>0,"未清房缺少出生模板");
                foreach(var loot in r.Loot)
                    Need(Point(loot.Position)&&loot.Gold>=0&&loot.Items.All(i=>c.Items.Any(t=>t.Id==i.ItemId)&&i.Price>=0&&i.Count>0),"战利品数据");
            }
            Need(f.Passages.All(p=>p.A>=0&&p.B>=0&&p.A<f.Rooms.Count&&p.B<f.Rooms.Count&&f.Rooms[p.A].Neighbors.Contains(p.B)),"走廊引用");
            var connected=new HashSet<int>();var pending=new Queue<int>();pending.Enqueue(0);
            while(pending.Count>0){int id=pending.Dequeue();if(!connected.Add(id))continue;foreach(int n in f.Rooms[id].Neighbors)pending.Enqueue(n);}
            Need(connected.Count==f.Rooms.Count,"房间不连通");
        }
        bool inDungeon=s.World==null||s.World.Stage==CampaignStage.Dungeon;
        if(inDungeon)
        {
            Need(s.FloorIndex>=0&&s.FloorIndex<s.Floors.Count,"当前楼层");var f=s.Floors[s.FloorIndex];
            Need(s.CurrentRoom>=0&&s.CurrentRoom<f.Rooms.Count&&f.Rooms[s.CurrentRoom].Revealed&&s.Destination>=-1&&s.Destination<f.Rooms.Count&&s.Phase>=0&&s.Phase<=1,"当前房间/移动");
            Need(s.Destination<0||f.Rooms[s.CurrentRoom].Neighbors.Contains(s.Destination),"移动目标不相邻");
            Need(float.IsFinite(s.DoorWait)&&s.DoorWait>=0&&s.DoorWait<=3600,"门前等待计时无效");
            Need(s.DoorOpenerHero>=-1&&s.DoorOpenerHero<s.Heroes.Count&&s.DoorOpenerSummon>=-1&&s.DoorOpenerSummon<s.Summons.Count&&!(s.DoorOpenerHero>=0&&s.DoorOpenerSummon>=0),"开门先锋引用无效");
            bool waiting=s.Destination>=0&&s.Phase==1&&(s.DoorOpenerHero>=0||s.DoorOpenerSummon>=0);
            Need(!waiting||s.DoorOpenerHero<0||s.Heroes[s.DoorOpenerHero].Alive,"开门先锋已阵亡");
            Need(!waiting||s.DoorOpenerSummon<0||s.Summons[s.DoorOpenerSummon].Alive,"开门召唤物已阵亡");
            Need(waiting||s.DoorWait==0&&!s.DoorFallback&&s.DoorOpenerHero<0&&s.DoorOpenerSummon<0,"非门前等待状态残留先锋或计时");
            Need(!s.DoorFallback||waiting,"超时兜底缺少开门先锋");
        }
        if(s.World!=null)
        {
            WorldCampaign.Validate(c,s.World);
            Need(!inDungeon||s.ActiveDungeonId==s.World.TargetId,"世界/地牢目标不一致");
        }
    }
}

public sealed partial class WorldCampaign
{
    internal WorldSnapshot Capture() => new() {OutdoorTrail=TrailCurrent?outdoorTrail.ToList():new(),FollowGather=TrailCurrent?followGather:0,WaitingApproval=WaitingApproval,WaitApprovalTicks=WaitApprovalTicks,PortalOrigin=PortalOrigin,ReturnPortalVisible=Progress.ReturnPortalVisible,PortalPhase=PortalPhase,PortalReturning=PortalReturning,PortalElapsed=PortalElapsed,PortalWaypoint=PortalWaypoint,TownVersion=1,TownLayoutVersion=config.Town?.LayoutVersion??0,TownPhase=TownPhase,MapVersion=config.MapVersion,Failures=new(failures),PausedAtLevel=new(pausedAtLevel),RetryCastles=retryCastles.OrderBy(id=>id).ToList(),TeamLevel=teamLevel,RecoveryRandomState=recoveryRandom.State,BranchCastle=BranchCastle,TerrainVersion=1,LayoutVersion=1,AreaPartAccessVersion=1,Stage=Stage,Position=Position,TargetId=Target.Dungeon.Id,Farming=Target.Farming,VillageId=Village.Id,TradeElapsed=TradeElapsed,Trades=Trades,DungeonVisits=DungeonVisits,Status=Status,PendingCastles=pendingCastles.ToList(),NavigationVersion=1,Explored=Progress.Explored.OrderBy(i=>i).ToList(),Cleared=Progress.Cleared.OrderBy(i=>i).ToList(),Route=route.ToList(),VillagePaths=villagePaths.ToDictionary(p=>p.Key,p=>p.Value.ToList())};
    internal static WorldCampaign Restore(Config c,WorldSnapshot s,int difficulty=0)
    {
        var restoredProgress=new WorldProgress {ReturnPortalVisible=s.ReturnPortalVisible};restoredProgress.Explored.UnionWith(s.Explored);restoredProgress.Cleared.UnionWith(s.Cleared);
        var w=new WorldCampaign(c,difficulty,restoredProgress,s.BranchCastle) {WaitingApproval=s.WaitingApproval,WaitApprovalTicks=s.WaitApprovalTicks,PortalOrigin=s.PortalOrigin,PortalPhase=s.PortalPhase,PortalReturning=s.PortalReturning,PortalElapsed=s.PortalElapsed,PortalWaypoint=s.PortalWaypoint,TownPhase=s.TownPhase,Stage=s.Stage,Position=s.Position,Target=new(c.Dungeons.Single(d=>d.Id==s.TargetId),s.Farming),Village=c.Villages.Single(v=>v.Id==s.VillageId),TradeElapsed=s.TradeElapsed,Trades=s.Trades,DungeonVisits=s.DungeonVisits,Status=s.Status};
        foreach(var p in s.Failures)w.failures.Add(p.Key,p.Value);foreach(var p in s.PausedAtLevel)w.pausedAtLevel.Add(p.Key,p.Value);w.retryCastles.UnionWith(s.RetryCastles);w.teamLevel=s.TeamLevel;if(s.RecoveryRandomState!=0)w.recoveryRandom.State=s.RecoveryRandomState;
        w.Progress.Explored.Clear();w.Progress.Explored.UnionWith(s.Explored);w.Progress.Cleared.UnionWith(s.Cleared);w.pendingCastles.AddRange(s.PendingCastles.Count>0?s.PendingCastles:s.PendingCastle!=""?new[]{s.PendingCastle}:Array.Empty<string>());w.route.Clear();foreach(var p in s.Route)w.route.Enqueue(p);
        if(s.AreaPartAccessVersion==0||s.NavigationVersion==0||s.LayoutVersion==0||s.TerrainVersion==0){s.OutdoorTrail=new();s.FollowGather=0;}
        w.ValidateOutdoorTrail(s);w.outdoorTrail.AddRange(s.OutdoorTrail);w.followGather=s.FollowGather;
        foreach(var p in s.VillagePaths)w.villagePaths[p.Key]=new Queue<Vector2>(p.Value);
        if(s.NavigationVersion==0&&s.Stage is CampaignStage.TravelDungeon or CampaignStage.TravelVillage)
        {
            var last=s.Route.Last();w.BuildRoute(w.CurrentRegion,last);
        }
        return w;
    }
    internal static void Validate(Config c,WorldSnapshot s)
    {
        if(s.WaitApprovalTicks<0)throw new InvalidDataException("Invalid approval clock");
        if(!Enum.IsDefined(s.PortalPhase)||!float.IsFinite(s.PortalElapsed)||s.PortalElapsed<0||s.PortalElapsed>c.PortalSeconds)throw new InvalidDataException("传送表演状态无效");
        if(s.PortalPhase!=PortalPhase.None)
        {
            if(!float.IsFinite(s.PortalOrigin.X)||!float.IsFinite(s.PortalOrigin.Y))throw new InvalidDataException("传送门施放位置无效");
            if(c.Town==null||(s.PortalReturning?s.Stage is not (CampaignStage.TravelVillage or CampaignStage.Village):s.Stage!=CampaignStage.TravelDungeon||!c.Waypoints.Any(w=>w.Id==s.PortalWaypoint)))throw new InvalidDataException("传送场景或目标无效");
            if(!s.PortalReturning){var p=new WorldProgress();p.Cleared.UnionWith(s.Cleared);if(!p.WaypointUnlocked(c,c.Waypoints.Single(w=>w.Id==s.PortalWaypoint)))throw new InvalidDataException("传送目标未解锁");}
        }
        if(s.TownVersion is not (0 or 1)||!Enum.IsDefined(s.TownPhase))throw new InvalidDataException("营地流程版本或阶段无效");
        if(s.TownLayoutVersion<0||s.TownLayoutVersion>(c.Town?.LayoutVersion??0))throw new InvalidDataException("不支持的主城布局版本");
        if(s.Failures==null||s.PausedAtLevel==null||s.RetryCastles==null||s.TeamLevel<1||s.TeamLevel>c.MaxHeroLevel||s.Failures.Any(p=>!c.Dungeons.Any(d=>d.Id==p.Key)||p.Value<1||p.Value>100000)||s.PausedAtLevel.Any(p=>!c.Dungeons.Any(d=>d.Id==p.Key)||p.Value<1||p.Value>c.MaxHeroLevel)||s.RetryCastles.Distinct().Count()!=s.RetryCastles.Count||s.RetryCastles.Any(id=>!c.Dungeons.Any(d=>d.Id==id&&d.Castle))||s.BranchCastle!=""&&!c.Dungeons.Any(d=>d.Id==s.BranchCastle&&d.Castle&&c.Optional(id:d.Id)))throw new InvalidDataException("失败回刷或支线状态引用非法");
        if(s.LayoutVersion is not (0 or 1))throw new InvalidDataException("世界布局存档版本无效");
        if(s.AreaPartAccessVersion is not (0 or 1))throw new InvalidDataException("统治区前置存档版本无效");
        if(s.NavigationVersion is not (0 or 1))throw new InvalidDataException("不支持的世界寻路版本");
        bool Point(Vector2 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&p.X>=0&&p.Y>=0&&p.X<c.WorldWidth&&p.Y<c.WorldHeight;
        if(s.VillagePaths==null||s.VillagePaths.Count>4||s.VillagePaths.Any(p=>string.IsNullOrWhiteSpace(p.Key)||p.Value.Count>20000||p.Value.Any(v=>s.TownVersion==1&&c.Town!=null?!c.Town.Walkable(v):!Point(v))))throw new InvalidDataException("村庄路径无效");
        if(!Enum.IsDefined(s.Stage)||!Point(s.Position)||!c.Dungeons.Any(d=>d.Id==s.TargetId)||!c.Villages.Any(v=>v.Id==s.VillageId)||s.Explored==null||s.Cleared==null||s.Route==null||s.Explored.Any(id=>!c.Regions.Any(r=>r.Id==id))||s.Cleared.Any(id=>!c.Dungeons.Any(d=>d.Id==id))||s.Route.Count>c.Regions.Count*WorldNavigation.CellsPerRegion*WorldNavigation.CellsPerRegion+2||s.Route.Any(p=>!Point(p))||s.Trades<0||s.DungeonVisits<0||!float.IsFinite(s.TradeElapsed)||s.TradeElapsed<0||s.TradeElapsed>(s.TownVersion==0?c.TradeSeconds:c.Town?.ServiceSeconds??c.TradeSeconds)||s.PendingCastle!=""&&!c.Dungeons.Any(d=>d.Id==s.PendingCastle&&d.Castle))
            throw new InvalidDataException("存档世界状态/引用无效");
        if(s.PendingCastles==null||s.PendingCastles.Distinct().Count()!=s.PendingCastles.Count||s.PendingCastles.Any(id=>!c.Dungeons.Any(d=>d.Id==id&&d.Castle)))throw new InvalidDataException("城堡挑战队列无效");
        int current=c.Regions.Single(r=>r.X==(int)s.Position.X&&r.Y==(int)s.Position.Y).Id;
        if(!WorldNavigation.Walkable(c,s.Position)||s.Route.Any(p=>!WorldNavigation.Walkable(c,p)))throw new InvalidDataException("存档位置或路线进入不可通行地块");
        if(s.AreaPartAccessVersion==1)
        {
            var progress=new WorldProgress();progress.Cleared.UnionWith(s.Cleared);
            var allowed=progress.AccessibleRegions(c);
            int RegionAt(Vector2 point)=>c.Regions.Single(r=>r.X==(int)point.X&&r.Y==(int)point.Y).Id;
            if(!allowed.Contains(current)||s.Stage is CampaignStage.TravelDungeon or CampaignStage.Dungeon&&!allowed.Contains(c.Dungeons.Single(d=>d.Id==s.TargetId).RegionId)||s.Route.Any(p=>!allowed.Contains(RegionAt(p))))throw new InvalidDataException("存档路线或目标进入未解锁统治区");
        }
        if(!s.Explored.Contains(current)||((s.Stage==CampaignStage.TravelDungeon||s.Stage==CampaignStage.TravelVillage)&&s.Route.Count==0)||((s.Stage==CampaignStage.Dungeon||s.Stage==CampaignStage.Village)&&s.Route.Count!=0))throw new InvalidDataException("存档探索/路径状态无效");
        if(c.Gates.Count>0)
        {
            int At(Vector2 p)=>c.Regions.Single(r=>r.X==(int)p.X&&r.Y==(int)p.Y).Id;
            var previous=s.Position;
            foreach(var point in s.Route)
            {if(!c.CanCross(At(previous),At(point)))throw new InvalidDataException("存档路径跨越非关口边界");previous=point;}
        }
    }
}
