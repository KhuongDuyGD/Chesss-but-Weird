using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessButWeird.Editor
{
    public static class ApplyVoidPixBlinkSkybox
    {
        private const string MaterialPath = "Assets/Skyboxes/VoidPixFreePack/VoidPix_Blink_Skybox.mat";
        private const string ScenePath = "Assets/Scenes/ChessClassic.unity";

        [MenuItem("Tools/Chess But Weird/Apply VoidPix Blink Skybox")]
        public static void ApplyFromMenu()
        {
            ApplySkybox(saveScene: true);
        }

        private static void ApplySkybox(bool saveScene)
        {
            ConfigureTexture("front");
            ConfigureTexture("back");
            ConfigureTexture("left");
            ConfigureTexture("right");
            ConfigureTexture("top");
            ConfigureTexture("bottom");

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Skybox/6 Sided"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = Shader.Find("Skybox/6 Sided");
            }

            material.SetTexture("_FrontTex", LoadTexture("front"));
            material.SetTexture("_BackTex", LoadTexture("back"));
            material.SetTexture("_LeftTex", LoadTexture("left"));
            material.SetTexture("_RightTex", LoadTexture("right"));
            material.SetTexture("_UpTex", LoadTexture("top"));
            material.SetTexture("_DownTex", LoadTexture("bottom"));
            material.SetColor("_Tint", Color.white);
            material.SetFloat("_Exposure", 1f);
            material.SetFloat("_Rotation", 0f);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            RenderSettings.skybox = material;
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(scene);

            if (saveScene)
            {
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"Applied VoidPix blink skybox to {ScenePath}.");
        }

        private static Texture2D LoadTexture(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(name));
        }

        private static void ConfigureTexture(string name)
        {
            var path = TexturePath(name);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"Could not configure missing skybox texture: {path}");
                return;
            }

            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        private static string TexturePath(string name)
        {
            return $"Assets/Skyboxes/VoidPixFreePack/blink/{name}.png";
        }
    }
}
