using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ChessButWeird.Domain;
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class AramAuditRunner
{
    private static int checks,ticks;
    private static readonly List<string> report=new List<string>();
    private static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly List<string> errors=new List<string>();
    public static void Run()
    {
        if(!Application.isBatchMode||SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null||
            !Application.dataPath.Replace('\\','/').Contains("/Logs/aram-headless/"))throw new Exception("Requires the isolated -nographics fixture project.");
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        Application.logMessageReceived+=(message,stack,type)=>
        {if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))errors.Add(message+"\n"+stack);};
        EditorApplication.update+=Wait;EditorApplication.EnterPlaymode();
    }
    private static void Wait()
    {
        if(!EditorApplication.isPlaying||++ticks<12)return;EditorApplication.update-=Wait;
        int exit=0;
        try{Audit();Check(errors.Count==0,"Runtime logged an error: "+string.Join("\n",errors));report.Add($"PASS {checks} ARAM assertions: production runtime / bridge / extracted Core rules; no rendering, accounts or transport.");}
        catch(Exception e){report.Add("FAIL "+e);exit=1;}
        File.WriteAllLines(Path.Combine(Path.GetDirectoryName(Application.dataPath),"audit-result.txt"),report);EditorApplication.Exit(exit);
    }
    private static object Call(object source,string name,params object[] args)
    {try{return source.GetType().GetMethod(name,flags).Invoke(source,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    private static T Field<T>(object source,string name)=>(T)source.GetType().GetField(name,flags).GetValue(source);
    private static void Set(object source,string name,object value)=>source.GetType().GetField(name,flags).SetValue(source,value);
    private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    private static ChessGame Match(AramBuffId buff,AramBuffId? enemy=null)
    {
        var host=new GameObject("ARAM fixture "+buff);var game=host.AddComponent<ChessGame>();var runtime=host.AddComponent<AramBuffRuntime>();game.Connect(runtime);
        foreach(var team in new[]{PieceTeam.White,PieceTeam.Black})
        {
            int rank=team==PieceTeam.White?0:7;var order=new[]{PieceType.Rook,PieceType.Knight,PieceType.Bishop,PieceType.Queen,PieceType.King,PieceType.Bishop,PieceType.Knight,PieceType.Rook};
            for(int x=0;x<8;x++){game.Add(team,order[x],x,rank);game.Add(team,PieceType.Pawn,x,rank+(team==PieceTeam.White?1:-1));}
        }
        runtime.BeginMatch(game);var pool=Field<List<AramBuffDefinition>>(runtime,"draftPool");
        var white=new List<AramBuffDefinition>{AramBuffLibrary.FindById(pool,buff)};
        var black=new List<AramBuffDefinition>();if(enemy.HasValue)black.Add(AramBuffLibrary.FindById(pool,enemy.Value));
        Call(runtime,"HandleDraftCompleted",white,black);
        while(runtime.IsSelectingSetupTargets)
        {
            var task=Field<object>(runtime,"currentTargetTask");var team=(PieceTeam)task.GetType().GetProperty("Team").GetValue(task);
            string kind=Field<object>(runtime,"currentTargetKind").ToString();var type=kind.Contains("Pawn")?PieceType.Pawn:kind.Contains("Bishop")?PieceType.Bishop:PieceType.Knight;
            var pending=Field<List<ChessPiece>>(runtime,"pendingTargetPieces");var candidate=game.GetActivePiecesForAram(team).Find(p=>p.Type==type&&!pending.Contains(p));
            Check(candidate!=null,"No setup candidate for "+kind);runtime.TryHandleSetupPieceClick(candidate);
        }
        return game;
    }
    private static void End(ChessGame game){game.Runtime.EndMatch();UnityEngine.Object.DestroyImmediate(game.gameObject);}
    private static void BeginDecision(AramBuffRuntime runtime)
    {var queue=Field<Queue<Action>>(runtime,"decisions");Check(queue.Count>0,"Expected a queued mandatory decision");queue.Dequeue()();}
    private static bool Select(AramBuffRuntime runtime,int x,int y)
    {
        var handler=Field<Func<Vector2Int,bool>>(runtime,"targetAction");Check(handler!=null,"No target selection is active");bool done=handler(new Vector2Int(x,y));
        if(done&&Field<Func<Vector2Int,bool>>(runtime,"targetAction")==handler){Set(runtime,"targetAction",null);Set(runtime,"targetCancel",null);}
        return done;
    }
    private static void ClearArmy(ChessGame game,params ChessPiece[] keep)
    {var retain=new HashSet<ChessPiece>(keep);foreach(var team in new[]{PieceTeam.White,PieceTeam.Black})foreach(var p in game.GetActivePiecesForAram(team))if(!retain.Contains(p))Call(game,"AramRemove",p,false);}
    private static void Ply(AramBuffRuntime runtime)=>Call(runtime,"AdvanceCombinedPly");
    private static void Audit()
    {
        new GameObject("Fixture camera",typeof(Camera)).tag="MainCamera";
        foreach(AramBuffId id in Enum.GetValues(typeof(AramBuffId)))
        {
            var game=Match(id);Check(game.Runtime.HasBuff(PieceTeam.White,id),"Missing buff "+id);Check(!game.InputLocked,"Setup did not release input: "+id);Check(!game.GameOver,"Initialization ended match: "+id);
            AuditTooltip(game,id);End(game);
        }
        report.Add("PASS all 35 buff initializations and setup transitions");
        TestClocks();TestActions();TestSpecialRules();TestCastle();TestLoot();TestQuest();TestRepetition();TestInteractions();TestDisguise();TestAutomaticSniper();TestRifleCursor();
    }
    private static void TestDisguise()
    {
        var game=Match(AramBuffId.Doppelganger);var runtime=game.Runtime;
        Check(runtime.GetDisplayBuffs(PieceTeam.White,PieceTeam.White)[0].Id==AramBuffId.Doppelganger,"Disguise hid the owner's own buff");
        var fake=runtime.GetDisplayBuffs(PieceTeam.White,PieceTeam.Black)[0];
        Check(fake.Tier==AramBuffTier.Gold&&fake.Id!=AramBuffId.Doppelganger,"Opponent did not see a different Gold buff");
        for(int i=0;i<10;i++)Check(runtime.GetDisplayBuffs(PieceTeam.White,PieceTeam.Black)[0]==fake,"Disguise changed on HUD refresh");
        Check(runtime.GetBuffs(PieceTeam.White)[0].Id==AramBuffId.Doppelganger&&runtime.HasBuff(PieceTeam.White,AramBuffId.Doppelganger),"Presentation disguise changed actual rules");
        Check(runtime.DisguisesBuff(PieceTeam.White,PieceTeam.Black)&&!runtime.DisguisesBuff(PieceTeam.White,PieceTeam.White),"Disguise viewer scope is wrong");
        game.IsBotGame=true;game.PlayerTeam=PieceTeam.White;game.CurrentTurn=PieceTeam.Black;
        Check(runtime.AbilityBuffSummary.Contains("Doppel")&&runtime.AbilityBuffDetails.Contains("Doppelganger"),"Solo buff panel followed bot's private buff instead of player");
        var actions=new List<AramAbilityView.AbilityAction>();runtime.GetAbilityActions(actions);
        Check(actions.Count==0,"Solo player could use actions on bot's turn");
        runtime.EndMatch();Check(!runtime.DisguisesBuff(PieceTeam.White,PieceTeam.Black)&&Field<AramBuffDefinition[]>(runtime,"disguisedBuff")[0]==null,"Disguise survived match reset");
        UnityEngine.Object.DestroyImmediate(game.gameObject);
        report.Add("PASS stable same-tier Doppelganger disguise, owner visibility and match reset");
    }
    private static void AuditTooltip(ChessGame game,AramBuffId id)
    {
        var root=new GameObject("Tooltip fixture",typeof(RectTransform),typeof(Canvas)).GetComponent<RectTransform>();root.sizeDelta=new Vector2(1280,720);
        var target=new GameObject("Buff name",typeof(RectTransform)).GetComponent<RectTransform>();target.SetParent(root,false);target.sizeDelta=new Vector2(220,30);target.anchoredPosition=new Vector2(430,260);
        var tooltip=root.gameObject.AddComponent<MatchHudTooltip>();tooltip.Initialize(root);var definition=game.Runtime.GetBuffs(PieceTeam.White)[0];
        tooltip.Show(target,definition.DisplayName,definition.Description+"\n\n"+game.Runtime.BuffProgress(PieceTeam.White,id));
        var panel=Field<RectTransform>(tooltip,"panel");Check(panel.rect.height<=root.rect.height-32,"Buff tooltip taller than screen: "+id);
        var body=Field<TextMeshProUGUI>(tooltip,"body");body.ForceMeshUpdate(true,true);Check(!body.isTextOverflowing,"Progress tooltip text clipped: "+id);
        UnityEngine.Object.DestroyImmediate(root.gameObject);
    }
    private static void TestClocks()
    {
        var game=Match(AramBuffId.AbsoluteSniper);var bishop=game.Board[2,0];var queen=game.Board[3,7];
        game.Runtime.BeforeNormalMove(bishop,queen,new Vector2Int(0,0),new Vector2Int(4,4));
        var charges=Field<Dictionary<ChessPiece,int>>(game.Runtime,"sniperUntil");Check(charges.ContainsKey(bishop),"Long non-Pawn capture did not charge Sniper");
        Ply(game.Runtime);for(int i=0;i<3;i++)Ply(game.Runtime);Check(charges.ContainsKey(bishop),"Sniper expired before four subsequent plies");Ply(game.Runtime);Check(!charges.ContainsKey(bishop),"Sniper did not expire after four subsequent plies");End(game);
        game=Match(AramBuffId.HighTechEra);var cannons=Field<Dictionary<ChessPiece,int>>(game.Runtime,"cannonTurns");
        for(int i=0;i<9;i++)Ply(game.Runtime);Check(cannons.Count==2,"Initial cannons expired before move 10");Ply(game.Runtime);Check(cannons.Count==0,"Initial cannons survived move 10");End(game);
        game=Match(AramBuffId.GachaBanner,AramBuffId.PlagueTown);var pawn=game.Board[0,1];var enemy=game.Board[0,6];
        game.Runtime.BeforeNormalMove(pawn,enemy,pawn.BoardPosition,enemy.BoardPosition);var infections=Field<Dictionary<ChessPiece,int>>(game.Runtime,"infectedUntil");
        Check(infections.ContainsKey(pawn),"Capture did not infect Pawn");Ply(game.Runtime);for(int i=0;i<3;i++)Ply(game.Runtime);Check(game.Board[0,1]==pawn,"Plague killed before four subsequent plies");Ply(game.Runtime);Check(!game.Board[0,1],"Plague survived expiry");
        var king=game.Board[4,0];game.Runtime.BeforeNormalMove(king,enemy,king.BoardPosition,enemy.BoardPosition);Check(!infections.ContainsKey(king),"Plague infected King");End(game);
        game=Match(AramBuffId.Doppelganger);var state=Call(game.Runtime,"GetState",PieceTeam.White);var selected=(ChessPiece)state.GetType().GetProperty("SwappedBishop").GetValue(state);
        foreach(var knight in game.GetActivePiecesForAram(PieceTeam.White).FindAll(p=>p.Type==PieceType.Knight))Call(game,"AramRemove",knight,false);
        Ply(game.Runtime);Check(!game.Runtime.SuppressesStandardMovement(selected),"Doppelganger did not restore the surviving Bishop");End(game);
        game=Match(AramBuffId.DefinitionOfAram);Field<int[]>(game.Runtime,"completedTurns")[0]=9;Ply(game.Runtime);Check(Field<int>(game.Runtime,"collapsedFiles")==0,"Collapse triggered before owner's turn 10");
        Field<int[]>(game.Runtime,"completedTurns")[0]=10;Ply(game.Runtime);Check(Field<int>(game.Runtime,"collapsedFiles")==1,"First collapse not triggered");for(int i=0;i<4;i++)Ply(game.Runtime);Check(Field<int>(game.Runtime,"collapsedFiles")==1,"Second collapse triggered early");Ply(game.Runtime);Check(Field<int>(game.Runtime,"collapsedFiles")==2,"Second collapse did not trigger after five plies");End(game);
        report.Add("PASS combined-clock expiry, King immunity, Doppelganger restoration and staged collapse");
    }
    private static void TestActions()
    {
        var game=Match(AramBuffId.GachaBanner);var runtime=game.Runtime;var own=game.Board[0,1];var enemy=game.Board[0,6];
        runtime.BeforeNormalMove(own,enemy,own.BoardPosition,enemy.BoardPosition);Check(Field<int[]>(runtime,"tickets")[0]==0,"Pawn capture granted a ticket");
        runtime.BeforeNormalMove(own,game.Board[0,7],own.BoardPosition,new Vector2Int(0,7));Check(Field<int[]>(runtime,"tickets")[0]==1,"Non-Pawn capture failed to grant one ticket");
        Check((bool)Call(game,"AramCanPlace",PieceType.Rook,PieceTeam.White,new Vector2Int(4,5),true,null),"Gacha wrongly rejects a rook drop shielded by an enemy Pawn");
        // Clear a file and test the actual hypothetical deployment checker.
        Call(game,"AramRemove",game.Board[4,6],false);
        Check(!(bool)Call(game,"AramCanPlace",PieceType.Rook,PieceTeam.White,new Vector2Int(4,5),true,null),"Gacha permits checking enemy King");
        Field<int[]>(runtime,"tickets")[0]=2;Call(runtime,"RollBattleGacha",PieceTeam.White);int left=Field<int[]>(runtime,"tickets")[0];Check(left==1,"Gacha did not spend exactly one ticket");Call(runtime,"RollBattleGacha",PieceTeam.White);Check(Field<int[]>(runtime,"tickets")[0]==left,"Gacha exceeds once-per-turn limit");End(game);
        game=Match(AramBuffId.CreditCard);runtime=game.Runtime;Call(runtime,"BuyPiece",PieceTeam.White,PieceType.Queen);Check(Select(runtime,0,3),"Credit placement failed");Check(Field<int[]>(runtime,"credit")[0]==-10,"Credit debt was not debited");
        Call(runtime,"BuyPiece",PieceTeam.White,PieceType.Pawn);Check(Field<Func<Vector2Int,bool>>(runtime,"targetAction")==null,"Purchase started while in debt");End(game);
        game=Match(AramBuffId.MobileFortress);runtime=game.Runtime;Call(runtime,"LoadPassenger",PieceTeam.White);Check(Select(runtime,0,0),"Carrier selection failed");Check(!Select(runtime,7,1),"Carrier swallowed a distant Pawn");Check(Select(runtime,0,1),"Nearby passenger did not load");
        Call(runtime,"UnloadPassenger",PieceTeam.White);Check(!Select(runtime,0,0),"Passenger unloaded in loading turn");Call(runtime,"CompleteOwnerTurn",PieceTeam.White);Call(runtime,"UnloadPassenger",PieceTeam.White);Check(Select(runtime,0,0)&&Select(runtime,0,1),"Passenger failed to unload next turn");Check(game.Board[0,1]&&game.Board[0,1].Type==PieceType.Pawn,"Unloaded passenger missing");End(game);
        game=Match(AramBuffId.SacUrQueen);runtime=game.Runtime;BeginDecision(runtime);Check(Select(runtime,0,0),"Initial Queen replacement selection failed");Check(game.Board[3,0].Type==PieceType.Rook,"Initial Queen was not replaced with chosen type");
        Field<int[]>(runtime,"completedTurns")[0]=9;Call(runtime,"RefundQueen",PieceTeam.White);Check(Field<Func<Vector2Int,bool>>(runtime,"targetAction")==null,"Queen refund offered before turn 10");Field<int[]>(runtime,"completedTurns")[0]=10;Call(runtime,"RefundQueen",PieceTeam.White);Check(Select(runtime,0,1)&&game.Board[0,1].Type==PieceType.Queen,"Queen refund failed at turn 10");End(game);
        report.Add("PASS capture tickets, safe gacha / turn cap, credit debt, nearby transport and Queen refund");
    }
    private static void TestSpecialRules()
    {
        var game=Match(AramBuffId.KingIsPoorPiece);Check(!game.GetActivePiecesForAram(PieceTeam.White).Exists(p=>p.Type==PieceType.King)&&game.Board[4,0].Type==PieceType.Queen,"Kingless initial replacement failed");
        Check(!(bool)Call(game,"AramFinishIfKingMissing"),"Kingless army lost because King is absent");Field<int[]>(game.Runtime,"completedTurns")[0]=24;Check(!(bool)Call(game.Runtime,"TryFinishTurnDeadline"),"Kingless deadline early");Field<int[]>(game.Runtime,"completedTurns")[0]=25;Check((bool)Call(game.Runtime,"TryFinishTurnDeadline")&&game.Winner==PieceTeam.Black,"Kingless deadline did not lose at 25");End(game);
        game=Match(AramBuffId.DoubleEdgedTrap);var pawn=game.Board[0,6];Call(game,"AramRemove",game.Board[0,0],false);Call(game,"AramRelocate",pawn,new Vector2Int(0,0),false,false,true);
        Call(game.Runtime,"RegisterSpawn",pawn);Check(game.Runtime.SuppressesStandardMovement(pawn),"Far-rank trapped Pawn retained normal movement");Check(game.Runtime.IsLegalAramMove(pawn,pawn.BoardPosition,new Vector2Int(0,1),game.Board),"Far-rank trapped Pawn cannot move orthogonally");End(game);
        game=Match(AramBuffId.Paratrooper);var original=game.Board[0,1];var promoted=(ChessPiece)Call(game,"AramReplace",original,PieceType.Queen,PieceTeam.White);Call(game.Runtime,"PawnPromoted",original,promoted);BeginDecision(game.Runtime);Check(Select(game.Runtime,0,3)&&game.Board[0,3]==promoted,"Paratrooper did not relocate promoted piece");Check(game.Statistics.MoveCount==0,"Paratrooper counted an extra move");End(game);
        game=Match(AramBuffId.HidingKing);BeginDecision(game.Runtime);Check(Select(game.Runtime,0,0)&&game.Board[0,0].Type==PieceType.King,"Initial Hiding King swap failed");Check(Field<int[]>(game.Runtime,"hidingReady")[0]==10,"Hiding King uses wrong clock");End(game);
        game=Match(AramBuffId.AutoCastling);Check(game.Board[6,0].Type==PieceType.King&&game.Board[5,0].Type==PieceType.Rook&&game.Board[4,0].Type==PieceType.Knight&&game.Board[7,0].Type==PieceType.Bishop,"Auto Castling did not preserve blockers");Check(game.GetActivePiecesForAram(PieceTeam.White).Count==16&&game.Statistics.MoveCount==0,"Auto Castling deleted a piece or spent a move");End(game);
        game=Match(AramBuffId.SuicideBomber);var king=game.Board[4,0];Call(game,"AramRemove",game.Board[3,0],true);Check(!game.Board[2,0]&&!game.Board[3,1]&&game.Board[4,0]==king,"Non-move Queen destruction did not detonate or harmed King");End(game);
        report.Add("PASS Kingless deadline, trap, Paratrooper, Hiding King and unconditional Auto Castling");
    }
    private static void TestRepetition()
    {
        var game=Match(AramBuffId.GhostArmy);string before=(string)game.Runtime.GetType().GetProperty("RepetitionState",flags).GetValue(game.Runtime);
        Set(game.Runtime,"combinedPlies",99);Field<int[]>(game.Runtime,"completedTurns")[0]=50;
        Check(before==(string)game.Runtime.GetType().GetProperty("RepetitionState",flags).GetValue(game.Runtime),"Absolute irrelevant clock prevents passive-buff repetition");
        for(int i=0;i<5;i++)Call(game,"RecordCurrentPosition");Check(!(bool)Call(game,"HasThreefoldRepetition"),"Repeated free refresh caused a draw");
        game.Statistics.AddLocal(Team.White,"committed fixture move",false,null);Call(game,"RecordCurrentPosition");Check(!(bool)Call(game,"HasThreefoldRepetition"),"Second recorded position caused a draw");
        game.Statistics.AddLocal(Team.Black,"committed fixture move",false,null);Call(game,"RecordCurrentPosition");Check((bool)Call(game,"HasThreefoldRepetition"),"Third recorded position did not cause repetition");End(game);
        game=Match(AramBuffId.CreditCard);before=(string)game.Runtime.GetType().GetProperty("RepetitionState",flags).GetValue(game.Runtime);Field<int[]>(game.Runtime,"credit")[0]=5;
        Check(before!=(string)game.Runtime.GetType().GetProperty("RepetitionState",flags).GetValue(game.Runtime),"Credit resources absent from draw position identity");End(game);
        report.Add("PASS repetition state: relevant resources, mature passive clocks and once-per-committed-move recording");
    }
    private static void TestInteractions()
    {
        var game=Match(AramBuffId.CommandantPawn);var marker=game.Board[0,1].GetComponent<AramBuffPieceMarker>();
        Check(marker&&marker.enabled,"Committed Commandant marker is absent");Call(game.Runtime,"ApplyAllMarkers");
        Check(game.Board[0,1].GetComponents<AramBuffPieceMarker>().Length==1&&game.Board[0,1].GetComponent<AramBuffPieceMarker>()==marker&&marker.enabled,"Marker refresh duplicated or scheduled destruction of the current marker");
        Check(Field<Transform>(marker,"markerRoot")&&Field<Transform>(marker,"markerRoot").gameObject.activeSelf&&Field<TextMesh>(marker,"label")==null,"Committed marker lacks its small ring or still has a text label");End(game);
        game=Match(AramBuffId.AbsoluteSniper,AramBuffId.SuicideBomber);var bishop=game.Board[2,0];var king=game.Board[4,0];var enemyKing=game.Board[4,7];var queen=game.Board[3,7];ClearArmy(game,bishop,king,enemyKing,queen);
        Call(game,"AramRelocate",enemyKing,new Vector2Int(7,7),false,false,true);game.Add(PieceTeam.Black,PieceType.Rook,4,7);
        Call(game,"AramRelocate",bishop,new Vector2Int(1,1),false,false,true);Call(game,"AramRelocate",queen,new Vector2Int(3,3),false,false,true);game.Add(PieceTeam.White,PieceType.Pawn,4,3);
        Check((bool)Call(game,"AramSafeRemoval",queen,PieceTeam.White)&&!(bool)Call(game,"AramSafeSnipe",queen,PieceTeam.White),"Sniper safety failed to project loss of the blast's King shield");
        Field<Dictionary<ChessPiece,int>>(game.Runtime,"sniperUntil")[bishop]=5;
        Check(!(bool)Call(game.Runtime,"TrySniperAttack",bishop,new Vector2Int(3,3))&&game.Board[3,3]==queen,"Unsafe exploding snipe was committed");End(game);
        game=Match(AramBuffId.SuicideBomber,AramBuffId.SuicideBomber);var whiteQueen=game.Board[3,0];var blackQueen=game.Board[3,7];king=game.Board[4,0];enemyKing=game.Board[4,7];ClearArmy(game,whiteQueen,blackQueen,king,enemyKing);
        Call(game,"AramRelocate",whiteQueen,new Vector2Int(3,3),false,false,true);Call(game,"AramRelocate",blackQueen,new Vector2Int(4,4),false,false,true);game.Add(PieceTeam.Black,PieceType.Pawn,5,5);
        Call(game,"AramRemove",whiteQueen,true);Check(!game.Board[4,4]&&!game.Board[5,5]&&game.Board[4,0]==king&&game.Board[4,7]==enemyKing,"Second original Queen failed to chain-explode correctly");End(game);
        game=Match(AramBuffId.Paratrooper,AramBuffId.PlagueTown);var original=game.Board[0,1];var captured=game.Board[0,6];game.Runtime.BeforeNormalMove(original,captured,original.BoardPosition,captured.BoardPosition);
        var replacement=(ChessPiece)Call(game,"AramReplace",original,PieceType.Queen,PieceTeam.White);Call(game.Runtime,"PawnPromoted",original,replacement);Check(Field<Dictionary<ChessPiece,int>>(game.Runtime,"infectedUntil").ContainsKey(replacement),"Promotion cured Plague without permission");End(game);
        game=Match(AramBuffId.KingIsPoorPiece);ClearArmy(game);Check((bool)Call(game,"AramFinishIfKingMissing")&&game.Drawn,"Simultaneous royal-condition loss awarded a win to an eliminated army");End(game);
        game=Match(AramBuffId.KingIsPoorPiece);Field<int[]>(game.Runtime,"completedTurns")[0]=25;var queue=Field<Queue<Action>>(game.Runtime,"decisions");queue.Enqueue(()=>{});queue.Enqueue(()=>{});
        Check(game.Runtime.HandleAbilityInput()&&!game.GameOver,"Queued decisions failed to retain input until the last decision");game.Runtime.HandleAbilityInput();Check(game.GameOver&&game.Winner==PieceTeam.Black,"Empty final decision did not resolve the deferred turn deadline");End(game);
        report.Add("PASS marker reuse, exploding Sniper safety, chained bombs, Plague after promotion and simultaneous elimination");
    }
    private static void TestCastle()
    {
        var game=Match(AramBuffId.StrongFortress);var king=game.Board[4,0];var enemyKing=game.Board[4,7];var left=game.Board[0,0];var right=game.Board[7,0];ClearArmy(game,king,enemyKing,left,right);
        Call(game,"AramRelocate",enemyKing,new Vector2Int(7,7),false,false,true);var attacker=game.Add(PieceTeam.Black,PieceType.Rook,4,7);
        Check((bool)Call(game,"IsTeamInCheck",PieceTeam.White),"Fortress setup must be checked");
        Check((bool)Call(game,"IsCastlingMove",king,king.BoardPosition,new Vector2Int(2,0))&&(bool)Call(game,"DoesMoveKeepTeamKingSafe",king,king.BoardPosition,new Vector2Int(2,0),PieceTeam.White),"Fortress could not castle out of check safely");
        Call(game,"AramRelocate",king,new Vector2Int(2,0),false,false,true);Call(game,"AramRelocate",left,new Vector2Int(3,1),false,false,true);game.Runtime.OnMoveAccepted(king,new Vector2Int(4,0),new Vector2Int(2,0),game.Board);
        Check((bool)Call(game,"IsCastlingMove",king,king.BoardPosition,new Vector2Int(6,0)),"Fortress cannot castle the other wing from c-file");
        game.Runtime.OnMoveAccepted(king,new Vector2Int(2,0),new Vector2Int(6,0),game.Board);Check(!(bool)Call(game,"IsCastlingMove",king,king.BoardPosition,new Vector2Int(6,0)),"Fortress reused a spent wing");End(game);
        report.Add("PASS extracted Core castling: checked start, safe finish, second wing and no repeated wing");
    }
    private static void TestLoot()
    {
        var game=Match(AramBuffId.LootBox);var king=game.Board[4,0];var black=game.Board[4,7];ClearArmy(game,king,black);
        Call(game,"AramRelocate",king,new Vector2Int(3,0),false,false,true);Set(game.Runtime,"collapsedFiles",3);
        for(int x=3;x<=4;x++)for(int y=0;y<8;y++)if(!game.Board[x,y])game.Add(y<4?PieceTeam.White:PieceTeam.Black,PieceType.Pawn,x,y);
        for(int i=0;i<14;i++)Call(game.Runtime,"DropCrate",PieceTeam.White,25);
        Check(game.Board[3,0]==king&&game.Board[4,7]==black,"Loot 25 landed on a King");
        Call(game.Runtime,"DropCrate",PieceTeam.White,50);Check(!game.Board[3,0]||!game.Board[4,7],"Loot 50 did not permit King landing when only Kings remain eligible");Check(game.Statistics.MoveCount==0,"Loot destruction was counted as a chess move");End(game);
        report.Add("PASS occupied loot landing, milestone King protection and statistics event semantics");
    }
    private static void TestAutomaticSniper()
    {
        var game=Match(AramBuffId.AbsoluteSniper);var runtime=game.Runtime;
        var bishop=game.Board[2,0];var king=game.Board[4,0];var enemyKing=game.Board[4,7];
        ClearArmy(game,bishop,king,enemyKing);
        Call(game,"AramRelocate",bishop,new Vector2Int(1,1),false,false,true);
        var enemy=game.Add(PieceTeam.Black,PieceType.Rook,3,3);
        var charges=Field<Dictionary<ChessPiece,int>>(runtime,"sniperUntil");charges[bishop]=5;
        var actions=new List<AramAbilityView.AbilityAction>();runtime.GetAbilityActions(actions);
        Check(!actions.Exists(a=>a.Label=="Snipe"),"Sniper still requires a separate activation button");
        Check((bool)Call(runtime,"IsSniperAttack",bishop,new Vector2Int(3,3)),"Charged diagonal capture is not empowered");
        Check(!(bool)Call(runtime,"TrySniperAttack",bishop,new Vector2Int(7,7)),"Sniper accepted an empty square");
        var blocker=game.Add(PieceTeam.White,PieceType.Pawn,2,2);
        Check(!(bool)Call(runtime,"IsSniperAttack",bishop,new Vector2Int(3,3)),"Sniper shot through a blocking piece");Call(game,"AramRemove",blocker,false);
        game.PauseLocked=true;Check(!(bool)Call(runtime,"TrySniperAttack",bishop,new Vector2Int(3,3)),"Paused Sniper executed");game.PauseLocked=false;
        game.CurrentTurn=PieceTeam.Black;Check(!(bool)Call(runtime,"TrySniperAttack",bishop,new Vector2Int(3,3)),"Sniper executed on opponent turn");game.CurrentTurn=PieceTeam.White;
        Check((bool)Call(runtime,"TrySniperAttack",bishop,new Vector2Int(3,3)),"Automatic Sniper did not capture");
        Check(game.Board[1,1]==bishop&&!game.Board[3,3]&&bishop.BoardPosition==new Vector2Int(1,1),"Sniper moved the Bishop or kept the victim");
        Check(!charges.ContainsKey(bishop)&&game.CurrentTurn==PieceTeam.Black&&game.Statistics.MoveCount==1,"Sniper did not consume charge and exactly one move");
        bool captureRecorded=false;foreach(var row in game.Statistics.Entries)if(row.IsMove&&row.Captured==PieceKind.Rook)captureRecorded=true;
        Check(captureRecorded&&!runtime.HasPendingDecision,"Sniper capture lost statistics or asked for confirmation");
        End(game);
        game=Match(AramBuffId.AbsoluteSniper);runtime=game.Runtime;bishop=game.Board[2,0];king=game.Board[4,0];enemyKing=game.Board[4,7];ClearArmy(game,bishop,king,enemyKing);
        Call(game,"AramRelocate",bishop,new Vector2Int(4,2),false,false,true);game.Add(PieceTeam.Black,PieceType.Rook,4,6);enemy=game.Add(PieceTeam.Black,PieceType.Knight,6,4);
        Field<Dictionary<ChessPiece,int>>(runtime,"sniperUntil")[bishop]=5;
        Check(!(bool)Call(game,"DoesMoveKeepTeamKingSafe",bishop,bishop.BoardPosition,new Vector2Int(5,3),PieceTeam.White),"Non-capture exposed the King");
        Check((bool)Call(game,"DoesMoveKeepTeamKingSafe",bishop,bishop.BoardPosition,new Vector2Int(6,4),PieceTeam.White),"Ranged capture incorrectly moved a pinned Bishop in safety projection");
        Check((bool)Call(runtime,"TrySniperAttack",bishop,new Vector2Int(6,4))&&game.Board[4,2]==bishop,"Pinned Bishop could not shoot while shielding King");
        End(game);
        report.Add("PASS automatic Sniper: charge, blocked/empty targets, pause/turn guards, stationary capture, one move, King safety and statistics");
    }
    private static void TestRifleCursor()
    {
        var game=Match(AramBuffId.OneManArmy);var runtime=game.Runtime;
        var savedLock=Cursor.lockState;bool savedVisible=Cursor.visible;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        Call(runtime,"EnterRifle",PieceTeam.White);
        Check(runtime.RifleActive&&Field<bool>(runtime,"rifleMouseLocked"),"FPS entry did not request automatic mouse locking");
        var keyboard=InputSystem.AddDevice<Keyboard>();
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftAlt));InputSystem.Update();keyboard.MakeCurrent();
        Check(keyboard.leftAltKey.wasPressedThisFrame,"Fixture did not deliver ALT input");Call(runtime,"TickRifle");
        Check(!Field<bool>(runtime,"rifleMouseLocked"),"ALT did not release cursor lock preference");
        Check(Cursor.visible,"ALT did not show cursor");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightAlt));InputSystem.Update();Call(runtime,"TickRifle");
        Check(Field<bool>(runtime,"rifleMouseLocked"),"Right ALT did not request relocking");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
        Call(runtime,"SetRifleCursorSuspended",true);Check(Field<bool>(runtime,"rifleCursorSuspended")&&Cursor.visible,"Pause/focus loss did not release cursor");
        Call(runtime,"SetRifleCursorSuspended",false);Check(!Field<bool>(runtime,"rifleCursorSuspended")&&Field<bool>(runtime,"rifleMouseLocked"),"Resume lost lock preference");
        Call(runtime,"ExitRifle",false);
        Check(!runtime.RifleActive&&Cursor.lockState==CursorLockMode.None&&Cursor.visible&&Field<int[]>(runtime,"rifleReady")[0]==0,"FPS exit did not restore cursor or retain unused shot");
        Call(runtime,"EnterRifle",PieceTeam.White);Call(runtime,"ExitRifle",true);
        Check(Field<int[]>(runtime,"rifleReady")[0]==3,"FPS shot cooldown changed");
        Field<int[]>(runtime,"rifleReady")[0]=0;Call(runtime,"EnterRifle",PieceTeam.White);runtime.enabled=false;
        Check(!runtime.RifleActive&&Cursor.visible,"Disabling runtime stranded FPS cursor/camera");
        InputSystem.RemoveDevice(keyboard);End(game);Cursor.lockState=savedLock;Cursor.visible=savedVisible;
        report.Add("PASS FPS cursor intent, left/right ALT input, suspend/resume, exit/disable restoration and shot cooldown (headless; no physical cursor capture)");
    }
    private static void TestQuest()
    {
        var game=Match(AramBuffId.EveryManForHimself);var runtime=game.Runtime;
        foreach(var kind in new[]{PieceType.Pawn,PieceType.Knight,PieceType.Bishop,PieceType.Rook,PieceType.Queen})
        {
            var own=game.GetActivePiecesForAram(PieceTeam.White).Find(p=>p.Type==kind);var enemy=game.GetActivePiecesForAram(PieceTeam.Black).Find(p=>p.Type==kind);
            runtime.BeforeNormalMove(own,enemy,own.BoardPosition,enemy.BoardPosition);
        }
        var pawn=game.Board[0,1];var promoted=(ChessPiece)Call(game,"AramReplace",pawn,PieceType.Queen,PieceTeam.White);Call(runtime,"PawnPromoted",pawn,promoted);
        BeginDecision(runtime);Check(!Select(runtime,1,1)&&Select(runtime,2,1),"Army quest did not select exactly two Pawns");Check(game.Board[1,1].Type==PieceType.Queen&&game.Board[2,1].Type==PieceType.Queen,"Army quest reward missing");
        Call(runtime,"TryQueueQuestReward",PieceTeam.White);Check(Field<Queue<Action>>(runtime,"decisions").Count==0,"Army quest reward repeated");Check((bool)Call(runtime,"DrawIsLoss",PieceTeam.White),"Army quest draw policy missing");End(game);
        report.Add("PASS five-type quest, promotion requirement, two-Queen reward, once-only and draw loss");
    }
}
