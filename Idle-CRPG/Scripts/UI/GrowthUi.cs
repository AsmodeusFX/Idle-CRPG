using Godot;
using IdleCrpg.Core;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Main
{
    private readonly Dictionary<string,Button> upgradeCards=new();
    private Button? kpPurchase;
    private Label? growthBalance,growthFeedback;
    private GameSession? growthSession;
    /// <summary>右栏实例升级置顶，逐笔Lmax购买独立卡；点击只调用GameSession命令。</summary>
    private void RefreshGrowthInteractions()
    {
        if(!ReferenceEquals(growthSession,session))
        {
            foreach(var b in upgradeCards.Values){interactionList.RemoveChild(b);b.QueueFree();}upgradeCards.Clear();growthSession=session;
        }
        if(growthBalance==null)
        {
            growthBalance=UiLabel("",14);growthBalance.Name="PublicExperienceBalance";growthBalance.AutowrapMode=TextServer.AutowrapMode.WordSmart;interactionList.AddChild(growthBalance);
            growthFeedback=UiLabel("",12);growthFeedback.AutowrapMode=TextServer.AutowrapMode.WordSmart;interactionList.AddChild(growthFeedback);
            kpPurchase=new Button {Name="BuyMonsterLevelMaximum",CustomMinimumSize=new Vector2(0,90)};interactionList.AddChild(kpPurchase);
            kpPurchase.Pressed+=()=>{if(session==null)return;session.BuyKpUpgrade(KpPurchase.MonsterLevelMaximum,out string reason);growthFeedback.Text=reason;Refresh();SaveNow();};
        }
        growthBalance.Text=session==null?"":$"公共XP {session.PublicExperience:N0}  ·  怪级 [{session.MonsterLevelMin},{session.MonsterLevelMax}]";
        interactionList.MoveChild(growthBalance,0);int index=1;
        if(session!=null)foreach(var hero in session.Heroes)
        {
            string id=hero.HeroInstanceId;
            if(!upgradeCards.TryGetValue(id,out var b))
            {
                b=new Button {Name="UpgradeHero_"+session.Heroes.IndexOf(hero),CustomMinimumSize=new Vector2(0,74)};upgradeCards[id]=b;interactionList.AddChild(b);
                b.Pressed+=()=>{if(session==null)return;session.UpgradeHero(id,out string reason);growthFeedback!.Text=reason;Refresh();SaveNow();};
            }
            int cost=session.ExperienceRequired(hero);
            b.Text=$"{hero.Name} #{session.Heroes.IndexOf(hero)+1}  Lv.{hero.Level} → {hero.Level+1}\n"+(cost==0?"已达原型表上限":$"升级费用 XP {cost} · {(session.PublicExperience>=cost?"可升级":"XP不足")}")+ $"\nAR {session.AttackRating(hero):0.#}  DR {session.DefenseRating(hero):0.#}";
            b.Disabled=cost==0;b.TooltipText=id;interactionList.MoveChild(b,index++);
        }
        kpPurchase!.Visible=session!=null;
        if(session!=null){session.CanBuyLmax(out string reason);kpPurchase.Text=$"KP购买 Lmax {session.MonsterLevelMax} → {session.MonsterLevelMax+1}\n费用 KP {config.Progression.LmaxCost}\n"+(reason==""?"全员评级达标 · 点击购买":reason);kpPurchase.TooltipText=reason;}
        interactionList.MoveChild(kpPurchase,index++);
        RefreshEquipmentCards(index);
        interactionList.MoveChild(growthFeedback!,index);
    }
    private readonly System.Collections.Generic.Dictionary<string,Button> equipCards=new();
    private Button? batchEquipButton;
    /// <summary>
    /// 右侧装备卡（WP5）：逐部位只能玩家点击替换；达到5个不同英雄×部位可明确提升时，出现一次批量换装按钮。
    /// </summary>
    private void RefreshEquipmentCards(int index)
    {
        if(session==null){if(batchEquipButton!=null)batchEquipButton.Visible=false;return;}
        foreach(var hero in session.Heroes)
        {
            foreach(var baseSpec in config.EquipmentBases)
            {
                var candidate=session.CandidateEquipment(hero,baseSpec.Slot);
                string key=hero.HeroInstanceId+"|"+baseSpec.Slot;
                if(candidate==null)
                {
                    if(equipCards.TryGetValue(key,out var stale)){stale.Visible=false;}
                    continue;
                }
                if(!equipCards.TryGetValue(key,out var button))
                {
                    string heroId=hero.HeroInstanceId;string slot=baseSpec.Slot;
                    button=new Button {Name="Equip_"+slot,CustomMinimumSize=new Vector2(0,56)};equipCards[key]=button;interactionList.AddChild(button);
                    button.Pressed+=()=>{if(session==null)return;session.EquipCandidate(heroId,slot);growthFeedback!.Text="已更换装备";Refresh();SaveNow();};
                }
                var worn=session.WornEquipment(hero,baseSpec.Slot);
                string attr=candidate.Attr=="atk"?"ATK":"HP";
                button.Text=$"{hero.Name} #{session.Heroes.IndexOf(hero)+1} {baseSpec.Name}\n候选 Lv.{candidate.Level} {config.EquipmentQualityOf(candidate.Quality).Name} {attr}+{candidate.Value:0.#}"+(worn==null?"（空位）":$" · 当前 {worn.Value:0.#}");
                button.Visible=true;button.TooltipText="点击穿戴该候选；跨属性由你决定";
                interactionList.MoveChild(button,index++);
            }
        }
        int ready=session.BatchEquipReadyCount();
        if(batchEquipButton==null)
        {
            batchEquipButton=new Button {Name="BatchEquip" ,CustomMinimumSize=new Vector2(0,64)};
            batchEquipButton.Pressed+=()=>{if(session==null)return;int applied=session.BatchEquipCandidates();growthFeedback!.Text=applied>0?$"已批量换装 {applied} 件":$"需至少5件可明确提升（当前{session.BatchEquipReadyCount()}件）";Refresh();SaveNow();};
            interactionList.AddChild(batchEquipButton);
        }
        batchEquipButton.Visible=ready>0;
        batchEquipButton.Disabled=ready<5;
        batchEquipButton.Text=ready>=5?$"一键换装（{ready} 件可明确提升）":$"一键换装需≥5件（当前{ready}件）";
        interactionList.MoveChild(batchEquipButton,index++);
    }
}
