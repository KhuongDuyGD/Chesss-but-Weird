using System.Threading.Tasks;

public sealed class UserService
{
    private readonly ApiClient client;

    public UserService(ApiClient client = null) => this.client = client ?? ApiClient.Shared;

    public Task<UserMeResponse> GetMeAsync() => client.GetAsync<UserMeResponse>("/api/users/me", true);
}
