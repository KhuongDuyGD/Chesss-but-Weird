using System.Collections;
using UnityEngine;

// Also covers removal of the service component while Destroy(instance) is pending.
public sealed class DeferredAssetRelease : MonoBehaviour
{
    private static bool quitting;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { quitting = false; Application.quitting -= Quit; Application.quitting += Quit; }
    private static void Quit() => quitting = true;

    public static void Schedule(params AssetLoader[] scopes)
    {
        if (quitting) { foreach (var scope in scopes) scope?.Dispose(); return; }
        var owner = new GameObject("Release departed match content");
        DontDestroyOnLoad(owner);
        owner.AddComponent<DeferredAssetRelease>().StartCoroutine(Release(scopes, owner));
    }
    private static IEnumerator Release(AssetLoader[] scopes, GameObject owner)
    {
        yield return null;
        foreach (var scope in scopes) scope?.Dispose();
        Destroy(owner);
    }
}
