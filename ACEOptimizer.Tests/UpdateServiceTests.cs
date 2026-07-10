using System.ComponentModel;
using System.Text;
using ACEOptimizer.Services;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.Events;
using NetSparkleUpdater.Interfaces;

namespace ACEOptimizer.Tests;

public class UpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdate_WhenLatestIsNewer_ReturnsAvailable()
    {
        using UpdateService service = CreateService(CreateAppCast("99.0.0", critical: true));

        UpdateCheckResult result = await service.CheckForUpdateAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.True(result.CanInstall);
        Assert.True(result.IsCriticalUpdate);
        Assert.Equal(new Version(99, 0, 0), result.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdate_WhenLatestIsOlder_ReturnsNoUpdate()
    {
        using UpdateService service = CreateService(CreateAppCast("0.0.1"));

        UpdateCheckResult result = await service.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.False(result.CheckFailed);
    }

    [Fact]
    public async Task CheckForUpdate_WhenAppCastSignatureIsRejected_ReturnsFailure()
    {
        using UpdateService service = CreateService(
            CreateAppCast("99.0.0"),
            new StubSignatureVerifier(ValidationResult.Invalid));

        UpdateCheckResult result = await service.CheckForUpdateAsync();

        Assert.True(result.CheckFailed);
        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdate_WhenCancelled_ThrowsCancellation()
    {
        using UpdateService service = CreateService(CreateAppCast("99.0.0"));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.CheckForUpdateAsync(cancellation.Token));
    }

    [Fact]
    public async Task DownloadInstallerAsync_ReturnsVerifiedFileAndHash()
    {
        byte[] installerBytes = Encoding.UTF8.GetBytes("signed installer fixture");
        string downloadDirectory = Path.Combine(Path.GetTempPath(), $"ACEOptimizerTests_{Guid.NewGuid():N}");
        SparkleUpdater updater = CreateSparkleUpdater(
            CreateAppCast("99.0.0"),
            new StubSignatureVerifier(ValidationResult.Valid));
        updater.TmpDownloadFilePath = downloadDirectory;
        updater.UpdateDownloader = new StubUpdateDownloader(installerBytes);
        using UpdateService service = new(updater);
        UpdateCheckResult update = UpdateCheckResult.Available(
            new Version(99, 0, 0),
            CreatePackage("99.0.0"));
        int reportedProgress = 0;

        try
        {
            (string path, string sha256) = await service.DownloadInstallerAsync(
                update,
                new Progress<int>(progress => reportedProgress = progress));

            Assert.True(File.Exists(path));
            Assert.Equal("78406fc74fb60fb5a9babc70740938a6d95da5ca2cd0b5ec143f43dc57865604", sha256);
            Assert.Equal(100, reportedProgress);
        }
        finally
        {
            if (Directory.Exists(downloadDirectory))
                Directory.Delete(downloadDirectory, recursive: true);
        }
    }

    private static UpdateService CreateService(
        string appCast,
        ISignatureVerifier? signatureVerifier = null)
    {
        return new UpdateService(CreateSparkleUpdater(
            appCast,
            signatureVerifier ?? new StubSignatureVerifier(ValidationResult.Valid)));
    }

    private static SparkleUpdater CreateSparkleUpdater(
        string appCast,
        ISignatureVerifier signatureVerifier)
    {
        return new SparkleUpdater(
            "https://example.com/appcast.xml",
            signatureVerifier,
            typeof(UpdateService).Assembly.Location)
        {
            UIFactory = null,
            CheckServerFileName = false,
            AppCastDataDownloader = new StubAppCastDataDownloader(appCast)
        };
    }

    private static AppCastItem CreatePackage(string version)
    {
        return new AppCastItem
        {
            Title = $"ACE Optimizer {version}",
            Version = version,
            ShortVersion = version,
            DownloadLink = $"https://example.com/ACEOptimizer_Setup_v{version}.exe",
            DownloadSignature = "signature",
            OperatingSystem = "windows-x64"
        };
    }

    private static string CreateAppCast(string version, bool critical = false)
    {
        return $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <rss version="2.0" xmlns:sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle">
              <channel>
                <title>ACE Optimizer</title>
                <link>https://example.com/appcast.xml</link>
                <description>Signed updates</description>
                <language>en</language>
                <item>
                  <title>ACE Optimizer {{version}}</title>
                  <pubDate>Fri, 10 Jul 2026 00:00:00 +00:00</pubDate>
                  <sparkle:version>{{version}}</sparkle:version>
                  <sparkle:shortVersionString>{{version}}</sparkle:shortVersionString>
                  <enclosure url="https://example.com/ACEOptimizer_Setup_v{{version}}.exe"
                    sparkle:version="{{version}}"
                    sparkle:shortVersionString="{{version}}"
                    sparkle:os="windows-x64"
                    sparkle:criticalUpdate="{{critical.ToString().ToLowerInvariant()}}"
                    sparkle:signature="signature"
                    length="24"
                    type="application/octet-stream" />
                </item>
              </channel>
            </rss>
            """;
    }
}

internal sealed class StubAppCastDataDownloader(string appCast) : IAppCastDataDownloader
{
    public string DownloadAndGetAppCastData(string url)
    {
        return url.EndsWith(".signature", StringComparison.OrdinalIgnoreCase)
            ? "signature"
            : appCast;
    }

    public Task<string> DownloadAndGetAppCastDataAsync(string url)
    {
        return Task.FromResult(DownloadAndGetAppCastData(url));
    }

    public Encoding GetAppCastEncoding()
    {
        return Encoding.UTF8;
    }
}

internal sealed class StubSignatureVerifier(ValidationResult result) : ISignatureVerifier
{
    public SecurityMode SecurityMode { get; set; } = SecurityMode.Strict;

    public bool HasValidKeyInformation()
    {
        return true;
    }

    public ValidationResult VerifySignature(string signature, byte[] dataToVerify)
    {
        return result;
    }

    public ValidationResult VerifySignatureOfFile(string signature, string binaryPath)
    {
        return result;
    }

    public ValidationResult VerifySignatureOfString(string signature, string data)
    {
        return result;
    }
}

internal sealed class StubUpdateDownloader(byte[] content) : IUpdateDownloader
{
    public bool IsDownloading { get; private set; }

    public event DownloadFromPathToPathEvent? DownloadStarted;
    public event DownloadProgressEvent? DownloadProgressChanged;
    public event AsyncCompletedEventHandler? DownloadFileCompleted;

    public async Task DownloadFile(Uri? uri, string downloadFilePath)
    {
        IsDownloading = true;
        DownloadStarted?.Invoke(this, uri?.ToString() ?? string.Empty, downloadFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(downloadFilePath)!);
        await File.WriteAllBytesAsync(downloadFilePath, content);
        DownloadProgressChanged?.Invoke(this, new ItemDownloadProgressEventArgs(100, this, content.Length, content.Length));
        IsDownloading = false;
        DownloadFileCompleted?.Invoke(this, new AsyncCompletedEventArgs(null, false, null));
    }

    public void CancelDownload()
    {
        IsDownloading = false;
        DownloadFileCompleted?.Invoke(this, new AsyncCompletedEventArgs(null, true, null));
    }

    public void Dispose()
    {
    }

    public Task<string?> RetrieveDestinationFileNameAsync(AppCastItem item)
    {
        return Task.FromResult<string?>(null);
    }
}
