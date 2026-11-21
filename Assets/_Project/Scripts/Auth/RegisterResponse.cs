// The register endpoint may return account data or an empty success body.
public sealed class RegisterResponse
{
    public string userId;
    public string username;
    public string email;
}
