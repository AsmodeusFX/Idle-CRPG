using Godot;
using System;

/// <summary>学习、装配和编队共用拖拽预览；数据只标识意图，目标重新校验后才修改草稿或提交命令。</summary>
public partial class InteractionButton : Button
{
    public Func<Variant>? DragData;
    public Func<Variant,bool>? AcceptDrop;
    public Action<Variant>? ReceiveDrop;
    public Func<string>? Details;
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if(DragData==null)return default;
        var data=DragData();if(data.VariantType==Variant.Type.Nil)return default;
        SetDragPreview(CreateInformationPreview());return data;
    }
    public Control CreateInformationPreview()
    {var card=(InteractionInfoCard)InformationCard(Details?.Invoke()??TooltipText);card.FollowCursor=true;return card;}
    public override bool _CanDropData(Vector2 atPosition,Variant data)=>!Disabled&&AcceptDrop?.Invoke(data)==true;
    public override void _DropData(Vector2 atPosition,Variant data)
    {if(_CanDropData(atPosition,data))ReceiveDrop?.Invoke(data);}
    public override GodotObject _MakeCustomTooltip(string forText)=>InformationCard(Details?.Invoke()??forText);
    private static Control InformationCard(string text)
    {
        var card=new InteractionInfoCard {MouseFilter=MouseFilterEnum.Ignore,CustomMinimumSize=new Vector2(370,0)};
        card.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("14202d"),BorderColor=new Color("b79a63"),BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,ContentMarginLeft=14,ContentMarginRight=14,ContentMarginTop=12,ContentMarginBottom=12});
        var label=new Label {Text=text,CustomMinimumSize=new Vector2(342,0),AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=MouseFilterEnum.Ignore};label.AddThemeFontSizeOverride("font_size",14);card.AddChild(label);return card;
    }
    public static Variant Payload(string kind,string owner,string id)=>new Godot.Collections.Dictionary {{"kind",kind},{"owner",owner},{"id",id}};
    public static bool ReadPayload(Variant data,string kind,string owner,out string id)
    {
        id="";if(data.VariantType!=Variant.Type.Dictionary)return false;var d=data.AsGodotDictionary();
        if(!d.TryGetValue("kind",out var k)||k.VariantType!=Variant.Type.String||k.AsString()!=kind||!d.TryGetValue("owner",out var o)||o.VariantType!=Variant.Type.String||o.AsString()!=owner||!d.TryGetValue("id",out var value)||value.VariantType!=Variant.Type.String)return false;
        id=value.AsString();return id!="";
    }
}

/// <summary>拖动说明随鼠标移动并保持在视口内，普通悬浮说明仍由Godot安排位置。</summary>
public partial class InteractionInfoCard : PanelContainer
{
    public bool FollowCursor;
    public override void _Process(double delta)
    {
        if(!FollowCursor)return;
        var mouse=GetViewport().GetMousePosition();var bounds=GetViewportRect().Size;
        GlobalPosition=new Vector2(Math.Clamp(mouse.X+16,8,Math.Max(8,bounds.X-Size.X-8)),Math.Clamp(mouse.Y+18,8,Math.Max(8,bounds.Y-Size.Y-8)));
    }
}

/// <summary>保留滚轮和滚动条；左键在空白处拖动、中键在整个列表拖动均可浏览，不触发学习或装配。</summary>
public partial class DragScrollContainer : ScrollContainer
{
    private bool blankDrag,middleDrag;
    private Vector2 last;
    public override void _GuiInput(InputEvent e)
    {
        if(e is InputEventMouseButton b&&b.ButtonIndex==MouseButton.Left){blankDrag=b.Pressed;last=b.Position;}
        if(e is InputEventMouseMotion m&&blankDrag&&(m.ButtonMask&MouseButtonMask.Left)!=0)
        {ScrollVertical-=(int)m.Relative.Y;ScrollHorizontal-=(int)m.Relative.X;AcceptEvent();}
    }
    public override void _Input(InputEvent e)
    {
        if(!IsVisibleInTree()){blankDrag=middleDrag=false;return;}
        if(e is InputEventMouseButton b)
        {
            if(b.ButtonIndex==MouseButton.Left&&!b.Pressed)blankDrag=false;
            if(b.ButtonIndex==MouseButton.Middle){var hovered=GetViewport().GuiGetHoveredControl();middleDrag=b.Pressed&&hovered!=null&&(hovered==this||IsAncestorOf(hovered));last=GetGlobalMousePosition();if(middleDrag)GetViewport().SetInputAsHandled();}
        }
        if(e is InputEventMouseMotion&&middleDrag)
        {var now=GetGlobalMousePosition();ScrollVertical-=(int)(now.Y-last.Y);ScrollHorizontal-=(int)(now.X-last.X);last=now;GetViewport().SetInputAsHandled();}
    }
}
