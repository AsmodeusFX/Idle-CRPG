using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleCrpg.Core;

public enum KpPurchase { MonsterLevelMaximum,MonsterLevelMinimum }
public sealed partial class GameSession
{
    public long Kp {get;private set;}
    public long KpEarned {get;private set;}
    public long KpSpent {get;private set;}
    /// <summary>本轮怪物等级下限。玩家入口恒为 1（Lmin 未通过安全验收）；只有测试程序集的隔离前置可构造极端区间，
    /// 且该现场不可写入正式存档——读档校验会拒绝 MonsterLevelMin ≠ 1。</summary>
    public int MonsterLevelMin {get;private set;}=1;
    public int MonsterLevelMax {get;private set;}=1;
    private readonly HashSet<string> kpKillReceipts=new();
    private long dungeonEntrySerial;
    /// <summary>击杀收据跨检查点重开保留，同一出生实例只发一次KP；XP仍沿已批准的房间重开规则发放。</summary>
    private void GainKillKp(Actor monster)
    {
        string key=$"{RunId}:{dungeonEntrySerial}:{DifficultyIndex}:{ChapterIndex}:{dungeonSeed}:{FloorIndex}:{CurrentRoom}:{Floor.Rooms[CurrentRoom].Monsters.IndexOf(monster)}";
        if(kpKillReceipts.Contains(key))return;
        long next=checked(Kp+Config.Progression.KillKp),earned=checked(KpEarned+Config.Progression.KillKp);
        long before=Kp;kpKillReceipts.Add(key);Kp=next;KpEarned=earned;AuditCurrency("monster_kill","receive","kp",before,Kp,origin:key);MarkChanged();
    }
    public bool CanBuyLmax(out string reason)
    {
        if(MonsterLevelMax>=Config.MaxMonsterLevel){reason="已达原型怪级表上限";return false;}
        if(Kp<Config.Progression.LmaxCost){reason=$"KP不足：需要{Config.Progression.LmaxCost}，当前{Kp}";return false;}
        double required=(MonsterLevelMax+1)*Config.Progression.MonsterRatingPerLevel;
        foreach(var hero in Heroes)
        {
            if(AttackRating(hero)<required){reason=$"{hero.Name} #{Heroes.IndexOf(hero)+1} AR不足：{AttackRating(hero):0.##}/{required:0.##}";return false;}
            if(DefenseRating(hero)<required){reason=$"{hero.Name} #{Heroes.IndexOf(hero)+1} DR不足：{DefenseRating(hero):0.##}/{required:0.##}";return false;}
        }
        reason="";return true;
    }
    /// <summary>一次交易仅购买Lmax+1；Lmin在Core公开命令层拒绝，不以隐藏按钮代替禁用。</summary>
    public bool BuyKpUpgrade(KpPurchase purchase,out string reason)
    {
        if(purchase!=KpPurchase.MonsterLevelMaximum){reason="Lmin购买未通过安全验收，禁止开放";return false;}
        if(!CanBuyLmax(out reason))return false;
        long spent=checked(KpSpent+Config.Progression.LmaxCost);
        long before=Kp;Kp-=Config.Progression.LmaxCost;KpSpent=spent;MonsterLevelMax++;AuditCurrency("player_command","buy_lmax","kp",before,Kp);
        World?.SyncMonsterLevelRange(this);
        Note($"KP购买 Lmax → {MonsterLevelMax}，支出{Config.Progression.LmaxCost}，余额{Kp}；已生成房间保持快照");reason="购买成功";return true;
    }
    public int CastleRequiredMonsterLevel(string id)=>Config.PoiProfiles.ContainsKey(id)?Config.Challenge(id,DifficultyIndex).Challenge:Config.Dungeons.Single(d=>d.Id==id).LevelMin;
    public bool CanChallengeCastle(string id,out string reason)
    {
        if(World==null||!Config.Dungeons.Any(d=>d.Id==id&&d.Castle)){reason="城堡不存在";return false;}
        int required=CastleRequiredMonsterLevel(id);
        if(MonsterLevelMax<required){reason=$"需要Lmax≥{required}，当前{MonsterLevelMax}";return false;}
        if(!World.Progress.CastleUnlocked(Config,id)){reason="章节／地图前置未完成";return false;}
        reason="";return true;
    }
    private static void ValidateKpState(Config config,SessionSnapshot s)
    {
        if(s.MonsterLevelMin!=1||s.MonsterLevelMax<1||s.MonsterLevelMax>config.MaxMonsterLevel||s.Kp<0||s.KpEarned<0||s.KpSpent<0||s.KpEarned-s.KpSpent!=s.Kp||s.KpKillReceipts==null||s.KpKillReceipts.Any(string.IsNullOrWhiteSpace)||s.KpKillReceipts.Distinct().Count()!=s.KpKillReceipts.Count)throw new InvalidDataException("KP账本或禁止修改的Lmin无效");
    }
}
