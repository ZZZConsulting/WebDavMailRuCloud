namespace YaR.Clouds.WebDavStore;

public class BrowserAuthenticatorInfo(string url, string password)
{
    public string Url { get; private set; } = url;

    public string Password { get; private set; } = password;
}

public static class BrowserAuthenticator
{
    public static BrowserAuthenticatorInfo Instance { get; set; }
}
