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
        Debug.Log("Mouse Click0");
        RefreshPieceMap();
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Mouse.current == null) return;
        
        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("Mouse Click1");
            Vector2Int hoveredTile = chessboard.HoveredTile;
            if (chessboard.IsValidTile(hoveredTile))
            {
                Debug.Log("Mouse Click2");
                ChessPiece pieceAtTile = pieces[hoveredTile.x, hoveredTile.y];
Debug.Log("Hover tile = " + hoveredTile + 
          " | pieceAtTile = " + (pieceAtTile ? pieceAtTile.name : "NULL") +
          " | currentTurn = " + currentTurn);
                if (selectedPiece == null)
                {
                    Debug.Log("Mouse Click3");
                    if (pieceAtTile != null && pieceAtTile.team == currentTurn)
                    {
                        Debug.Log("Mouse Click4");
                        TrySelectPiece(pieceAtTile);
                    }
                }
                else
                {
                    if (pieceAtTile != null && pieceAtTile.team == currentTurn)
                    {
                        TrySelectPiece(pieceAtTile);
                    }
                    else
                    {
                        if (!TryMoveSelectedPiece(hoveredTile))
                        {
                            ClearSelection();
                        }
                    }
                }
            }
            else
            {
                ClearSelection();
            }
        }
    }

   public void RefreshPieceMap()
{
    ClearPieceMap();

    Transform searchRoot = piecesRoot ? piecesRoot : chessboard ? chessboard.transform : transform;
    ChessPiece[] scenePieces = searchRoot.GetComponentsInChildren<ChessPiece>(true);

    Debug.Log("Found pieces: " + scenePieces.Length);

    for (int i = 0; i < scenePieces.Length; i++)
    {
        ChessPiece piece = scenePieces[i];

        Debug.Log(piece.name + " pos = " + piece.boardPosition + " team = " + piece.team);

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
        
        if (selectedPiece.MovementRule == null || !selectedPiece.MovementRule.IsLegalMove(selectedPiece, from, destination, pieces))
            return false;

        ChessPiece targetPiece = pieces[destination.x, destination.y];
        if (targetPiece != null)
        {
            Destroy(targetPiece.gameObject);
        }

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
