using Godot;
public partial class Main
{
    /// <summary>角色模型与名称都可选择同一单选项，锁定角色只显示解锁条件。</summary>
    private void AddCampHitTargets()
    {
        for(int i=0;i<choices.Length;i++)
        {
            var choice=choices[i];
            var hit=new Button {Flat=true,Position=new Vector2(125+i*182,(i%2==0?370:305)-80),Size=new Vector2(80,86),TooltipText=choice.TooltipText};
            hit.Pressed+=()=>{if(!choice.Disabled)choice.ButtonPressed=true;else summary.Text=choice.TooltipText;};partySetup.AddChild(hit);
        }
    }
}
