using ACEOptimizer.Services;
using NetSparkleUpdater;

namespace ACEOptimizer.Tests;

public class UpdateCheckResultTests
{
    [Fact]
    public void NoUpdate_IsNotAvailable()
    {
        UpdateCheckResult result = UpdateCheckResult.NoUpdate();

        Assert.False(result.IsUpdateAvailable);
        Assert.False(result.CheckFailed);
        Assert.False(result.CanInstall);
        Assert.Null(result.LatestVersion);
    }

    [Fact]
    public void Failed_RequiresBrowserFallback()
    {
        UpdateCheckResult result = UpdateCheckResult.Failed();

        Assert.False(result.IsUpdateAvailable);
        Assert.True(result.CheckFailed);
        Assert.False(result.CanInstall);
        Assert.NotEmpty(result.ReleasePageUrl);
    }

    [Fact]
    public void Available_HasInstallablePackage()
    {
        AppCastItem package = new()
        {
            Version = "2.0.0",
            DownloadLink = "https://example.com/setup.exe",
            DownloadSignature = "signature",
            IsCriticalUpdate = true
        };

        UpdateCheckResult result = UpdateCheckResult.Available(new Version(2, 0, 0), package);

        Assert.True(result.IsUpdateAvailable);
        Assert.True(result.CanInstall);
        Assert.True(result.IsCriticalUpdate);
        Assert.Equal(new Version(2, 0, 0), result.LatestVersion);
    }

    [Fact]
    public void FallbackBrowser_IsAvailableWithoutPackage()
    {
        UpdateCheckResult result = UpdateCheckResult.FallbackBrowser("https://example.com/releases");

        Assert.True(result.IsUpdateAvailable);
        Assert.False(result.CanInstall);
        Assert.Equal("https://example.com/releases", result.ReleasePageUrl);
    }
}
