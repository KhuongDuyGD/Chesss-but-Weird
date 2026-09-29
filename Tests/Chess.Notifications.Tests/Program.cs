using System.Globalization;

int checks = 0;
void Expect(string actual, string expected, string description)
{
    if (actual != expected)
        throw new InvalidOperationException(description + ": " + actual);
    if (actual.Any(c => c > 127))
        throw new InvalidOperationException(description + " returned non-English source text.");
    checks++;
}

const string credentials = "Incorrect email or password. Please try again.";
const string emailTaken = "This email is already registered. Log in or use another email.";
const string usernameTaken = "This username is already taken. Please choose another.";
foreach (string source in new[] { "Sai mật khẩu", "Email hoặc mật khẩu không đúng", "Thông tin đăng nhập không chính xác", "Bad credentials", "INVALID_CREDENTIALS" })
    Expect(PlayerNotificationText.FromServer(source, 401), credentials, "Login failure: " + source);
foreach (string source in new[] { "Email đã tồn tại", "Email đã được sử dụng", "Email already exists" })
    Expect(PlayerNotificationText.FromServer(source, 409), emailTaken, "Duplicate email: " + source);
foreach (string source in new[] { "Tên đăng nhập đã tồn tại", "Username đã tồn tại", "Username already exists" })
    Expect(PlayerNotificationText.FromServer(source, 409), usernameTaken, "Duplicate username: " + source);

// Structured codes take precedence over arbitrary localized prose.
Expect(PlayerNotificationText.FromServer("Accès refusé", 400, "INVALID_CREDENTIALS"), credentials, "Auth code precedence");
Expect(PlayerNotificationText.FromServer("不允许移动", code: "MATCH_PAUSED"),
    "The match is paused. Wait for it to resume before moving.", "Socket error code");
Expect(PlayerNotificationText.FromServer("Erreur inconnue", code: "NOT_YOUR_TURN"),
    "Please wait for your turn.", "Turn error code");
Expect(PlayerNotificationText.FromServer("Phòng đã đầy"),
    "This room is full. Please join another room.", "Room message");
Expect(PlayerNotificationText.FromServer("Phòng không tồn tại"),
    "Room not found. Check the room code and try again.", "Missing room");
Expect(PlayerNotificationText.FromServer("Máy chủ đang bảo trì"),
    "The server is under maintenance. Please try again later.", "Maintenance message");

foreach (string source in new[] { "Lỗi mới chưa biết", "Erreur inconnue", "Fehler unbekannt", "不明なエラー", "<html>Service error</html>" })
    Expect(PlayerNotificationText.FromServer(source), PlayerNotificationText.Retry, "Unknown language/content fallback");
Expect(PlayerNotificationText.FromServer(null, 0), PlayerNotificationText.Connection, "Offline fallback");
Expect(PlayerNotificationText.FromServer("Máy chủ bị lỗi", 503),
    "The server is temporarily unavailable. Please try again later.", "Server failure fallback");
Expect(PlayerNotificationText.FromServer("Session expired", 401),
    "Your session has expired. Please log in again.", "Session expiration");
Expect(PlayerNotificationText.FromServer("Đã hết thời gian", 504), PlayerNotificationText.Timeout, "Timeout");
Expect(PlayerNotificationText.FromServer("Bitte warten", 429),
    "Too many requests. Please wait a moment and try again.", "Rate limit fallback");
Expect(PlayerNotificationText.FromServer("Not enough players", 400),
    "Please check your details and try again.", "Room errors do not become currency errors");
Expect(PlayerNotificationText.FromServer("Email already verified", 400),
    "Please check your details and try again.", "Email state errors do not become registration conflicts");
Expect(PlayerNotificationText.FromException(new ApiException("Authentication failed", 401, errorCode: "ACCOUNT_DISABLED")),
    "This account is unavailable. Please contact support.", "Explicit account code takes precedence");

var originalCulture = CultureInfo.CurrentCulture;
var originalUiCulture = CultureInfo.CurrentUICulture;
try
{
    CultureInfo.CurrentCulture = new CultureInfo("vi-VN");
    CultureInfo.CurrentUICulture = new CultureInfo("vi-VN");
    var api = new ApiException("Đăng nhập thất bại", 401, "{\"message\":\"Đăng nhập thất bại\"}", errorCode: "BAD_CREDENTIALS");
    Expect(PlayerNotificationText.FromException(api), credentials, "Localized API exception");
    if (api.Message != "Đăng nhập thất bại" || !api.ResponseBody.Contains("Đăng nhập thất bại"))
        throw new InvalidOperationException("Original diagnostic details were lost.");
    checks++;
    Expect(PlayerNotificationText.FromException(new Exception("Une erreur est survenue")), PlayerNotificationText.Retry, "Localized OS exception");
    Expect(PlayerNotificationText.FromException(new TimeoutException("Hết thời gian")), PlayerNotificationText.Timeout, "Typed timeout");
    Expect(PlayerNotificationText.FromException(new OperationCanceledException("Đã hủy")),
        "The request was canceled. Please try again.", "Typed cancellation");
}
finally
{
    CultureInfo.CurrentCulture = originalCulture;
    CultureInfo.CurrentUICulture = originalUiCulture;
}

Expect(PlayerNotificationText.MatchResult("BLACK_WON"), "Black wins", "Match result code");
Expect(PlayerNotificationText.MatchResult("Kết thúc"), "Match ended", "Unknown match result");
Expect(PlayerNotificationText.MatchEndReason("STALEMATE", true), "Stalemate", "Stalemate result UI");
Expect(PlayerNotificationText.MatchEndReason("DRAW_AGREEMENT", true), "Draw by agreement", "Draw agreement");
Expect(PlayerNotificationText.MatchEndReason("Hòa", true), "Draw", "Localized draw fallback");
Console.WriteLine($"PASS {checks} English notification checks.");
