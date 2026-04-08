namespace BilibiliApp.Services;

/// <summary>
/// Scoped service (one instance per Blazor Server circuit) that stores the
/// authenticated user's Bilibili credentials obtained via QR-code login.
/// </summary>
public class UserSession
{
    public string? Sessdata { get; set; }
    public string? BiliJct { get; set; }
    public string? DedeUserId { get; set; }
    public string? Username { get; set; }

    public bool IsLoggedIn =>
        !string.IsNullOrEmpty(Sessdata) && !string.IsNullOrEmpty(BiliJct);

    public void Clear()
    {
        Sessdata = null;
        BiliJct = null;
        DedeUserId = null;
        Username = null;
    }
}
