using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UnattendHost.Tests;

public sealed partial class WebTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GeneratorPage_RendersWithStrictCsp()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/", Cancel);
        string html = await response.Content.ReadAsStringAsync(Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("Generate an answer file", html);
        Assert.Contains("win11-pro-scripted", html);
    }

    [Fact]
    public async Task GeneratorPage_LoadsNothingFromOtherOrigins()
    {
        using var client = factory.CreateClient();

        string html = await client.GetStringAsync("/", Cancel);

        Assert.Empty(ExternalResource().Matches(html));
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain(" style=", html);
    }

    [Fact]
    public async Task Healthz_ReportsStatus()
    {
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync("/healthz", Cancel));

        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
        Assert.True(json.RootElement.TryGetProperty("upstreamCommit", out _));
    }

    [Fact]
    public async Task LoadingAPreset_FillsTheForm()
    {
        using var client = factory.CreateClient();

        string html = await client.GetStringAsync("/?preset=win11-pro", Cancel);

        Assert.Contains("value=\"labadmin\"", html);
        Assert.Contains("value=\"WIN11-LAB\"", html);
    }

    [Fact]
    public async Task LoadingAnUnknownPreset_ShowsAnError()
    {
        using var client = factory.CreateClient();

        string html = await client.GetStringAsync("/?preset=does-not-exist", Cancel);

        Assert.Contains("does not exist", html);
    }

    [Fact]
    public async Task PostingTheForm_ShowsTheResultAndDownloads()
    {
        using var client = factory.CreateClient();
        string token = await GetAntiforgeryTokenAsync(client);

        using var generate = await client.PostAsync("/?handler=Generate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Form.ComputerNameMode"] = "Custom",
            ["Form.ComputerName"] = "WEBTEST-PC",
        }), Cancel);
        string html = await generate.Content.ReadAsStringAsync(Cancel);

        Assert.Equal(HttpStatusCode.OK, generate.StatusCode);
        Assert.Contains("autounattend.xml is ready", html);
        Assert.Contains("&lt;ComputerName&gt;WEBTEST-PC&lt;/ComputerName&gt;", html);

        string configJson = WebUtility.HtmlDecode(ConfigJsonField().Match(html).Groups[1].Value);
        using var download = await client.PostAsync("/?handler=Download", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiforgeryField().Match(html).Groups[1].Value,
            ["configJson"] = configJson,
        }), Cancel);

        Assert.Equal("application/xml", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("autounattend.xml", download.Content.Headers.ContentDisposition?.FileName);
        Assert.Contains("<ComputerName>WEBTEST-PC</ComputerName>", await download.Content.ReadAsStringAsync(Cancel));
    }

    [Fact]
    public async Task PostingAnInvalidForm_ShowsErrors()
    {
        using var client = factory.CreateClient();
        string token = await GetAntiforgeryTokenAsync(client);

        using var response = await client.PostAsync("/?handler=Generate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Form.ComputerNameMode"] = "Custom",
            ["Form.ComputerName"] = "not valid!",
        }), Cancel);
        string html = await response.Content.ReadAsStringAsync(Cancel);

        Assert.Contains("The configuration has problems", html);
        Assert.Contains("Generate an answer file", html);
    }

    [Fact]
    public async Task UploadingAPresetWithNullEntries_ShowsAnError()
    {
        using var client = factory.CreateClient();
        string token = await GetAntiforgeryTokenAsync(client);

        using var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("""{ "scripts": { "items": [ null ] } }"""), "presetFile", "preset.json" },
        };
        using var response = await client.PostAsync("/?handler=Upload", content, Cancel);
        string html = await response.Content.ReadAsStringAsync(Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("scripts.items[0] must not be null", html);
    }

    [Fact]
    public async Task PostWithoutAntiforgeryToken_IsRejected()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/?handler=Generate", new FormUrlEncodedContent([]), Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        string html = await client.GetStringAsync("/", Cancel);
        return AntiforgeryField().Match(html).Groups[1].Value;
    }

    [GeneratedRegex(@"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""")]
    private static partial Regex AntiforgeryField();

    [GeneratedRegex(@"name=""configJson"" value=""([^""]*)""")]
    private static partial Regex ConfigJsonField();

    // Scripts, images and stylesheets loaded from another origin. Plain links (<a href>) are fine.
    [GeneratedRegex(@"(?:src=""|<link[^>]*href="")(?:https?:)?//", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalResource();
}
