using System;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

/// <summary>本次运行的 GM 战斗参数；不属于成长、科技或存档，重新创建/读取会话后恢复默认。</summary>
public record GmBattleSettings(bool Invincible=false,float MonsterHp=1,float MonsterAttack=1,int RoomCountBonus=0,bool InstantDungeonClear=false,bool SkipSupplies=false);

public sealed partial class GameSession
{
    /// <summary>明确GM例外：当前执行章节全部通关，不发奖励；放弃当前现场，回城治疗售卖后按正常规则推进。</summary>
    public void CompleteGmChapter()
    {
        if(World==null)return;
        World.Progress.Cleared.UnionWith(Config.Dungeons.Select(d=>d.Id));
        World.Progress.Explored.UnionWith(Config.Regions.Where(r=>r.Enterable).Select(r=>r.Id));
        ClearMapScene();World.RespawnInVillage(this,Config.StartRegionId);
        foreach(var hero in Heroes){hero.Hp=hero.MaxHp;hero.Timer=0;}
        Note("GM测试：当前章节全部内容已解锁并通关，无战斗奖励；完成城镇补给后继续推进");
    }
    /// <summary>
    /// 仅 QA/自动化：把本轮直接定位到指定章，并为之前的章节补空进度。
    /// 不发放任何奖励、不标记通关；正式跨章仍只走 AdvanceChapter。
    /// </summary>
    internal void QaPrepareChapterBoot(int chapter)
    {
        if(chapter<=0)return;
        if(chapter>=Config.ChapterMaps.Count)throw new ArgumentOutOfRangeException(nameof(chapter));
        for(int key=0;key<chapter;key++)completedMaps[key]=new WorldProgress();
        LatestMapIndex=chapter;DifficultyIndex=chapter/5;Config=Config.ChapterMaps[chapter%5];
    }

    /// <summary>只读调试状态；UI 必须通过 SetGmBattle 命令修改。</summary>
    public GmBattleSettings Gm { get; private set; }=new();

    /// <summary>调整当局战斗参数；倍率立即生效，数量只参与后续房间首次生成，不触发存档。</summary>
    public void SetGmBattle(GmBattleSettings settings)
    {
        if(settings==null||!float.IsFinite(settings.MonsterHp)||settings.MonsterHp<.5f||settings.MonsterHp>50||!float.IsFinite(settings.MonsterAttack)||settings.MonsterAttack<.5f||settings.MonsterAttack>50||settings.RoomCountBonus is < -999 or > 999)
            throw new ArgumentOutOfRangeException(nameof(settings),"GM 参数超出范围");
        Gm=settings;
    }

    /// <summary>仅进入地牢的边界调用；GM例外跳过战斗/出口，不伪造击杀或奖励。正常门禁、手动要塞授权和返村交易仍有效。</summary>
    private void ApplyGmEntryClear()
    {
        if(!Gm.InstantDungeonClear||World==null||World.Stage!=CampaignStage.Dungeon)return;
        Won=true;
        Note("GM测试：进入地牢瞬间通关（跳过战斗，无击杀及掉落奖励）");
        World.CompleteDungeon(this);
    }

    /// <summary>
    /// 仅 QA/自动化：直接进入指定 POI 的指定楼层 Boss 房，用于真实窗口下的阶段前后取证。
    /// 不开放为玩家入口（internal），不改门禁/城堡准入，也不发放任何资源。
    /// </summary>
    internal void QaEnterBossRoom(string poiId,int floorNumber=0)
    {
        var dungeon=Config.Dungeons.Single(d=>d.Id==poiId);
        if(World!=null)
        {
            // QA前置：只为让"目标在已解锁统治区"这一存档校验成立；不改 CanChallengeCastle 判定，也不发奖励。
            if(!World.Progress.CastleUnlocked(Config,dungeon.Id))
            {
                World.Progress.Cleared.UnionWith(Config.Dungeons.Where(d=>!d.Castle).Select(d=>d.Id));
                World.Progress.Explored.UnionWith(Config.Regions.Where(r=>r.Enterable).Select(r=>r.Id));
            }
            World.QaForceDungeon(this,dungeon);
        }
        else BeginDungeon(dungeon,1);
        if(Floors.Count==0)throw new InvalidOperationException("QA前置失败：未生成楼层");
        FloorIndex=Math.Clamp(floorNumber<=0?Floors.Count-1:floorNumber-1,0,Floors.Count-1);
        EnterFloor();
        var room=Floor.BossRoom;
        var door=room.Center+new Vector2(0,-room.HalfHeight);
        // 队伍按真实进房方向站在门内侧，敌群保留房内原有落点，避免把阶段取证摆成中心叠人。
        var inward=Vector2.Normalize(room.Center-door);
        var side=new Vector2(-inward.Y,inward.X);
        for(int i=0;i<Heroes.Count;i++)
            Heroes[i].Position=door+inward*(1.1f+i*.45f)+side*PartyFormation.SideOffset(i,Heroes.Count);
        foreach(var summon in Summons)summon.Position=door+inward*1.4f;
        CurrentRoom=room.Id;
        Reveal(room,door);
        CaptureRoomEntry();
        StartCombatEntry(room,door);
        Note($"QA：直接进入 {dungeon.Name} 第{FloorIndex+1}层 BOSS 房");
    }

    /// <summary>
    /// 仅 QA/自动化：走统一伤害管线清空本房已激活敌人，用于复现阶段序列。
    /// 不跳过击杀收据与奖励路径，因此账本与正常战斗一致。
    /// </summary>
    internal void QaKillRoomEnemies()
    {
        var room=Floor.Rooms[CurrentRoom];
        foreach(var monster in room.Monsters.Where(m=>m.Alive&&!m.BossPhaseDormant).ToArray())
            SettleDamage(Heroes[0],monster,"basic_player",100000,DamageType.Physical,false);
    }

    /// <summary>GM 发放已实现的金币、公共XP和累计击杀；击杀只推进既有解锁条件，不伪造战斗掉落。</summary>
    public void GrantGmResources(long gold=0,int experience=0,int kills=0)
    {
        if(gold<0||experience<0||experience>1000000||kills<0||kills>1000000)throw new ArgumentOutOfRangeException(nameof(experience));
        long total=checked(Gold+gold);int totalKills=checked(Kills+kills);
        long before=Gold;Gold=total;Kills=totalKills;if(gold>0)AuditCurrency("gm","grant","gold",before,Gold);
        if(experience>0)GainExperience(experience);
        if(gold>0||experience>0||kills>0){MarkChanged();Note($"GM：获得 {gold:N0} 金币 / {experience:N0} 公共XP / {kills:N0} 击杀");}
    }

}
