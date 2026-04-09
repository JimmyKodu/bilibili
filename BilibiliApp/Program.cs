using BilibiliApp.Components;
using BilibiliApp.Services;

const string UserAgent =
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
    "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Typed HttpClient for the Bilibili service (API calls)
builder.Services.AddHttpClient<BilibiliService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
    client.DefaultRequestHeaders.Add("Referer", "https://www.bilibili.com");
});

// Named HttpClient for auth / QR-code polling — UseCookies=false so that
// Set-Cookie response headers are accessible for manual parsing.
builder.Services.AddHttpClient("bilibili-auth", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
    client.DefaultRequestHeaders.Add("Referer", "https://www.bilibili.com");
}).ConfigurePrimaryHttpMessageHandler(
    () => new HttpClientHandler { UseCookies = false });

// Named HttpClient for proxying audio CDN streams
builder.Services.AddHttpClient("bilibili-cdn", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
    client.DefaultRequestHeaders.Add("Referer", "https://www.bilibili.com");
});

// Scoped service: one UserSession per Blazor Server circuit (per connected user)
builder.Services.AddScoped<UserSession>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Audio stream proxy — resolves the CDN URL via yt-dlp, then streams it to the browser.
// Forwarding Range headers allows the HTML5 audio element to seek.
app.MapGet("/api/audio-stream/{bvid}", async (
    string bvid,
    BilibiliService bilibiliService,
    IHttpClientFactory httpClientFactory,
    HttpContext ctx) =>
{
    var url = await bilibiliService.GetAudioStreamUrlAsync(bvid);
    if (string.IsNullOrEmpty(url))
    {
        ctx.Response.StatusCode = 404;
        return;
    }

    using var client = httpClientFactory.CreateClient("bilibili-cdn");
    using var req = new HttpRequestMessage(HttpMethod.Get, url);

    if (ctx.Request.Headers.TryGetValue("Range", out var rangeVal))
        req.Headers.TryAddWithoutValidation("Range", rangeVal.ToString());

    using var resp = await client.SendAsync(
        req, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);

    ctx.Response.StatusCode = (int)resp.StatusCode;

    if (resp.Content.Headers.ContentType is { } ct)
        ctx.Response.ContentType = ct.ToString();
    if (resp.Content.Headers.ContentLength is { } cl)
        ctx.Response.ContentLength = cl;
    if (resp.Headers.TryGetValues("Content-Range", out var cr))
        ctx.Response.Headers.Append("Content-Range", cr.First());
    ctx.Response.Headers.Append("Accept-Ranges", "bytes");

    await resp.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
});

app.Run();
