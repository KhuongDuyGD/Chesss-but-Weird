using UnityEngine;

public class ChessGame : MonoBehaviour
{
    [SerializeField] private Chessboard chessboard;
    [SerializeField] private Transform piecesRoot;

    private readonly ChessPiece[,] pieces = new ChessPiece[8, 8];
    private PieceTeam currentTurn = PieceTeam.White;
    private ChessPiece selectedPiece;

    public PieceTeam CurrentTurn => currentTurn;
    public ChessPiece SelectedPiece => selectedPiece;

    private void Awake()
    {
        if (!chessboard)
            chessboard = FindFirstObjectByType<Chessboard>();
    }

    private void Start()
    {
        RefreshPieceMap();
    }

    public void RefreshPieceMap()
    {
        ClearPieceMap();

        Transform searchRoot = piecesRoot ? piecesRoot : chessboard ? chessboard.transform : transform;
        ChessPiece[] scenePieces = searchRoot.GetComponentsInChildren<ChessPiece>(true);
        for (int i = 0; i < scenePieces.Length; i++)
        {
            ChessPiece piece = scenePieces[i];
            if (!chessboard || !chessboard.IsValidTile(piece.boardPosition))
                continue;

            pieces[piece.boardPosition.x, piece.boardPosition.y] = piece;
        }

        currentTurn = PieceTeam.White;
        selectedPiece = null;
    }

    public bool TrySelectPiece(ChessPiece piece)
    {
        if (!piece || piece.team != currentTurn)
            return false;

        selectedPiece = piece;
        return true;
    }

    public bool TryMoveSelectedPiece(Vector2Int destination)
    {
        if (!selectedPiece || !chessboard || !chessboard.IsValidTile(destination))
            return false;

        Vector2Int from = selectedPiece.boardPosition;
        if (!ChessMoveRules.IsLegalMove(selectedPiece.type, selectedPiece.team, from, destination, pieces))
            return false;

        pieces[from.x, from.y] = null;
        pieces[destination.x, destination.y] = selectedPiece;

        Vector3 targetPosition = chessboard.GetTileCenterWorld(destination);
        targetPosition.y = selectedPiece.transform.position.y;
        selectedPiece.transform.position = targetPosition;
        selectedPiece.boardPosition = destination;
        selectedPiece.hasMoved = true;

        selectedPiece = null;
        currentTurn = currentTurn == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        return true;
    }

    public void ClearSelection()
    {
        selectedPiece = null;
    }

    private void ClearPieceMap()
    {
        for (int x = 0; x < pieces.GetLength(0); x++)
            for (int y = 0; y < pieces.GetLength(1); y++)
                pieces[x, y] = null;
    }
}
