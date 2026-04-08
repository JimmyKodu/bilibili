using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using QRCoder;

namespace BilibiliApp.Services;

public class VideoInfo
{
    public string Title { get; set; } = "";
    public int Duration { get; set; }
}

/// <summary>Result of one QR-code login poll tick.</summary>
public record QrPollResult(
    int Code,
    string? Sessdata,
    string? BiliJct,
    string? DedeUserId);

public class BilibiliService(HttpClient httpClient, IHttpClientFactory httpClientFactory)
{
    private const string ApiBase = "https://api.bilibili.com";
    private const string PassportBase = "https://passport.bilibili.com";
    private const string VideoBase = "https://www.bilibili.com/video";

    // ── Video info ──────────────────────────────────────────────────────────

    /// <summary>Fetch video title and duration via the public Bilibili API.</summary>
    public async Task<VideoInfo?> GetVideoInfoAsync(string bvid)
    {
        try
        {
            var response = await httpClient.GetAsync(
                $"{ApiBase}/x/web-interface/view?bvid=BV{bvid}");
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (root.GetProperty("code").GetInt32() != 0)
                return null;

            var data = root.GetProperty("data");
            return new VideoInfo
            {
                Title = data.GetProperty("title").GetString() ?? "",
                Duration = data.GetProperty("duration").GetInt32(),
            };
        }
        catch
        {
            return null;
        }
    }

    // ── Like ────────────────────────────────────────────────────────────────

    /// <summary>Like a video using credentials stored in <paramref name="session"/>.</summary>
    public async Task<(bool Success, string Message)> LikeVideoAsync(
        string bvid, UserSession session)
    {
        if (!session.IsLoggedIn)
            return (false, "请先扫码登录 B 站");

        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Post, $"{ApiBase}/x/web-interface/archive/like");
            req.Headers.TryAddWithoutValidation(
                "Cookie", $"SESSDATA={session.Sessdata}; bili_jct={session.BiliJct}");
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["bvid"] = $"BV{bvid}",
                ["like"] = "1",
                ["csrf"] = session.BiliJct!,
            });

            using var resp = await httpClient.SendAsync(req);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var code = doc.RootElement.GetProperty("code").GetInt32();
            var msg = doc.RootElement.GetProperty("message").GetString() ?? "未知";
            return code == 0 ? (true, "✅ 点赞成功！") : (false, $"点赞失败: {msg}");
        }
        catch (Exception ex)
        {
            return (false, $"请求异常: {ex.Message}");
        }
    }

    // ── QR-code login ───────────────────────────────────────────────────────

    /// <summary>
    /// Call the Bilibili passport API to generate a new QR code.
    /// Returns <c>(qrKey, qrImageBase64)</c> on success, or <c>null</c> on failure.
    /// </summary>
    public async Task<(string QrKey, string QrImageBase64)?> GenerateQrCodeAsync()
    {
        try
        {
            using var resp = await httpClient.GetAsync(
                $"{PassportBase}/x/passport-login/web/qrcode/generate");
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (root.GetProperty("code").GetInt32() != 0)
                return null;

            var data = root.GetProperty("data");
            var url = data.GetProperty("url").GetString() ?? "";
            var key = data.GetProperty("qrcode_key").GetString() ?? "";

            var imageBase64 = RenderQrCode(url);
            return (key, imageBase64);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Poll the Bilibili passport API for QR login status.
    /// Status codes: 86101 = not scanned, 86090 = scanned/unconfirmed,
    ///               86038 = expired, 0 = success.
    /// On success the SESSDATA / bili_jct / DedeUserID cookies are captured from
    /// the Set-Cookie response headers.
    /// </summary>
    public async Task<QrPollResult> PollQrLoginAsync(string qrKey)
    {
        // Use the raw client (UseCookies=false) so we can read Set-Cookie headers.
        using var client = httpClientFactory.CreateClient("bilibili-auth");
        try
        {
            using var resp = await client.GetAsync(
                $"{PassportBase}/x/passport-login/web/qrcode/poll?qrcode_key={Uri.EscapeDataString(qrKey)}");

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var dataCode = doc.RootElement
                .GetProperty("data").GetProperty("code").GetInt32();

            if (dataCode != 0)
                return new QrPollResult(dataCode, null, null, null);

            // Parse cookies from Set-Cookie response headers
            string? sessdata = null, biliJct = null, dedeUserId = null;
            if (resp.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var cookie in setCookies)
                {
                    var nameValue = cookie.Split(';')[0];
                    var eq = nameValue.IndexOf('=');
                    if (eq < 1) continue;
                    var name = nameValue[..eq].Trim();
                    var value = nameValue[(eq + 1)..].Trim();
                    if (name.Equals("SESSDATA", StringComparison.OrdinalIgnoreCase))
                        sessdata = Uri.UnescapeDataString(value);
                    else if (name.Equals("bili_jct", StringComparison.OrdinalIgnoreCase))
                        biliJct = value;
                    else if (name.Equals("DedeUserID", StringComparison.OrdinalIgnoreCase))
                        dedeUserId = value;
                }
            }

            return new QrPollResult(0, sessdata, biliJct, dedeUserId);
        }
        catch
        {
            return new QrPollResult(-1, null, null, null);
        }
    }

    /// <summary>Fetch the login name for the authenticated user.</summary>
    public async Task<string?> GetUsernameAsync(UserSession session)
    {
        if (!session.IsLoggedIn) return null;
        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Get, $"{ApiBase}/x/web-interface/nav");
            req.Headers.TryAddWithoutValidation(
                "Cookie", $"SESSDATA={session.Sessdata}");
            using var resp = await httpClient.SendAsync(req);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (root.GetProperty("code").GetInt32() != 0) return null;
            return root.GetProperty("data").GetProperty("uname").GetString();
        }
        catch
        {
            return null;
        }
    }

    // ── Audio stream ────────────────────────────────────────────────────────

    /// <summary>
    /// Use yt-dlp to resolve the best-audio stream URL for the given BV video.
    /// Returns an empty string if yt-dlp is unavailable or fails.
    /// </summary>
    public async Task<string> GetAudioStreamUrlAsync(string bvid)
    {
        // Validate bvid — only alphanumeric characters are expected
        if (!Regex.IsMatch(bvid, @"^[A-Za-z0-9]+$"))
            return "";

        var videoUrl = $"{VideoBase}/BV{bvid}";
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        proc.StartInfo.ArgumentList.Add("-g");
        proc.StartInfo.ArgumentList.Add("-f");
        proc.StartInfo.ArgumentList.Add("bestaudio/best");
        proc.StartInfo.ArgumentList.Add(videoUrl);

        try
        {
            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            var lines = output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return lines.LastOrDefault()?.Trim() ?? "";
        }
        catch
        {
            return "";
        }
    }

    // ── QR code rendering ───────────────────────────────────────────────────

    /// <summary>Render <paramref name="url"/> as a base64-encoded PNG QR code.</summary>
    private static string RenderQrCode(string url)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        var bytes = qr.GetGraphic(10);
        return Convert.ToBase64String(bytes);
    }
}
