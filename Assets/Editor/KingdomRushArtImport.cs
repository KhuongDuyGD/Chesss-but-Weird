using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Art preparation only. This does not edit the cosmetic catalog, shop, or gameplay scenes.
public static class KingdomRushArtImport
{
    private const string Root = "Assets/Skins/KingdomRushCollab";
    private static readonly string[] Prefixes = { "01_Quan_tot", "02_Quan_xe", "03_Quan_ngua", "04_Quan_tuong", "05_Quan_hau", "06_Quan_vua" };
    private static readonly string[] OriginalNames = { "Pawn", "Rook", "Knight", "Bishop", "Queen", "King" };

    [Serializable] private sealed class Palette { public MaterialSpec[] materials; }
    [Serializable] private sealed class ModelManifest { public float common_scale_from_old_heights; public float maximum_piece_footprint_m; public ModelSpec[] models; }
    [Serializable] private sealed class ModelSpec { public string name; public string material; public string texture; public string metallic_smoothness_texture; public string normal_texture; public float[] dimensions_m; public float original_type_height_m; }
    [Serializable] private sealed class MaterialSpec
    {
        public string name;
        public float[] color;
        public float metallic;
        public float roughness;
        public float[] emission;
        public float emissionStrength;
    }

    [MenuItem("Chess/Kingdom Rush/Prepare Materials and Art Review Scene")]
    public static void Prepare()
    {
        AssetDatabase.Refresh();
        Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory(Root + "/Scenes");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new InvalidOperationException("The project URP Lit shader is unavailable.");
        var palette = JsonUtility.FromJson<Palette>(File.ReadAllText(Root + "/material_palette.json"));
        var materials = new Dictionary<string, Material>();
        foreach (var spec in palette.materials)
        {
            string safeName = "KR_" + Regex.Replace(spec.name.Replace("KR • ", ""), "[^A-Za-z0-9]+", "_").Trim('_');
            string path = Root + "/Materials/" + safeName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.name = safeName;
            material.shader = shader;
            material.SetColor("_BaseColor", ToColor(spec.color));
            material.SetFloat("_Metallic", spec.metallic);
            material.SetFloat("_Smoothness", 1f - spec.roughness);
            // Thin flags and terrain remain visible from both sides.
            material.SetFloat("_Cull", 0);
            material.enableInstancing = true;
            if (spec.emissionStrength > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", ToColor(spec.emission) * spec.emissionStrength);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            else { material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.black); }
            if (spec.name == "KR • Baked meadow")
            {
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/KR_Meadow_Albedo.png"));
            }
            string lowerName = spec.name.ToLowerInvariant();
            bool meadow = lowerName.Contains("baked meadow");
            if (meadow || new[] { "stone", "basalt", "limestone", "rock", "grass", "timber", "terracotta" }.Any(lowerName.Contains))
            {
                string family = meadow || lowerName.Contains("grass") ? "Meadow" : "Stone";
                string detail = Root + "/Textures/KR_" + family + "_Detail.png";
                string normal = Root + "/Textures/KR_" + family + "_Normal.png";
                ConfigureSurfaceTexture(detail, false, false, true);
                ConfigureSurfaceTexture(normal, false, true, true);
                material.SetTexture("_DetailAlbedoMap", AssetDatabase.LoadAssetAtPath<Texture2D>(detail));
                material.SetTexture("_DetailNormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
                material.SetTextureScale("_DetailAlbedoMap", meadow ? new Vector2(48, 63) : Vector2.one);
                material.SetFloat("_DetailAlbedoMapScale", 1f);
                material.SetFloat("_DetailNormalMapScale", meadow ? .35f : .22f);
                material.DisableKeyword("_DETAIL_SCALED"); material.EnableKeyword("_DETAIL_MULX2");
            }
            EditorUtility.SetDirty(material);
            materials.Add(spec.name, material);
        }
        var manifest = JsonUtility.FromJson<ModelManifest>(File.ReadAllText(Root + "/model_manifest.json"));
        var paintedShader = shader;
        foreach (var spec in manifest.models)
        {
            ConfigureSurfaceTexture(spec.texture, true);
            ConfigureSurfaceTexture(spec.metallic_smoothness_texture, false);
            if (!string.IsNullOrEmpty(spec.normal_texture)) ConfigureSurfaceTexture(spec.normal_texture, false, true);
            string path = Root + "/Materials/" + spec.material + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(paintedShader); AssetDatabase.CreateAsset(material, path); }
            material.name = spec.material; material.shader = paintedShader;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(spec.texture));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(spec.metallic_smoothness_texture));
            material.SetFloat("_Metallic", 1f); material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            material.SetFloat("_WorkflowMode", 1f); material.SetFloat("_ReceiveShadows", 1f);
            material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.black);
            if (!string.IsNullOrEmpty(spec.normal_texture))
            {
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(spec.normal_texture));
                material.SetFloat("_BumpScale", .65f); material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Cull", 2);
            material.SetFloat("_Surface", 0f); material.SetFloat("_AlphaClip", 0f); material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.DisableKeyword("_ALPHATEST_ON");
            material.SetShaderPassEnabled("ShadowCaster", true);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material); materials[spec.material] = material;
        }
        foreach (string file in Directory.GetFiles(Root + "/Models", "*.fbx"))
        {
            string path = file.Replace('\\', '/');
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1f;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
        }
        foreach (string path in Directory.GetFiles(Root + "/Previews", "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        CreateReviewScene();
        VerifyImportedArt();
    }

    private static Color ToColor(float[] v) => new Color(v[0], v[1], v[2], v.Length > 3 ? v[3] : 1f);

    private static void ConfigureSurfaceTexture(string path, bool srgb, bool normal = false, bool repeat = false)
    {
        if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("A required surface texture path is missing.");
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (!importer) throw new InvalidOperationException("Missing surface texture: " + path);
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        var wrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        if (importer.textureType == type && importer.sRGBTexture == srgb && importer.maxTextureSize == 4096 && importer.compressionQuality == 100 && importer.mipmapEnabled && importer.anisoLevel == 16 && importer.wrapMode == wrap && importer.filterMode == FilterMode.Trilinear && importer.textureCompression == TextureImporterCompression.CompressedHQ) return;
        importer.textureType = type; importer.sRGBTexture = srgb;
        importer.maxTextureSize = 4096; importer.mipmapEnabled = true;
        importer.anisoLevel = 16; importer.wrapMode = wrap; importer.filterMode = FilterMode.Trilinear;
        importer.alphaIsTransparency = false;
        importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.compressionQuality = 100;
        importer.SaveAndReimport();
    }

    private static GameObject Spawn(string name, Vector3 position, bool faceCenter = false)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + name + ".fbx");
        if (!asset) throw new InvalidOperationException("Missing model: " + name);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        instance.name = name;
        instance.transform.position = position;
        if (faceCenter)
        {
            // The exported marker survives FBX axis conversion and unique UVs.
            var front = ArtworkForward(instance); front.y = 0; front.Normalize();
            var inward = position.z < 0 ? Vector3.forward : Vector3.back;
            instance.transform.rotation = Quaternion.FromToRotation(front, inward) * instance.transform.rotation;
        }
        return instance;
    }

    private static Vector3 ArtworkForward(GameObject root)
    {
        var marker = root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "KR_Forward");
        if (marker) return (marker.position - root.transform.position).normalized;
        Vector3 sum = Vector3.zero;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = filter.sharedMesh;
            var vertices = mesh.vertices; var normals = mesh.normals;
            var uv = mesh.uv; var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                if (uv[a].x >= .5f || uv[b].x >= .5f || uv[c].x >= .5f) continue;
                float area = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).magnitude;
                sum += filter.transform.TransformDirection(normals[a] + normals[b] + normals[c]) * area;
            }
        }
        if (sum.sqrMagnitude < .000001f) throw new InvalidOperationException(root.name + " has no identifiable artwork front.");
        return sum.normalized;
    }

    private static void CreateReviewScene()
    {
        // Run interactively only after saving the current work; batch mode has no user scene open.
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Spawn("KR_Linirea_Valley", Vector3.zero);
        Spawn("KR_Citadel_Board", Vector3.zero);
        int[] backRank = { 1, 2, 3, 4, 5, 3, 2, 1 };
        for (int team = 0; team < 2; team++)
        for (int rank = 0; rank < 2; rank++)
        for (int file = 0; file < 8; file++)
        {
            int kind = rank == 1 ? 0 : backRank[file];
            Spawn(Prefixes[kind] + (team == 0 ? "_Denas" : "_Veznan"),
                new Vector3(file - 3.5f, 0f, team == 0 ? -3.5f + rank : 3.5f - rank), true);
        }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.52f, .59f, .69f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(.56f, .70f, .80f);
        RenderSettings.fogDensity = .004f;
        var sun = new GameObject("Linirea afternoon sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 2f;
        sun.color = new Color(1f, .94f, .80f); sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        var camera = new GameObject("Art Review Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true; camera.orthographicSize = 9.4f;
        camera.transform.position = new Vector3(12, 18, -17);
        camera.transform.LookAt(new Vector3(0, 0, .5f));
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = RenderSettings.fogColor;
        camera.nearClipPlane = .1f; camera.farClipPlane = 250;
        EditorSceneManager.SaveScene(scene, Root + "/Scenes/KR_ArtReview.unity");
    }

    public static void VerifyImportedArt()
    {
        var checks = new List<string>();
        var manifest = JsonUtility.FromJson<ModelManifest>(File.ReadAllText(Root + "/model_manifest.json"));
        for (int type = 0; type < Prefixes.Length; type++)
        {
            string originalPath = "Assets/Skins/Tazji'sLowPoly/Chess_Set/Chess_" + OriginalNames[type] + ".fbx";
            var original = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(originalPath));
            Bounds expected = BoundsOf(original);
            UnityEngine.Object.DestroyImmediate(original);
            foreach (string faction in new[] { "Denas", "Veznan" })
            {
                string name = Prefixes[type] + "_" + faction;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + name + ".fbx");
                var instance = UnityEngine.Object.Instantiate(asset);
                try
                {
                    var actual = BoundsOf(instance);
                    var spec = manifest.models.Single(m => m.name == name);
                    var intended = new Vector3(spec.dimensions_m[0], spec.dimensions_m[2], spec.dimensions_m[1]);
                    if ((actual.size - intended).magnitude > .0002f || Mathf.Abs(actual.min.y) > .0001f)
                        throw new InvalidOperationException(name + " does not match its proportion-preserving delivery bounds: " + actual.size + " versus " + intended);
                    if (Mathf.Max(actual.size.x, actual.size.z) > manifest.maximum_piece_footprint_m + .0001f)
                        throw new InvalidOperationException(name + " exceeds its chess cell footprint.");
                    if (Mathf.Abs(actual.size.y - expected.size.y * manifest.common_scale_from_old_heights) > .0002f)
                        throw new InvalidOperationException(name + " does not preserve the original type height hierarchy.");
                    var renderers = instance.GetComponentsInChildren<MeshRenderer>();
                    if (renderers.Length != 1) throw new InvalidOperationException(name + " must use one combined mesh renderer.");
                    foreach (var material in renderers.SelectMany(r => r.sharedMaterials))
                        if (!material || material.shader.name != "Universal Render Pipeline/Lit" || !material.GetTexture("_BaseMap") || !material.GetTexture("_MetallicGlossMap") || !AssetDatabase.GetAssetPath(material).StartsWith(Root + "/Materials/", StringComparison.Ordinal))
                            throw new InvalidOperationException(name + " has an unmapped material: " + (material ? material.name : "null"));
                    var texture = (TextureImporter)AssetImporter.GetAtPath(spec.texture);
                    if (!texture.mipmapEnabled || texture.anisoLevel != 16 || texture.filterMode != FilterMode.Trilinear)
                        throw new InvalidOperationException(name + " has incorrect oblique-view texture filtering.");
                    if (!instance.GetComponentsInChildren<Transform>().Any(t => t.name == "KR_Forward"))
                        throw new InvalidOperationException(name + " has no explicit forward marker.");
                    checks.Add("PASS " + name + ": volumetric mesh, uniform set scale, footprint <= 0.82 m, ground pivot, 1 renderer, Lit/PBR maps, mipmaps + anisotropy 16, explicit facing marker");
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
        }
        var meadow = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/KR_Baked_meadow.mat");
        if (!meadow || !meadow.GetTexture("_BaseMap")) throw new InvalidOperationException("Meadow texture is missing.");
        checks.Add("PASS meadow albedo texture is assigned");
        var pieces = EditorSceneManager.GetActiveScene().GetRootGameObjects()
            .Where(o => Prefixes.Any(prefix => o.name.StartsWith(prefix, StringComparison.Ordinal))).ToArray();
        if (pieces.Length != 32) throw new InvalidOperationException("The art review must contain 32 chess pieces.");
        foreach (var piece in pieces)
        {
            var front = ArtworkForward(piece); front.y = 0; front.Normalize();
            var inward = piece.transform.position.z < 0 ? Vector3.forward : Vector3.back;
            if (Vector3.Dot(front, inward) < .999f)
                throw new InvalidOperationException(piece.name + " does not face the opposing army.");
        }
        checks.Add("PASS all 32 pieces face inward; opposing files are literally face-to-face");
        File.WriteAllLines("ArtSource/KingdomRush/unity_import_validation.txt", checks);
        Debug.Log("[KingdomRushArt] PASS " + checks.Count + " import checks. Art review scene: " + Root + "/Scenes/KR_ArtReview.unity");
    }

    private static Bounds BoundsOf(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) throw new InvalidOperationException(root.name + " has no renderers.");
        Bounds result = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) result.Encapsulate(renderer.bounds);
        return result;
    }
}
