using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IdleCrpg.Core;



/// <summary>玩家星图和节点编辑器共用坐标/命中/连线绘制；拖拽与点击分离。</summary>
public partial class StarCanvas : Control
{
    public const float CellWidth = 176, CellHeight = 90, NodeSize = 60;
    public Func<IEnumerable<StarLayout>> LayoutNodes = () => Array.Empty<StarLayout>();
    public Func<string, string> NodeText = id => id;
    public Func<string, int> NodeState = _ => 0;
    public Func<string, string> NodeIcon = _ => "◇";
    public Action<string> ClickNode = _ => { };
    public Action<int, int> ClickCell = (_, _) => { };
    public Action<string> HoverNode = _ => { };
    public Action<Vector2, float> ViewChanged = (_, _) => { };
    public Vector2 Pan = new(100, 75);
    public float Zoom = 1;
    public string Selected = "";
    private bool _pressed, _moved;
    private Vector2 _start;
    private string _hover = "";
    public StarCanvas() { MouseFilter = MouseFilterEnum.Stop; ClipContents = true; }
    public void ResetView()
    {
        Zoom = 1;
        var nodes = LayoutNodes().ToArray();
        Pan = nodes.Length == 0 ? new(100, 75) : new(100 - nodes.Min(n => n.Col) * CellWidth, 75 - nodes.Min(n => n.Row) * CellHeight);
        ViewChanged(Pan, Zoom); QueueRedraw();
    }
    public static Vector2 Center(StarLayout n) => new(n.Col * CellWidth, n.Row * CellHeight);
    private string Hit(Vector2 p) => LayoutNodes().FirstOrDefault(n => new Rect2(Center(n) - Vector2.One * NodeSize / 2, Vector2.One * NodeSize).HasPoint((p - Pan) / Zoom))?.Id ?? "";
    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton b)
        {
            if (b.ButtonIndex == MouseButton.Left)
            {
                if (b.Pressed) { _pressed = true; _moved = false; _start = b.Position; }
                else if (_pressed)
                {
                    _pressed = false;
                    if (!_moved)
                    {
                        string hit = Hit(b.Position);
                        if (hit.Length > 0) ClickNode(hit);
                        else { var p = (b.Position - Pan) / Zoom; ClickCell((int)MathF.Round(p.X / CellWidth), (int)MathF.Round(p.Y / CellHeight)); }
                    }
                }
            }
            if (b.Pressed && b.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                float old = Zoom; Zoom = Math.Clamp(Zoom * (b.ButtonIndex == MouseButton.WheelUp ? 1.1f : 1 / 1.1f), .5f, 2);
                Pan = b.Position - (b.Position - Pan) * (Zoom / old); ViewChanged(Pan, Zoom);
            }
            AcceptEvent(); QueueRedraw();
        }
        if (e is InputEventMouseMotion m)
        {
            if (_pressed && (_moved || _start.DistanceTo(m.Position) > 4))
            { _moved = true; Pan += m.Relative; ViewChanged(Pan, Zoom); QueueRedraw(); }
            string hover = Hit(m.Position); if (hover != _hover) { _hover = hover; HoverNode(hover); }
            AcceptEvent();
        }
    }
    public override void _Draw()
    {
        DrawRect(new(Vector2.Zero, Size), new Color("202727"));
        float xStep = CellWidth * Zoom, yStep = CellHeight * Zoom;
        for (float x = Pan.X % xStep; x < Size.X; x += xStep) DrawLine(new(x, 0), new(x, Size.Y), new Color("303938"));
        for (float y = Pan.Y % yStep; y < Size.Y; y += yStep) DrawLine(new(0, y), new(Size.X, y), new Color("303938"));
        DrawSetTransform(Pan, 0, Vector2.One * Zoom);
        var nodes = LayoutNodes().ToDictionary(n => n.Id);
        foreach (var node in nodes.Values)
            foreach (string parent in node.Prerequisites)
                if (nodes.TryGetValue(parent, out var from))
                {
                    var a = Center(from); var b = Center(node); var dir = (b - a).Normalized(); var end = b - dir * 36;
                    var color = new Color(NodeState(node.Id) >= 2 ? "bca15d" : "646f67");
                    DrawLine(a + dir * 32, end, color, 2);
                    DrawLine(end, end - dir.Rotated(.4f) * 11, color, 2); DrawLine(end, end - dir.Rotated(-.4f) * 11, color, 2);
                }
        foreach (var n in nodes.Values)
        {
            var p = Center(n); int state = NodeState(n.Id);
            var rect = new Rect2(p - Vector2.One * 30, Vector2.One * 60);
            DrawRect(rect, new Color(state >= 2 ? "51492d" : state == 1 ? "304941" : "282e2c"));
            DrawRect(rect, new Color(n.Id == Selected || state == 3 ? "f3cf70" : state == 1 ? "8cae93" : "7b806b"), false, state == 3 ? 3 : 2);
            DrawString(GetThemeDefaultFont(), p + new Vector2(-12, 9), NodeIcon(n.Id), fontSize: 27, modulate: new Color("d8bf88"));
            DrawString(GetThemeDefaultFont(), p + new Vector2(-65, 48), NodeText(n.Id), fontSize: 13, modulate: new Color("ddcfac"));
        }
        DrawSetTransform(Vector2.Zero);
    }
}
