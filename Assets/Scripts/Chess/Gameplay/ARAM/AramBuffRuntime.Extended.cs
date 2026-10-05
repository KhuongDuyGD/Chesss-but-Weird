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
    private bool rifleMouseLocked, rifleCursorSuspended;

    public bool HasPendingDecision => targetAction != null || decisions.Count > 0 || customizing;
    private bool IsSwapActive(PieceTeam team) => networkMatch || swapActive[(int)team];
    private bool Live(ChessPiece p) => game && game.AramIsAlive(p);
    private ChessPiece At(Vector2Int p) => ChessMoveRules.IsInsideBoard(p) ? game.AramBoard[p.x,p.y] : null;
    private int Side(PieceTeam team) => (int)team;
    private int Home(PieceTeam team) => homeRanks[Side(team)];
    private bool InHome(Vector2Int p, PieceTeam team, int ranks=4) => Home(team)==0 ? p.y<ranks : p.y>=8-ranks;
    internal bool SquareAvailable(Vector2Int p) => ChessMoveRules.IsInsideBoard(p) && p.x>=collapsedFiles && p.x<8-collapsedFiles;

    private void ResetExtendedState()
    {
        ExitRifle(false);
        ResetDocumentState();
        Array.Clear(disguisedBuff,0,2); foreach(var display in displayBuffs) display.Clear();
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

    public bool AbilityHudVisible => active && game && game.GameStarted && !game.GameOver && !game.PauseLocked && !game.HasPendingPromotion && !IsSelectingSetupTargets;
    public bool RifleActive => rifleCamera;
    // The view discards clicks from an earlier turn or decision phase.
    public string AbilityContext => game ? $"{game.CurrentTurn}:{completedTurns[Side(game.CurrentTurn)]}:{game.Statistics.Revision}:{(rifleCamera?"rifle":customizing?"formation":targetAction!=null?"target":decisions.Count>0?"queued":"normal")}" : "";
    public string AbilityPresentationContext => game ? $"{AbilityViewer}:{(rifleCamera?"rifle":customizing?"formation":targetAction!=null?"target":decisions.Count>0?"queued":"normal")}" : "";
    public float AbilityPanelHeight => abilityView ? abilityView.PanelHeight : 0;
    public bool AbilityInstructionRequired => HasPendingDecision || Live(forcedPawn) || rifleCamera;
    // Buff controls occupy the left edge; the right journal keeps its full height.
    public float StatisticsHudBottom => 0;
    public string RifleMouseHint => "ALT: " + (Cursor.lockState==CursorLockMode.Locked?"unlock mouse":"lock mouse");
    private PieceTeam AbilityViewer => networkMatch ? visibleTeam : game.IsBotGame ? game.PlayerTeam : game.CurrentTurn;
    public string AbilityBuffSummary
    {
        get
        {
            if(!game)return "";
            var team=AbilityViewer;var parts=new List<string>();
            foreach(var buff in GetDisplayBuffs(team,team))if(buff)
            {
                string progress=BuffProgress(team,buff.Id);
                parts.Add(buff.ShortName+(string.IsNullOrEmpty(progress)?"":" · "+progress));
            }
            return team+" · "+(parts.Count==0?"No buff":string.Join("\n",parts));
        }
    }
    public string AbilityBuffDetails
    {
        get
        {
            if(!game)return "";
            var parts=new List<string>();var team=AbilityViewer;
            foreach(var buff in GetDisplayBuffs(team,team))if(buff)
                parts.Add(buff.DisplayName+"\n"+buff.Description+"\n"+BuffProgress(team,buff.Id));
            if(!string.IsNullOrEmpty(actionMessage)&&!AbilityInstructionRequired)parts.Add(actionMessage);
            return string.Join("\n\n",parts);
        }
    }
    private void Update()
    {
        if(rifleCamera && (!game || !game.GameStarted || game.GameOver))ExitRifle(false);
        if(rifleCamera)SetRifleCursorSuspended(game.PauseLocked||!Application.isFocused);
    }

    private void OnApplicationFocus(bool focused)
    {if(rifleCamera)SetRifleCursorSuspended(!focused||(game&&game.PauseLocked));}
    private void OnDisable(){ExitRifle(false);}

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
        if(army.Count>0)homeRanks[Side(team)]=army[0].ForwardDirection<0?7:0;
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
        swapReady[Side(team)]=AramBalanceRules.DoppelgangerCooldown;
        ApplyDocumentInitialEffects(team);
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
        ObserveCheckConditions();
        grantExtraTurn=false;
        int side=Side(moving.Team);
        if(cannonTurns.ContainsKey(moving) && captured && !MovementRules.IsPathClear(new UnityBoardAdapter(game.AramBoard),UnityBoardAdapter.ToSquare(from),UnityBoardAdapter.ToSquare(to)))
            cannonTurns[moving]=AramBalanceRules.ExpiryAfterCapture(combinedPlies,AramBalanceRules.CannonLifetime);
        if(!captured)return;
        OnPieceRemoved(captured);
        if(captured.Team==moving.Team)
        {
            if(moving.Type==PieceType.Pawn && HasBuff(moving.Team,AramBuffId.NobleSacrifice)) bloodthirsty.Add(moving);
            return;
        }
        if((moving.Type!=PieceType.King||decoys.Contains(moving))&&HasBuff(captured.Team,AramBuffId.PlagueTown))infectedUntil[moving]=AramBalanceRules.ExpiryAfterCapture(combinedPlies,AramBalanceRules.PlagueLifetime);
        CaptureReward(moving.Team,captured);
        if(moving.Type!=PieceType.King&&moving.Type==captured.Type&&HasBuff(moving.Team,AramBuffId.EveryManForHimself))questCaptures[side].Add(moving.Type);
        if(moving.Type!=PieceType.Queen&&captured.Type==PieceType.Queen&&HasBuff(moving.Team,AramBuffId.UltimateQuest))queenMastery.Add(moving);
        if(moving.Type==PieceType.Pawn && HasBuff(moving.Team,AramBuffId.GamblingLeadsToMisery) && extraUses[side]<AramBalanceRules.GamblingMaximumUses && UnityEngine.Random.value<AramBalanceRules.GamblingChance)
        { grantExtraTurn=true; }
        if(moving.Type==PieceType.Bishop && HasBuff(moving.Team,AramBuffId.AbsoluteSniper) && captured.Type!=PieceType.Pawn &&
            Mathf.Max(Mathf.Abs(to.x-from.x),Mathf.Abs(to.y-from.y))>=4) {sniperUntil[moving]=AramBalanceRules.ExpiryAfterCapture(combinedPlies,AramBalanceRules.SniperLifetime);AddMarker(moving,GetState(moving.Team).GetBuff(AramBuffId.AbsoluteSniper),"");}
    }

    public PieceTeam AfterNormalMove(ChessPiece moving, ChessPiece captured, Vector2Int from, Vector2Int to)
    {
        PieceTeam team=moving.Team;
        if(!active||networkMatch)return team==PieceTeam.White?PieceTeam.Black:PieceTeam.White;
        if(decoys.Contains(moving) && captured && captured.Team!=team)
        { var original=moving;decoys.Remove(moving);moving=game.AramReplace(moving,captured.Type,team);TransferInfection(original,moving); }
        if(Live(moving) && moving.Type==PieceType.Pawn && HasBuff(team,AramBuffId.RiseOfPawn) && to.y==Home(team) && to.y-from.y==-moving.ForwardDirection)
        { game.AramRemove(moving,false); mines[to]=team; RefreshBoardMarkers(); }
        if(Live(moving)&&BlocksPromotion(moving)&&to.y==(Home(team)==0?7:0))trapPawns.Add(moving);
        ResolveLanding(moving,to);
        if(grantExtraTurn && Live(moving) && moving.Type==PieceType.Pawn && to.y!=(Home(team)==0?7:0))
        {
            forcedPawn=moving;
            if(game.AramHasMove(moving))
            {
                AdvanceCombinedPly();
                if(Live(forcedPawn)){extraUses[Side(team)]++;Say("Extra turn: move the same Pawn again.");return team;}
                forcedPawn=null;CompleteOwnerTurn(team);CheckCollapse();return team==PieceTeam.White?PieceTeam.Black:PieceTeam.White;
            }
        }
        forcedPawn=null;
        CompleteOwnerTurn(team);AdvanceCombinedPly();
        return team==PieceTeam.White?PieceTeam.Black:PieceTeam.White;
    }

    internal void CompleteOwnerTurn(PieceTeam team)
    {
        completedTurns[Side(team)]++;
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
        {
            var traits=passengerTraits[piece];passengers.Remove(piece);passengerTraits.Remove(piece);passengerReady.Remove(piece);
            QueueDeployment(team,PieceType.Pawn,piece.BoardPosition,4,traits);
        }
    }

    private void BeginExtendedTurn(PieceTeam team)
    {
        int side=Side(team);rollsThisTurn[side]=0;bought[side]=false;
        if(combinedPlies<AramBalanceRules.PeaceGoal&&game.AramInCheck(team))peaceFailed[side]=true;
        if(game.AramInCheck(team)&&HasBuff(team,AramBuffId.SubstituteNinjutsu)&&!substituteUsed[side])
        {
            substituteUsed[side]=true;var king=game.AramKing(team);
            if(king)
            {
                var original=king.BoardPosition;var safe=EmptySquares(team);safe.RemoveAll(p=>!game.AramSafeDestination(king,p));
                if(safe.Count>0)
                {
                    game.AramRelocate(king,Pick(safe));var decoy=game.AramSpawn(PieceType.King,team,original);
                    if(decoy){decoy.gameObject.AddComponent<AramDecoyTag>();decoys.Add(decoy);AddMarker(decoy,GetState(team).GetBuff(AramBuffId.SubstituteNinjutsu),"");}
                    Say("Substitute Ninjutsu activated.");
                }
                else Say("Substitute Ninjutsu: no safe destination.");
            }
        }
        EnsureActionView();
    }

    private void QueueDeployment(PieceTeam team,PieceType kind,Vector2Int? center,int ranks=4,PassengerTraits? traits=null)
    {
        decisions.Enqueue(()=>
        {
            var allowed=center.HasValue||ranks==0?EmptySquares():EmptySquares(team,ranks);
            if(center.HasValue)allowed.RemoveAll(p=>Mathf.Abs(p.x-center.Value.x)>1||Mathf.Abs(p.y-center.Value.y)>1);
            allowed.RemoveAll(p=>!game.AramCanPlace(DeploymentKind(kind,team,p),team,p));
            if(allowed.Count==0){Say("No eligible deployment square; the reinforcement was lost.");return;}
            Choose($"{team}: deploy your {kind} on a highlighted square.",p=>
            {
                if(!allowed.Contains(p)||At(p)||!game.AramCanPlace(DeploymentKind(kind,team,p),team,p))return false;
                var type=DeploymentKind(kind,team,p);
                if(traits.HasValue)RestorePassenger(team,type,p,traits.Value);
                else{var spawned=game.AramSpawn(type,team,p,true);if(kind==PieceType.Pawn&&type!=PieceType.Pawn)PawnPromoted(null,spawned);}
                return true;
            },false);game.AramHighlight(allowed);
        });
    }
    private PieceType DeploymentKind(PieceType kind,PieceTeam team,Vector2Int p) =>
        kind==PieceType.Pawn&&p.y==(Home(team)==0?7:0)&&!HasBuff(team==PieceTeam.White?PieceTeam.Black:PieceTeam.White,AramBuffId.DoubleEdgedTrap)?PieceType.Queen:kind;

    private void Choose(string message,Func<Vector2Int,bool> action,bool cancellable=true)
    { game.ClearSelection(); actionMessage=message; targetAction=action; targetCancel=cancellable?(Action)(()=>{}):null; }
    private void CancelTarget()
    { if(targetCancel==null)return; targetCancel(); targetAction=null; targetCancel=null; game.AramHighlight(null); actionMessage="Selection cancelled."; }
    private void Say(string message)
    { actionMessage=message; game.RecordStatisticsEvent(game.CurrentTurn,message); draftView?.ShowBuffToast(game.CurrentTurn,message,null); }

    public bool HandleAbilityInput()
    {
        if(!active||networkMatch||!game||!game.GameStarted||game.GameOver)return false;
        if(game.PauseLocked)return rifleCamera||targetAction!=null||customizing;
        if(rifleCamera) { TickRifle(); return true; }
        if(IsSelectingSetupTargets)return false;
        if(customizing && Time.unscaledTime>=formationDeadline) FinishFormation();
        bool processed=false;
        if(targetAction==null && decisions.Count>0 && !game.InputLocked && !game.HasPendingPromotion){processed=true;decisions.Dequeue()();}
        if(targetAction==null)
        {
            if(processed&&decisions.Count==0&&!customizing){game.AramInitializeDrawTracking();game.AramRefreshPosition();}
            return HasPendingDecision;
        }
        if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame) {CancelTarget();return true;}
        if(Mouse.current==null||!Mouse.current.leftButton.wasPressedThisFrame || (EventSystem.current && EventSystem.current.IsPointerOverGameObject()))return true;
        if(game.AramReadPointer(out var square))
        {
            var handler=targetAction;
            if(handler(square))
            {
                if(targetAction==handler){targetAction=null;targetCancel=null;game.AramHighlight(null);}
                if(targetAction==null && decisions.Count==0){game.AramInitializeDrawTracking();game.AramRefreshPosition();}
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
            var parts=new List<string>();
            if(game.IsBotGame&&game.CurrentTurn!=game.PlayerTeam&&!AbilityInstructionRequired)parts.Add("Opponent's turn");
            if(customizing)parts.Add($"Formation: {Mathf.CeilToInt(Mathf.Max(0,formationDeadline-Time.unscaledTime))}s");
            if(rifleCamera)parts.Add($"Aim: {Mathf.CeilToInt(Mathf.Max(0,shotDeadline-Time.unscaledTime))}s");
            if(!string.IsNullOrEmpty(actionMessage)&&AbilityInstructionRequired)parts.Add(actionMessage);
            if(targetAction==null&&decisions.Count>0)parts.Add("Preparing the next buff decision...");
            return string.Join(" · ",parts);
        }
    }

    public void GetAbilityActions(List<AramAbilityView.AbilityAction> actions)
    {
        actions.Clear();
        if(!active||networkMatch||!game||!game.GameStarted||game.GameOver)return;
        if(rifleCamera){actions.Add(new AramAbilityView.AbilityAction("Exit rifle",()=>ExitRifle(false)));return;}
        if(customizing){actions.Add(new AramAbilityView.AbilityAction("Confirm formation",FinishFormation));return;}
        if(targetAction!=null){if(targetCancel!=null)actions.Add(new AramAbilityView.AbilityAction("Cancel",CancelTarget));return;}
        if(decisions.Count>0||IsSelectingSetupTargets||game.InputLocked||game.PauseLocked||game.HasPendingPromotion||Live(forcedPawn))return;
        if(game.IsBotGame&&game.CurrentTurn!=game.PlayerTeam)return;
        var team=game.CurrentTurn; int side=Side(team);
        if(HasBuff(team,AramBuffId.GachaBanner))AddAction(actions,$"Gacha ({tickets[side]} tickets)",()=>RollBattleGacha(team),tickets[side]>0&&rollsThisTurn[side]<1,tickets[side]<=0?"No tickets":rollsThisTurn[side]>=1?"Turn limit reached":"Ready","gacha");
        if(HasBuff(team,AramBuffId.Doppelganger)&&!doppelDisabled[side])
        {
            bool ready=combinedPlies>=swapReady[side];string detail=CombinedCooldown(swapReady[side]-combinedPlies);
            AddAction(actions,"Swap movement",()=>ToggleSwap(team),ready,detail,"swap");
            AddAction(actions,"Keep movement",()=>{swapReady[side]=combinedPlies+4;Say("Movement kept.");},ready,detail,"swap-keep");
            AddAction(actions,"Choose new pair",()=>ReselectDoppelganger(team),ready,detail,"swap-pair");
        }
        if(HasBuff(team,AramBuffId.HidingKing))AddAction(actions,"Hide King",()=>HideKing(team),combinedPlies>=hidingReady[side],CombinedCooldown(hidingReady[side]-combinedPlies));
        if(HasBuff(team,AramBuffId.MobileFortress))
        {AddAction(actions,"Load Pawn",()=>LoadPassenger(team));AddAction(actions,"Unload Pawn",()=>UnloadPassenger(team));}
        if(HasBuff(team,AramBuffId.PeaceTShirt))AddAction(actions,"Recruit enemy",()=>Recruit(team),combinedPlies>=AramBalanceRules.PeaceGoal&&!peaceFailed[side]&&!peaceUsed[side],peaceUsed[side]?"Already used":peaceFailed[side]?"Condition failed":CombinedCooldown(AramBalanceRules.PeaceGoal-combinedPlies));
        if(HasBuff(team,AramBuffId.SacUrQueen)&&!queenRefundUsed[side])AddAction(actions,"Restore Queen",()=>RefundQueen(team),completedTurns[side]>=10,CooldownLabel(10-completedTurns[side]));
        if(HasBuff(team,AramBuffId.CreditCard))
            foreach(var kind in new[]{PieceType.Pawn,PieceType.Knight,PieceType.Bishop,PieceType.Rook,PieceType.Queen})
                AddAction(actions,$"Buy {kind}",()=>BuyPiece(team,kind),AramBalanceRules.CanPurchase(credit[side],bought[side],(PieceKind)kind),$"{AramBalanceRules.PurchasePrice((PieceKind)kind)} points",$"buy-{kind}");
        if(HasBuff(team,AramBuffId.OneManArmy))AddAction(actions,"Aim rifle",()=>EnterRifle(team),completedTurns[side]>=rifleReady[side],CooldownLabel(rifleReady[side]-completedTurns[side]));
    }
    private static string CombinedCooldown(int plies) => plies>0?$"{plies} combined moves":"Ready";
    private static string CooldownLabel(int turns) => turns>0?$"{turns} owner turns":"Ready";
    private static void AddAction(List<AramAbilityView.AbilityAction> actions,string label,Action action,bool enabled=true,string detail=null,string id=null)
    { actions.Add(new AramAbilityView.AbilityAction(label,action,enabled,detail,id)); }

    private void RollBattleGacha(PieceTeam team)
    {
        int side=Side(team); var empty=EmptySquares();
        if(tickets[side]<1||rollsThisTurn[side]>=1||empty.Count==0){Say("No tickets, turn limit reached, or no empty square.");return;}
        int roll=UnityEngine.Random.Range(0,100);
        PieceType kind=roll<70?PieceType.Pawn:roll<80?PieceType.Knight:roll<90?PieceType.Bishop:roll<99?PieceType.Rook:PieceType.Queen;
        empty.RemoveAll(p=>!game.AramCanPlace(kind,team,p,true));
        if(empty.Count==0){Say("No drop can keep both Kings safe; ticket retained.");return;}
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
        swapReady[side]=combinedPlies+AramBalanceRules.DoppelgangerCooldown;game.ClearSelection();Say("Both movement sets swapped.");game.AramRefreshPosition();
    }

    private void HideKing(PieceTeam team,bool initial=false)
    {
        var king=game.AramKing(team);if(!king||(!initial&&king.BoardPosition.y!=Home(team))){Say("King must be on the back rank.");return;}
        Choose($"{team}: choose an ally on your back rank to swap with the King.",p=>
        {
            var piece=At(p);if(!piece||piece==king||piece.Team!=team||p.y!=Home(team)||!game.AramRelocate(king,p,!initial,true))return false;
            hidingReady[Side(team)]=combinedPlies+AramBalanceRules.HidingKingCooldown;return true;
        },!initial);
    }

    private void LoadPassenger(PieceTeam team)
    {
        Choose("Choose an allied Rook without a passenger.",p=>
        {
            var rook=At(p);if(!rook||rook.Team!=team||rook.Type!=PieceType.Rook||passengers.ContainsKey(rook))return false;
            Choose("Choose an allied Pawn for this Rook to carry.",q=>
            {
                var pawn=At(q);if(!Live(rook)||!pawn||pawn.Team!=team||pawn.Type!=PieceType.Pawn||Mathf.Abs(q.x-rook.BoardPosition.x)>1||Mathf.Abs(q.y-rook.BoardPosition.y)>1)return false;
                if(!game.AramSafeRemoval(pawn,team))return false;
                var traits=new PassengerTraits(bloodthirsty.Contains(pawn),GetState(team).CommandantPawns.Contains(pawn),queenMastery.Contains(pawn),trapPawns.Contains(pawn));
                game.AramRemove(pawn,false);passengers[rook]=team;passengerTraits[rook]=traits;passengerReady[rook]=completedTurns[Side(team)]+1;
                AddMarker(rook,GetState(team).GetBuff(AramBuffId.MobileFortress),"");return true;
            });return true;
        });
    }

    private void Recruit(PieceTeam team)
    {
        Choose("Choose an enemy Pawn, Knight, Bishop or Rook to recruit.",p=>
        {
            var enemy=At(p);if(!enemy||enemy.Team==team||enemy.Type==PieceType.King||enemy.Type==PieceType.Queen)return false;
            bool full=EmptySquares(team,3).Count==0;
            Choose($"{team}: choose {(full?"a Pawn square":"an empty square")} in your first three ranks.",q=>
            {
                var occupant=At(q);if(!Live(enemy)||!SquareAvailable(q)||!InHome(q,team,3)||(occupant&&(!full||occupant.Type!=PieceType.Pawn)))return false;
                if(!game.AramCanPlace(enemy.Type,team,q,false,enemy))return false;
                var kind=enemy.Type;if(occupant&&occupant!=enemy)game.AramRemove(occupant);
                game.AramRemove(enemy,false);game.AramSpawn(kind,team,q,true);peaceUsed[Side(team)]=true;return true;
            });return true;
        });
    }

    internal bool IsSniperAttack(ChessPiece bishop,Vector2Int destination)
    {
        if(!active||networkMatch||!Live(bishop)||bishop.Type!=PieceType.Bishop||
            !HasBuff(bishop.Team,AramBuffId.AbsoluteSniper)||!sniperUntil.TryGetValue(bishop,out int expiry)||combinedPlies>=expiry||!SquareAvailable(destination))return false;
        var enemy=At(destination);var from=bishop.BoardPosition;var delta=destination-from;
        return enemy&&enemy.Team!=bishop.Team&&(enemy.Type!=PieceType.King||enemy.GetComponent<AramDecoyTag>())&&
            Mathf.Abs(delta.x)==Mathf.Abs(delta.y)&&MovementRules.IsPathClear(new UnityBoardAdapter(game.AramBoard),UnityBoardAdapter.ToSquare(from),UnityBoardAdapter.ToSquare(destination));
    }

    internal bool TrySniperAttack(ChessPiece bishop,Vector2Int destination)
    {
        if(!IsSniperAttack(bishop,destination)||game.CurrentTurn!=bishop.Team||game.InputLocked||game.PauseLocked||
            game.HasPendingPromotion||HasPendingDecision||!CanMove(bishop,destination)||!game.AramSafeSnipe(At(destination),bishop.Team))return false;
        var enemy=At(destination);var team=bishop.Team;var from=bishop.BoardPosition;
        game.AramAbilityCapture(team,enemy,$"Bishop on {from} sniped {enemy.Type} on {destination}",true);
        BeforeNormalMove(bishop,enemy,from,destination);
        sniperUntil.Remove(bishop);RemoveMarker(bishop);game.AramRemove(enemy,false);
        DetonateRemovedQueen(enemy,destination,bishop);
        CompleteOwnerTurn(team);AdvanceCombinedPly();game.AramCompleteAbilityTurn();return true;
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
