using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ACEOptimizer.Services
{
    internal sealed class UpdateService : IDisposable
    {
        private const string ReleasesApiUrl = "https://api.github.com/repos/Ninthless/ACEOptimizer/releases/latest";
        private const string UserAgent = "ACEOptimizer-UpdateChecker";

        private readonly HttpClient _httpClient;

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public Version CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

        public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                GitHubRelease? release = await _httpClient
                    .GetFromJsonAsync<GitHubRelease>(ReleasesApiUrl, cancellationToken)
                    .ConfigureAwait(false);

                if (release is null || string.IsNullOrWhiteSpace(release.TagName))
                    return UpdateCheckResult.NoUpdate();

                string tag = release.TagName.TrimStart('v', 'V');
                if (!Version.TryParse(tag, out Version? latestVersion))
                    return UpdateCheckResult.NoUpdate();

                if (latestVersion <= CurrentVersion)
                    return UpdateCheckResult.NoUpdate();

                string? installerUrl = FindInstallerAssetUrl(release);
                return UpdateCheckResult.Available(latestVersion, release.HtmlUrl ?? string.Empty, installerUrl);
            }
            catch (OperationCanceledException)
            {
                return UpdateCheckResult.NoUpdate();
            }
            catch
            {
                return UpdateCheckResult.NoUpdate();
            }
        }

        public async Task<string> DownloadInstallerAsync(
            string url,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"ACEOptimizer_Update_{Guid.NewGuid():N}.exe");

            using HttpResponseMessage response = await _httpClient
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            long total = response.Content.Headers.ContentLength ?? -1;
            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using FileStream dest = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            byte[] buffer = new byte[81920];
            long downloaded = 0;
            int read;

            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await dest.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                downloaded += read;

                if (total > 0)
                    progress?.Report((int)(downloaded * 100 / total));
            }

            return tempPath;
        }

        public void LaunchInstallerAndExit(string installerPath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                System.Windows.Application.Current.Shutdown());
        }

        private static string? FindInstallerAssetUrl(GitHubRelease release)
        {
            if (release.Assets is null) return null;

            foreach (GitHubAsset asset in release.Assets)
            {
                string name = asset.Name ?? string.Empty;
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                    return asset.BrowserDownloadUrl;
            }

            foreach (GitHubAsset asset in release.Assets)
            {
                if ((asset.Name ?? string.Empty).EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return asset.BrowserDownloadUrl;
            }

            return null;
        }

        public void Dispose() => _httpClient.Dispose();
    }

    internal sealed class UpdateCheckResult
    {
        public bool IsUpdateAvailable { get; private init; }
        public Version? LatestVersion { get; private init; }
        public string ReleasePageUrl { get; private init; } = string.Empty;
        public string? InstallerUrl { get; private init; }

        public static UpdateCheckResult NoUpdate() => new() { IsUpdateAvailable = false };

        public static UpdateCheckResult Available(Version version, string pageUrl, string? installerUrl) => new()
        {
            IsUpdateAvailable = true,
            LatestVersion = version,
            ReleasePageUrl = pageUrl,
            InstallerUrl = installerUrl
        };

        public static UpdateCheckResult FallbackBrowser(string pageUrl) => new()
        {
            IsUpdateAvailable = true,
            ReleasePageUrl = pageUrl,
            InstallerUrl = null
        };
    }

    internal sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("assets")] public GitHubAsset[]? Assets { get; set; }
    }

    internal sealed class GitHubAsset
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
    }
}
