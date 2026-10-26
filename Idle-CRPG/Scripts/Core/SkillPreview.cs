using System;
using System.Linq;
using System.Numerics;

namespace IdleCrpg.Core;

public sealed partial class GameSession
{
    private bool skillPreviewMode;
    /// <summary>独立技能试场命令：绕过学习门槛但复用正式技能、索敌和伤害；调用方不得持久化此会话。</summary>
    public static GameSession CreateSkillPreview(Config config,string heroId,string skillId,int rank,string monsterId,int count,int monsterLevel)
    {
        var node=config.Talents.Nodes[skillId];
        if(node.Hero!=heroId||node.Kind!="active"||rank<1||rank>node.MaxRank||count is <1 or >40||monsterLevel<1||monsterLevel>config.MaxMonsterLevel)
            throw new ArgumentOutOfRangeException(nameof(rank),"试场职业、技能等级或怪物参数无效");
        var spec=config.Monsters.Single(m=>m.Id==monsterId);
        var game=CreateWorld(config,20261022,new[]{heroId});
        // 保留正常地牢阶段供原场景绘制读取；不使用玩家会话或改写共享配置。
        for(int i=0;i<20000&&game.World!.Stage!=CampaignStage.Dungeon;i++)game.Tick(.05f);
        if(game.World!.Stage!=CampaignStage.Dungeon)throw new InvalidOperationException("试场初始化未进入地牢阶段");
        var room=new Room {Id=0,HalfWidth=5,HalfHeight=5,Generated=true,Revealed=true,EventUsed=true,SnapshotLevel=monsterLevel};
        game.Floors=new(){new Floor {Rooms=new(){room}}};game.FloorIndex=0;game.CurrentRoom=0;game.destination=-1;game.Won=false;
        game.skillState=new();game.Summons.Clear();game.ClearVisualEvents();
        var hero=game.Heroes[0];hero.Talents.Clear();hero.TalentBonuses.Clear();hero.SkillSwapLocks.Clear();hero.ActiveAuraSkills.Clear();
        // 职业特性T的节点 id 与绑定光环技能 id 不同：试场必须施放 skill.csv 中的真实主动定义。
        string castId=node.SkillId!=""?node.SkillId:skillId;
        hero.Talents[skillId]=rank;hero.SkillIds=new[]{castId};hero.Position=new Vector2(-2,0);hero.Timer=0;
        // 治疗技能保留真实缺血条件；无敌仅用于延长观察，不替换技能命中规则。
        if(skillId=="pal_prayer")hero.Hp=hero.MaxHp*.35f;
        for(int i=0;i<count;i++)
        {
            var monster=config.Spawn.Create(spec,monsterLevel,false,new());config.ApplyCombatProfile(monster);
            monster.Position=new Vector2(1+(i%5)*.65f,((i/5)-(int)Math.Ceiling(count/5f)/2f)*.65f);
            room.Monsters.Add(monster);
        }
        game.SetGmBattle(new(Invincible:true));game.CaptureRoomEntry();game.Log.Clear();
        if(skillId=="nec_corpse_explosion")
        {
            // 沙盒明确提供施法前置，不制造正式击杀、奖励或玩家尸体状态。
            var corpse=room.Monsters[0].Copy();corpse.Position=new Vector2(1,0);corpse.Hp=0;
            room.Monsters.Add(corpse);game.RecordCorpse(corpse);
        }
        game.skillPreviewMode=true;return game;
    }

    /// <summary>试场固定步仅运行本房正式战斗；清房或全灭后停住，等待重播，不进入远征与交易。</summary>
    public void TickSkillPreview(float dt)
    {
        if(dt<=0||dt>.1f)throw new ArgumentOutOfRangeException(nameof(dt));
        Floor.ElapsedTicks++;
        var room=Floor.Rooms[CurrentRoom];
        if(room.Clear||!Heroes.Any(h=>h.Alive))return;
        AdvanceSwapLocks(dt);AdvanceRoomEntry(room,dt);Fight(room,dt);
    }
}
