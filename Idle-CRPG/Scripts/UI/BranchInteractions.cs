using Godot;
using IdleCrpg.Core;
using System.Collections.Generic;
using System.Linq;

public partial class Main
{
    private readonly Dictionary<BranchRequest,Button> branchCards=new();
    private void RefreshBranchInteractions()
    {
        var available=new List<BranchRequest>();
        if(session?.World!=null)for(int key=0;key<=session.LatestMapIndex;key++)
        {
            var map=config.ChapterMaps[key%5];var progress=session.MapProgress(key/5,key%5);
            foreach(var castle in map.Dungeons.Where(d=>d.Castle&&map.Optional(d.Id)&&!progress.Cleared.Contains(d.Id)))
                if(progress.AreaPartUnlocked(map,map.AreaParts.Single(a=>a.CastleId==castle.Id).Id))available.Add(new(key/5,key%5,castle.Id));
        }
        foreach(var key in branchCards.Keys.Where(key=>!available.Contains(key)).ToArray()){interactionList.RemoveChild(branchCards[key]);branchCards[key].QueueFree();branchCards.Remove(key);}
        foreach(var request in available)
        {
            var map=config.ChapterMaps[request.Chapter];var region=map.Regions.First(r=>r.Enterable&&!map.IsTown(r.Id)&&map.Rulers[r.Id]==request.Castle);var level=map.Challenge(request.Castle,request.Difficulty);
            if(!branchCards.TryGetValue(request,out var button))
            {
                button=new Button {CustomMinimumSize=new Vector2(0,98),Alignment=HorizontalAlignment.Left};button.AddThemeFontSizeOverride("font_size",14);interactionList.AddChild(button);branchCards.Add(request,button);
                button.Pressed+=()=>{if(session?.RequestBranchChallenge(request.Difficulty,request.Chapter,request.Castle)==true){PlaySound("sfx_click");Refresh();SaveNow();}};
            }
            bool active=session!.ActiveBranch==request;int index=session.BranchQueue.ToList().IndexOf(request);
            button.Text=$"◆ 挑战支线-{region.Name}\n挑战等级 {level.Challenge} · 推荐小队等级 {level.Recommended}\n{config.Difficulties[request.Difficulty].Name} · {config.Chapters[request.Chapter].Name}\n{(active?"执行中":index>=0?$"计划第{index+1}项":"包含未通关地牢及最终要塞 · 原型内容")}";
            button.Disabled=active||index>=0;
        }
        interactionEmpty.Visible=castleCards.Count==0&&branchCards.Count==0;
    }
}
