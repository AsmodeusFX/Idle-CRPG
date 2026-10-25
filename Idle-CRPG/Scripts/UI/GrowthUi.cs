using Godot;
using IdleCrpg.Core;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Main
{
    private readonly Dictionary<string,Button> upgradeCards=new();
    private readonly Dictionary<string,Button> equipCards=new();
    private Button? kpUnlock,kpRetire,equipBatch;
    private Label? growthBalance,growthFeedback,growthEmpty;
    private GameSession? growthSession;
    /// <summary>城堡卡从这里之后追加，保证"升级→装备→KP解锁→KP退休→城堡"次序稳定。</summary>
    private int growthTailIndex=6;

    /// <summary>
    /// 右栏成长交互：只显示可升级角色与可替换装备；装备≤4件逐件显示，≥5件合并为一个大按钮并列出最好5件。
    /// 每次交互后由 Refresh() → 本方法重排。
    /// </summary>
    private void RefreshGrowthInteractions()
    {
        if(!ReferenceEquals(growthSession,session))
        {
            foreach(var b in upgradeCards.Values){interactionList.RemoveChild(b);b.QueueFree();}upgradeCards.Clear();
            foreach(var b in equipCards.Values){interactionList.RemoveChild(b);b.QueueFree();}equipCards.Clear();
            growthSession=session;
        }
        if(growthBalance==null)
        {
            growthBalance=UiLabel("",14);growthBalance.Name="PublicExperienceBalance";growthBalance.AutowrapMode=TextServer.AutowrapMode.WordSmart;interactionList.AddChild(growthBalance);
            growthEmpty=UiLabel("",12);growthEmpty.Name="GrowthEmptyHint";growthEmpty.AutowrapMode=TextServer.AutowrapMode.WordSmart;interactionList.AddChild(growthEmpty);
            growthFeedback=UiLabel("",12);growthFeedback.AutowrapMode=TextServer.AutowrapMode.WordSmart;interactionList.AddChild(growthFeedback);
            kpUnlock=new Button {Name="BuyMonsterLevelMaximum",CustomMinimumSize=new Vector2(0,112)};interactionList.AddChild(kpUnlock);
            kpUnlock.Pressed+=()=>{if(session==null)return;session.BuyKpUpgrade(KpPurchase.MonsterLevelMaximum,out string reason);growthFeedback.Text=reason;Refresh();SaveNow();};
            kpRetire=new Button {Name="RetireMonsterLevelMinimum",CustomMinimumSize=new Vector2(0,86),Disabled=true};interactionList.AddChild(kpRetire);
            equipBatch=new Button {Name="BatchEquip",CustomMinimumSize=new Vector2(0,150)};
            equipBatch.Pressed+=()=>{if(session==null)return;int applied=session.EquipAllReplaceable();growthFeedback!.Text=applied>0?$"已穿戴 {applied} 件更好的装备":"没有可替换的装备";Refresh();SaveNow();};
            interactionList.AddChild(equipBatch);
        }
        growthBalance.Text=session==null?"":$"公共XP {session.PublicExperience:N0}  ·  怪级 [{session.MonsterLevelMin},{session.MonsterLevelMax}]";
        interactionList.MoveChild(growthBalance,0);int index=1;

        // 1) 英雄升级：只显示现在就能升级的角色；卡面为 图标+名字+消耗XP+等级/AR/DR。
        int affordable=0;
        if(session!=null)foreach(var hero in session.Heroes)
        {
            string id=hero.HeroInstanceId;
            if(!upgradeCards.TryGetValue(id,out var b))
            {
                b=new Button {Name="UpgradeHero_"+session.Heroes.IndexOf(hero),CustomMinimumSize=new Vector2(0,86)};
                b.AddChild(new HudIcon {Kind="hero",Position=new Vector2(6,8),Size=new Vector2(30,30),MouseFilter=Control.MouseFilterEnum.Ignore,Accent=heroColors[session.Heroes.IndexOf(hero)%heroColors.Length]});
                upgradeCards[id]=b;interactionList.AddChild(b);
                b.Pressed+=()=>{if(session==null)return;session.UpgradeHero(id,out string reason);growthFeedback!.Text=reason;Refresh();SaveNow();};
            }
            bool can=session.CanUpgradeHero(hero);
            b.Visible=can;
            if(!can)continue;
            int cost=session.ExperienceRequired(hero);
            b.Text=$"        {hero.Name} #{session.Heroes.IndexOf(hero)+1}          {cost} XP\n等级 {hero.Level} · AR {session.AttackRating(hero):0} · DR {session.DefenseRating(hero):0}\n点击升级（消耗公共XP）";
            b.TooltipText=id;interactionList.MoveChild(b,index++);affordable++;
        }

        // 2) 装备：收集"可替换"条目（空位或同属性更高）。
        var replaceable=new List<(Actor Hero,EquipmentBase Spec,EquipmentItem Candidate)>();
        if(session!=null)foreach(var hero in session.Heroes)foreach(var spec in config.EquipmentBases)
        {
            var candidate=session.CandidateEquipment(hero,spec.Slot);
            if(candidate!=null&&session.HasReplaceable(hero,spec.Slot))replaceable.Add((hero,spec,candidate));
        }
        // 先清理不再可替换的逐件卡。
        foreach(var key in equipCards.Keys.ToArray())
        {
            bool keep=replaceable.Count<=4&&replaceable.Any(r=>r.Hero.HeroInstanceId+"|"+r.Spec.Slot==key);
            if(!keep){interactionList.RemoveChild(equipCards[key]);equipCards[key].QueueFree();equipCards.Remove(key);}
        }
        if(replaceable.Count<=4)
        {
            // ≤4 件：逐件显示（装备图标、名字、品质、部位与数值），点击即穿戴该件。
            // 隐藏时仍固定占位，保证"装备在 KP 之前"的次序可断言且不漂移。
            if(equipBatch!=null){equipBatch.Visible=false;interactionList.MoveChild(equipBatch,index++);}
            foreach(var (hero,spec,candidate) in replaceable)
            {
                string key=hero.HeroInstanceId+"|"+spec.Slot;
                if(!equipCards.TryGetValue(key,out var card))
                {
                    string heroId=hero.HeroInstanceId;string slot=spec.Slot;
                    card=new Button {Name="Equip_"+spec.Slot,CustomMinimumSize=new Vector2(0,74)};
                    card.AddChild(new HudIcon {Kind="weapon",Position=new Vector2(6,8),Size=new Vector2(26,26),MouseFilter=Control.MouseFilterEnum.Ignore,Accent=new Color(QualityColor(candidate.Quality))});
                    equipCards[key]=card;interactionList.AddChild(card);
                    card.Pressed+=()=>{if(session==null)return;session.EquipCandidate(heroId,slot);growthFeedback!.Text="已更换装备";Refresh();SaveNow();};
                }
                var worn=session!.WornEquipment(hero,spec.Slot);
                string attr=candidate.Attr=="atk"?"ATK":"HP";
                card.Text=$"        {spec.Name}·{config.EquipmentQualityOf(candidate.Quality).Name}  Lv.{candidate.Level}\n        {attr}+{candidate.Value:0.#}"+(worn==null?"（空位）":$" · 当前 {worn.Value:0.#}");
                card.Visible=true;card.TooltipText="点击穿戴该件装备";
                interactionList.MoveChild(card,index++);
            }
        }
        else
        {
            // >4 件：合并为一个大按钮，并列出最好的 5 件（图标、名字、品质）。
            if(equipBatch!=null)
            {
                var top=replaceable.OrderByDescending(r=>r.Candidate.Value).Take(5).ToArray();
                string lines=string.Join('\n',top.Select(r=>$"   ◆ {r.Spec.Name}·{config.EquipmentQualityOf(r.Candidate.Quality).Name}   Lv.{r.Candidate.Level}"));
                equipBatch.Text=$"装备所有道具升级（{replaceable.Count} 件，只替换明确更优）\n{lines}";
                equipBatch.Visible=true;interactionList.MoveChild(equipBatch,index++);
            }
        }

        // 3) KP：解锁怪物等级（可购买）与退休怪物等级（对玩家关闭）。
        if(session!=null)
        {
            int next=Math.Min(session.MonsterLevelMax+1,config.MaxMonsterLevel);
            int required=(int)Math.Ceiling(next*config.Progression.MonsterRatingPerLevel);
            session.CanBuyLmax(out string reason);
            kpUnlock!.Text=$"解锁怪物等级 {next}\n费用 KP {config.Progression.LmaxCost} · 最低角色等级需求 {required}\n评定：{Band(next)}\n{MonsterStats(next)}\n"+(reason==""?"点击购买":reason);
            kpUnlock.TooltipText=reason;
            int retire=session.MonsterLevelMin-1;
            kpRetire!.Text=retire>=1?$"退休怪物等级 {retire}\n未通过安全验收，需独立裁决后方可开放":$"退休怪物等级 —\n当前已是最低等级 {session.MonsterLevelMin}；该功能对玩家关闭";
            kpRetire.TooltipText="Lmin 永久提升/退休需另行批准，本阶段不开放";
        }
        kpUnlock!.Visible=session!=null;kpRetire!.Visible=session!=null;
        interactionList.MoveChild(kpUnlock,index++);interactionList.MoveChild(kpRetire,index++);

        // 4) 空态与反馈；城堡卡从 growthTailIndex 之后追加。
        growthEmpty!.Visible=session!=null&&affordable==0&&replaceable.Count==0;
        growthEmpty.Text=affordable==0&&replaceable.Count==0?"暂无英雄升级或装备可替换：积攒XP/击杀掉落后再来。":"";
        interactionList.MoveChild(growthEmpty!,index++);
        interactionList.MoveChild(growthFeedback!,index);
        growthTailIndex=index+1;
    }
    private string Band(int level)=>level switch{<=3=>"小菜一碟",<=6=>"略有挑战",<=10=>"势均力敌",<=15=>"危险",_=>"致命"};
    private string MonsterStats(int level)
    {
        var definition=config.MonsterLevels.GetValueOrDefault(level);var baseMonster=config.Monsters[0];
        if(definition==null)return"怪物数值：配置缺失";
        return $"伤害 {baseMonster.Attack*definition.AttackMultiplier:0}  攻击 {baseMonster.Attack*definition.AttackMultiplier:0}\n护甲 {baseMonster.Defense*definition.AttackMultiplier:0}  生命 {baseMonster.Hp*definition.HpMultiplier:0}";
    }
    private static string QualityColor(string quality)=>quality switch{"legendary"=>"ff8a3d","rare"=>"6bb6ff","uncommon"=>"6ee07a",_=>"d8d8d8"};
}
