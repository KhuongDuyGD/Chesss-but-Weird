using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.EngineDiagnostics;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using Newtonsoft.Json.Linq;
using ChessButWeird.CinematicDemo.FinalSprint;
using ChessButWeird.CinematicIntegration.M4A;

namespace CBWRelease
{
    // Explicit batch-only local demo builder. Never runs on import or ordinary game startup.
    public sealed class RoyalDemoBuild : IPreprocessBuildWithReport, IProcessSceneWithReport
    {
        public const string Scene="Assets/CinematicDemo/FinalSprint/Scenes/RoyalAscension_CommittedDemo.unity";
        public int callbackOrder=>-1000;
        public void OnPreprocessBuild(BuildReport report)
        {
            if(EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==Scene)&&!report.summary.options.HasFlag(BuildOptions.Development))
                throw new BuildFailedException("Royal Ascension fixture scene is development-only. Remove it from ordinary release scenes.");
        }
        public void OnProcessScene(UnityEngine.SceneManagement.Scene scene,BuildReport report)
        { if(scene.path==Scene&&report!=null&&!report.summary.options.HasFlag(BuildOptions.Development))throw new BuildFailedException("Development demo cannot be included in release"); }
        static string Arg(string name)=>Environment.GetCommandLineArgs().FirstOrDefault(x=>x.StartsWith(name+"=",StringComparison.Ordinal))?.Substring(name.Length+1);
        static void Need(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        public static void Verify()
        {
            Need(Application.isBatchMode,"Explicit batch invocation required");
            var result=new JObject{["utc"]=DateTime.UtcNow.ToString("O"),["unity"]=Application.unityVersion};
            var scene=EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
            var root=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FinalDemoRoot>(true)).Single();
            Need(root.ritualPrefab&&root.one&&root.ten,"Serialized cinematic and committed fixtures required");
            Need(!new GachaCinematicFeatureConfig().EnableRoyalAscensionCinematic,"Production feature default must be OFF");
            Need(!EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==Scene),"Demo must not enter the ordinary build scene list");
            Need(root.bindings.Length==10&&root.bindings.Select(b=>b.itemId).Distinct().Count()==10,"Ten distinct synthetic identities required");
            Need(root.bindings.All(b=>b.itemId.StartsWith("fd00000000000000000000",StringComparison.Ordinal)),"Only explicit synthetic demo identities permitted");
            Need(root.bindings.Count(b=>!string.IsNullOrEmpty(b.previewGuid))==7,"Seven original candidate artwork associations required");
            Need(root.bindings.Where(b=>new[]{"A07","A08","A09"}.Contains(b.alias)).All(b=>string.IsNullOrEmpty(b.previewGuid)&&string.IsNullOrEmpty(b.localId)),"Missing art must stay missing");
            var deps=AssetDatabase.GetDependencies(Scene,true);
            Need(deps.All(p=>!p.Contains("TestLab")&&!p.Contains("/Validation/")),"No historical evidence dependency");
            foreach(var path in deps.Where(p=>p.EndsWith(".prefab")||p.EndsWith(".fbx")))
            {
                var go=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(go)Need(go.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"Missing MonoBehaviour: "+path);
            }
            result["status"]="PASS";result["dependencies"]=JArray.FromObject(deps);result["productionFeatureOff"]=true;result["ordinarySceneListUntouched"]=true;
            string output=Arg("-cbwEvidence");Need(!string.IsNullOrEmpty(output),"External -cbwEvidence required");Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,result.ToString());
            Debug.Log("CBW_RELEASE_DEMO_VERIFY_PASS");
        }
        public static void Build()
        {
            Verify();string output=Arg("-cbwPlayer");Need(!string.IsNullOrEmpty(output),"Explicit -cbwPlayer=<fresh external exe> required");
            output=Path.GetFullPath(output);string project=Path.GetFullPath(Application.dataPath+"/..");
            Need(!output.StartsWith(project+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&!File.Exists(output),"Fresh output outside project required");
            Need(EditorUserBuildSettings.activeBuildTarget==BuildTarget.StandaloneWindows64,"Windows target required");
            var settings=AddressableAssetSettingsDefaultObject.Settings;Need(settings&&!settings.BuildRemoteCatalog,"Local Addressables only");
            bool diagnostics=EngineDiagnosticsSettings.enabled;
            var preloaded=PlayerSettings.GetPreloadedAssets();
            var settingsFiles=Directory.GetFiles(project+"/ProjectSettings").ToDictionary(p=>p,File.ReadAllBytes);
            var parentType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Connect.UnityConnectSettings",false)).FirstOrDefault(t=>t!=null);
            var parent=parentType?.GetProperty("enabled",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
            object parentBefore=parent?.GetValue(null);
            var previous=AddressablesPlayerBuildProcessor.BuildAddressablesOverride;
            var result=new JObject{["utc"]=DateTime.UtcNow.ToString("O"),["engineDiagnosticsBefore"]=diagnostics};
            try
            {
                AddressableAssetSettings.BuildPlayerContent(out var content);Need(content!=null&&string.IsNullOrEmpty(content.Error),"Local content build: "+content?.Error);
                AddressablesPlayerBuildProcessor.BuildAddressablesOverride=s=>null;
                EngineDiagnosticsSettings.enabled=false;AssetDatabase.SaveAssets();Need(!EngineDiagnosticsSettings.enabled,"Demo build diagnostics must be OFF");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development,extraScriptingDefines=new[]{"ROYAL_ASCENSION_DEMO"}});
                result["result"]=report.summary.result.ToString();result["errors"]=report.summary.totalErrors;result["warnings"]=report.summary.totalWarnings;result["bytes"]=report.summary.totalSize;result["seconds"]=report.summary.totalTime.TotalSeconds;
                Need(report.summary.result==BuildResult.Succeeded,"Demo Player build failed");
            }
            finally
            {
                AddressablesPlayerBuildProcessor.BuildAddressablesOverride=previous;
                PlayerSettings.SetPreloadedAssets(preloaded);
                EngineDiagnosticsSettings.enabled=diagnostics;if(parent!=null)parent.SetValue(null,parentBefore);AssetDatabase.SaveAssets();
                var drift=settingsFiles.Where(p=>!File.Exists(p.Key)||!File.ReadAllBytes(p.Key).SequenceEqual(p.Value)).Select(p=>p.Key).ToArray();
                result["settingsRestored"]=drift.Length==0;result["settingsDrift"]=JArray.FromObject(drift);
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(Arg("-cbwEvidence")),"PR_B_BUILD.json"),result.ToString());
                Need(drift.Length==0&&EngineDiagnosticsSettings.enabled==diagnostics,"Build settings did not restore exactly; inspect isolated worktree");
            }
        }
        public static void VerifyReleaseIsolation()
        {
            Need(Application.isBatchMode,"Explicit batch invocation required");
            string output=Arg("-cbwReleaseOutput");Need(!string.IsNullOrEmpty(output)&&!Directory.Exists(output),"Fresh external release verification output required");
            var preloaded=PlayerSettings.GetPreloadedAssets();var settingsBefore=File.ReadAllBytes("ProjectSettings/ProjectSettings.asset");
            Directory.CreateDirectory(output);
            var compiled=UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(new UnityEditor.Build.Player.ScriptCompilationSettings{group=BuildTargetGroup.Standalone,target=BuildTarget.StandaloneWindows64,options=UnityEditor.Build.Player.ScriptCompilationOptions.None},output+"/Scripts");
            var assembly=compiled.assemblies.Single(p=>Path.GetFileName(p)=="Assembly-CSharp.dll");
            if(!File.Exists(assembly))assembly=Path.Combine(output+"/Scripts",assembly);
            var bytes=System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(assembly));
            string[] forbidden={"FinalDemoRoot","M35RoyalController","NativeSpriteLoader","DemoOnlyAccount"};
            Need(forbidden.All(n=>!bytes.Contains(n)),"Demo type names must be absent from release assembly metadata");
            Need(bytes.Contains("GachaMenuController")&&bytes.Contains("GachaService"),"Ordinary game/gacha code must remain compiled");
            bool sawGuard=false;string thrown=null;BuildReport report=null;
            Application.LogCallback callback=(message,stack,type)=>{if(message.Contains("Development demo cannot be included in release"))sawGuard=true;};
            var previous=AddressablesPlayerBuildProcessor.BuildAddressablesOverride;
            try
            {
                AddressablesPlayerBuildProcessor.BuildAddressablesOverride=s=>null;Application.logMessageReceived+=callback;
                try{report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=output+"/RejectedPlayer/RoyalDemo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});}
                catch(Exception ex){thrown=ex.ToString();sawGuard|=thrown.Contains("Development demo cannot be included in release");}
            }
            finally{Application.logMessageReceived-=callback;AddressablesPlayerBuildProcessor.BuildAddressablesOverride=previous;PlayerSettings.SetPreloadedAssets(preloaded);AssetDatabase.SaveAssets();}
            bool restored=settingsBefore.SequenceEqual(File.ReadAllBytes("ProjectSettings/ProjectSettings.asset"));
            bool rejected=sawGuard&&!File.Exists(output+"/RejectedPlayer/RoyalDemo.exe")&&(report==null||report.summary.result==BuildResult.Failed);
            var result=new JObject{["utc"]=DateTime.UtcNow.ToString("O"),["releaseCompile"]=true,["forbiddenTypeNamesAbsent"]=JArray.FromObject(forbidden),["legacyGameCodePresent"]=true,["expectedRejectionObserved"]=sawGuard,["rejectedReleaseDemo"]=rejected,["settingsRestored"]=restored,["result"]=report?.summary.result.ToString(),["exception"]=thrown};
            File.WriteAllText(Arg("-cbwEvidence"),result.ToString());Need(rejected&&restored,"Release-negative guard or exact settings restoration failed");
            Debug.Log("CBW_RELEASE_ISOLATION_PASS");
        }
    }
}
