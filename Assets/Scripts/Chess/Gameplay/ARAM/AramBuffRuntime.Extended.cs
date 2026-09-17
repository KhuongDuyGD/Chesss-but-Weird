using System;
using System.Collections.Generic;
using ChessButWeird.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed partial class AramBuffRuntime
{
    private readonly int[] completedTurns = new int[2];
    private readonly int[] tickets = new int[2], rollsThisTurn = new int[2], extraUses = new int[2], escapeUses = new int[2];
    private readonly int[] swapReady = new int[2], hidingReady = new int[2], rifleReady = new int[2];
    private readonly bool[] swapActive = { true, true }, peaceFailed = new bool[2], peaceUsed = new bool[2], substituteUsed = new bool[2];
    private readonly HashSet<ChessPiece> bloodthirsty = new HashSet<ChessPiece>();
    private readonly Dictionary<ChessPiece,int> cannonTurns = new Dictionary<ChessPiece,int>(), sniperUntil = new Dictionary<ChessPiece,int>(), infectedUntil = new Dictionary<ChessPiece,int>();
    private readonly Dictionary<ChessPiece,PieceTeam> passengers = new Dictionary<ChessPiece,PieceTeam>();
    private readonly HashSet<ChessPiece> decoys = new HashSet<ChessPiece>();
    private readonly Dictionary<Vector2Int,PieceTeam> mines = new Dictionary<Vector2Int,PieceTeam>();
    private readonly Dictionary<Vector2Int,SupplyCrate> crates = new Dictionary<Vector2Int,SupplyCrate>();
    private readonly List<GameObject> boardMarkers = new List<GameObject>();
    private readonly Queue<Action> decisions = new Queue<Action>();
    private readonly HashSet<int> lootRounds = new HashSet<int>();
    private readonly HashSet<ChessPiece> unstableQueens = new HashSet<ChessPiece>();
    private ChessPiece forcedPawn;
    private bool grantExtraTurn, initializedEffects;
    private int collapsedFiles;
    private Func<Vector2Int,bool> targetAction;
    private Action targetCancel;
    private string actionMessage = "";
    private AramAbilityView abilityView;
    private readonly Dictionary<ChessPiece,Vector2Int> formation = new Dictionary<ChessPiece,Vector2Int>();
    private PieceTeam formationTeam;
    private float formationDeadline;
    private ChessPiece formationPiece;
    private bool customizing;
    private Camera rifleCamera;
    private Camera previousCamera;
    private Quaternion rifleRotation;
    private float shotDeadline, rifleYaw, riflePitch;
    private PieceTeam rifleTeam;
    private GameObject rifleModel;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    public bool HasPendingDecision => targetAction != null || decisions.Count > 0 || customizing;
    private bool IsSwapActive(PieceTeam team) => networkMatch || swapActive[(int)team];
    private bool Live(ChessPiece p) => game && game.AramIsAlive(p);
    private ChessPiece At(Vector2Int p) => ChessMoveRules.IsInsideBoard(p) ? game.AramBoard[p.x,p.y] : null;
    private int Side(PieceTeam team) => (int)team;
    private int Home(PieceTeam team) => game.AramKing(team) && game.AramKing(team).ForwardDirection < 0 ? 7 : 0;
    private bool InHome(Vector2Int p, PieceTeam team, int ranks=4) => Home(team)==0 ? p.y<ranks : p.y>=8-ranks;
    internal bool SquareAvailable(Vector2Int p) => ChessMoveRules.IsInsideBoard(p) && p.x>=collapsedFiles && p.x<8-collapsedFiles;

    private void ResetExtendedState()
    {
        ExitRifle(false);
        Array.Clear(completedTurns,0,2); Array.Clear(tickets,0,2); Array.Clear(rollsThisTurn,0,2);
        Array.Clear(extraUses,0,2); Array.Clear(escapeUses,0,2); Array.Clear(swapReady,0,2);
        Array.Clear(hidingReady,0,2); Array.Clear(rifleReady,0,2); Array.Clear(peaceFailed,0,2);
        Array.Clear(peaceUsed,0,2); Array.Clear(substituteUsed,0,2);
        swapActive[0]=swapActive[1]=true;
        bloodthirsty.Clear(); cannonTurns.Clear(); sniperUntil.Clear(); infectedUntil.Clear();
        passengers.Clear(); decoys.Clear(); mines.Clear(); crates.Clear(); decisions.Clear(); lootRounds.Clear();
        foreach(var marker in boardMarkers) if(marker) Destroy(marker);
        boardMarkers.Clear(); formation.Clear(); customizing=false; targetAction=null; targetCancel=null;
        forcedPawn=null; unstableQueens.Clear(); collapsedFiles=0; initializedEffects=false; actionMessage="";
        if(abilityView) abilityView.SetVisible(false);
    }

    public bool AbilityHudVisible => active && !networkMatch && game && game.GameStarted && !game.GameOver && !IsSelectingSetupTargets;
    public bool RifleActive => rifleCamera;
    private void Update()
    {
        if(rifleCamera && (!game || !game.GameStarted || game.GameOver))ExitRifle(false);
    }

    private void OnDestroy()
    {
        ResetExtendedState();
        foreach(var definition in draftPool)if(definition)Destroy(definition);
        draftPool.Clear();
    }

    private void ApplyInitialEffects()
    {
        if(initializedEffects) return;
        initializedEffects=true;
        foreach(PieceTeam team in Enum.GetValues(typeof(PieceTeam))) ApplyTeamInitialEffects(team);
        RefreshBoardMarkers();
    }

    private void ApplyTeamInitialEffects(PieceTeam team)
    {
        var army=game.GetActivePiecesForAram(team);
        if(HasBuff(team,AramBuffId.PawnsRevolution) || HasBuff(team,AramBuffId.OneManArmy))
        {
            foreach(var p in army) if(p.Type!=PieceType.King) game.AramRemove(p,false);
            if(HasBuff(team,AramBuffId.PawnsRevolution))
                foreach(var p in EmptySquares(team,4)) game.AramSpawn(PieceType.Pawn,team,p);
        }
        if(HasBuff(team,AramBuffId.RngFiesta))
            foreach(var p in army) if(Live(p) && p.Type!=PieceType.King && p.Type!=PieceType.Queen)
                game.AramReplace(p,new[]{PieceType.Pawn,PieceType.Knight,PieceType.Bishop,PieceType.Rook}[UnityEngine.Random.Range(0,4)],team);
        if(HasBuff(team,AramBuffId.QueensBetrayal))
        {
            var squares=EmptySquares(team,3); squares.RemoveAll(p=>p.y!=(Home(team)==0?2:5));
            if(squares.Count>0) unstableQueens.Add(game.AramSpawn(PieceType.Queen,team,Pick(squares)));
        }
        if(HasBuff(team,AramBuffId.HighTechEra))
            foreach(var p in army) if(Live(p)&&p.Type==PieceType.Rook) cannonTurns[p]=10;
        if(HasBuff(team,AramBuffId.CustomizeArmy)) decisions.Enqueue(()=>StartFormation(team));
        swapReady[Side(team)]=5;
    }

    private List<Vector2Int> EmptySquares(PieceTeam? team=null,int ranks=4)
    {
        var result=new List<Vector2Int>();
        for(int x=collapsedFiles;x<8-collapsedFiles;x++) for(int y=0;y<8;y++)
        {
            var p=new Vector2Int(x,y);
            if(!At(p)&&(!team.HasValue||InHome(p,team.Value,ranks)))result.Add(p);
        }
        return result;
    }
    private static T Pick<T>(List<T> values) => values[UnityEngine.Random.Range(0,values.Count)];

    public bool CanMove(ChessPiece piece, Vector2Int destination)
    {
        if(networkMatch) return true;
        return Live(piece) && SquareAvailable(destination) && (!Live(forcedPawn)||piece==forcedPawn);
    }

    internal bool MovingPieceWillDie(ChessPiece piece,Vector2Int from,Vector2Int to)
    {
        if(mines.TryGetValue(to,out var mineTeam)&&mineTeam!=piece.Team)return true;
        return piece.Type==PieceType.Pawn&&HasBuff(piece.Team,AramBuffId.RiseOfPawn)&&
            to.y==Home(piece.Team)&&to.y-from.y==-piece.ForwardDirection;
    }

    public void BeforeNormalMove(ChessPiece moving, ChessPiece captured, Vector2Int from, Vector2Int to)
    {
        if(!active||networkMatch)return;
        grantExtraTurn=false;
        int side=Side(moving.Team);
        if(cannonTurns.ContainsKey(moving) && captured && !MovementRules.IsPathClear(new UnityBoardAdapter(game.AramBoard),UnityBoardAdapter.ToSquare(from),UnityBoardAdapter.ToSquare(to)))
            cannonTurns[moving]=completedTurns[side]+11;
        if(!captured)return;
        OnPieceRemoved(captured);
        if(captured.Team==moving.Team)
        {
            if(moving.Type==PieceType.Pawn && HasBuff(moving.Team,AramBuffId.NobleSacrifice)) bloodthirsty.Add(moving);
            return;
        }
        if(HasBuff(captured.Team,AramBuffId.PlagueTown)) infectedUntil[moving]=completedTurns[side]+5;
        if(HasBuff(moving.Team,AramBuffId.GachaBanner)) tickets[side]+=captured.Type==PieceType.Pawn?1:captured.Type==PieceType.Queen?3:2;
        if(moving.Type==PieceType.Pawn && HasBuff(moving.Team,AramBuffId.GamblingLeadsToMisery) && extraUses[side]<2 && UnityEngine.Random.value<.3f)
        { extraUses[side]++; grantExtraTurn=true; }
        if(moving.Type==PieceType.Bishop && HasBuff(moving.Team,AramBuffId.AbsoluteSniper) && captured.Type!=PieceType.Pawn &&
            Mathf.Max(Mathf.Abs(to.x-from.x),Mathf.Abs(to.y-from.y))>=4) sniperUntil[moving]=completedTurns[side]+6;
    }

    public PieceTeam AfterNormalMove(ChessPiece moving, ChessPiece captured, Vector2Int from, Vector2Int to)
    {
        PieceTeam team=moving.Team;
        if(decoys.Contains(moving) && captured && captured.Team!=team)
        { decoys.Remove(moving); moving=game.AramReplace(moving,captured.Type,team); }
        if(Live(moving) && moving.Type==PieceType.Pawn && HasBuff(team,AramBuffId.RiseOfPawn) && to.y==Home(team) && to.y-from.y==-moving.ForwardDirection)
        { game.AramRemove(moving,false); mines[to]=team; RefreshBoardMarkers(); }
        ResolveLanding(moving,to);
        if(grantExtraTurn && Live(moving) && moving.Type==PieceType.Pawn && to.y!=(Home(team)==0?7:0))
        { forcedPawn=moving; if(game.AramHasMove(moving)){Say("Extra turn: move the same Pawn again."); return team;} }
        forcedPawn=null;
        CompleteOwnerTurn(team);
        return team==PieceTeam.White?PieceTeam.Black:PieceTeam.White;
    }

    internal void CompleteOwnerTurn(PieceTeam team)
    {
        int side=Side(team); completedTurns[side]++;
        if(game.AramInCheck(team) && completedTurns[side]<=15)peaceFailed[side]=true;
        foreach(var p in new List<ChessPiece>(infectedUntil.Keys))
            if(!Live(p)) infectedUntil.Remove(p);
            else if(p.Team==team && completedTurns[side]>=infectedUntil[p]) { infectedUntil.Remove(p); game.AramRemove(p); Say("An infected piece died from Plague."); }
        foreach(var queen in new List<ChessPiece>(unstableQueens))
        {
            if(!Live(queen)){unstableQueens.Remove(queen);continue;}
            if(queen.Team==team&&UnityEngine.Random.value<.1f)
            {
                unstableQueens.Remove(queen);
                unstableQueens.Add(game.AramReplace(queen,PieceType.Queen,team==PieceTeam.White?PieceTeam.Black:PieceTeam.White));
                Say("An unstable Queen changed sides!");
            }
        }
        game.AramFinishIfKingMissing();
    }

    private void ResolveLanding(ChessPiece moving,Vector2Int to)
    {
        if(!Live(moving))return;
        if(mines.TryGetValue(to,out var mineTeam) && mineTeam!=moving.Team)
        { mines.Remove(to); game.AramRemove(moving); Say("A mine detonated."); RefreshBoardMarkers(); return; }
        if(crates.TryGetValue(to,out var crate))
        {
            crates.Remove(to); RefreshBoardMarkers();
            if(crate.Team==moving.Team) QueueDeployment(crate.Team,crate.Kind,null);
            else Say("Enemy supply crate destroyed.");
        }
    }

    public void OnPieceRemoved(ChessPiece piece)
    {
        if(!piece||networkMatch)return;
        if(passengers.TryGetValue(piece,out var team))
        { passengers.Remove(piece); QueueDeployment(team,PieceType.Pawn,piece.BoardPosition); }
    }

    private void BeginExtendedTurn(PieceTeam team)
    {
        int side=Side(team); rollsThisTurn[side]=0;
        foreach(var p in new List<ChessPiece>(sniperUntil.Keys))
            if(!Live(p)||completedTurns[Side(p.Team)]>=sniperUntil[p])sniperUntil.Remove(p);
        foreach(var p in new List<ChessPiece>(cannonTurns.Keys))
            if(!Live(p)||completedTurns[Side(p.Team)]>=cannonTurns[p])cannonTurns.Remove(p);
        if(game.AramInCheck(team))
        {
            if(completedTurns[side]<15)peaceFailed[side]=true;
            if(HasBuff(team,AramBuffId.SubstituteNinjutsu)&&!substituteUsed[side])
            {
                substituteUsed[side]=true;
                var king=game.AramKing(team); var original=king.BoardPosition;
                var safe=EmptySquares(team); safe.RemoveAll(p=>!game.AramSafeDestination(king,p));
                if(safe.Count>0)
                {
                    game.AramRelocate(king,Pick(safe));
                    var decoy=game.AramSpawn(PieceType.King,team,original);
                    if(decoy) { decoy.gameObject.AddComponent<AramDecoyTag>(); decoys.Add(decoy); AddMarker(decoy,GetState(team).GetBuff(AramBuffId.SubstituteNinjutsu),"DECOY"); }
                    Say("Substitute Ninjutsu activated.");
                }
                else Say("Substitute Ninjutsu: no safe destination.");
            }
        }
        int round=1+Mathf.Min(completedTurns[0],completedTurns[1]);
        if((round>=25&&!lootRounds.Contains(25))||(round>=50&&!lootRounds.Contains(50)))
        {
            int milestone=round>=50?50:25; lootRounds.Add(milestone);
            foreach(PieceTeam owner in Enum.GetValues(typeof(PieceTeam))) if(HasBuff(owner,AramBuffId.LootBox))
            { var empty=EmptySquares(); empty.RemoveAll(p=>crates.ContainsKey(p)); if(empty.Count>0)crates[Pick(empty)]=new SupplyCrate(owner,milestone==25?PieceType.Rook:PieceType.Queen); }
            RefreshBoardMarkers();
        }
        if(HasBuff(PieceTeam.White,AramBuffId.DefinitionOfAram)||HasBuff(PieceTeam.Black,AramBuffId.DefinitionOfAram))
        {
            int required=round>=15?2:round>=10?1:0;
            if(required>collapsedFiles)
            {
                collapsedFiles=required;
                foreach(PieceTeam owner in Enum.GetValues(typeof(PieceTeam)))
                    foreach(var p in game.GetActivePiecesForAram(owner)) if(!SquareAvailable(p.BoardPosition))game.AramRemove(p);
                RefreshBoardMarkers(); game.AramFinishIfKingMissing();
            }
        }
        EnsureActionView();
    }

    private void QueueDeployment(PieceTeam team,PieceType kind,Vector2Int? center)
    {
        decisions.Enqueue(()=>
        {
            var allowed=center.HasValue?EmptySquares():EmptySquares(team);
            if(center.HasValue) allowed.RemoveAll(p=>Mathf.Abs(p.x-center.Value.x)>1||Mathf.Abs(p.y-center.Value.y)>1);
            if(allowed.Count==0){Say("No empty deployment square; the reinforcement was lost.");return;}
            Choose($"{team}: deploy your {kind} on a highlighted eligible square.",p=>
            {
                if(!allowed.Contains(p)||At(p))return false;
                var type=kind==PieceType.Pawn && p.y==(Home(team)==0?7:0)?PieceType.Queen:kind;
                game.AramSpawn(type,team,p,true); return true;
            },false);
            game.AramHighlight(allowed);
        });
    }

    private void Choose(string message,Func<Vector2Int,bool> action,bool cancellable=true)
    { game.ClearSelection(); actionMessage=message; targetAction=action; targetCancel=cancellable?(Action)(()=>{}):null; }
    private void CancelTarget()
    { if(targetCancel==null)return; targetCancel(); targetAction=null; targetCancel=null; game.AramHighlight(null); actionMessage="Selection cancelled."; }
    private void Say(string message)
    { actionMessage=message; draftView?.ShowBuffToast(game.CurrentTurn,message,null); }

    public bool HandleAbilityInput()
    {
        if(!active||networkMatch||!game||!game.GameStarted||game.GameOver)return false;
        if(game.PauseLocked)return rifleCamera||targetAction!=null||customizing;
        if(rifleCamera) { TickRifle(); return true; }
        if(IsSelectingSetupTargets)return false;
        if(customizing && Time.unscaledTime>=formationDeadline) FinishFormation();
        if(targetAction==null && decisions.Count>0 && !game.InputLocked && !game.HasPendingPromotion)decisions.Dequeue()();
        if(targetAction==null)return false;
        if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame) {CancelTarget();return true;}
        if(Mouse.current==null||!Mouse.current.leftButton.wasPressedThisFrame || (EventSystem.current && EventSystem.current.IsPointerOverGameObject()))return true;
        if(game.AramReadPointer(out var square))
        {
            var handler=targetAction;
            if(handler(square))
            {
                if(targetAction==handler){targetAction=null;targetCancel=null;game.AramHighlight(null);}
                if(targetAction==null && decisions.Count==0)game.AramRefreshPosition();
            }
        }
        return true;
    }

    private void EnsureActionView()
    {
        if(!abilityView) { abilityView=gameObject.AddComponent<AramAbilityView>(); abilityView.Initialize(this); }
        abilityView.SetVisible(true);
    }

    public string AbilityStatus
    {
        get
        {
            if(!game)return "";
            int side=Side(game.CurrentTurn);
            string value=$"{game.CurrentTurn} | Turn {completedTurns[side]+1}";
            if(HasBuff(game.CurrentTurn,AramBuffId.GachaBanner)) value+=$" | Tickets: {tickets[side]} | Rolls: {rollsThisTurn[side]}/2";
            if(customizing)value+=$" | Formation: {Mathf.CeilToInt(Mathf.Max(0,formationDeadline-Time.unscaledTime))}s";
            if(rifleCamera)value+=$" | Aim: {Mathf.CeilToInt(Mathf.Max(0,shotDeadline-Time.unscaledTime))}s";
            return value+"\n"+actionMessage;
        }
    }

    public void GetAbilityActions(List<AramAbilityView.AbilityAction> actions)
    {
        actions.Clear();
        if(!active||networkMatch||!game||!game.GameStarted||game.GameOver)return;
        if(rifleCamera){actions.Add(new AramAbilityView.AbilityAction("Exit rifle",()=>ExitRifle(false)));return;}
        if(customizing){actions.Add(new AramAbilityView.AbilityAction("Confirm formation",FinishFormation));return;}
        if(targetAction!=null){if(targetCancel!=null)actions.Add(new AramAbilityView.AbilityAction("Cancel",CancelTarget));return;}
        if(IsSelectingSetupTargets||game.InputLocked||game.PauseLocked||Live(forcedPawn))return;
        var team=game.CurrentTurn; int side=Side(team);
        if(HasBuff(team,AramBuffId.GachaBanner))AddAction(actions,$"Gacha ({tickets[side]} tickets)",()=>RollBattleGacha(team),tickets[side]>0&&rollsThisTurn[side]<2);
        if(HasBuff(team,AramBuffId.Doppelganger))AddAction(actions,"Swap movement",()=>ToggleSwap(team),completedTurns[side]>=swapReady[side]);
        if(HasBuff(team,AramBuffId.HidingKing))AddAction(actions,"Hide King",()=>HideKing(team),completedTurns[side]>=hidingReady[side]);
        if(HasBuff(team,AramBuffId.MobileFortress))AddAction(actions,"Load Pawn",()=>LoadPassenger(team));
        if(HasBuff(team,AramBuffId.AbsoluteSniper))AddAction(actions,"Snipe",()=>Snipe(team));
        if(HasBuff(team,AramBuffId.PeaceTShirt))AddAction(actions,"Recruit enemy",()=>Recruit(team),completedTurns[side]>=15&&!peaceFailed[side]&&!peaceUsed[side]);
        if(HasBuff(team,AramBuffId.OneManArmy))AddAction(actions,"Aim rifle",()=>EnterRifle(team),completedTurns[side]>=rifleReady[side]);
    }
    private static void AddAction(List<AramAbilityView.AbilityAction> actions,string label,Action action,bool enabled=true)
    { actions.Add(new AramAbilityView.AbilityAction(label,action,enabled)); }

    private void RollBattleGacha(PieceTeam team)
    {
        int side=Side(team); var empty=EmptySquares();
        if(tickets[side]<1||rollsThisTurn[side]>=2||empty.Count==0){Say("No tickets, turn limit reached, or no empty square.");return;}
        int roll=UnityEngine.Random.Range(0,100);
        PieceType kind=roll<70?PieceType.Pawn:roll<80?PieceType.Knight:roll<90?PieceType.Bishop:roll<99?PieceType.Rook:PieceType.Queen;
        var square=Pick(empty); var piece=game.AramSpawn(kind,team,square,true);
        if(!piece)return;
        tickets[side]--;rollsThisTurn[side]++;Say($"Gacha deployed a {kind}.");ResolveLanding(piece,square);game.AramRefreshPosition();
    }

    private void ToggleSwap(PieceTeam team)
    {
        int side=Side(team); var state=GetState(team);
        if(!Live(state.SwappedKnight)||!Live(state.SwappedBishop)){Say("Both selected pieces must survive to swap.");return;}
        swapActive[side]=!swapActive[side];
        if(game.AramInCheck(team)){swapActive[side]=!swapActive[side];Say("This swap would leave your King in check.");return;}
        swapReady[side]=completedTurns[side]+5;game.ClearSelection();Say("Both movement sets swapped.");game.AramRefreshPosition();
    }

    private void HideKing(PieceTeam team)
    {
        Choose("Choose an allied piece on your back rank to swap with the King.",p=>
        {
            var piece=At(p);var king=game.AramKing(team);
            if(!piece||piece==king||piece.Team!=team||p.y!=Home(team)||!game.AramRelocate(king,p,true,true))return false;
            hidingReady[Side(team)]=completedTurns[Side(team)]+10;return true;
        });
    }

    private void LoadPassenger(PieceTeam team)
    {
        Choose("Choose an allied Rook without a passenger.",p=>
        {
            var rook=At(p);if(!rook||rook.Team!=team||rook.Type!=PieceType.Rook||passengers.ContainsKey(rook))return false;
            Choose("Choose an allied Pawn for this Rook to carry.",q=>
            {
                var pawn=At(q);if(!Live(rook)||!pawn||pawn.Team!=team||pawn.Type!=PieceType.Pawn)return false;
                if(!game.AramSafeRemoval(pawn,team))return false;
                game.AramRemove(pawn,false);passengers[rook]=team;AddMarker(rook,GetState(team).GetBuff(AramBuffId.MobileFortress),"PAWN");return true;
            });return true;
        });
    }

    private void Recruit(PieceTeam team)
    {
        Choose("Choose an enemy Pawn, Knight, Bishop or Rook to recruit.",p=>
        {
            var enemy=At(p);if(!enemy||enemy.Team==team||enemy.Type==PieceType.King||enemy.Type==PieceType.Queen)return false;
            Choose("Choose an empty square within your first three ranks.",q=>
            {
                if(!Live(enemy)||!SquareAvailable(q)||At(q)||!InHome(q,team,3))return false;
                var kind=enemy.Type;game.AramRemove(enemy,false);game.AramSpawn(kind,team,q,true);peaceUsed[Side(team)]=true;return true;
            });return true;
        });
    }

    private void Snipe(PieceTeam team)
    {
        Choose("Choose a charged Bishop.",p=>
        {
            var bishop=At(p);if(!bishop||bishop.Team!=team||!sniperUntil.ContainsKey(bishop))return false;
            Choose("Choose an enemy along a clear Bishop diagonal. The Bishop will not move.",q=>
            {
                var enemy=At(q);if(!Live(bishop)||!enemy||enemy.Team==team||enemy.Type==PieceType.King)return false;
                var delta=q-p;if(Mathf.Abs(delta.x)!=Mathf.Abs(delta.y)||!MovementRules.IsPathClear(new UnityBoardAdapter(game.AramBoard),UnityBoardAdapter.ToSquare(p),UnityBoardAdapter.ToSquare(q)))return false;
                if(!game.AramSafeRemoval(enemy,team))return false;
                BeforeNormalMove(bishop,enemy,p,q);sniperUntil.Remove(bishop);game.AramRemove(enemy,false);
                DetonateRemovedQueen(enemy,q,bishop);CompleteOwnerTurn(team);game.AramCompleteAbilityTurn();return true;
            });return true;
        });
    }

    private void DetonateRemovedQueen(ChessPiece queen,Vector2Int square,ChessPiece attacker)
    {
        var victims=new List<ChessPiece>();
        if(TryGetQueenExplosion(queen,square,attacker,game.AramBoard,victims))foreach(var victim in victims)game.AramRemove(victim);
    }

    public bool TryOfferEscape(PieceTeam team)
    {
        if(networkMatch||!HasBuff(team,AramBuffId.IFrameRoll)||escapeUses[Side(team)]>=3)return false;
        if(targetAction!=null)return true;
        var king=game.AramKing(team);if(!king)return false;
        var from=king.BoardPosition;var squares=EmptySquares();
        squares.RemoveAll(p=>Mathf.Abs(p.x-from.x)>2||Mathf.Abs(p.y-from.y)>2||!game.AramSafeDestination(king,p));
        if(squares.Count==0)return false;
        Choose("I-Frame Roll: choose a safe square within two squares of your King.",p=>
        {if(!squares.Contains(p)||!game.AramRelocate(king,p))return false;escapeUses[Side(team)]++;return true;},false);
        game.AramHighlight(squares);return true;
    }

    private void StartFormation(PieceTeam team)
    {
        customizing=true;formationTeam=team;formationDeadline=Time.unscaledTime+120f;formationPiece=null;formation.Clear();
        foreach(var p in game.GetActivePiecesForAram(team))formation[p]=p.BoardPosition;
        Choose($"{team}: select a piece, then a square in your half. You have 120 seconds.",p=>
        {
            var piece=At(p);
            if(!formationPiece){if(piece&&piece.Team==team)formationPiece=piece;return false;}
            if(!InHome(p,team)||!SquareAvailable(p))return false;
            if(game.AramRelocate(formationPiece,p,false,true,false))formationPiece=null;
            else if(piece&&piece.Team==team)formationPiece=piece;
            return false;
        },false);
    }

    private void FinishFormation()
    {
        if(!customizing)return;
        bool unchanged=true;foreach(var pair in formation)if(Live(pair.Key)&&pair.Key.BoardPosition!=pair.Value)unchanged=false;
        if(game.AramInCheck(formationTeam))
        { game.AramRestoreFormation(formation);unchanged=true;Say("Unsafe formation restored."); }
        customizing=false;targetAction=null;targetCancel=null;formationPiece=null;formation.Clear();
        if(unchanged)
        {
            var pool=GetBuffsByTier(draftPool,AramBuffTier.Gold);var replacement=Pick(pool);
            GetState(formationTeam).SetBuffs(new List<AramBuffDefinition>{replacement});
            CaptureInitialTeamState(GetState(formationTeam));ApplyTeamInitialEffects(formationTeam);
            EnqueueTargetTasks(GetState(formationTeam));TryStartNextTargetTask();
            Say($"Unchanged formation refunded: {replacement.DisplayName}.");
        }
        else Say("Formation confirmed.");
    }

    private void RefreshBoardMarkers()
    {
        foreach(var marker in boardMarkers)if(marker)Destroy(marker);boardMarkers.Clear();
        foreach(var pair in mines)MarkSquare(pair.Key,"MINE",new Color(.9f,.2f,.15f));
        foreach(var pair in crates)MarkSquare(pair.Key,pair.Value.Kind==PieceType.Rook?"ROOK CRATE":"QUEEN CRATE",new Color(1f,.8f,.1f));
        for(int x=0;x<8;x++)if(x<collapsedFiles||x>=8-collapsedFiles)for(int y=0;y<8;y++)MarkSquare(new Vector2Int(x,y),"VOID",new Color(.15f,.05f,.25f));
    }

    private void MarkSquare(Vector2Int square,string label,Color color)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="ARAM "+label;Destroy(go.GetComponent<Collider>());
        float tile=Vector3.Distance(game.AramTile(Vector2Int.zero),game.AramTile(Vector2Int.right));
        go.transform.position=game.AramTile(square)+Vector3.up*.04f;go.transform.localScale=new Vector3(tile*.85f,.04f,tile*.85f);
        var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);block.SetColor("_Color",color);go.GetComponent<Renderer>().SetPropertyBlock(block);
        var text=new GameObject(label).AddComponent<TextMesh>();text.transform.SetParent(go.transform,false);text.transform.localPosition=new Vector3(0,2f,0);text.transform.localRotation=Quaternion.Euler(90,0,0);
        text.text=label;text.characterSize=.12f;text.fontSize=48;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;
        boardMarkers.Add(go);
    }

    private readonly struct SupplyCrate
    { public readonly PieceTeam Team;public readonly PieceType Kind;public SupplyCrate(PieceTeam team,PieceType kind){Team=team;Kind=kind;} }
}
