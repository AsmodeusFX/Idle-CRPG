using Godot;
using IdleCrpg.Core;
using System.Linq;

public partial class Main
{
    private int mapDifficulty,mapChapter;
    private OptionButton difficultyPicker=null!;
    private readonly Button[] chapterTabs=new Button[5];
    private VBoxContainer castleList=null!;
    private Config? listMap;
    private WorldCampaign? archivedMap;
    private GameSession? mapSession;
    private Config MapConfig=>(session?.Config??config).ChapterMaps[mapChapter];
    private bool ViewingCurrentMap=>session!=null&&mapDifficulty==session.DifficultyIndex&&mapChapter==session.ChapterIndex;
    private int SavedSelectedRegion=>ViewingCurrentMap?selectedRegion:session?.World?.CurrentRegion??-1;
    private WorldCampaign? MapWorld
    {
        get
        {
            if(session?.World==null)return null;
            if(ViewingCurrentMap)return session.World;
            if(!session.MapUnlocked(mapDifficulty,mapChapter))return null;
            if(archivedMap==null)
            {
                archivedMap=new WorldCampaign(MapConfig);
                var progress=session.MapProgress(mapDifficulty,mapChapter);
                archivedMap.Progress.Explored.Clear();archivedMap.Progress.Explored.UnionWith(progress.Explored);archivedMap.Progress.Cleared.UnionWith(progress.Cleared);
            }
            return archivedMap;
        }
    }
    private void BuildChapterControls()
    {
        difficultyPicker=new OptionButton {Position=new Vector2(12,5),Size=new Vector2(150,36)};castlePage.AddChild(difficultyPicker);
        for(int i=0;i<3;i++)difficultyPicker.AddItem(config.Difficulties[i].Name,i);
        difficultyPicker.ItemSelected+=index=>BrowseChapter((int)index,0);
        for(int i=0;i<5;i++)
        {
            int chapter=i;var b=new Button {Position=new Vector2(174+i*154,5),Size=new Vector2(148,36)};
            b.AddThemeFontSizeOverride("font_size",13);b.Pressed+=()=>BrowseChapter(mapDifficulty,chapter);castlePage.AddChild(b);chapterTabs[i]=b;
        }
    }
    /// <summary>章节页签只切换查看范围；锁定页不改变正在自动执行的远征。</summary>
    private void BrowseChapter(int difficulty,int chapter)
    {
        if(session?.MapUnlocked(difficulty,chapter)!=true){RefreshChapterControls();return;}
        mapDifficulty=difficulty;mapChapter=chapter;archivedMap=null;detailRegion=-1;
        selectedRegion=ViewingCurrentMap?session.World!.CurrentRegion:MapConfig.StartRegionId;
        RefreshWorldPage();
    }
    private void OpenCurrentChapter()
    {
        mapSession=session;mapDifficulty=session?.DifficultyIndex??0;mapChapter=session?.ChapterIndex??0;archivedMap=null;detailRegion=-1;
        selectedRegion=session?.World?.CurrentRegion??-1;
    }
    private void RefreshChapterControls()
    {
        if(!ReferenceEquals(mapSession,session))OpenCurrentChapter();
        for(int i=0;i<3;i++)
        {
            bool unlocked=session?.MapUnlocked(i,0)==true;
            difficultyPicker.SetItemDisabled(i,!unlocked);
            difficultyPicker.GetPopup().SetItemTooltip(i,unlocked?config.Difficulties[i].Name:"通关上一难度全部五章后解锁");
        }
        difficultyPicker.Select(mapDifficulty);
        for(int i=0;i<5;i++)
        {
            var def=config.Chapters[i];var b=chapterTabs[i];b.Text=def.Name;
            b.Disabled=session?.MapUnlocked(mapDifficulty,i)!=true;
            b.TooltipText=b.Disabled?"通关上一章节的全部地牢及城堡，回村交易后解锁":$"{def.Name} · {def.Width}×{def.Height}";
            b.Modulate=i==mapChapter?new Color("80b8ff"):Colors.White;
        }
    }
    private void SyncCampaignConfig()
    {
        if(session==null||ReferenceEquals(config,session.Config))return;
        config=session.Config;interactionSession=null;OpenCurrentChapter();cameraReady=false;Refresh();
    }
}
