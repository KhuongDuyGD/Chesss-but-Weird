using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessButWeird.BuffVfx
{
    // Renderer-only mesh copies. Never copies gameplay scripts, colliders or selection state.
    internal sealed class BuffMeshEcho
    {
        private const int Limit=16;
        private readonly Transform root;
        private readonly BuffVisualAssets assets;
        private readonly MeshFilter[] meshes=new MeshFilter[Limit];
        private readonly MeshRenderer[] renderers=new MeshRenderer[Limit];
        private readonly Vector3[] offsets=new Vector3[Limit],scales=new Vector3[Limit];
        private readonly Quaternion[] rotations=new Quaternion[Limit];
        private readonly List<MeshFilter> source=new List<MeshFilter>(Limit);
        private readonly MaterialPropertyBlock tint=new MaterialPropertyBlock();
        private Quaternion original;
        private int count;
        internal BuffMeshEcho(Transform parent,BuffVisualAssets assets,string name)
        {this.assets=assets;root=new GameObject(name).transform;root.SetParent(parent,false);root.gameObject.SetActive(false);}
        internal void Capture(Transform actor)
        {
            count=0;if(!actor){Hide();return;}original=actor.rotation;source.Clear();actor.GetComponentsInChildren(false,source);
            foreach(var mesh in source)
            {
                var renderer=mesh.GetComponent<Renderer>();if(!mesh.sharedMesh||!renderer||!renderer.enabled||count>=Limit)continue;
                if(!meshes[count])
                {
                    var part=new GameObject("Echo mesh "+count,typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(root,false);
                    meshes[count]=part.GetComponent<MeshFilter>();renderers[count]=part.GetComponent<MeshRenderer>();renderers[count].sharedMaterial=assets.Ink;
                    renderers[count].shadowCastingMode=ShadowCastingMode.Off;renderers[count].receiveShadows=false;BuffVisualAssets.IgnoreRaycasts(part);
                }
                meshes[count].sharedMesh=mesh.sharedMesh;offsets[count]=mesh.transform.position-actor.position;rotations[count]=mesh.transform.rotation;scales[count]=mesh.transform.lossyScale;count++;
            }
            for(int i=0;i<Limit;i++)if(meshes[i])meshes[i].gameObject.SetActive(i<count);
        }
        internal void Pose(Vector3 origin,Quaternion rotation,Color color,float scale=1)
        {
            root.gameObject.SetActive(count>0&&color.a>.001f);var delta=rotation*Quaternion.Inverse(original);tint.SetColor("_Color",color);
            for(int i=0;i<count;i++){meshes[i].transform.SetPositionAndRotation(origin+delta*offsets[i]*scale,delta*rotations[i]);meshes[i].transform.localScale=scales[i]*scale;renderers[i].SetPropertyBlock(tint);}
        }
        internal void Hide()=>root.gameObject.SetActive(false);
    }
}
