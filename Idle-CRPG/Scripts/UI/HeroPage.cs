using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;
public partial class Main
{
    private Control heroPage=null!;private AcceptDialog heroNotice=null!;
    private Label heroDetails=null!;
    private HBoxContainer heroSlots=null!;

    private string selectedHero="";
    private string heroStamp="";
    private void BuildHeroPage()
    {
        heroNotice=new AcceptDialog {Title="英雄席位"};uiLayer.AddChild(heroNotice);heroPage=HudPanel(uiLayer,new Vector2(24,92),new Vector2(1392,784));heroPage.Hide();
        heroSlots=new HBoxContainer {Position=new Vector2(16,16),Size=new Vector2(1360,90)};heroPage.AddChild(heroSlots);
        var left=HudPanel(heroPage,new Vector2(16,126),new Vector2(320,636));
        var scroll=new ScrollContainer {Position=new Vector2(12,12),Size=new Vector2(296,610)};left.AddChild(scroll);
        heroDetails=UiLabel("请选择英雄",17);scroll.AddChild(heroDetails);
        var tabs=new TabContainer {Position=new Vector2(352,126),Size=new Vector2(1024,636)};heroPage.AddChild(tabs);
        foreach(var name in new[]{"技能","装备"}){var content=new VBoxContainer {Name=name};tabs.AddChild(content);content.AddChild(UiLabel(name+"内容后续开放",18));}
        var manage=new Button {Text="调整编队",Position=new Vector2(1210,126),Size=new Vector2(166,30)};manage.Pressed+=()=>OpenFormation(0);heroPage.AddChild(manage);
    }
    private void TickHeroPage()
    {
        heroPage.Visible=viewMode==4;if(!heroPage.Visible)return;
        string stamp=session==null?"none":$"{session.Revision}:{session.Heroes.Count}:{selectedHero}:{session.Heroes.Sum(h=>h.Hp):0}";
        if(stamp!=heroStamp){heroStamp=stamp;RefreshHeroes();}
    }
    private void RefreshHeroes()
    {
        foreach(var c in heroSlots.GetChildren()){heroSlots.RemoveChild(c);c.QueueFree();}
        for(int i=0;i<5;i++)
        {
            var hero=session?.Heroes.ElementAtOrDefault(i);int slot=i;
            var b=new Button {Text=hero==null?(i<(session?.PartyCapacity??1)?"+\n席位空缺 · 选择英雄":"席位未解锁\n前往星图扩编"):$"{hero.Name}\nLv.{hero.Level}   HP {hero.Hp:0}/{hero.MaxHp:0}",CustomMinimumSize=new Vector2(264,90)};
            b.Pressed+=()=>{if(hero!=null){selectedHero=hero.SpecId;heroStamp="";}else OpenHeroPicker(slot);};heroSlots.AddChild(b);
        }
        var h=session?.Heroes.FirstOrDefault(h=>h.SpecId==selectedHero)??session?.Heroes.FirstOrDefault();
        heroDetails.Text=h==null?"英雄\n\n请先选择一位英雄开始游戏。":$"{h.Name}\n\n角色成长\n等级：{h.Level}\n共享经验：{session!.Experience}\n自动升级 · 上限 {config.MaxHeroLevel}\n\n基础属性\n生命：{h.Hp:0} / {h.MaxHp:0}\n攻击：{h.Attack:0}\n护甲：{h.Defense:0}\n移动速度：{h.Speed:0.##}\n攻击距离：{h.Range:0.##}\n普攻间隔：{h.Cooldown:0.##} 秒\n\n战斗属性\n闪避：{h.Battle.Dodge:P0}\n暴击：{h.Battle.CriticalChance:P0}\n格挡：{h.Battle.BlockChance:P0}\n\n队伍：{session.Heroes.Count}/{session.PartyCapacity}\n累计击杀：{session.Kills}\n\n天赋：后续开放";
    }
    private void OpenHeroPicker(int slot)
    {
        if(session!=null&&slot>=session.PartyCapacity){heroNotice.Title="英雄席位";heroNotice.DialogText="此席位尚未解锁，请在星图购买远征扩编节点。";heroNotice.PopupCentered();return;}
        OpenFormation(slot);
    }
    private void PaintCreationCamp()
    {
        painter.DrawSetTransform(-SceneRect.Position+CreationOffset);
        DrawString(font,new Vector2(360,180),"选择你的第一位英雄",HorizontalAlignment.Left,-1,28,new Color("e7ba71"));
        DrawString(font,new Vector2(312,220),"一人启程 · 在星图扩编 · 最多五人同行",HorizontalAlignment.Left,-1,18,new Color("91a6b7"));
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
