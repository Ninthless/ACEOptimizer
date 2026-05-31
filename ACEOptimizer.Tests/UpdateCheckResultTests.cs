using ACEOptimizer.Services;

namespace ACEOptimizer.Tests;

public class UpdateCheckResultTests
{
    [Fact]
    public void NoUpdate_IsNotAvailable()
    {
        var result = UpdateCheckResult.NoUpdate();
        Assert.False(result.IsUpdateAvailable);
        Assert.False(result.IsRateLimited);
        Assert.Null(result.LatestVersion);
    }

    [Fact]
    public void RateLimited_IsNotAvailableButIsRateLimited()
    {
        var result = UpdateCheckResult.RateLimited();
        Assert.False(result.IsUpdateAvailable);
        Assert.True(result.IsRateLimited);
        Assert.NotEmpty(result.ReleasePageUrl);
    }

    [Fact]
    public void Available_HasCorrectFields()
    {
        var version = new Version(2, 0, 0);
        var result = UpdateCheckResult.Available(version, "https://example.com", "https://example.com/setup.exe");
        Assert.True(result.IsUpdateAvailable);
        Assert.False(result.IsRateLimited);
        Assert.Equal(version, result.LatestVersion);
        Assert.Equal("https://example.com", result.ReleasePageUrl);
        Assert.Equal("https://example.com/setup.exe", result.InstallerUrl);
    }

    [Fact]
    public void Available_WithNullInstallerUrl_HasNullInstallerUrl()
    {
        var result = UpdateCheckResult.Available(new Version(1, 0, 0), "https://example.com", null);
        Assert.True(result.IsUpdateAvailable);
        Assert.Null(result.InstallerUrl);
    }

    [Fact]
    public void FallbackBrowser_IsAvailableWithNoInstaller()
    {
        var result = UpdateCheckResult.FallbackBrowser("https://example.com/releases");
        Assert.True(result.IsUpdateAvailable);
        Assert.Null(result.InstallerUrl);
        Assert.Equal("https://example.com/releases", result.ReleasePageUrl);
    }
}