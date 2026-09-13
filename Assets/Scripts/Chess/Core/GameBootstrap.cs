using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameBootstrap : MonoBehaviour
{
    public const string MenuScene = "MainMenu";
    public const string MatchScene = "ChessMatch";

    private IEnumerator Start()
    {
        var loading = LoadingUI.Create(transform);
        yield return AssetLoader.Initialize(p => loading.Report(p * .6f, "Starting Chess but Weird..."));
        yield return CoreArtworkCache.Prepare("AuthUI");
        loading.Report(1, "Ready");
        yield return loading.Finish();
        if (gameObject.scene.name == "Boot")
        {
            yield return SceneManager.LoadSceneAsync(MenuScene);
            yield break;
        }
        if (!FindAnyObjectByType<ChessGame>())
        {
            var host = new GameObject("Chess Game Services");
            DontDestroyOnLoad(host);
            host.AddComponent<ChessGame>();
        }
        Destroy(gameObject);
    }
}
