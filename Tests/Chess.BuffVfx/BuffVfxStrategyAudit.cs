using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChessButWeird.BuffVfx;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static partial class BuffVfxAudit
{
    private static void RunStrategyCases(BuffVfxDebugGallery gallery)
    {
        var fx=gallery.Effects;
        foreach(string name in new[]{"CrownHalo","Parachute","CreditCard"})
        {
            var asset=Resources.Load<GameObject>("BuffVfx/Models/"+name);Check(asset,"Authored strategy prop: "+name);
            Check(asset.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.vertexCount)>100,"Actual modeled silhouette: "+name);
            Check(asset.GetComponentsInChildren<Collider>(true).Length==0,"Prop cannot intercept board input: "+name);
        }
        string[] names={"mobile-fortress","gacha","rng-fiesta","revolution","sacrifice","mastery","credit","kingless","trap","paratrooper","commandant","fortress","freestyle","doppelganger","gambling","peace","formation","quest","auto-castle"};
        for(int i=0;i<names.Length;i++)
        {
            gallery.Select(16+i);Capture(names[i]+"-start",true);
            var at=gallery.Caster.position;var materials=gallery.Caster.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).ToArray();
            Check(gallery.PlaySelected(),"Production strategy timeline: "+names[i]);fx.Advance(.22f);Capture(names[i]+"-transition",true);
            fx.Advance(.30f);Capture(names[i]+"-resolved",true);
            if(i==0||i==5||i==6||i==9)Closeup(gallery.Caster,names[i]+"-prop");
            Check(gallery.Caster.position==at,"Render-only sequence cannot relocate piece: "+names[i]);
            Check(materials.SequenceEqual(gallery.Caster.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials)),"Existing piece materials preserved: "+names[i]);
            Check(fx.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount)>0,"Bounded native resolution sparks: "+names[i]);
            fx.Advance(1);Check(fx.ActiveCount==0,"Resolved cleanup: "+names[i]);
            gallery.Select(16+i,true,PieceTeam.Black);Check(gallery.PlaySelected(true),"Black edge strategy: "+names[i]);fx.Paused=true;fx.Advance(1);Check(fx.ActiveCount==1,"Pause strategy: "+names[i]);
            fx.Paused=false;fx.PlaybackSpeed=8;fx.Advance(.2f);fx.PlaybackSpeed=1;Check(fx.ActiveCount==0,"Fast-forward strategy: "+names[i]);
            UserSettings.Presentation.ReducedMotion=true;Check(gallery.PlaySelected(true),"Reduced strategy: "+names[i]);fx.Advance(.2f);Check(fx.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount)==0,"Reduced strategy suppresses sparks: "+names[i]);fx.Advance(1);UserSettings.Presentation.ReducedMotion=false;
            for(int repeat=0;repeat<4;repeat++){Check(gallery.PlaySelected(true),"Repeat strategy: "+names[i]);fx.Advance(1);fx.Advance(1);}
        }
        gallery.Select(23);Check(fx.HudRows==1,"Owner Kingless HUD exists");
        Check(fx.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.Contains("2 / 25 owner turns")),"Actual countdown label includes explicit clock unit");
        Check(fx.GetComponentsInChildren<Graphic>().Any(g=>g.name=="Kingless hourglass"&&!(g is Image)),"Hourglass uses an authored geometric symbol");
        Canvas.ForceUpdateCanvases();var hourglass=fx.GetComponentsInChildren<Graphic>().Single(g=>g.name=="Kingless hourglass");var hourMesh=hourglass.canvasRenderer.GetMesh();Check(hourMesh&&hourMesh.vertexCount==18,"Hourglass generates actual visible UI geometry");
        gallery.Select(33);Check(fx.GetComponentsInChildren<Image>().Count(g=>g.name.StartsWith("Quest milestone"))==6,"Six actual quest milestones");
        Check(fx.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.Contains("4 / 6")),"Quest has exact milestone count");
        fx.SyncHud(new[]{new BuffHudState{BuffId=8,Text="Extra moves used",Current=1,Target=3}});fx.Play(new BuffVisualEvent{Id="actual-gambling-HUD",BuffId=8,Amount=1,Outcome="ExtraMove",ViewerAuthorized=true});fx.Advance(.02f);Capture("gambling-owned-objective",true);
        var objective=fx.GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.text.Contains("Extra moves used"));var extra=fx.GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.text=="+1 extra move");
        Check(extra.rectTransform.position.y+extra.rectTransform.rect.height*.5f<objective.rectTransform.position.y-objective.rectTransform.rect.height,"Extra move pulse remains below the owned objective row");
        gallery.Select(32);Check(fx.PreviewGhosts==1&&fx.PreviewTargets==2,"Formation owns visible uncommitted ghost and destination cells");
        int geometry=fx.GetComponentsInChildren<MeshFilter>(true).Length;
        for(int i=0;i<50;i++)fx.SyncFormation(new[]{new BuffPreviewPiece(gallery.Caster,new Vector3(1,0,1),PieceTeam.Black)});
        Check(fx.GetComponentsInChildren<MeshFilter>(true).Length==geometry,"Repeated hover never allocates more mesh holders");
        fx.ClearPreview();Check(fx.PreviewGhosts==0&&fx.PreviewTargets==0,"Cancel removes all ghost/target visuals");
        gallery.Select(25);gallery.Caster.position+=Vector3.up*3;gallery.PlaySelected();fx.Advance(.1f);
        Check(fx.GetComponentsInChildren<Transform>().Single(t=>t.name=="Parachute mounted prop").position.y>gallery.Caster.position.y,"Parachute remains above a piece during its existing drop animation");
        var invalid=new BuffVisualEvent{Id="invalid-strategy",BuffId=24,ViewerAuthorized=true,Changes=new BuffVisualChange[65]};Check(!fx.Play(invalid),"Oversized transformation batch rejected");
        invalid.Changes=new[]{new BuffVisualChange{To=new Vector3(float.PositiveInfinity,0,0)}};Check(!fx.Play(invalid),"Nonfinite transformation cell rejected");
        fx.ClearVisuals(true);Check(fx.HudRows==0&&fx.PreviewGhosts==0&&fx.PreviewTargets==0,"Scene/phase clear includes independent HUD and preview pools");
        Check(fx.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"Strategy visuals have no gameplay collider");
        report.Add("PASS all nineteen strategy timelines, authored crown/card/parachute, objective UI, uncommitted pooled previews, pause/speed/reduced motion and cleanup.");
    }
    private static int RunStrategyFixtures()
    {
        var host=new GameObject("Actual strategy snapshot QA");var fx=host.AddComponent<BuffVfxOrchestrator>();fx.Initialize(2);fx.enabled=false;int count=0;
        var fixtures=JArray.Parse(File.ReadAllText(Path.Combine(Root,"backend-strategy-fixtures.json")));
        foreach(var fixture in fixtures)foreach(string viewer in new[]{"white","black","spectator"})
        {
            var actors=new Dictionary<int,Transform>();Vector3 Map(Vector2Int p)=>new Vector3(20-2*p.y,0,2*p.x);
            void Board(JToken snapshot)
            {
                foreach(var a in actors.Values){a.gameObject.SetActive(false);Object.Destroy(a.gameObject);}actors.Clear();
                foreach(var p in snapshot["board"]){Check(BuffVfxOnlineAdapter.TrySquare((string)p["square"],out var cell),"Valid actual strategy board cell");var go=new GameObject("Resolved entity "+p["id"]);go.transform.position=Map(cell);actors[(int)p["id"]]=go.transform;}
            }
            var before=fixture[viewer]["before"];var after=fixture[viewer]["after"];long sequence=(long)before["chaos"]["visualSequence"];
            var adapter=new BuffVfxOnlineAdapter(fx,Map,id=>actors.TryGetValue(id,out var a)?a:null);
            Board(before);adapter.Apply("strategy",(long)before["stateVersion"],(JObject)before["chaos"],false);Check(fx.ActiveCount==0,"Reconnect never replays strategy history");
            Board(after);adapter.Apply("strategy",(long)after["stateVersion"],(JObject)after["chaos"],false);
            int expected=after["chaos"]["visualCues"].Count(c=>(long)c["sequence"]>sequence);Check(fx.ActiveCount==expected,"Exactly sanitized strategy cues: "+fixture["name"]+" / "+viewer);
            Check(fx.HudRows==after["chaos"]["visualHud"].Count(),"Only explicitly authorized HUD entries restored");
            if((string)fixture["name"]=="selected-3"&&viewer!="white")
            {Check(expected==0&&!fx.GetComponentsInChildren<LineRenderer>().Any(l=>l.name.StartsWith("Resolved deployment circle")),"Opponent/spectator creates no private pair rendering object");Check(fx.ActiveStatuses==0,"No private sigil from raw Doppelganger pair fields");}
            fx.Advance(.5f);
            if(expected>0)
            {
                var cells=after["chaos"]["visualCues"].Where(c=>(long)c["sequence"]>sequence).SelectMany(c=>c["changes"]??new JArray()).Select(c=>(string)c[c["toKind"]?.Type==JTokenType.Null?"from":"to"]).ToHashSet();
                foreach(var line in fx.GetComponentsInChildren<LineRenderer>().Where(l=>l.name.StartsWith("Resolved deployment circle")))
                {var center=Vector3.zero;for(int j=0;j<line.positionCount;j++)center+=line.GetPosition(j);center/=line.positionCount;Check(cells.Count==0||cells.Any(s=>BuffVfxOnlineAdapter.TrySquare(s,out var p)&&Vector2.Distance(new Vector2(center.x,center.z),new Vector2(Map(p).x,Map(p).z))<.01f),"Sequence uses exact server cell on rotated/scaled board");}
            }
            adapter.Apply("strategy",(long)after["stateVersion"],(JObject)after["chaos"],false);Check(fx.ActiveCount==expected,"Duplicate strategy snapshot ignored");
            fx.ClearVisuals(true);var reconnect=new BuffVfxOnlineAdapter(fx,Map,id=>actors.TryGetValue(id,out var a)?a:null);reconnect.Apply("strategy",(long)after["stateVersion"],(JObject)after["chaos"],false);
            Check(fx.ActiveCount==0&&fx.HudRows==after["chaos"]["visualHud"].Count(),"Reconnect restores objective progress without replaying reward");
            reconnect.Apply("strategy",(long)after["stateVersion"]+1,(JObject)after["chaos"],true);Check(fx.ActiveCount==0&&fx.ActiveStatuses==0&&fx.HudRows==0,"Terminal strategy cleanup");
            if((string)fixture["name"]=="initial-32"&&viewer=="white")
            {
                var outcomeAdapter=new BuffVfxOnlineAdapter(fx,Map,id=>actors.TryGetValue(id,out var a)?a:null);outcomeAdapter.Apply("deadline",1,(JObject)after["chaos"],false);outcomeAdapter.Apply("deadline",2,(JObject)after["chaos"],true,"PoorKingDeadline");
                Check(fx.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text=="Kingless deadline reached"),"Only actual server deadline reason triggers terminal feedback");fx.Advance(1.2f);
                outcomeAdapter.Apply("deadline",3,(JObject)after["chaos"],true,"PoorKingDeadline");Check(!fx.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text=="Kingless deadline reached"),"Further final snapshots never replay deadline outcome");
            }
            foreach(var a in actors.Values)Object.Destroy(a.gameObject);count++;
        }
        Object.Destroy(host);return count;
    }
}
