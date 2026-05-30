using UnityEngine;

public static class ChessRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateChessGameIfNeeded()
    {
        if (Object.FindAnyObjectByType<ChessGame>())
            return;

        Chessboard chessboard = Object.FindAnyObjectByType<Chessboard>();
        if (!chessboard)
            return;

        GameObject gameObject = new GameObject("Chess Game");
        ChessGame chessGame = gameObject.AddComponent<ChessGame>();
        gameObject.transform.SetParent(chessboard.transform.parent);
    }
}
