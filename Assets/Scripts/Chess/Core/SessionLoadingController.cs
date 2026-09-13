using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Independent coroutine host: ChessGame.PrepareGame calls StopAllCoroutines on itself.
public sealed class SessionLoadingController : MonoBehaviour
{
    private ChessGame game;
    private LoadingUI loading;
    private Coroutine preparation;
    private Stack<IEnumerator> activeIterators;
    private int generation;
    private float progress;
    public bool IsLoading => loading != null;

    public static SessionLoadingController For(ChessGame game)
    {
        var loader = game.GetComponent<SessionLoadingController>();
        if (!loader) loader = game.gameObject.AddComponent<SessionLoadingController>();
        loader.game = game;
        return loader;
    }

    public void Begin()
    {
        Cancel();
        progress = 0f;
        if (game) game.SetContentLoading(true);
        loading = LoadingUI.Create(transform);
        loading.Report(0f, "Preparing your chess table...");
        preparation = StartCoroutine(Guard(Prepare(++generation)));
    }

    public void Cancel()
    {
        generation++;
        if (preparation != null) StopCoroutine(preparation);
        preparation = null;
        DisposeActiveIterators();
        if (loading)
        {
            loading.gameObject.SetActive(false);
            Destroy(loading.gameObject);
        }
        loading = null;
        if (game) game.SetContentLoading(false);
    }

    private IEnumerator Prepare(int ticket)
    {
        // Allow the blocker to render; subsequent progress is completed work only.
        yield return null;
        if (!ContinueOrCancel(ticket)) yield break;
        yield return LoadingManager.For(game).Initialize(
            p => Report(ticket, p * .15f, "Preparing your chess table..."));
        if (!ContinueOrCancel(ticket)) yield break;
        Report(ticket, .15f, "Preparing artwork...");
        yield return CoreArtworkCache.Prepare("CoreUI");
        if (!ContinueOrCancel(ticket)) yield break;
        HandDrawnMenuAssets artwork = game.GetComponent<HandDrawnMenuAssets>();
        if (!artwork) artwork = game.gameObject.AddComponent<HandDrawnMenuAssets>();
        var steps = new List<Action>(artwork.CreatePreparationSteps());
        steps.Insert(0, () => { var font = ChessFontCatalog.TmpFont; });
        steps.Add(() => game.PrepareAuthenticatedBoard());
        steps.Add(() => game.CompleteAuthenticatedSession());
        for (int i = 0; i < steps.Count; i++)
        {
            if (!ContinueOrCancel(ticket)) yield break;
            string message = i == steps.Count - 1 ? "Setting up the menu..." : i == steps.Count - 2 ? "Preparing the board..." : "Preparing artwork...";
            Report(ticket, progress, message);
            steps[i]();
            progress = .15f + .85f * (i + 1f) / steps.Count;
            Report(ticket, progress, message);
            yield return null;
        }
        if (ticket != generation || !loading) yield break;
        Report(ticket, 1f, "Ready");
        yield return loading.Finish();
        if (ticket != generation) yield break;
        preparation = null;
        loading = null;
        if (game) game.SetContentLoading(false);
    }

    private bool ContinueOrCancel(int ticket)
    {
        if (ticket != generation || !game) return false;
        if (PlayerAuthService.IsAuthenticated || PlayerAuthService.IsGuestSession) return true;
        Cancel();
        return false;
    }

    private void Report(int ticket, float value, string message)
    {
        if (ticket != generation || !loading) return;
        progress = Mathf.Clamp01(value);
        loading.Report(progress, message);
    }

    private IEnumerator Guard(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>();
        activeIterators = stack;
        stack.Push(routine);
        while (stack.Count > 0)
        {
            object next = null;
            Exception failure = null;
            bool more = false;
            try
            {
                IEnumerator current = stack.Peek();
                more = current.MoveNext();
                if (more) next = current.Current;
            }
            catch (Exception ex) { failure = ex; }

            // Cancel can be called by the iterator itself when authentication expires.
            if (!ReferenceEquals(activeIterators, stack)) yield break;

            if (failure != null)
            {
                if (!ReferenceEquals(activeIterators, stack)) yield break;
                DisposeIteratorStack(stack);
                activeIterators = null;
                Debug.LogException(failure);
                preparation = null;
                if (loading)
                    loading.Error("Preparation could not finish. Please try again.", Begin, ReturnToLogin);
                else if (game)
                    game.SetContentLoading(false);
                yield break;
            }

            if (!more)
            {
                (stack.Pop() as IDisposable)?.Dispose();
                continue;
            }
            if (next is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }
            yield return next;
        }
        if (ReferenceEquals(activeIterators, stack)) activeIterators = null;
    }

    private void DisposeActiveIterators()
    {
        var stack = activeIterators;
        activeIterators = null;
        if (stack != null) DisposeIteratorStack(stack);
    }

    private static void DisposeIteratorStack(Stack<IEnumerator> stack)
    {
        while (stack.Count > 0)
            (stack.Pop() as IDisposable)?.Dispose();
    }

    private void ReturnToLogin()
    {
        Cancel();
        if (!game) return;
        PlayerAuthService.Logout();
        AuthController.Create(game, game.QueueAuthenticatedSession);
    }

    private void OnDestroy() => Cancel();
}
