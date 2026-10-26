using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using Godot;


using IdleCrpg.Core;




public partial class Main
{
    private bool _editingStars;
    private StarEditorStore _starStore=null!;
    private StarEditorDocument _starDraft=null!;
    private string _starSelected = "", _starMode = "选择", _starMessage = "";
    private Vector2 _starPan = new(100, 75), _editorPan = new(100, 45);
    private float _starZoom = 1, _editorZoom = 1;
    private StarCanvas _starCanvas=null!;
    private Label _starHint=null!, _starGold=null!;
    private long _starDisplayedGold = -1;
    private string _starHovered="";
    private LineEdit _tnName=null!, _tnLabel=null!, _tnMax=null!, _tnCost=null!, _tnValue=null!, _tnIcon=null!, _tnParents=null!;
    private TextEdit _tnIntent=null!;
    private OptionButton _tnAttribute=null!, _tnOperation=null!;

    private string StarEffect(StarNode n, int level)
    {
        if(n.Attribute=="class_trait_unlock")return $"购买后本轮同职 {session?.Heroes.Count(h=>h.SpecId==n.TargetId)??0} 人可分别学习T（不自动投点）";
        if(n.Attribute=="xp_gain_pct")return "公共XP获取 +"+(n.ValuePerLevel*level*100).ToString("0.##")+"%";
        if(n.Attribute=="skill_slots")return "全队主动技能槽 +"+(n.ValuePerLevel*level);
        if(n.Attribute=="talent_reset")return "解锁城镇洗点";
        if(n.Attribute=="talent_reset_discount")return "洗点费用降低 "+(n.ValuePerLevel*level*100).ToString("0")+"%";
        if(n.Attribute=="party_slots")return "上阵人数 +"+(n.ValuePerLevel*level);if(n.Attribute=="")return "尚未配置效果";
        return (n.Attribute=="attack"?"全队攻击":"全队生命上限")+" +"+(n.ValuePerLevel*level).ToString("0.##",CultureInfo.InvariantCulture);
    }
    private void BuildStarUi()
    {
        if(session==null&&!_editingStars){starPage.AddChild(UiLabel("星图 · 请先选择小队并开始远征，再使用金币点亮星辰。",20));return;}
        var tools = new HBoxContainer(); starPage.AddChild(tools);
        _starGold = UiLabel(GoldText(), 16); tools.AddChild(_starGold);
        UiButton(tools, "重置视图", () => _starCanvas.ResetView());
        if (_editingStars) UiButton(tools, "关闭编辑器 / 返回玩家星图", ToggleStarEditor).Name = "StarEditorReturn";
        UiButton(tools, "关闭星图 · 返回", () => SelectStarView(0));
        if (_editingStars) BuildStarEditorToolbar();
        var row = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; starPage.AddChild(row);
        _starCanvas = new StarCanvas { Name = "StarMapCanvas", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new(0, 220),
            Pan = _editingStars ? _editorPan : _starPan, Zoom = _editingStars ? _editorZoom : _starZoom };
        _starCanvas.ViewChanged = (pan, zoom) => { if (_editingStars) { _editorPan = pan; _editorZoom = zoom; } else { _starPan = pan; _starZoom = zoom; } };
        row.AddChild(_starCanvas);
        if (_editingStars)
        {
            _starCanvas.LayoutNodes = () => _starDraft.Layout.Values;
            _starCanvas.NodeText = id => _starDraft.Layout[id].Label.Length > 0 ? _starDraft.Layout[id].Label : _starDraft.Nodes[id].Name;
            _starCanvas.NodeState = _ => 1; _starCanvas.Selected = _starSelected;
            _starCanvas.NodeIcon = id => StarIcon(_starDraft.Nodes[id].Icon);
            _starCanvas.ClickNode = EditorClickNode; _starCanvas.ClickCell = EditorClickCell;
            BuildStarEditorFields(row);
        }
        else
        {
            _starCanvas.LayoutNodes = () => session!.Stars.Configuration.Layout.Values.Where(n => session!.Stars.Visible(n.Id));
            _starCanvas.NodeText = id => { var n = session!.Stars.Configuration.Nodes[id]; return n.Name + $" {session!.Stars.Level(id)}/{n.MaxLevel}"; };
            _starCanvas.NodeState = id => session!.Stars.Level(id) >= session!.Stars.Configuration.Nodes[id].MaxLevel ? 3
                : session!.Stars.Level(id) > 0 ? 2 : session!.Stars.CanBuy(id, session!.Gold, out _) ? 1 : 0;
            _starCanvas.ClickNode = BuyStarNode;
            _starCanvas.NodeIcon = id => StarIcon(session!.Stars.Configuration.Nodes[id].Icon);
            _starCanvas.HoverNode = id => { _starHovered=id;if (id.Length > 0) _starHint.Text = DescribeStar(id); };
        }
        _starHint = UiLabel(_starMessage.Length > 0 ? _starMessage : _editingStars
            ? "点空格新建；选择后可移动/复制/连线；修改字段后点应用，再保存三表。"
            : "单击节点升一级 · 任一前置点亮即可开放 · 拖拽平移 · 滚轮缩放", 14);
        // 首次布局宽度为0时，自动换行会把最小高度放大到窗口之外；使用明确换行并允许裁剪长诊断。
        _starHint.ClipText = true; _starHint.CustomMinimumSize = new(0, 62); _starHint.TooltipText = _starHint.Text; starPage.AddChild(_starHint);
    }
    private void UpdateStarUi()
    {
        if (!starPage.Visible || _starGold == null || !GodotObject.IsInstanceValid(_starGold)) return;
        if (_starDisplayedGold == (session?.Gold??0)) return;
        _starDisplayedGold = (session?.Gold??0); _starGold.Text = GoldText();
        if(!_editingStars&&session!=null&&session.Stars.Configuration.Nodes.ContainsKey(_starHovered))_starHint.Text=DescribeStar(_starHovered);
        if (_starCanvas != null && GodotObject.IsInstanceValid(_starCanvas)) _starCanvas.QueueRedraw();
    }
    private string DescribeStar(string id)
    {
        var node = session!.Stars.Configuration.Nodes[id]; int rank = session!.Stars.Level(id);
        bool buy = session!.Stars.CanBuy(id, session!.Gold, out string reason);
        return $"{node.Name} · {rank}/{node.MaxLevel}级\n{StarEffect(node, rank)}" +
            (rank < node.MaxLevel ? $" → {StarEffect(node, rank + 1)} · 下一等级 {node.Costs[rank]} 金币" : " · 已满级") +
            (buy ? " · 单击升级" : " · " + reason);
    }
    private static string StarIcon(string icon) => icon switch { "attack" => "⚔", "defense" => "◆", _ => "✦" };
    private void BuyStarNode(string id)
    {
        try
        {
            if (session!.BuyStar(id, out string reason)) { _starMessage = session!.Stars.Configuration.Nodes[id].Name + "升级成功 · " + DescribeStar(id); PlaySound("sfx_reward");SaveNow();Refresh(); }
            else { _starMessage = reason; PlaySound("sfx_deny"); }
        }
        catch (Exception error) { _starMessage = "未升级：" + error.Message; }
        _starCanvas.QueueRedraw(); _starGold.Text = GoldText(); _starHint.Text = _starMessage;
    }
    private void ToggleStarEditor()
    {
        if (_editingStars)
        {
            if (_starDraft.Dirty) { _starMessage = "请先保存或重新加载丢弃草稿，再返回玩家星图。"; _starHint.Text = _starMessage; return; }
            _editingStars = false; _starMessage = ""; RefreshStarPage(); return;
        }
        StarEditorAction(() =>
        {
            if(starEditorConfigRoot==null)
            {
                string workspace=ProjectSettings.GlobalizePath("res://..");
                string qa=QaOutput.NewDirectory(workspace,"star-editor");
                foreach(string relative in new[]{StarConfiguration.ContentPath,StarConfiguration.LayoutPath,StarConfiguration.PlanPath})
                {
                    string target=System.IO.Path.Combine(qa,relative);System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
                    string source=System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://config"),relative);
                    if(System.IO.File.Exists(source))System.IO.File.Copy(source,target);
                }
                starEditorConfigRoot=qa;
            }
            _starStore = new StarEditorStore(starEditorConfigRoot);
            _starDraft = _starStore.Load(); _starSelected = ""; _editingStars = true; _starMode = "选择";
        });
    }
    private void StarEditorAction(Action action)
    {
        try { action(); }
        catch (Exception error) { _starMessage = "操作未完成：" + error.Message; }
        RefreshStarPage();
    }
    private void BuildStarEditorToolbar()
    {
        var row = new HBoxContainer(); starPage.AddChild(row);
        foreach (string mode in new[] { "选择", "移动", "复制", "连线" })
        {
            string captured = mode;
            UiButton(row, (_starMode == mode ? "● " : "") + mode, () => { _starMode = captured; _starHint.Text = "模式：" + captured + "；连线时先选下游节点，再点前置节点。"; });
        }
        UiButton(row, "撤销", () => StarEditorAction(_starDraft.Undo));
        UiButton(row, "重做", () => StarEditorAction(_starDraft.Redo));
        row=new HBoxContainer();starPage.AddChild(row);
        UiButton(row, "设为根", () => StarEditorAction(() => { if (_starSelected.Length > 0) _starDraft.SetRoot(_starSelected); }));
        UiButton(row, "删除节点", () => StarEditorAction(() => { if (_starSelected.Length > 0) { _starDraft.Delete(_starSelected); _starSelected = ""; } }));
        UiButton(row, "重新加载 / 丢弃", () => StarEditorAction(() => { _starDraft = _starStore.Load(); _starSelected = ""; _starMessage = "已重新加载源表"; }));
        var save = UiButton(row, "保存三表", SaveStarEditor); save.Name = "StarEditorSave";
    }
    private void EditorClickNode(string id)
    {
        StarEditorAction(() =>
        {
            if (_starMode == "连线" && _starSelected.Length > 0)
            {
                if (id == _starSelected) throw new InvalidOperationException("节点不能以前置指向自己。");
                var parents = _starDraft.Layout[_starSelected].Prerequisites.ToList();
                if (!parents.Remove(id)) parents.Add(id);
                _starDraft.SetPrerequisites(_starSelected, parents); _starMessage = "已切换前置连线；再次点击可移除。";
            }
            else _starSelected = id;
        });
    }
    private void EditorClickCell(int col, int row)
    {
        StarEditorAction(() =>
        {
            if (_starMode == "移动" && _starSelected.Length > 0) _starDraft.Move(_starSelected, col, row);
            else if (_starMode == "复制" && _starSelected.Length > 0) _starSelected = _starDraft.Create(col, row, _starSelected);
            else if (_starMode == "选择") _starSelected = _starDraft.Create(col, row);
        });
    }
    private LineEdit StarField(VBoxContainer panel, string title, string text)
    {
        panel.AddChild(UiLabel(title, 12)); var field = new LineEdit { Text = text, CustomMinimumSize = new(310, 28) }; panel.AddChild(field); return field;
    }
    private void BuildStarEditorFields(Node row)
    {
        var scroll = new ScrollContainer { CustomMinimumSize = new(340, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill }; row.AddChild(scroll);
        var panel = new VBoxContainer { CustomMinimumSize = new(320, 0) }; scroll.AddChild(panel);
        if (!_starDraft.Nodes.TryGetValue(_starSelected, out var n)) { panel.AddChild(UiLabel("选择节点查看属性\n或点击空格新建", 15)); return; }
        var idLabel = UiLabel("稳定ID：" + n.Id, 13); idLabel.ClipText = true; idLabel.TooltipText = n.Id; panel.AddChild(idLabel);
        _tnName = StarField(panel, "玩家名称", n.Name); _tnLabel = StarField(panel, "编辑可读名（游戏不读取）", _starDraft.Layout[n.Id].Label);
        _tnMax = StarField(panel, "最大等级", n.MaxLevel.ToString());
        _tnCost = StarField(panel, "逐级金币费用（分号分隔，数量=等级数）", string.Join(';', n.Costs));
        panel.AddChild(UiLabel("属性效果", 12)); _tnAttribute = new OptionButton(); panel.AddChild(_tnAttribute); _tnAttribute.AddItem("未配置（占位）");
        int index = 1;
        foreach (var attr in new[]{"attack","hp","party_slots","skill_slots","talent_reset","talent_reset_discount"})
        {
            _tnAttribute.AddItem(attr=="skill_slots"?"主动技能槽":attr=="talent_reset"?"解锁洗点":attr=="talent_reset_discount"?"洗点折扣":attr=="party_slots"?"上阵席位":attr=="attack"?"全队攻击":"全队生命上限");
            _tnAttribute.SetItemMetadata(index,attr); if (n.Attribute == attr) _tnAttribute.Select(index); index++;
        }
        panel.AddChild(UiLabel("操作：固定值（当前开放）", 12)); _tnOperation = new OptionButton(); panel.AddChild(_tnOperation);
        _tnOperation.AddItem("固定值 flat");
        _tnValue = StarField(panel, "每级固定属性贡献", n.ValuePerLevel.ToString(CultureInfo.InvariantCulture));
        _tnIcon = StarField(panel, "图标分类（attack / defense / utility）", n.Icon);
        _tnParents = StarField(panel, "前置ID（分号分隔，任一满足）", string.Join(';', _starDraft.Layout[n.Id].Prerequisites));
        panel.AddChild(UiLabel("节点意图（仅编辑交接）", 12)); _tnIntent = new TextEdit { Text = _starDraft.Intents.GetValueOrDefault(n.Id, ""), CustomMinimumSize = new(310, 70) }; panel.AddChild(_tnIntent);
        UiButton(panel, "应用字段到草稿", ApplyStarFields).Name="StarEditorApply";
    }
    private void ApplyStarFields()
    {
        StarEditorAction(() =>
        {
            var n = _starDraft.Nodes[_starSelected];
            int max = int.Parse(_tnMax.Text, CultureInfo.InvariantCulture);
            long[] costs = _tnCost.Text.Split(';').Select(v => long.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            double value = double.Parse(_tnValue.Text, CultureInfo.InvariantCulture);
            if (max <= 0 || costs.Length != max || costs.Any(c => c < 0) || !double.IsFinite(value) || value < 0) throw new InvalidOperationException("等级、价目或效果数值无效。");
            string attr="",op="";
            if (_tnAttribute.Selected > 0)
            {
                attr=_tnAttribute.GetItemMetadata(_tnAttribute.Selected).AsString();op="flat";
            }
            else value = 0;
            _starDraft.SetNode(n with { Name = _tnName.Text, MaxLevel = max, Costs = Array.AsReadOnly(costs), Attribute = attr,
                Operation = op, ValuePerLevel = value, Icon = _tnIcon.Text }, _tnLabel.Text, _tnIntent.Text);
            _starDraft.SetPrerequisites(n.Id, _tnParents.Text.Length == 0 ? Array.Empty<string>() : _tnParents.Text.Split(';').Select(p => p.Trim()));
            _starMessage = "字段已应用到草稿，保存前将校验完整引用图。";
        });
    }
    private void SaveStarEditor()
    {
        StarEditorAction(() =>
        {
            if (!System.IO.File.Exists(ProjectSettings.GlobalizePath("res://Idle-CRPG.csproj"))) throw new InvalidOperationException("仅源码工程允许保存配置。");
            if(session!=null)session.ValidateStarConfiguration(_starDraft.Validate());
            _starStore.Save(_starDraft, session?.Stars.Capture()??new StarProgressSave(Array.Empty<StarRankSave>()));
            // QA草稿不发布到玩家会话或生产配置；正式发布必须由用户专项授权后走工程修改流程。
            _starDraft = _starStore.Load(); SaveNow();
            _starMessage = "已保存QA副本，未发布到生产配置："+starEditorConfigRoot;
        });
    }
}
