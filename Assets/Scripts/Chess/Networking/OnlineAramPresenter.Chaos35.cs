using System;
using System.Collections.Generic;
using System.Linq;
using ChessButWeird.Online;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal sealed partial class OnlineAramPresenter
{
    private string actionPieceKind;
    private void ResetPracticeDefinitions()
    {
        var originals = AramBuffLibrary.CreateBuiltInDefinitions();
        foreach (var original in originals)
        {
            var target = Definition((int)original.Id);
            if (target) target.Configure(original.Id, original.Tier, original.DisplayName, original.ShortName, original.Description, original.AccentColor, original.Summary);
            UnityEngine.Object.Destroy(original);
        }
    }
    private AramAbilityView.Presentation BuildChaos35Presentation()
    {
        var a = state.chaos;
        var view = new AramAbilityView.Presentation {
            Visible = Visible && !draftView.IsDraftVisible,
            Context = state.matchId + "|" + state.stateVersion + "|" + action,
            PresentationContext = state.matchId + "|" + a["phase"] + "|" + side?["buffId"] + "|" + a["deployments"]?.FirstOrDefault()?["id"] + "|" + action,
            RifleActive = aiming, RifleHint = "Hold right mouse to aim. Left mouse fires."
        };
        if (!view.Visible) return view;
        var summaries = new List<string>(); var details = new List<string>();
        foreach (var s in a["sides"] ?? new JArray())
        {
            var definition = (int?)s["buffId"] is int id ? Definition(id) : null;
            summaries.Add((string)s["team"] + ": " + (definition ? definition.DisplayName : "Choosing a buff..."));
            if (definition) details.Add((string)s["team"] + ": " + definition.Description);
        }
        view.Summary = string.Join("\n", summaries);
        view.Details = string.Join("\n\n", details);
        int ply = (int?)a["combinedPlies"] ?? 0, turns = (int?)side?["completedTurns"] ?? 0;
        int? buff = (int?)side?["buffId"];
        view.Status = "CHAOS / " + a["phase"] + "\n" + ply + " combined moves / " + turns + " owner turns";
        if (!CanInteract) view.Status += "\nWaiting for server...";
        void Add(string label, Action callback, string id, bool ready = true, string detail = null) =>
            view.Actions.Add(new AramAbilityView.AbilityAction(label, callback, CanInteract && ready, detail, id));
        void Target(string label, string kind, bool ready = true) => Add(label, () => { CancelTargeting(); action = kind; }, kind, ready);
        if ((string)a["phase"] == "Draft") view.Status += "\nWaiting for your opponent to choose...";
        if ((string)a["phase"] == "Setup")
        {
            view.InstructionRequired = true;
            if ((bool?)side?["setupComplete"] != true)
            {
                if (buff == 0) Target("Choose commandant Pawns", "SelectTargets");
                if (buff == 2) Target("Choose one Freestyle Knight", "SelectTargets");
                if (buff == 3) Target("Choose a Knight and Bishop", "SelectTargets");
                if (buff == 13) Target("Opening King swap: choose an ally", "HideKing");
                if (buff == 27) foreach (string kind in new[] { "Rook", "Bishop", "Knight" })
                    Add("Replace Queen with " + kind, () => Send(new AbilityCommand { kind = "ReplaceQueen", pieceKind = kind }), "ReplaceQueen-" + kind);
            }
            if (buff == 23 && (bool?)side?["formationComplete"] != true)
            {
                if (side?["formationDeadline"]?.Type == JTokenType.Date)
                    view.Status += "\nFormation ends (UTC): " + ((DateTime)side["formationDeadline"]).ToString("HH:mm:ss");
                Add("Arrange your army", () => {
                    CancelTargeting(); action = "ConfirmFormation";
                    foreach (var p in state.board.Where(p => p.team == team)) formation[p.id] = p.square;
                }, "Arrange");
            }
            if ((bool?)side?["setupComplete"] == true && buff != 23) view.Status += "\nSetup ready. Waiting for your opponent...";
        }
        else if (state.status == "InProgress")
        {
            var pending = a["deployments"]?.FirstOrDefault();
            if (pending != null)
            {
                view.InstructionRequired = true;
                string decision = (string)pending["action"];
                string instruction = decision == "QuestPawns" ? "Quest reward: choose a Pawn to become a Queen" :
                    decision == "Redeploy" ? "Choose a safe square for your promoted piece" : "Deploy " + pending["kind"];
                if ((string)pending["team"] == team) Target(instruction, "Deploy");
                else view.Status += "\nWaiting for opponent's reinforcement choice...";
            }
            else if ((string)a["escapeTeam"] == team) { view.InstructionRequired = true; Target("Escape: choose a nearby safe square", "Escape"); }
            else if (a["forcedPawn"]?.Type == JTokenType.Integer)
            { view.InstructionRequired = true; view.Status += "\nExtra move: use Pawn #" + a["forcedPawn"]; }
            else if (state.turn == team && a["rifle"]?.Type != JTokenType.Object)
            {
                if (buff == 3 && (bool?)side["doppelDisabled"] != true)
                {
                    bool ready = ply >= ((int?)side["swapReady"] ?? 0);
                    Add("Swap movement", () => Send(new AbilityCommand { kind = "ToggleSwap" }), "ToggleSwap", ready);
                    Add("Keep movement", () => Send(new AbilityCommand { kind = "KeepSwap" }), "KeepSwap", ready);
                    Target("Choose a new exchange pair", "ReselectSwap", ready);
                }
                if (buff == 10) Target("Recruit an enemy piece", "Recruit", ply >= 14 && (bool?)side["peaceFailed"] != true && (bool?)side["peaceUsed"] != true);
                if (buff == 12) { Target("Load nearby Pawn: Rook, then Pawn", "LoadPawn"); Target("Unload Pawn: Rook, then square", "UnloadPawn"); }
                if (buff == 13) Target("Hide King: choose a back-rank ally", "HideKing", ply >= ((int?)side["hidingReady"] ?? 0));
                if (buff == 14)
                {
                    int tickets = (int?)side["tickets"] ?? 0; view.Status += "\nTickets: " + tickets;
                    Add("Battle gacha", () => Send(new AbilityCommand { kind = "BattleGacha" }), "BattleGacha", tickets > 0 && ((int?)side["rollsThisTurn"] ?? 0) < 1);
                }
                if (buff == 25) Add("Aim rifle", () => Send(new AbilityCommand { kind = "AimRifle" }), "AimRifle", turns >= ((int?)side["rifleReady"] ?? 0));
                if (buff == 27 && (bool?)side["queenRefundUsed"] != true) Target("Restore Queen: choose an ally", "RestoreQueen", turns >= 10);
                if (buff == 31)
                {
                    int credit = (int?)side["credit"] ?? 0; view.Status += "\nBalance: " + credit + " points";
                    foreach (string kind in new[] { "Pawn", "Knight", "Bishop", "Rook", "Queen" })
                    {
                        int cost = kind == "Pawn" ? 2 : kind == "Rook" ? 5 : kind == "Queen" ? 10 : 4;
                        Add("Buy " + kind + " (" + cost + ")", () => { CancelTargeting(); action = "BuyPiece"; actionPieceKind = kind; },
                            "Buy-" + kind, credit >= 0 && (bool?)side["boughtThisTurn"] != true);
                    }
                }
            }
        }
        if (buff == 26) view.Details += "\n\nQuest captures: " + string.Join(", ", side["questCaptures"] ?? new JArray()) +
            "\nPawn promoted: " + ((bool?)side["questPromoted"] == true ? "Done" : "Pending") + " / Reward: " + ((bool?)side["questRewarded"] == true ? "Earned" : "Pending");
        if (buff == 32) view.Status += "\nCheckmate deadline: " + Math.Max(0, 25 - turns) + " owner turns";
        if (aiming)
        {
            view.InstructionRequired = true;
            Add("Fire", Fire, "FireRifle"); Add("Exit rifle", () => Send(new AbilityCommand { kind = "ExitRifle" }), "ExitRifle");
        }
        if (!string.IsNullOrEmpty(action))
        {
            view.InstructionRequired = true;
            view.Status += "\n" + ActionInstruction() + "\nSelected: " + string.Join(", ", targets);
            if (action == "ConfirmFormation")
            {
                view.Details += "\n\nPlanned formation:\n" + string.Join("\n", formation.Select(entry => {
                    var piece = state.board.First(p => p.id == entry.Key);
                    return piece.kind + " #" + entry.Key + ": " + piece.square + " -> " + entry.Value + (formationPiece == entry.Key ? " (selected)" : "");
                }));
                Add("Confirm formation", () => Send(new AbilityCommand { kind = action, formation = formation.Select(p => new FormationPlacement { pieceId = p.Key, square = p.Value }).ToList() }), "ConfirmFormation");
            }
            Add("Cancel selection", CancelTargeting, "CancelTargeting");
        }
        return view;
    }
    private string ActionInstruction()
    {
        switch (action)
        {
            case "BuyPiece": return "Choose an empty square in your half for your " + actionPieceKind + ".";
            case "LoadPawn": return targets.Count == 0 ? "Choose an allied Rook." : "Choose a nearby allied Pawn.";
            case "UnloadPawn": return targets.Count == 0 ? "Choose a Rook carrying a ready Pawn." : "Choose an empty square next to that Rook.";
            case "Recruit": return targets.Count == 0 ? "Choose an enemy Pawn, Knight, Bishop or Rook." : "Choose a square in your first three ranks.";
            case "SelectTargets": return (int?)side["buffId"] == 2 ? "Choose one allied Knight." : (int?)side["buffId"] == 0 ? "Choose your commandant Pawns." : "Choose one allied Knight and Bishop.";
            case "ReselectSwap": return "Choose a new allied Knight and Bishop.";
            case "RestoreQueen": return "Choose an ally other than a King or promoted Queen.";
            case "ConfirmFormation": return "Select a piece, then a square in your half. Occupied allied squares swap.";
            default: return "Choose a board square: " + action;
        }
    }
    private bool HandleChaos35Click(Vector2Int square, int? pieceId)
    {
        string target = ((char)('a' + square.x)).ToString() + (square.y + 1);
        if (action == "Deploy" || action == "Escape" || action == "BuyPiece")
            Send(new AbilityCommand { kind = action, target = target, pieceKind = actionPieceKind });
        else if ((action == "Recruit" || action == "UnloadPawn") && targets.Count == 1)
            Send(new AbilityCommand { kind = action, targetPieceId = action == "Recruit" ? (int?)targets[0] : null,
                pieceId = action == "UnloadPawn" ? (int?)targets[0] : null, target = target });
        else if (pieceId.HasValue)
        {
            var p = state.board.FirstOrDefault(p => p.id == pieceId.Value);
            if (p == null) return true;
            if (action == "HideKing" || action == "RestoreQueen")
            { if (p.team == team) Send(new AbilityCommand { kind = action, targetPieceId = pieceId }); }
            else if (action == "Recruit")
            { if (p.team != team && p.kind != "King" && p.kind != "Queen") targets.Add(pieceId.Value); }
            else if (action == "UnloadPawn")
            { if (p.team == team && p.kind == "Rook") targets.Add(pieceId.Value); }
            else
            {
                if (p.team != team) return true;
                int? buff = (int?)side["buffId"];
                if (action == "SelectTargets" && buff == 0 && p.kind != "Pawn" || action == "SelectTargets" && buff == 2 && p.kind != "Knight") return true;
                if ((action == "ReselectSwap" || action == "SelectTargets" && buff == 3) && p.kind != "Knight" && p.kind != "Bishop") return true;
                if (action == "LoadPawn" && (targets.Count == 0 ? p.kind != "Rook" : p.kind != "Pawn")) return true;
                if (targets.Contains(pieceId.Value)) targets.Remove(pieceId.Value); else targets.Add(pieceId.Value);
                int count = action == "SelectTargets" ? (buff == 0 ? Math.Min(3, state.board.Count(b => b.team == team && b.kind == "Pawn")) : buff == 2 ? 1 : 2) : 2;
                if (targets.Count == count)
                    Send(new AbilityCommand { kind = action, pieceIds = new List<int>(targets), pieceId = action == "LoadPawn" ? (int?)targets[0] : null,
                        targetPieceId = action == "LoadPawn" ? (int?)targets[1] : null });
            }
        }
        return true;
    }
}
