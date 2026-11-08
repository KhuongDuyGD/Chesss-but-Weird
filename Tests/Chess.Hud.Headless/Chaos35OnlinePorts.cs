using System.Collections.Generic;
using UnityEngine;

// Inert transport/account ports for the isolated production-presenter audit.
public static class PlayerAuthService { public static string UserId; }
public sealed class ChessLanController
{
    public bool CanSendOnlineCommand = true;
    public readonly List<ChessButWeird.Online.AbilityCommand> Sent = new List<ChessButWeird.Online.AbilityCommand>();
    public bool BlocksOnlineBoardPointer(Vector2 point) => false;
    public void SendAbility(ChessButWeird.Online.AbilityCommand command) { Sent.Add(command); CanSendOnlineCommand = false; }
}
public sealed partial class ChessGame
{
    public FixtureOnlineBoard Board;
    public ChessPiece OnlinePieceView(int id) => null;
    public static Vector2Int ParseOnlineSquare(string value) => new Vector2Int(value[0]-'a',value[1]-'1');
}
public sealed class FixtureOnlineBoard : MonoBehaviour
{ public Vector3 GetTileCenterWorld(Vector2Int square) => new Vector3(square.x,0,square.y); }
namespace ChessButWeird.Online { public sealed class RankOverview { } }
