using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChessButWeird.BuffVfx;

public static class BuffVfxGalleryMenu
{
    [MenuItem("Chess/CHAOS/Buff VFX Gallery (Additive)")]
    public static void Open()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        scene.name="Buff VFX Developer Gallery";
        var host=new GameObject("Buff VFX Developer Gallery");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host,scene);
        var gallery=host.AddComponent<BuffVfxDebugGallery>();
        string root="Assets/Skins/Tazji'sLowPoly/Chess_Set/";
        gallery.PieceModels=new GameObject[6];
        string[] kinds={"King","Queen","Rook","Bishop","Knight","Pawn"};
        for(int i=0;i<kinds.Length;i++)gallery.PieceModels[i]=AssetDatabase.LoadAssetAtPath<GameObject>(root+"Chess_"+kinds[i]+".fbx");
        Selection.activeGameObject=host;
        Debug.Log("Buff VFX gallery created in a separate additive scene. Enter Play Mode; no gameplay state is attached.");
    }
}
