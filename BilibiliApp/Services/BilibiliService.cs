using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BilibiliApp.Services;

public class VideoInfo
{
    public string Title { get; set; } = "";
    public int Duration { get; set; }
}

public class BilibiliService(HttpClient httpClient)
{
    private const string ApiBase = "https://api.bilibili.com";
    private const string VideoBase = "https://www.bilibili.com/video";

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

    /// <summary>Like a video using the user's SESSDATA and bili_jct cookies.</summary>
    public async Task<(bool Success, string Message)> LikeVideoAsync(
        string bvid, string sessdata, string biliJct)
    {
        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Post, $"{ApiBase}/x/web-interface/archive/like");
            req.Headers.TryAddWithoutValidation(
                "Cookie", $"SESSDATA={sessdata}; bili_jct={biliJct}");
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["bvid"] = $"BV{bvid}",
                ["like"] = "1",
                ["csrf"] = biliJct,
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
}
