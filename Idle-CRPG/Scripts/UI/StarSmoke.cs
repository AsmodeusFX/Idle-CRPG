using Godot;
using IdleCrpg.Core;
using System;
using System.IO;
using System.Linq;

public partial class Main
{
    /// <summary>实际画布购买、GM 入口及编辑器按钮检查；只写 QA 配置副本，绝不修改源表。</summary>
    private void RunStarSmoke()
    {
        var original=session;var definition=config.Stars;
        try
        {
            void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("STAR_UI_SMOKE: "+why);}
            FinishProgressWrite();
            var snapshot=SaveStore.Decode(config,SaveStore.Encode(new SaveDocument {Session=session!.Capture()})).Session!;
            snapshot.Gold=100;session=GameSession.Restore(config,snapshot);SelectStarView(3);
            float attack=session.Heroes[0].Attack;var point=_starCanvas.Pan+StarCanvas.Center(session.Stars.Configuration.Layout["s_root"])*_starCanvas.Zoom;
            _starCanvas._GuiInput(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=true,Position=point});
            _starCanvas._GuiInput(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=false,Position=point});
            Check(session.Gold==97&&session.Heroes[0].Attack==attack+1&&session.Stars.Level("s_root")==1,"实际画布购买未生效");
            var pan=_starCanvas.Pan;var zoom=_starCanvas.Zoom;BuyStarNode("s_hp");Check(_starCanvas.Pan==pan&&_starCanvas.Zoom==zoom,"购买重置了视图");
            starEditorConfigRoot=Path.Combine(Path.GetDirectoryName(saveStore.PathName)!,"editor-config");Directory.CreateDirectory(Path.Combine(starEditorConfigRoot,"star"));
            string source=ProjectSettings.GlobalizePath("res://config/star");
            foreach(var file in new[]{"star.csv","star_layout.csv","star_plan.csv"})File.Copy(Path.Combine(source,file),Path.Combine(starEditorConfigRoot,"star",file),true);
            uiLayer.GetNode<Button>("GmMenuButton").EmitSignal(BaseButton.SignalName.Pressed);Check(gmRoot.Visible,"GM 入口未打开");
            gmRoot.FindChild("StarEditorEntry",true,false).EmitSignal(BaseButton.SignalName.Pressed);Check(_editingStars&&!gmRoot.Visible,"编辑器入口未打开");
            EditorClickCell(4,2);string id=_starSelected;Check(_starDraft.Dirty,"创建未进入草稿");
            SelectStarView(0);Check(viewMode==3,"未保存草稿被静默丢弃");
            _tnName.Text="UI 验证星辰";_tnMax.Text="1";_tnCost.Text="1";_tnValue.Text="1";_tnParents.Text="s_root";_tnAttribute.Select(2);_tnIntent.Text="验证编辑器保存功能";
            starPage.FindChild("StarEditorApply",true,false).EmitSignal(BaseButton.SignalName.Pressed);
            starPage.FindChild("StarEditorSave",true,false).EmitSignal(BaseButton.SignalName.Pressed);
            Check(!_starDraft.Dirty&&session.Stars.Configuration.Nodes.ContainsKey(id)&&new StarEditorStore(starEditorConfigRoot).Load().Nodes.ContainsKey(id),"字段或三表没有保存并刷新会话");
            Check(!File.ReadAllText(Path.Combine(source,"star.csv")).Contains(id),"检查修改了源表");
            ToggleStarEditor();Check(!_editingStars,"编辑器未返回玩家页");
            GD.Print("STAR_UI_SMOKE_OK: canvas purchase, ATK/HP, GM editor, draft guard, fields, three-table save, session refresh");
        }
        catch(Exception e){GD.PushError(e.ToString());QuitSafely(1);}
        finally
        {
            FinishProgressWrite();session=original;config.Stars=definition;starEditorConfigRoot=null;_editingStars=false;viewMode=0;TickStarPanels();SaveNow(true);
        }
    }
}
