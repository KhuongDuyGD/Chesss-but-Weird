using System;

[Serializable]
public class AuthAccount
{
    public string username;
    public string passwordSalt;
    public string passwordHash;
    public PlayerProfile profile;
}
