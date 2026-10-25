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
        interactionList.MoveChild(kpPurchase,index++);interactionList.MoveChild(growthFeedback!,index);
    }
}
