using Godot;
using IdleCrpg.Core;
using System;
using System.Linq;

public partial class Main
{
    private Control? skillPreviewRoot;
    private GameSession? previewOriginal;
    private Config? previewConfig;
    private int previewView,previewSpeed;
    private bool previewOriginalPaused,previewPaused;
    private double previewAccumulator,previewOriginalAccumulator,previewReplayWait;
    private OptionButton previewHero=null!,previewSkill=null!,previewMonster=null!;
    private SpinBox previewRank=null!,previewCount=null!,previewMonsterLevel=null!;
    private CheckBox previewLoop=null!,previewInvincible=null!;
    private Label previewInfo=null!;
    private Button previewPause=null!;
    private TalentNode[] previewNodes=Array.Empty<TalentNode>();
    private bool PreviewOpen=>skillPreviewRoot?.Visible==true;

    /// <summary>GM 专属临时场景；原会话原样保留，所有试场动作与存档隔离。</summary>
    private void OpenSkillPreview()
    {
        if(PreviewOpen)return;
        SaveNow(true);previewOriginal=session;previewConfig=config;previewView=viewMode;previewSpeed=speed;
        previewOriginalPaused=paused;previewOriginalAccumulator=accumulator;
        if(skillPreviewRoot==null)BuildSkillPreview();
        gmRoot.Hide();settingsRoot.Hide();viewMode=0;speed=1;paused=false;previewPaused=false;
        skillPreviewRoot!.Show();SelectPreviewClass();
    }
    private void BuildSkillPreview()
    {
        skillPreviewRoot=new Control {Size=new Vector2(1440,900),Visible=false,MouseFilter=Control.MouseFilterEnum.Stop};uiLayer.AddChild(skillPreviewRoot);
        GmPanelBox(skillPreviewRoot,new Vector2(0,0),new Vector2(1440,86),"101f2d");
        HudText(skillPreviewRoot,"GM · 技能预览 / 独立战斗试场",new Vector2(28,20),24);
        var close=new Button {Text="退出试场",Position=new Vector2(1260,20),Size=new Vector2(150,40)};skillPreviewRoot.AddChild(close);close.Pressed+=CloseSkillPreview;
        var side=GmPanelBox(skillPreviewRoot,new Vector2(1080,92),new Vector2(336,784),"101f2d");
        OptionButton Choice(string title,int y)
        {
            HudText(side,title,new Vector2(16,y),16);var box=new OptionButton {Position=new Vector2(16,y+26),Size=new Vector2(304,36)};side.AddChild(box);return box;
        }
        SpinBox Number(string title,int y,int min,int max,int value)
        {
            HudText(side,title,new Vector2(16,y),16);var box=new SpinBox {Position=new Vector2(174,y-4),Size=new Vector2(146,36),MinValue=min,MaxValue=max,Step=1,Value=value};side.AddChild(box);return box;
        }
        previewHero=Choice("职业（全部开放）",16);foreach(var hero in config.Heroes)previewHero.AddItem(hero.Name);
        previewSkill=Choice("技能（当前职业全部主动）",92);
        previewRank=Number("技能等级",174,1,100,1);
        previewMonster=Choice("怪物种类",226);foreach(var monster in config.Monsters)previewMonster.AddItem(monster.Name);
        previewCount=Number("怪物数量",314,1,40,6);
        previewMonsterLevel=Number("怪物等级",366,1,config.MaxMonsterLevel,1);
        previewInvincible=new CheckBox {Text="测试英雄无敌",Position=new Vector2(16,420),ButtonPressed=true};side.AddChild(previewInvincible);
        previewLoop=new CheckBox {Text="清房后自动重播",Position=new Vector2(16,462),ButtonPressed=true};side.AddChild(previewLoop);
        Button Action(string text,int y,Action action)
        {var b=new Button {Text=text,Position=new Vector2(16,y),Size=new Vector2(304,38)};side.AddChild(b);b.Pressed+=action;return b;}
        previewPause=Action("暂停",520,()=>{previewPaused=!previewPaused;previewPause.Text=previewPaused?"继续":"暂停";});
        Action("步进 0.05 秒",568,()=>{previewPaused=true;previewPause.Text="继续";StepSkillPreview();});
        Action("应用参数 / 重新开战",616,RestartSkillPreview);
        HudText(side,"修改参数后点击应用。\n更换职业或技能立即重新开战。\n只装备当前技能，保留正常普攻。",new Vector2(16,678),14);
        var bottom=GmPanelBox(skillPreviewRoot,new Vector2(24,658),new Vector2(1040,218),"101f2d");
        previewInfo=HudText(bottom,"",new Vector2(20,16),17);previewInfo.Size=new Vector2(996,190);previewInfo.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        previewHero.ItemSelected+=_=>SelectPreviewClass();
        previewSkill.ItemSelected+=_=>{previewRank.MaxValue=previewNodes[previewSkill.Selected].MaxRank;RestartSkillPreview();};
    }
    private void SelectPreviewClass()
    {
        string hero=config.Heroes[previewHero.Selected].Id;
        previewNodes=config.Talents.Nodes.Values.Where(n=>n.Hero==hero&&n.Kind=="active").OrderBy(n=>n.Tree).ThenBy(n=>n.Row).ThenBy(n=>n.Col).ToArray();
        previewSkill.Clear();foreach(var node in previewNodes)previewSkill.AddItem(node.Name);
        previewSkill.Select(0);previewRank.MaxValue=previewNodes[0].MaxRank;RestartSkillPreview();
    }
    private void RestartSkillPreview()
    {
        var node=previewNodes[previewSkill.Selected];
        session=GameSession.CreateSkillPreview(config,config.Heroes[previewHero.Selected].Id,node.Id,(int)previewRank.Value,config.Monsters[previewMonster.Selected].Id,(int)previewCount.Value,(int)previewMonsterLevel.Value);
        session.SetGmBattle(new(Invincible:previewInvincible.ButtonPressed));
        previewAccumulator=0;previewReplayWait=0;cameraReady=false;motionAlpha=1;motion.BeforeTick(session);ResetSoundBaseline();
        UpdatePreviewInfo();
    }
    private void StepSkillPreview()
    {motion.BeforeTick(session!);session!.TickSkillPreview(.05f);motionAlpha=1;UpdatePreviewInfo();}
    private void TickSkillPreview(double delta)
    {
        if(!previewPaused)
        {
            previewAccumulator+=Math.Min(delta,.25);
            while(previewAccumulator>=.05){StepSkillPreview();previewAccumulator-=.05;}
            motionAlpha=(float)(previewAccumulator/.05);
            if(session!.Floor.Clear||!session.Heroes.Any(h=>h.Alive))
            {previewReplayWait+=delta;if(previewLoop.ButtonPressed&&previewReplayWait>=1.5)RestartSkillPreview();}
        }
    }
    private void UpdatePreviewInfo()
    {
        var node=previewNodes[previewSkill.Selected];var hero=session!.Heroes[0];
        previewInfo.Text=$"{hero.Name} · {node.Name} Lv.{session.TalentLevel(hero,node.Id)}\n{node.Description}\n"+
            $"技能数值 {session.TalentValue(hero,node):0.###}　冷却 {node.Cooldown:0.##} 秒　范围 {node.Range:0.##}\n"+
            $"存活怪物 {session.Floor.Rooms[0].Monsters.Count(m=>m.Alive)}　召唤物 {session.Summons.Count(s=>s.Alive)}　命中 {session.HitCount}\n"+
            "复用正式战斗视角、索敌、技能及伤害；升级仅展示配置实际支持的变化。\n"+
            (node.Id=="nec_corpse_explosion"?"试场提供一具可消费尸体。":"")+"独立沙盒不保存，不影响远征、资源或角色成长。";
    }
    private void CloseSkillPreview()
    {
        if(!PreviewOpen)return;
        skillPreviewRoot!.Hide();session=previewOriginal;config=previewConfig!;viewMode=previewView;speed=previewSpeed;
        paused=previewOriginalPaused;accumulator=previewOriginalAccumulator;previewOriginal=null;cameraReady=false;motionAlpha=1;
        if(session!=null)motion.BeforeTick(session);ResetSoundBaseline();Refresh();TickStarPanels();
    }

    private void RunSkillPreviewSmoke()
    {
        var original=session;long revision=session?.Revision??0;int oldView=viewMode;bool oldPaused=paused;
        gmButtons["SkillPreviewEntry"].EmitSignal(Button.SignalName.Pressed);
        if(!PreviewOpen||ReferenceEquals(original,session))throw new Exception("试场入口没有创建独立会话");
        for(int i=0;i<config.Heroes.Count;i++)
        {
            previewHero.Select(i);previewHero.EmitSignal(OptionButton.SignalName.ItemSelected,(long)i);
            foreach(int index in Enumerable.Range(0,previewNodes.Length))
            {
                previewSkill.Select(index);previewSkill.EmitSignal(OptionButton.SignalName.ItemSelected,(long)index);
                previewRank.Value=previewNodes[index].MaxRank;RestartSkillPreview();
                for(int tick=0;tick<40;tick++)StepSkillPreview();
            }
        }
        AutoSave(1);SaveNow();
        CloseSkillPreview();
        if(!ReferenceEquals(original,session)||session?.Revision!=revision||viewMode!=oldView||paused!=oldPaused)throw new Exception("退出试场未恢复原远征现场");
        GD.Print("SKILL_PREVIEW_UI_OK: GM entry, all classes/skills/ranks, save guard and original session restoration");
    }
}
