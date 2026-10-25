using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;
public partial class Main
{
    private Control heroPage=null!;private AcceptDialog heroNotice=null!;
    private Label heroDetails=null!;
    private HBoxContainer heroSlots=null!;
    private Button heroManageButton=null!;

    private string HeroExperienceText(Actor h)=>session==null?"—":h.Level>=config.MaxHeroLevel?"已满级":session.PublicExperience.ToString("N0")+" / "+session.ExperienceRequired(h);
    private string selectedHero="";
    private string heroStamp="";
    private void BuildHeroPage()
    {
        heroNotice=new AcceptDialog {Title="英雄席位"};uiLayer.AddChild(heroNotice);heroPage=HudPanel(uiLayer,new Vector2(24,92),new Vector2(1392,784));heroPage.Hide();
        heroSlots=new HBoxContainer {Position=new Vector2(16,16),Size=new Vector2(1216,90)};heroSlots.AddThemeConstantOverride("separation",8);heroPage.AddChild(heroSlots);
        // 保留按钮实例，战斗中的属性刷新不能中断按下到松开的点击过程。
        for(int i=0;i<5;i++)
        {
            int slot=i;
            var button=new Button {Name="HeroSlot"+i,CustomMinimumSize=new Vector2(0,90),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
            button.Pressed+=()=>
            {
                var hero=session?.Heroes.ElementAtOrDefault(slot);
                if(hero!=null){selectedHero=hero.HeroInstanceId;heroStamp="";}else OpenHeroPicker(slot);
            };
            heroSlots.AddChild(button);
        }
        heroManageButton=new Button {Name="HeroManageFormation",Text="调整编队",Position=new Vector2(1240,16),Size=new Vector2(136,90)};heroManageButton.Pressed+=()=>OpenFormation(0);heroPage.AddChild(heroManageButton);
        var left=HudPanel(heroPage,new Vector2(16,126),new Vector2(320,636));
        var scroll=new ScrollContainer {Position=new Vector2(12,12),Size=new Vector2(296,610)};left.AddChild(scroll);
        heroDetails=UiLabel("请选择英雄",17);scroll.AddChild(heroDetails);
        var tabs=new TabContainer {Position=new Vector2(352,126),Size=new Vector2(1024,636)};heroPage.AddChild(tabs);
        BuildTalentContent(tabs);
        BuildEquipmentContent(tabs);
    }
    private void TickHeroPage()
    {
        heroPage.Visible=viewMode==4;if(!heroPage.Visible)return;
        string stamp=session==null?"none":$"{session.Revision}:{session.Heroes.Count}:{selectedHero}:{session.Heroes.Sum(h=>h.Hp):0}";
        if(stamp!=heroStamp){heroStamp=stamp;RefreshHeroes();RefreshEquipmentPage();}
        RefreshTalentPage();
    }
    private VBoxContainer? equipmentSlotsBox,equipmentBagBox;
    private Label? equipmentHint;
    private Button? wearBestButton;
    /// <summary>装备页签：上半区为六部位槽位与详情，下半区为背包（已拾取候选）与"穿戴更好装备"。</summary>
    private void BuildEquipmentContent(TabContainer tabs)
    {
        var content=new VBoxContainer {Name="装备",SizeFlagsVertical=Control.SizeFlags.ExpandFill};
        tabs.AddChild(content);
        content.AddChild(UiLabel("已装备道具",18));
        equipmentSlotsBox=new VBoxContainer {Name="EquippedSlots"};content.AddChild(equipmentSlotsBox);
        equipmentHint=UiLabel("",12);content.AddChild(equipmentHint);
        var bagHeader=new HBoxContainer {Name="BagHeader"};
        bagHeader.AddChild(UiLabel("背包",18));
        var spacer=new Control {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};bagHeader.AddChild(spacer);
        wearBestButton=new Button {Name="WearBestEquipment",Text="穿戴更好装备",CustomMinimumSize=new Vector2(160,36)};bagHeader.AddChild(wearBestButton);
        wearBestButton.Pressed+=()=>{
            if(session==null)return;var hero=CurrentHero();if(hero==null)return;
            int applied=session.EquipBestFor(hero);
            equipmentHint!.Text=applied>0?$"已穿戴 {applied} 件更好的装备":$"{hero.Name} 没有可替换的装备";
            heroStamp="";Refresh();
        };
        content.AddChild(bagHeader);
        equipmentBagBox=new VBoxContainer {Name="BagItems",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        var scroll=new ScrollContainer {SizeFlagsVertical=Control.SizeFlags.ExpandFill,CustomMinimumSize=new Vector2(0,280)};
        scroll.AddChild(equipmentBagBox);content.AddChild(scroll);
        RefreshEquipmentPage();
    }
    private Actor? CurrentHero()=>session?.Heroes.FirstOrDefault(h=>h.HeroInstanceId==selectedHero)??session?.Heroes.FirstOrDefault();
    private void RefreshEquipmentPage()
    {
        if(equipmentSlotsBox==null||equipmentBagBox==null)return;
        foreach(var child in equipmentSlotsBox.GetChildren())child.QueueFree();
        foreach(var child in equipmentBagBox.GetChildren())child.QueueFree();
        var hero=CurrentHero();
        if(hero==null){equipmentHint!.Text="请先选择一位英雄。";return;}
        foreach(var baseSpec in config.EquipmentBases)
        {
            var worn=session!.WornEquipment(hero,baseSpec.Slot);
            string attr=worn?.Attr=="hp"?"HP":"ATK";
            equipmentSlotsBox.AddChild(UiLabel(worn==null?$"{baseSpec.Name}：空":$"{baseSpec.Name}：Lv.{worn.Level} {config.EquipmentQualityOf(worn.Quality).Name} · {attr}+{worn.Value:0.#}"+(worn.Attr=="hp"?$"（当前生命）":""),15));
        }
        int candidates=0;
        foreach(var baseSpec in config.EquipmentBases)
        {
            var candidate=session!.CandidateEquipment(hero,baseSpec.Slot);
            if(candidate==null)continue;
            candidates++;
            var row=new HBoxContainer {Name="BagRow_"+baseSpec.Slot};
            string attr=candidate.Attr=="atk"?"ATK":"HP";
            var label=UiLabel($"{baseSpec.Name} · Lv.{candidate.Level} {config.EquipmentQualityOf(candidate.Quality).Name} · {attr}+{candidate.Value:0.#}",14);
            label.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;row.AddChild(label);
            var equip=new Button {Name="BagEquip_"+baseSpec.Slot,Text="装备",CustomMinimumSize=new Vector2(84,32)};
            equip.Pressed+=()=>{if(session==null)return;session.EquipCandidate(hero.HeroInstanceId,baseSpec.Slot);heroStamp="";Refresh();};
            row.AddChild(equip);equipmentBagBox.AddChild(row);
        }
        if(candidates==0)equipmentBagBox.AddChild(UiLabel("背包为空：击杀怪物有概率获得装备。",13));
        int wornCount=config.EquipmentBases.Count(b=>session!.WornEquipment(hero,b.Slot)!=null);
        equipmentHint!.Text=$"{hero.Name}：已装备 {wornCount}/6 部位 · 背包候选 {candidates} 件";
    }
    private void RefreshHeroes()
    {
        for(int i=0;i<5;i++)
        {
            var hero=session?.Heroes.ElementAtOrDefault(i);
            bool locked=i>=(session?.PartyCapacity??1);
            var b=heroSlots.GetChild<Button>(i);
            b.Text=hero==null?(!locked?"本轮空席\n已锁定":"未来永久第五席\n尚未解锁"):$"{hero.Name}\nLv.{hero.Level}  XP {HeroExperienceText(hero)}\nHP {hero.Hp:0}/{hero.MaxHp:0}";
            b.Disabled=locked;b.TooltipText=locked?"未来永久席位尚未解锁。":hero==null?"本轮队伍锁定，设置中可放弃本轮重新选队。":"点击查看英雄";
        }
        heroManageButton.Disabled=session==null;heroManageButton.Text="本轮已锁队";heroManageButton.TooltipText="更换队伍：设置 → 放弃本轮并重新选队";
        var h=session?.Heroes.FirstOrDefault(h=>h.HeroInstanceId==selectedHero)??session?.Heroes.FirstOrDefault();
        heroDetails.Text=h==null?"英雄\n\n请先选择一位英雄开始游戏。":$"{h.Name}\n\n角色成长\n等级：{h.Level}\n公共XP / 升级费用：{HeroExperienceText(h)}\n右栏指定英雄升级 · 原型上限 {config.MaxHeroLevel}\n\n基础属性\n生命：{h.Hp:0} / {h.MaxHp:0}\n攻击：{session!.HeroTalentAttribute(h,"atk",h.Attack):0}\nAR：{session.AttackRating(h):0} / DR：{session.DefenseRating(h):0}\n移动速度：{h.Speed:0.##}\n攻击距离：{h.Range:0.##}\n普攻间隔：{h.Cooldown:0.##} 秒\n\n战斗属性\n闪避：{h.Battle.Dodge:P0}\n暴击：{Math.Clamp(session!.HeroTalentAttribute(h,"crit_rate",h.Battle.CriticalChance),0,1):P0}\n格挡：{h.Battle.BlockChance:P0}\n\n队伍：{session.Heroes.Count}/{session.PartyCapacity}\n累计击杀：{session.Kills}\n\n可用天赋点：{session.TalentPoints(h)}";
    }
    private void OpenHeroPicker(int slot)
    {
        if(session!=null&&slot>=session.PartyCapacity)return;
        OpenFormation(slot);
    }
    private void PaintCreationCamp()
    {
        painter.DrawSetTransform(-SceneRect.Position+CreationOffset);
        DrawString(font,new Vector2(360,180),"选择本轮 1–4 名英雄",HorizontalAlignment.Left,-1,28,new Color("e7ba71"));
        DrawString(font,new Vector2(312,220),"下方选择各职业人数 · 同职可重复 · 确认后本轮锁队",HorizontalAlignment.Left,-1,18,new Color("91a6b7"));
        float flicker=Mathf.Sin(Time.GetTicksMsec()*.009f);var fire=new Vector2(530,458);DrawCircle(fire,74+flicker*3,new Color(.8f,.35f,.1f,.12f));
        DrawLine(fire+new Vector2(-34,12),fire+new Vector2(34,-4),new Color("765335"),9,true);
        DrawLine(fire+new Vector2(-34,-4),fire+new Vector2(34,12),new Color("765335"),9,true);
        DrawColoredPolygon(new[]{fire+new Vector2(-24,0),fire+new Vector2(-12,-40),fire+new Vector2(flicker*4,-68-flicker*5),fire+new Vector2(25,0)},new Color("e76b32"));
        DrawColoredPolygon(new[]{fire+new Vector2(-10,0),fire+new Vector2(3,-39),fire+new Vector2(13,0)},new Color("ffd17c"));
        for(int i=0;i<choices.Length;i++)
        {
            var pos=new Vector2(165+i*182,i%2==0?370:305);bool selected=choices[i].ButtonPressed;
            var tint=choices[i].Disabled?new Color("46505c"):heroColors[i];
            if(selected)DrawArc(pos+new Vector2(0,8),29,0,Mathf.Tau,32,new Color("f6ce83"),3,true);
            painter.DrawSetTransform(-SceneRect.Position+CreationOffset-pos,0,Vector2.One*2);DrawFighter(pos,tint,i,false,1,"");painter.DrawSetTransform(-SceneRect.Position+CreationOffset);
            if(selected)DrawString(font,pos+new Vector2(-7,-65),"▼",HorizontalAlignment.Left,-1,24,new Color("f6ce83"));
        }
    }
}
