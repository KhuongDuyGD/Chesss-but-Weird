using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessButWeird.BuffVfx
{
    internal sealed class BuffVisualAssets : IDisposable
    {
        internal readonly Material Ink;
        internal readonly Material[] PropMaterials;
        internal readonly Mesh Shard;
        private readonly Dictionary<string, Mesh> glyphs = new Dictionary<string, Mesh>();
        internal BuffVisualAssets()
        {
            var shader = Resources.Load<Shader>("BuffVfx/BoardInk");
            if (!shader) throw new InvalidOperationException("Buff ink shader missing.");
            var propShader = Resources.Load<Shader>("BuffVfx/PropInk");
            if(!propShader)throw new InvalidOperationException("Buff prop shader missing.");
            Ink = new Material(shader) { name = "Buff shared ink", hideFlags = HideFlags.DontSave };
            PropMaterials = new Material[4];
            var colors = new[] { new Color(.16f,.19f,.24f), new Color(.47f,.28f,.19f), new Color(.91f,.66f,.27f), new Color(.37f,.71f,.73f) };
            for (int i=0;i<4;i++) { PropMaterials[i] = new Material(propShader) { name = "Buff prop palette " + i, hideFlags = HideFlags.DontSave }; PropMaterials[i].color = colors[i]; }
            Shard = new Mesh { name = "Buff ink shard", hideFlags = HideFlags.DontSave };
            Shard.vertices = new[] { Vector3.up,Vector3.right,Vector3.forward,Vector3.left,Vector3.back,Vector3.down };
            Shard.triangles = new[] {0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1}; Shard.RecalculateNormals();
            var white = new Color[6]; for(int i=0;i<6;i++)white[i]=Color.white; Shard.colors=white;
        }
        internal static void IgnoreRaycasts(GameObject go)
        {
            int layer = LayerMask.NameToLayer("Ignore Raycast");
            if (layer >= 0) foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var collider in go.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }
        internal LineRenderer Line(Transform parent, string name, float width, int points, bool loop = false)
        {
            var go = new GameObject(name, typeof(LineRenderer)); go.transform.SetParent(parent, false);
            var line = go.GetComponent<LineRenderer>(); line.sharedMaterial = Ink; line.useWorldSpace = false;
            line.widthMultiplier = width; line.positionCount = points; line.loop = loop; line.numCapVertices=2; line.numCornerVertices=2;
            line.shadowCastingMode=ShadowCastingMode.Off; line.receiveShadows=false; IgnoreRaycasts(go); return line;
        }
        internal GameObject Prop(string name, Transform parent)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var source = Resources.Load<GameObject>("BuffVfx/Models/" + name);
            if (!source) { Debug.LogWarning("[Buff VFX] Model unavailable: "+name); return null; }
            var go = UnityEngine.Object.Instantiate(source, parent); go.name = name + " mounted prop";
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    // OBJ import can combine all paint groups into one renderer with submeshes.
                    string part=(materials[i]?materials[i].name:renderer.name).ToLowerInvariant();
                    int palette=part.Contains("wood")?1:part.Contains("brass")?2:part.Contains("glass")?3:0;
                    materials[i]=PropMaterials[palette];
                }
                renderer.sharedMaterials=materials;
                renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            }
            if(name=="SupplyCrate") {var lid=Prop("CrateLid",go.transform);if(lid)lid.transform.localPosition=new Vector3(0,.5f,-.27f);}
            IgnoreRaycasts(go); return go;
        }
        internal Mesh Glyph(string name)
        {
            if(glyphs.TryGetValue(name,out var cached))return cached;
            var v=new List<Vector3>(); var t=new List<int>();
            void Stroke(float x,float y,float xx,float yy,float width=.065f)
            {
                Vector3 a=new Vector3(x,y,0),b=new Vector3(xx,yy,0),d=b-a; var n=new Vector3(-d.y,d.x,0).normalized*width;
                int i=v.Count; v.Add(a-n);v.Add(a+n);v.Add(b+n);v.Add(b-n);t.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
            }
            void Ring(float radius,int count=24)
            {for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;Stroke(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,.04f);}}
            if(name=="counterPlate") {v.AddRange(new[]{new Vector3(-2.7f,-.9f,0),new Vector3(-2.7f,.9f,0),new Vector3(2.7f,.9f,0),new Vector3(2.7f,-.9f,0)});t.AddRange(new[]{0,1,2,0,2,3});}
            else if(name=="tilePlate") {v.AddRange(new[]{new Vector3(-.48f,-.48f,0),new Vector3(-.48f,.48f,0),new Vector3(.48f,.48f,0),new Vector3(.48f,-.48f,0)});t.AddRange(new[]{0,1,2,0,2,3});}
            else if(name=="lightning") {Stroke(.25f,.8f,-.35f,0);Stroke(-.35f,0,.3f,0);Stroke(.3f,0,-.2f,-.8f);}
            else if(name=="swap") {Stroke(-.7f,.3f,.7f,.3f);Stroke(.7f,.3f,.35f,.65f);Stroke(.7f,-.3f,-.7f,-.3f);Stroke(-.7f,-.3f,-.35f,-.65f);}
            else if(name=="log") {Ring(.45f);Stroke(-.7f,.5f,-.7f,-.5f);Stroke(.7f,.5f,.7f,-.5f);Stroke(-.7f,.5f,.7f,.5f);Stroke(-.7f,-.5f,.7f,-.5f);}
            else if(name=="evade") {Stroke(-.7f,-.5f,.65f,.55f);Stroke(.65f,.55f,.1f,.55f);Stroke(.65f,.55f,.65f,0);Stroke(-.7f,.15f,-.25f,.5f);}
            else if(name=="ghost") {Ring(.5f);Stroke(-.5f,0,-.6f,-.65f);Stroke(-.6f,-.65f,0,-.4f);Stroke(0,-.4f,.6f,-.65f);Stroke(.6f,-.65f,.5f,0);Stroke(-.2f,.1f,-.2f,.27f);Stroke(.2f,.1f,.2f,.27f);}
            else if(name=="truck") {Stroke(-.7f,-.15f,.7f,-.15f);Stroke(-.7f,-.15f,-.7f,.5f);Stroke(-.7f,.5f,.25f,.5f);Stroke(.25f,.5f,.7f,-.15f);Stroke(-.4f,-.2f,-.4f,-.55f,.13f);Stroke(.4f,-.2f,.4f,-.55f,.13f);}
            else if(name=="poison") {Ring(.5f);Stroke(-.25f,.2f,.25f,-.3f);Stroke(.25f,.2f,-.25f,-.3f);Stroke(0,.5f,0,.85f);Stroke(-.5f,-.15f,-.8f,-.35f);Stroke(.5f,-.15f,.8f,-.35f);}
            else if(name=="lock") {Stroke(-.5f,-.5f,.5f,-.5f);Stroke(-.5f,-.5f,-.5f,.2f);Stroke(.5f,-.5f,.5f,.2f);Stroke(-.5f,.2f,.5f,.2f);Stroke(-.3f,.2f,-.3f,.65f);Stroke(-.3f,.65f,.3f,.65f);Stroke(.3f,.65f,.3f,.2f);Stroke(0,-.1f,0,-.3f);}
            else if(name=="arrows") {foreach(var d in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right}){var n=new Vector2(-d.y,d.x);Stroke(0,0,d.x*.75f,d.y*.75f);var a=d*.75f;var b=d*.45f+n*.2f;var c=d*.45f-n*.2f;Stroke(a.x,a.y,b.x,b.y);Stroke(a.x,a.y,c.x,c.y);}}
            else if(name=="commander") {Stroke(-.6f,.7f,-.6f,-.25f);Stroke(-.6f,-.25f,0,-.75f);Stroke(0,-.75f,.6f,-.25f);Stroke(.6f,-.25f,.6f,.7f);Stroke(.6f,.7f,-.6f,.7f);Stroke(-.35f,0,0,.3f);Stroke(0,.3f,.35f,0);}
            else if(name=="leap") {Stroke(-.65f,-.6f,-.65f,.55f);Stroke(-.65f,.55f,.6f,.55f);Stroke(.6f,.55f,.3f,.8f);Stroke(.6f,.55f,.3f,.3f);}
            else if(name=="peace") {Ring(.65f);Stroke(0,.65f,0,-.65f);Stroke(0,0,-.48f,-.45f);Stroke(0,0,.48f,-.45f);}
            else if(name=="passenger") {Stroke(-.7f,.7f,.7f,.7f);Stroke(-.7f,.7f,-.7f,-.6f);Stroke(.7f,.7f,.7f,-.6f);Stroke(-.7f,-.6f,.7f,-.6f);Stroke(-.3f,.1f,.3f,.1f);Stroke(0,-.2f,0,.4f);}
            else if(name=="fortress") {Stroke(-.7f,-.6f,.7f,-.6f);Stroke(-.7f,-.6f,-.7f,.65f);Stroke(-.7f,.65f,-.3f,.3f);Stroke(-.3f,.3f,0,.65f);Stroke(0,.65f,.3f,.3f);Stroke(.3f,.3f,.7f,.65f);Stroke(.7f,.65f,.7f,-.6f);}
            else if(name=="hourglass") {Stroke(-.6f,.7f,.6f,.7f);Stroke(-.6f,-.7f,.6f,-.7f);Stroke(-.5f,.6f,.5f,-.6f);Stroke(.5f,.6f,-.5f,-.6f);}
            else if(name!=null&&name.StartsWith("unit")) {string kind=name.Substring(4);Ring(kind=="Pawn"?.35f:.55f);Stroke(-.65f,-.7f,.65f,-.7f);if(kind=="Knight"){Stroke(-.3f,.35f,.5f,.6f);Stroke(.5f,.6f,.3f,-.6f);}else if(kind=="Bishop"){Stroke(-.4f,-.3f,.4f,.55f);}else if(kind=="Rook"){Stroke(-.6f,.6f,.6f,.6f);Stroke(-.6f,.6f,-.6f,.25f);Stroke(.6f,.6f,.6f,.25f);}else if(kind=="King"){Stroke(0,.35f,0,.9f);Stroke(-.25f,.7f,.25f,.7f);}else if(kind=="Queen"){Stroke(-.6f,.65f,0,.3f);Stroke(0,.3f,.6f,.65f);}}
            else if(name=="fang") { Stroke(-.7f,.7f,-.35f,-.6f);Stroke(-.35f,-.6f,0,.35f);Stroke(0,.35f,.35f,-.6f);Stroke(.35f,-.6f,.7f,.7f);Stroke(-.6f,.45f,.6f,.45f); }
            else if(name=="fracture") { Stroke(-.65f,.45f,-.5f,-.65f);Stroke(-.5f,-.65f,.5f,-.65f);Stroke(.5f,-.65f,.65f,.45f);Stroke(-.65f,.45f,-.2f,.1f);Stroke(-.2f,.1f,0,.8f);Stroke(0,.8f,.2f,.1f);Stroke(.2f,.1f,.65f,.45f);Stroke(.15f,.5f,-.1f,-.15f);Stroke(-.1f,-.15f,.18f,-.3f); }
            else if(name=="bomb") {Ring(.53f);Stroke(.2f,.5f,.55f,.85f);Stroke(.55f,.85f,.8f,.65f);Stroke(-.25f,.05f,.25f,.05f);Stroke(0,-.2f,0,.3f);}
            else if(name=="reticle") {Ring(.52f);Stroke(-.85f,0,-.28f,0);Stroke(.28f,0,.85f,0);Stroke(0,-.85f,0,-.28f);Stroke(0,.28f,0,.85f);}
            else if(name=="cannon") {Stroke(-.7f,-.4f,.65f,-.4f);Stroke(-.6f,-.4f,-.4f,.25f);Stroke(-.4f,.25f,.65f,.55f);Stroke(.65f,.55f,.75f,.2f);Stroke(.75f,.2f,-.2f,0);Stroke(-.3f,-.3f,-.3f,-.65f);Stroke(.4f,-.3f,.4f,-.65f);}
            else {Stroke(-.7f,-.55f,.7f,-.55f);Stroke(-.7f,-.55f,-.8f,.5f);Stroke(-.8f,.5f,-.35f,.1f);Stroke(-.35f,.1f,0,.8f);Stroke(0,.8f,.35f,.1f);Stroke(.35f,.1f,.8f,.5f);Stroke(.8f,.5f,.7f,-.55f);Stroke(-.4f,-.25f,.4f,-.25f);}
            var mesh=new Mesh {name="Buff sigil "+name,hideFlags=HideFlags.DontSave};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();
            var colors=new Color[v.Count];for(int i=0;i<colors.Length;i++)colors[i]=Color.white;mesh.colors=colors;glyphs[name]=mesh;return mesh;
        }
        public void Dispose()
        {UnityEngine.Object.Destroy(Ink);foreach(var material in PropMaterials)UnityEngine.Object.Destroy(material);UnityEngine.Object.Destroy(Shard);foreach(var mesh in glyphs.Values)UnityEngine.Object.Destroy(mesh);}
    }
}
