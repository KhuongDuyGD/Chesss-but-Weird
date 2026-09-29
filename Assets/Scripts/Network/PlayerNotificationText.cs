using System;
using System.Globalization;
using System.Text;

/// <summary>Player-facing English copy for untrusted server and operating-system errors.</summary>
public static class PlayerNotificationText
{
    public const string Retry = "Something went wrong. Please try again.";
    public const string Connection = "Unable to contact the server. Check your connection and try again.";
    public const string Timeout = "The request timed out. Please try again.";

    public static string FromException(Exception error)
    {
        if (error is ApiException api)
            return FromServer(api.Message, api.StatusCode == 0 ? -1 : api.StatusCode, api.ErrorCode);
        if (error is TimeoutException)
            return Timeout;
        if (error is OperationCanceledException)
            return "The request was canceled. Please try again.";
        // Exception.Message can be localized by the player's OS. Never show it directly.
        return Retry;
    }

    public static string FromServer(string message, long statusCode = -1, string code = null)
    {
        string known = KnownError(Normalize(code));
        if (known != null) return known;

        string text = Normalize(message);
        known = KnownError(text);
        if (known != null) return known;

        if (Contains(text, "badcredentials", "invalidcredentials", "invalidemailorpassword", "incorrectemailorpassword",
            "invalidusernameorpassword", "incorrectusernameorpassword", "wrongpassword", "incorrectpassword",
            "saimatkhau", "saitaikhoanhoacmatkhau", "emailhoacmatkhaukhongdung", "thongtindangnhapkhongchinhxac"))
            return "Incorrect email or password. Please try again.";
        if (Contains(text, "sessionexpired", "loginexpired", "tokenexpired", "norefreshtoken"))
            return "Your session has expired. Please log in again.";
        if (Contains(text, "accountlocked", "accountdisabled", "accountbanned", "taikhoanbikhoa"))
            return "This account is unavailable. Please contact support.";
        if (Contains(text, "emailalreadyexists", "emailalreadyregistered", "emailalreadyused", "emailalreadyinuse",
            "emailhasalreadybeentaken", "emailexists", "emaildatontai", "emaildaduocsudung", "emailduocsudung"))
            return "This email is already registered. Log in or use another email.";
        if (Contains(text, "usernamealready", "usernameexists", "usernamedatontai", "tendangnhapdatontai", "tentaikhoandatontai"))
            return "This username is already taken. Please choose another.";
        if (Contains(text, "invalidemail", "emailkhonghople"))
            return "Please enter a valid email address.";
        if (Contains(text, "passwordmust", "passwordtooshort", "matkhauphaico", "matkhauquangan"))
            return "Your password does not meet the requirements. Please check it and try again.";
        if (Contains(text, "notenoughcurrency", "notenoughcoins", "notenoughgold", "insufficientfunds", "insufficientbalance",
            "khongdutien", "khongdukcoin"))
            return "You do not have enough currency for this action.";
        if (Contains(text, "roomnotfound", "roomdoesnotexist", "phongkhongtontai", "khongtimthayphong"))
            return "Room not found. Check the room code and try again.";
        if (Contains(text, "roomfull", "phongdaday", "phongday"))
            return "This room is full. Please join another room.";
        if (Contains(text, "timedout", "timeout", "hethoigian"))
            return Timeout;
        if (Contains(text, "cannotresolve", "couldnotresolve", "cannotconnect", "connectionfailed", "unabletocontactserver",
            "unabletocontacttheserver", "networkunreachable", "networkisoffline", "failedtoconnect"))
            return Connection;
        if (Contains(text, "maintenance", "baotri"))
            return "The server is under maintenance. Please try again later.";
        if (Contains(text, "invalidjson", "invalidrefreshresponse", "didnotcontainbothtokens", "incompletegacharesult",
            "wrongnumberofrewards", "didnotconfirmequipment", "unabletoparseserverresponse"))
            return "The server returned an incomplete response. Please try again.";

        // Unknown prose (including ASCII-only foreign languages) is never forwarded to players.
        switch (statusCode)
        {
            case 0: return Connection;
            case 400: case 422: return "Please check your details and try again.";
            case 401: return "Please log in again to continue.";
            case 403: return "You do not have permission to do that.";
            case 404: return "The requested content is unavailable. Please refresh and try again.";
            case 408: case 504: return Timeout;
            case 409: return "This action conflicts with your current state. Please refresh and try again.";
            case 429: return "Too many requests. Please wait a moment and try again.";
        }
        return statusCode >= 500
            ? "The server is temporarily unavailable. Please try again later."
            : Retry;
    }

    public static string MatchResult(string result)
    {
        switch (Normalize(result))
        {
            case "whitewon": return "White wins";
            case "blackwon": return "Black wins";
            case "draw": return "Draw";
            default: return "Match ended";
        }
    }

    public static string MatchEndReason(string reason, bool draw = false)
    {
        switch (Normalize(reason))
        {
            case "checkmate": return "Checkmate";
            case "stalemate": return "Stalemate";
            case "resignation": return "Resignation";
            case "timeout": case "timeforfeit": return "Time ran out";
            case "drawagreement": case "drawbyagreement": case "agreeddraw": case "agreement": case "drawaccepted": return "Draw by agreement";
            case "threefoldrepetition": case "repetition": return "Threefold repetition";
            case "fiftymoverule": case "50moverule": return "Fifty-move rule";
            case "insufficientmaterial": return "Insufficient material";
            case "disconnect": case "disconnection": return "Player disconnected";
            default: return draw ? "Draw" : "Match ended";
        }
    }

    private static string KnownError(string code)
    {
        switch (code)
        {
            case "invalidcredentials": case "badcredentials": case "authenticationfailed":
                return "Incorrect email or password. Please try again.";
            case "sessionexpired": case "tokenexpired": case "invalidtoken": case "unauthorized": case "unauthenticated":
                return "Your session has expired. Please log in again.";
            case "emailalreadyexists": case "emailalreadyregistered": case "emailalreadyused": case "emailtaken": case "duplicateemail":
                return "This email is already registered. Log in or use another email.";
            case "usernamealreadyexists": case "usernametaken": case "duplicateusername":
                return "This username is already taken. Please choose another.";
            case "accountdisabled": case "accountlocked": case "accountbanned":
                return "This account is unavailable. Please contact support.";
            case "invalidemail": return "Please enter a valid email address.";
            case "invalidusername": return "Please check your username and try again.";
            case "invalidpassword": case "weakpassword":
                return "Your password does not meet the requirements. Please check it and try again.";
            case "insufficientfunds": case "insufficientbalance": case "insufficientcurrency":
                return "You do not have enough currency for this action.";
            case "itemnotowned": return "You do not own this item.";
            case "iteminactive": case "bannerinactive": return "This item or banner is no longer available.";
            case "roomnotfound": return "Room not found. Check the room code and try again.";
            case "roomfull": return "This room is full. Please join another room.";
            case "alreadyinroom": return "Leave your current room before joining another.";
            case "playersnotready": case "roomnotready": return "Both players must be ready before starting.";
            case "matchpaused": return "The match is paused. Wait for it to resume before moving.";
            case "matchfinished": case "gameover": return "This match has already ended.";
            case "notyourturn": return "Please wait for your turn.";
            case "invalidmove": case "illegalmove": return "That move is not allowed. Please choose another.";
            case "ratelimited": case "toomanyrequests": return "Too many requests. Please wait a moment and try again.";
            case "connectionfailed": case "networkerror": return Connection;
            case "timeout": case "requesttimeout": return Timeout;
            default: return null;
        }
    }

    private static bool Contains(string text, params string[] phrases)
    {
        foreach (string phrase in phrases)
            if (text.IndexOf(phrase, StringComparison.Ordinal) >= 0) return true;
        return false;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = new StringBuilder(value.Length);
        foreach (char c in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            char lower = char.ToLowerInvariant(c);
            if (lower == '\u0111') lower = 'd';
            if (char.IsLetterOrDigit(lower)) normalized.Append(lower);
        }
        return normalized.ToString();
    }
}
