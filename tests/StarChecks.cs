using System;
using System.IO;
using System.Linq;
using IdleCrpg.Core;

public static class StarChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string message){if(!ok)throw new Exception("STAR_CHECK: "+message);}
        var c=LegacySkillConfig.LoadLegacy(Path.Combine(root,"Idle-CRPG/config"));
        GameSession Funded()
        {
            var s=GameSession.CreateWorld(c,42,new[]{"warrior","ranger","mage"}).Capture();s.Gold=1000;foreach(var h in s.Heroes)h.Hp=40;
            return GameSession.Restore(c,s);
        }
        var game=Funded();var before=game.Heroes.Select(h=>(h.Attack,h.MaxHp,h.Hp)).ToArray();long gold=game.Gold;
        Check(!game.BuyStar("s_hp",out _)&&game.Gold==gold,"绕过前置或失败扣费");
        Check(game.BuyStar("s_root",out _)&&game.Gold==gold-3,"根节点没有正确扣费");
        Check(game.Heroes.Select((h,i)=>h.Attack==before[i].Attack+1&&h.MaxHp==before[i].MaxHp&&h.Hp==before[i].Hp).All(v=>v),"攻击节点没有作用于全队");
        Check(game.BuyStar("s_hp",out _)&&game.Heroes.Select((h,i)=>h.MaxHp==before[i].MaxHp+1&&h.Hp==before[i].Hp).All(v=>v),"生命节点治疗或没有增加上限");
        Check(game.BuyStar("s_hp_01",out _)&&game.Stars.PrerequisitesMet("s_hp_02"),"多前置没有按任一已点亮开放");
        Check(game.Stars.Level("s_atk_01")==0&&game.BuyStar("s_hp_02",out _),"汇合节点错误要求所有前置");
        Check(game.BuyStar("s_root",out _)&&game.BuyStar("s_root",out _)&&!game.BuyStar("s_root",out _),"逐级购买/满级边界错误");
        var dir=Path.Combine(root,"artifacts","star-checks",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        var store=new SaveStore(Path.Combine(dir,"progress.json"));store.Save(game,viewMode:3);
        var resumed=GameSession.Restore(c,store.Load(c)!.Session!);
        Check(resumed.Gold==game.Gold&&resumed.Stars.Level("s_root")==3&&resumed.Heroes.Select((h,i)=>h.Attack==game.Heroes[i].Attack&&h.MaxHp==game.Heroes[i].MaxHp&&h.Hp==game.Heroes[i].Hp).All(v=>v),"存读档重复应用或丢失星图");
        var empty=GameSession.CreateWorld(c,1,new[]{"warrior","ranger","mage"});Check(!empty.BuyStar("s_root",out _)&&empty.Gold==0,"金币不足时扣费");
        void Reject(Action action,string message){bool failed=false;try{action();}catch(Exception e)when(e is InvalidOperationException or ArgumentException){failed=true;}Check(failed,message);}
        Reject(()=>new StarProgression(c.Stars,new(new[]{new StarRankSave("missing",1)})),"未知存档节点没拒绝");
        Reject(()=>new StarProgression(c.Stars,new(new[]{new StarRankSave("s_root",4)})),"超等级存档没拒绝");
        Reject(()=>new StarProgression(c.Stars,new(new[]{new StarRankSave("s_hp",1)})),"坏前置存档没拒绝");
        Reject(()=>new StarConfiguration(c.Stars.Nodes.Values,c.Stars.Layout.Values.Select(l=>l.Id=="s_root"?l with {Prerequisites=new[]{"s_hp"}}:l)),"配置环未拒绝");
        var configRoot=Path.Combine(dir,"config");Directory.CreateDirectory(Path.Combine(configRoot,"star"));
        foreach(var name in new[]{"star.csv","star_layout.csv","star_plan.csv"})File.Copy(Path.Combine(root,"Idle-CRPG/config/star",name),Path.Combine(configRoot,"star",name));
        var editor=new StarEditorStore(configRoot);var draft=editor.Load();
        string id=draft.Create(4,-1);draft.SetNode(draft.Nodes[id] with {Name="测试,星辰",Attribute="hp",Operation="flat",ValuePerLevel=1},"编辑标签","第一行\n第二行,\"备注\"");
        draft.SetPrerequisites(id,new[]{"s_root"});draft.Move(id,5,-1);
        string copied=draft.Create(6,-1,id);draft.SetPrerequisites(copied,new[]{id});draft.Delete(id);
        Check(draft.Layout[copied].Prerequisites.SequenceEqual(new[]{"s_root"}),"删除没有继承前置");
        draft.Undo();Check(draft.Nodes.ContainsKey(id),"撤销失败");draft.Redo();Check(!draft.Nodes.ContainsKey(id),"重做失败");
        editor.Save(draft,game.Stars.Capture());draft=editor.Load();
        Check(draft.Nodes[copied].Name=="测试,星辰"&&draft.Intents[copied]=="第一行\n第二行,\"备注\"","CSV 引号/换行或复制意图丢失");
        draft.Delete("s_hp_02");draft.Validate();Reject(()=>editor.Save(draft,game.Stars.Capture()),"允许删除已购买节点");
        draft=editor.Load();draft.SetRoot(copied);Check(draft.Layout[copied].Prerequisites.Count==0,"设根失败");draft.Validate();
        File.AppendAllText(Path.Combine(configRoot,"star/star.csv"),"\n");Reject(()=>editor.Save(draft,game.Stars.Capture()),"外部修改被覆盖");
        string planFile=Path.Combine(configRoot,"star/star_plan.csv"),planText=File.ReadAllText(planFile);
        File.WriteAllText(planFile,"id,intent\nmissing,损坏引用\n");Reject(()=>editor.Load(),"损坏意图侧车没有阻止加载");Check(File.ReadAllText(planFile).Contains("missing"),"损坏侧车被自动覆盖");File.WriteAllText(planFile,planText);
        File.Delete(planFile);var noPlan=new StarEditorStore(configRoot);var optional=noPlan.Load();Check(optional.Intents.Count==0,"缺少侧车没有按空意图处理");
        var rejectGame=Funded();rejectGame.Heroes[0].Attack=float.MaxValue;
        var extreme=new StarConfiguration(c.Stars.Nodes.Values.Select(n=>n.Id=="s_root"?n with {MaxLevel=1,Costs=new long[]{1},ValuePerLevel=1e38}:n),c.Stars.Layout.Values);
        c.Stars=extreme;var extremeGame=GameSession.Restore(c,rejectGame.Capture());long beforeFailure=extremeGame.Gold;
        Reject(()=>extremeGame.BuyStar("s_root",out _),"属性溢出没有拒绝购买");Check(extremeGame.Gold==beforeFailure&&extremeGame.Stars.Level("s_root")==0,"属性失败仍扣费或改变等级");
        c.Stars=game.Stars.Configuration;
        var repriced=new StarConfiguration(c.Stars.Nodes.Values.Select(n=>n.Id=="s_root"?n with {ValuePerLevel=2}:n),c.Stars.Layout.Values);
        var oldAttack=game.Heroes[0].Attack;game.ReloadStars(repriced);Check(game.Heroes[0].Attack==oldAttack+3,"热更新没有只调整星图差量");
        game.ReloadStars(repriced);Check(game.Heroes[0].Attack==oldAttack+3,"重复热更新重复叠加");
        Console.WriteLine("PASS: star gold purchases, team ATK/HP, OR prerequisites, save/load, editor CRUD/copy/link/root/undo/redo, quoted CSV, conflict and bought-node protection");
    }
}
