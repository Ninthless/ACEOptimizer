using System;
using System.IO;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.Events;

namespace ACEOptimizer.Services
{
    public sealed class UpdateService : IDisposable
    {
        private readonly SparkleUpdater _sparkleUpdater;
        private bool _disposed;

        public event Action? ExitRequested;

        public UpdateService()
            : this(CreateSparkleUpdater())
        {
        }

        internal UpdateService(SparkleUpdater sparkleUpdater)
        {
            _sparkleUpdater = sparkleUpdater;
            _sparkleUpdater.CloseApplication += OnCloseApplication;
        }

        public Version CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

        public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            UpdateInfo updateInfo = await _sparkleUpdater.CheckForUpdatesQuietly().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (updateInfo.Status == UpdateStatus.CouldNotDetermine)
                return UpdateCheckResult.Failed();

            if (updateInfo.Status != UpdateStatus.UpdateAvailable || updateInfo.Updates.Count == 0)
                return UpdateCheckResult.NoUpdate();

            AppCastItem package = updateInfo.Updates[0];
            string versionText = package.ShortVersion ?? package.Version ?? string.Empty;
            if (!Version.TryParse(versionText, out Version? latestVersion))
                return UpdateCheckResult.Failed();

            if (latestVersion <= CurrentVersion)
                return UpdateCheckResult.NoUpdate();

            return UpdateCheckResult.Available(latestVersion, package);
        }

        public async Task<(string Path, string Sha256)> DownloadInstallerAsync(
            UpdateCheckResult update,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            AppCastItem package = update.Package
                ?? throw new InvalidOperationException("The update does not contain an installable package.");

            if (string.IsNullOrWhiteSpace(package.DownloadLink))
                throw new InvalidOperationException("The update package has no download URL.");

            _sparkleUpdater.TmpDownloadFileNameWithExtension =
                $"ACEOptimizer_Setup_v{update.LatestVersion}.exe";

            TaskCompletionSource<string> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

            void DownloadFinished(AppCastItem item, string path) => completion.TrySetResult(path);
            void DownloadFailed(AppCastItem item, string? path, Exception exception) => completion.TrySetException(exception);
            void DownloadCanceled(AppCastItem item, string path) => completion.TrySetCanceled(cancellationToken);
            void DownloadCorrupt(AppCastItem item, string path) => completion.TrySetException(new SecurityException("The downloaded installer signature is invalid."));
            void DownloadSignatureFailed(AppCastItem item, string path) => completion.TrySetException(new SecurityException("The downloaded installer signature could not be verified."));
            void DownloadProgress(object sender, AppCastItem item, ItemDownloadProgressEventArgs args) => progress?.Report(args.ProgressPercentage);

            _sparkleUpdater.DownloadFinished += DownloadFinished;
            _sparkleUpdater.DownloadHadError += DownloadFailed;
            _sparkleUpdater.DownloadCanceled += DownloadCanceled;
            _sparkleUpdater.DownloadedFileIsCorrupt += DownloadCorrupt;
            _sparkleUpdater.DownloadedFileThrewWhileCheckingSignature += DownloadSignatureFailed;
            _sparkleUpdater.DownloadMadeProgress += DownloadProgress;

            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                _sparkleUpdater.CancelFileDownload();
                completion.TrySetCanceled(cancellationToken);
            });

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _sparkleUpdater.InitAndBeginDownload(package).ConfigureAwait(false);
                string installerPath = await completion.Task.ConfigureAwait(false);
                string sha256 = await ComputeSha256Async(installerPath).ConfigureAwait(false);
                return (installerPath, sha256);
            }
            finally
            {
                _sparkleUpdater.DownloadFinished -= DownloadFinished;
                _sparkleUpdater.DownloadHadError -= DownloadFailed;
                _sparkleUpdater.DownloadCanceled -= DownloadCanceled;
                _sparkleUpdater.DownloadedFileIsCorrupt -= DownloadCorrupt;
                _sparkleUpdater.DownloadedFileThrewWhileCheckingSignature -= DownloadSignatureFailed;
                _sparkleUpdater.DownloadMadeProgress -= DownloadProgress;
            }
        }

        public async Task InstallUpdateAsync(UpdateCheckResult update, string installerPath)
        {
            ThrowIfDisposed();
            AppCastItem package = update.Package
                ?? throw new InvalidOperationException("The update does not contain an installable package.");

            InstallUpdateFailureReason? failureReason = null;
            bool InstallFailed(InstallUpdateFailureReason reason, string? path)
            {
                failureReason = reason;
                return false;
            }

            _sparkleUpdater.InstallUpdateFailed += InstallFailed;
            try
            {
                await _sparkleUpdater.InstallUpdate(package, installerPath).ConfigureAwait(false);
            }
            finally
            {
                _sparkleUpdater.InstallUpdateFailed -= InstallFailed;
            }

            if (failureReason.HasValue)
                throw new InvalidOperationException($"The installer could not be started: {failureReason.Value}.");
        }

        public void DeleteInstallerTempDir(string installerPath)
        {
            try
            {
                if (File.Exists(installerPath))
                    File.Delete(installerPath);
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _sparkleUpdater.CloseApplication -= OnCloseApplication;
            _sparkleUpdater.CancelFileDownload();
            _sparkleUpdater.Dispose();
        }

        private static SparkleUpdater CreateSparkleUpdater()
        {
            TrustedUpdateSignatureVerifier signatureVerifier = new(UpdateSecurity.TrustedPublicKeys);
            if (!signatureVerifier.HasValidKeyInformation())
                throw new InvalidOperationException("No valid update signing key is configured.");

            return new SparkleUpdater(UpdateSecurity.AppCastUrl, signatureVerifier)
            {
                UIFactory = null,
                CheckServerFileName = false,
                TmpDownloadFilePath = Path.Combine(Path.GetTempPath(), "ACEOptimizer", "Updates"),
                RelaunchAfterUpdate = false,
                ShouldKillParentProcessWhenStartingInstaller = true
            };
        }

        private static async Task<string> ComputeSha256Async(string filePath)
        {
            await using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            byte[] hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private void OnCloseApplication()
        {
            ExitRequested?.Invoke();
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }

    public sealed class UpdateCheckResult
    {
        internal AppCastItem? Package { get; private init; }

        public bool IsUpdateAvailable { get; private init; }
        public bool CheckFailed { get; private init; }
        public bool CanInstall => Package is not null;
        public bool IsCriticalUpdate => Package?.IsCriticalUpdate == true;
        public Version? LatestVersion { get; private init; }
        public string ReleasePageUrl { get; private init; } = UpdateSecurity.ReleasePageUrl;

        public static UpdateCheckResult NoUpdate() => new();

        public static UpdateCheckResult Failed() => new()
        {
            CheckFailed = true
        };

        internal static UpdateCheckResult Available(Version version, AppCastItem package) => new()
        {
            IsUpdateAvailable = true,
            LatestVersion = version,
            Package = package
        };

        public static UpdateCheckResult FallbackBrowser(string pageUrl) => new()
        {
            IsUpdateAvailable = true,
            ReleasePageUrl = pageUrl
        };
    }
}
