using System.Net;
using System.Net.Http;
using System.Text;
using ACEOptimizer.Services;

namespace ACEOptimizer.Tests;

public class UpdateServiceTests
{
    private static UpdateService CreateService(HttpStatusCode status, string json)
    {
        var handler = new StubHttpMessageHandler(status, json);
        return new UpdateService(handler);
    }

    [Fact]
    public async Task CheckForUpdate_WhenLatestIsNewer_ReturnsAvailable()
    {
        string json = """
            {
                "tag_name": "v99.0.0",
                "html_url": "https://github.com/Ninthless/ACEOptimizer/releases/tag/v99.0.0",
                "assets": [
                    { "name": "ACEOptimizer_Setup_v99.0.0.exe", "browser_download_url": "https://example.com/setup.exe" }
                ]
            }
            """;
        using var svc = CreateService(HttpStatusCode.OK, json);

        var result = await svc.CheckForUpdateAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal(new Version(99, 0, 0), result.LatestVersion);
        Assert.Equal("https://example.com/setup.exe", result.InstallerUrl);
    }

    [Fact]
    public async Task CheckForUpdate_WhenLatestIsSameVersion_ReturnsNoUpdate()
    {
        string currentVersion = new UpdateService().CurrentVersion.ToString(3);
        string json = $$"""
            {
                "tag_name": "v{{currentVersion}}",
                "html_url": "https://github.com/Ninthless/ACEOptimizer/releases/latest",
                "assets": []
            }
            """;
        using var svc = CreateService(HttpStatusCode.OK, json);

        var result = await svc.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdate_WhenLatestIsOlder_ReturnsNoUpdate()
    {
        string json = """
            {
                "tag_name": "v0.0.1",
                "html_url": "https://github.com/Ninthless/ACEOptimizer/releases/tag/v0.0.1",
                "assets": []
            }
            """;
        using var svc = CreateService(HttpStatusCode.OK, json);

        var result = await svc.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdate_WhenForbidden_ReturnsRateLimited()
    {
        using var svc = CreateService(HttpStatusCode.Forbidden, "");

        var result = await svc.CheckForUpdateAsync();

        Assert.True(result.IsRateLimited);
        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdate_WhenTooManyRequests_ReturnsRateLimited()
    {
        using var svc = CreateService(HttpStatusCode.TooManyRequests, "");

        var result = await svc.CheckForUpdateAsync();

        Assert.True(result.IsRateLimited);
    }

    [Fact]
    public async Task CheckForUpdate_WhenResponseIsEmpty_ReturnsNoUpdate()
    {
        using var svc = CreateService(HttpStatusCode.OK, "null");

        var result = await svc.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdate_WhenTagIsInvalid_ReturnsNoUpdate()
    {
        string json = """{ "tag_name": "not-a-version", "html_url": "", "assets": [] }""";
        using var svc = CreateService(HttpStatusCode.OK, json);

        var result = await svc.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdate_WhenCancelled_ReturnsNoUpdate()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var svc = CreateService(HttpStatusCode.OK, "{}");

        var result = await svc.CheckForUpdateAsync(cts.Token);

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdate_WhenNoSetupAsset_InstallerUrlIsNull()
    {
        string json = """
            {
                "tag_name": "v99.0.0",
                "html_url": "https://github.com/Ninthless/ACEOptimizer/releases/tag/v99.0.0",
                "assets": [
                    { "name": "ACEOptimizer_Portable_v99.0.0.zip", "browser_download_url": "https://example.com/portable.zip" }
                ]
            }
            """;
        using var svc = CreateService(HttpStatusCode.OK, json);

        var result = await svc.CheckForUpdateAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Null(result.InstallerUrl);
    }

    [Fact]
    public async Task CheckForUpdate_WhenTagHasVPrefix_ParsesCorrectly()
    {
        string json = """
            {
                "tag_name": "V99.1.2",
                "html_url": "https://github.com/Ninthless/ACEOptimizer/releases/tag/V99.1.2",
                "assets": []
            }
            """;
        using var svc = CreateService(HttpStatusCode.OK, json);

        var result = await svc.CheckForUpdateAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal(new Version(99, 1, 2), result.LatestVersion);
    }
}

internal sealed class StubHttpMessageHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}