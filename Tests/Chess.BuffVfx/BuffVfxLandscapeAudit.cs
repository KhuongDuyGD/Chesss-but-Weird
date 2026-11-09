using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChessButWeird.BuffVfx;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

public static partial class BuffVfxAudit
{
    private static void RunLandscapeCases(BuffVfxDebugGallery gallery)
    {
        var fx=gallery.Effects;
        foreach(string name in new[]{"SupplyCrate","CrateLid","Landmine","ConstructionBarrier","DecoyLog"})
        {
            var model=Resources.Load<GameObject>("BuffVfx/Models/"+name);Check(model,"Authored landscape prop: "+name);
            Check(model.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.vertexCount)>60,"Recognizable mesh geometry: "+name);
            Check(model.GetComponentsInChildren<Collider>(true).Length==0,"Authored prop cannot intercept board clicks: "+name);
        }
        string[] names={"teleport","swap","substitute","escape","ghost","truck","plague","crate","mine","collapse"};
        for(int i=0;i<10;i++)
        {
            gallery.Select(i+6);Capture(names[i]+"-status");
            if(i<7)Closeup(gallery.Caster,names[i]+"-status-detail");
            var materials=gallery.Caster.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).ToArray();
            Check(gallery.PlaySelected(),"New production sequence: "+names[i]);fx.Advance(.22f);Capture(names[i]+"-travel");
            fx.Advance(.28f);Capture(names[i]+"-resolve");
            Check(fx.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount)>0,"Native resolve burst emitted: "+names[i]);
            if(i==2)Closeup(fx.GetComponentsInChildren<Transform>().First(t=>t.name=="DecoyLog mounted prop"),"substitute-smoke");
            if(i>=6)Closeup(i==6?gallery.Caster:gallery.Target,names[i]+"-prop");
            if(i==9){fx.Advance(.3f);Capture("collapse-barriers");}
            fx.Advance(1);Check(fx.ActiveCount==0,"New sequence cleanup: "+names[i]);
            Check(materials.SequenceEqual(gallery.Caster.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials)),"Piece original materials preserved: "+names[i]);
            gallery.Select(i+6,true,PieceTeam.Black);Check(gallery.PlaySelected(true),"Black/edge: "+names[i]);
            fx.Paused=true;fx.Advance(1);Check(fx.ActiveCount==1,"Pause new timeline: "+names[i]);fx.Paused=false;fx.PlaybackSpeed=8;fx.Advance(.2f);fx.PlaybackSpeed=1;Check(fx.ActiveCount==0,"Speed synchronized cleanup: "+names[i]);
            for(int repeat=0;repeat<5;repeat++){Check(gallery.PlaySelected(true),"Repeat new timeline: "+names[i]);fx.Advance(1);fx.Advance(1);}
            UserSettings.Presentation.ReducedMotion=true;UserSettings.Presentation.Flash=0;Check(gallery.PlaySelected(true),"Reduced new timeline: "+names[i]);fx.Advance(.2f);
            Check(fx.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount)==0,"No native particle burst in reduced motion: "+names[i]);fx.Advance(1);UserSettings.Presentation.ReducedMotion=false;UserSettings.Presentation.Flash=1;
            fx.ClearVisuals(true);Check(fx.ActiveBoardProps==0&&fx.ActiveStatuses==0,"Complete clear: "+names[i]);
        }
        gallery.Select(13);Check(fx.ActiveBoardProps==1,"Supply crate persists before collection");
        gallery.PlaySelected(false,"Claim");fx.SyncBoard(Array.Empty<BuffBoardVisual>());fx.Advance(.3f);Closeup(gallery.Target,"crate-open-lid");fx.Advance(1);Check(fx.ActiveBoardProps==0,"Consumed crate disappears");
        gallery.Select(13);UserSettings.Presentation.ReducedMotion=true;gallery.PlaySelected();fx.Advance(.33f);
        Check(fx.ActiveCount==0&&fx.GetComponentsInChildren<Transform>().Count(t=>t.name=="SupplyCrate mounted prop")==1,"Reduced drop immediately hands off to persistent crate");UserSettings.Presentation.ReducedMotion=false;
        gallery.Select(8);gallery.PlaySelected();Check(fx.GetComponentsInChildren<Transform>().Any(t=>t.name=="DecoyLog mounted prop"),"Decoy log belongs to actual entity");
        fx.SyncStatuses(Array.Empty<BuffVisualStatus>());fx.Advance(.2f);Check(!fx.GetComponentsInChildren<Transform>().Any(t=>t.name=="DecoyLog mounted prop"),"Decoy presentation fully retired");
        gallery.Select(10);var actor=gallery.Caster;var original=actor.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).ToArray();
        fx.SyncStatuses(new[]{new BuffVisualStatus(21,101,actor,PieceTeam.White),new BuffVisualStatus(22,101,actor,PieceTeam.White,4),new BuffVisualStatus(5,101,actor,PieceTeam.White,0)});
        Check(fx.ActiveStatuses==3,"Ghost/poison/ready overlays stack");
        fx.SyncStatuses(new[]{new BuffVisualStatus(22,101,actor,PieceTeam.White,2)});fx.Advance(.2f);Check(fx.ActiveStatuses==1,"Countdown transition retires other overlays");
        Check(fx.GetComponentsInChildren<TMPro.TextMeshPro>().Any(t=>t.text=="2"),"Actual refreshed poison count");
        fx.SyncStatuses(Array.Empty<BuffVisualStatus>());fx.Advance(.2f);Check(original.SequenceEqual(actor.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials)),"Ghost retirement leaves original materials intact");
        Check(!fx.GetComponentsInChildren<Transform>().Any(t=>t.name=="Ghost phase silhouette"),"Ghost silhouette fully removed after status ends");
        Check(fx.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"All presentation objects remain outside click/placement physics");
        fx.ClearVisuals(true);report.Add("PASS ten mobility/environment timelines, authored models, pause/speed/settings, counters, stacked overlays and cleanup.");
    }
    private static int RunLandscapeFixtures()
    {
        var host=new GameObject("Real landscape viewer snapshot QA");var fx=host.AddComponent<BuffVfxOrchestrator>();fx.Initialize(2);fx.enabled=false;int count=0;
        var fixtures=JArray.Parse(File.ReadAllText(Path.Combine(Root,"backend-landscape-fixtures.json")));
        foreach(var fixture in fixtures)foreach(string viewer in new[]{"white","black"})
        {
            var actors=new Dictionary<int,Transform>();Vector3 Map(Vector2Int p)=>new Vector3(20-2*p.y,0,2*p.x);
            void Board(JToken snapshot)
            {
                foreach(var a in actors.Values)Object.Destroy(a.gameObject);actors.Clear();
                foreach(var piece in snapshot["board"]){Check(BuffVfxOnlineAdapter.TrySquare((string)piece["square"],out var p),"Real landscape square");var go=new GameObject("Actual entity "+piece["id"]);go.transform.position=Map(p);actors[(int)piece["id"]]=go.transform;}
            }
            var before=fixture[viewer]["before"];var after=fixture[viewer]["after"];var adapter=new BuffVfxOnlineAdapter(fx,Map,id=>actors.TryGetValue(id,out var a)?a:null);
            Board(before);adapter.Apply("landscape",(long)before["stateVersion"],(JObject)before["chaos"],false);Check(fx.ActiveCount==0,"No historical trigger on first snapshot");
            Board(after);adapter.Apply("landscape",(long)after["stateVersion"],(JObject)after["chaos"],false);Check(fx.ActiveCount==1,"Real confirmed "+fixture["name"]+" / "+viewer);
            Check(fx.ActiveBoardProps==after["chaos"]["visualBoard"].Count(),"Only sanitized board props restored");
            if((string)fixture["name"]=="crate-drop"|| (string)fixture["name"]=="mine-arm")
            {
                string model=(string)fixture["name"]=="crate-drop"?"SupplyCrate mounted prop":"Landmine mounted prop";
                foreach(var prop in fx.GetComponentsInChildren<Transform>().Where(t=>t.name==model))Check(Quaternion.Angle(prop.rotation,Quaternion.LookRotation(Vector3.left))<.01f,"Prop orientation follows rotated board");
            }
            adapter.Apply("landscape",(long)after["stateVersion"],(JObject)after["chaos"],false);Check(fx.ActiveCount==1,"Repeated resolved snapshot deduplicated");
            if((string)fixture["name"]=="collapse")
            {
                fx.Advance(.2f);var slabs=fx.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Cosmetic falling cell")).ToArray();Check(slabs.Length==16,"All sixteen actual edge cells fall");
                foreach(var slab in slabs)Check(Mathf.Abs(slab.transform.position.z)<.01f||Mathf.Abs(slab.transform.position.z-14)<.01f,"Rotated board collapse remains on exact edge files");
            }
            fx.ClearVisuals(true);var reconnect=new BuffVfxOnlineAdapter(fx,Map,id=>actors.TryGetValue(id,out var a)?a:null);
            reconnect.Apply("landscape",(long)after["stateVersion"],(JObject)after["chaos"],false);Check(fx.ActiveCount==0,"Reconnect never repeats teleport/open/explosion");Check(fx.ActiveBoardProps==after["chaos"]["visualBoard"].Count(),"Reconnect restores actual hazards/barriers");
            // A future private trap must be absent from the explicit presentation projection.
            var masked=(JObject)after["chaos"].DeepClone();masked["visualBoard"]=new JArray();reconnect.Apply("landscape",(long)after["stateVersion"]+1,masked,false);Check(fx.ActiveBoardProps==0,"No trap inference from raw mine/crate arrays");
            reconnect.Apply("landscape",(long)after["stateVersion"]+2,masked,true);Check(fx.ActiveBoardProps==0&&fx.ActiveStatuses==0&&fx.ActiveCount==0,"Terminal clears all landscape state");
            foreach(var a in actors.Values)Object.Destroy(a.gameObject);count++;
        }
        Object.Destroy(host);return count;
    }
}
