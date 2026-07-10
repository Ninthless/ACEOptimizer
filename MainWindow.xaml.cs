using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Shapes;
using ACEOptimizer.Models;
using ACEOptimizer.Services;
using Wpf.Ui.Controls;

namespace ACEOptimizer
{
    public partial class MainWindow : FluentWindow
    {
        private enum AceUiState
        {
            Idle,
            Optimized,
            Blocked
        }

        private readonly AceProcessService _aceProcessService = new();
        private readonly AutoStartService _autoStartService = new();
        private readonly ElevationService _elevationService = new();
        private readonly UpdateService _updateService = new();
        private readonly CancellationTokenSource _updateCheckCts = new();
        private readonly DispatcherTimer _timer;
        private readonly Dictionary<string, Ellipse> _aceProcessDots;
        private readonly bool _isElevated;
        private bool _hasShownElevationPrompt;
        private bool _isExitRequested;
        private nint _affinityMask;

        private UpdateCheckResult? _pendingUpdate;
        private bool _isDownloading;
        private CancellationTokenSource? _downloadCts;

        public MainWindow()
        {
            InitializeComponent();
            _updateService.ExitRequested += UpdateService_ExitRequested;
            _isElevated = _elevationService.IsRunningElevated();
            _aceProcessDots = CreateAceProcessDots();

            string version = _updateService.CurrentVersion.ToString(3);
            Title = $"ACE Optimizer v{version}";
            AppTitleBar.Title = $"  ACE Optimizer v{version}";
            AppHeaderTitle.Text = $"ACE Optimizer v{version}";

            UpdatePrivilegeStatus();
            _affinityMask = _aceProcessService.CalculateAffinityMask();
            CheckAutoStartStatus();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            Timer_Tick(null, EventArgs.Empty);

            _ = CheckForUpdateAsync(_updateCheckCts.Token);
        }

        private async Task CheckForUpdateAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
                UpdateCheckResult result = await _updateService
                    .CheckForUpdateAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (result.CheckFailed)
                {
                    if (!cancellationToken.IsCancellationRequested && !Dispatcher.HasShutdownStarted)
                        Dispatcher.Invoke(() => ShowUpdateCheckFailedBanner(result));
                    return;
                }

                if (!result.IsUpdateAvailable)
                    return;

                if (!cancellationToken.IsCancellationRequested && !Dispatcher.HasShutdownStarted)
                    Dispatcher.Invoke(() => ShowUpdateBanner(result));
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (!cancellationToken.IsCancellationRequested && !Dispatcher.HasShutdownStarted)
                    Dispatcher.Invoke(() => ShowUpdateCheckFailedBanner(UpdateCheckResult.Failed()));
            }
        }

        private void ShowUpdateCheckFailedBanner(UpdateCheckResult result)
        {
            _pendingUpdate = UpdateCheckResult.FallbackBrowser(result.ReleasePageUrl);
            UpdateDescText.Text = GetString("String_UpdateCheckFailed", "Secure update check failed — click to check manually");
            UpdateActionButton.Content = GetString("String_UpdateOpenBrowser", "Open in Browser");
            UpdateBanner.Visibility = Visibility.Visible;
        }

        private void ShowUpdateBanner(UpdateCheckResult result)
        {
            _pendingUpdate = result;
            string descTemplate = GetString("String_UpdateDesc", "v{0} is ready — click to update now");
            UpdateDescText.Text = string.Format(descTemplate, result.LatestVersion);
            UpdateActionButton.Content = GetString("String_UpdateButton", "Update");
            UpdateBanner.Visibility = Visibility.Visible;
        }

        private async void UpdateActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading || _pendingUpdate is null) return;

            if (!_pendingUpdate.CanInstall)
            {
                Process.Start(new ProcessStartInfo(_pendingUpdate.ReleasePageUrl) { UseShellExecute = true });
                return;
            }

            _isDownloading = true;
            _downloadCts = new CancellationTokenSource();
            UpdateActionButton.IsEnabled = false;
            UpdateActionButton.Content = GetString("String_UpdateDownloading", "Downloading...");

            string? installerPath = null;
            try
            {
                Progress<int> progress = new(pct =>
                    Dispatcher.Invoke(() =>
                        UpdateActionButton.Content = $"{GetString("String_UpdateDownloading", "Downloading...")} {pct}%"));

                string sha256;
                (installerPath, sha256) = await _updateService
                    .DownloadInstallerAsync(_pendingUpdate, progress, _downloadCts.Token)
                    .ConfigureAwait(true);

                string confirmTitle = GetString("String_UpdateConfirmTitle", "Run installer?");
                string confirmMsg = GetString("String_UpdateConfirmMessage", "Download complete. SHA-256:\n{0}\n\nRun the installer now?");
                System.Windows.MessageBoxResult confirm = System.Windows.MessageBox.Show(
                    string.Format(confirmMsg, sha256),
                    confirmTitle,
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (confirm != System.Windows.MessageBoxResult.Yes)
                {
                    _updateService.DeleteInstallerTempDir(installerPath);
                    installerPath = null;
                    _isDownloading = false;
                    UpdateActionButton.IsEnabled = true;
                    UpdateActionButton.Content = GetString("String_UpdateButton", "Update");
                    return;
                }

                await _updateService.InstallUpdateAsync(_pendingUpdate, installerPath).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                if (installerPath is not null)
                    _updateService.DeleteInstallerTempDir(installerPath);
                _isDownloading = false;
            }
            catch
            {
                if (installerPath is not null)
                    _updateService.DeleteInstallerTempDir(installerPath);
                _isDownloading = false;
                UpdateActionButton.IsEnabled = true;
                UpdateActionButton.Content = GetString("String_UpdateOpenBrowser", "Open in Browser");
                string fallbackUrl = _pendingUpdate.ReleasePageUrl;
                _pendingUpdate = UpdateCheckResult.FallbackBrowser(fallbackUrl);
            }
            finally
            {
                _downloadCts?.Dispose();
                _downloadCts = null;
            }
        }

        private Dictionary<string, Ellipse> CreateAceProcessDots()
        {
            return new Dictionary<string, Ellipse>(StringComparer.OrdinalIgnoreCase)
            {
                ["SGuard64"] = SGuard64Dot,
                ["SGuardSvc64"] = SGuardSvc64Dot
            };
        }

        private string GetString(string resourceKey, string fallback)
        {
            return TryFindResource(resourceKey) as string ?? fallback;
        }

        private SolidColorBrush GetBrush(string resourceKey, Color fallbackColor)
        {
            return TryFindResource(resourceKey) as SolidColorBrush ?? new SolidColorBrush(fallbackColor);
        }

        private void UpdatePrivilegeStatus()
        {
            SolidColorBrush green = GetBrush("Green", Color.FromRgb(0x22, 0xc5, 0x5e));
            SolidColorBrush amber = GetBrush("Amber", Color.FromRgb(0xf5, 0x9e, 0x0b));

            if (_isElevated)
            {
                AdminStatusDot.Fill = green;
                AdminStatusText.Text = GetString("String_AdminStatusElevated", "Elevated");
                AdminStatusText.Foreground = green;
                AdminStatusDetailText.Text = GetString("String_AdminStatusElevatedDetail", "ACE Optimizer is running with administrator rights.");
                return;
            }

            AdminStatusDot.Fill = amber;
            AdminStatusText.Text = GetString("String_AdminStatusNormal", "Normal");
            AdminStatusText.Foreground = amber;
            AdminStatusDetailText.Text = GetString("String_AdminStatusNormalDetail", "Some ACE versions require administrator rights to change priority or CPU affinity.");
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!this.IsLoaded) return;

            SolidColorBrush green = GetBrush("Green", Color.FromRgb(0x22, 0xc5, 0x5e));
            SolidColorBrush gray = GetBrush("Gray", Color.FromRgb(0x6b, 0x72, 0x80));
            SolidColorBrush amber = GetBrush("Amber", Color.FromRgb(0xf5, 0x9e, 0x0b));

            AceOptimizationResult aceStatus = _aceProcessService.DetectAndOptimize(_affinityMask);
            UpdateAceProcessIndicators(aceStatus.DetectedProcesses, green, gray);

            if (!aceStatus.HasDetectedProcesses)
            {
                SetIdleState(gray);
                return;
            }

            SetMonitorPill(aceStatus.DetectedProcesses, green);

            if (aceStatus.AccessDenied)
            {
                HandleBlockedAceStatus(aceStatus, amber);
                return;
            }

            SetAceStatus(green, AceUiState.Optimized, BuildOptimizedDetail(aceStatus.DetectedProcesses));
        }

        private void HandleBlockedAceStatus(AceOptimizationResult aceStatus, SolidColorBrush blockedBrush)
        {
            SetAceStatus(blockedBrush, AceUiState.Blocked, BuildBlockedDetail(aceStatus.DetectedProcesses));

            if (_isElevated || _hasShownElevationPrompt)
                return;

            _hasShownElevationPrompt = true;
            PromptForElevation();
        }

        private void UpdateAceProcessIndicators(IEnumerable<string> detectedProcesses, SolidColorBrush runningBrush, SolidColorBrush idleBrush)
        {
            HashSet<string> detected = new(detectedProcesses, StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, Ellipse> entry in _aceProcessDots)
            {
                entry.Value.Fill = detected.Contains(entry.Key) ? runningBrush : idleBrush;
            }
        }

        private void SetMonitorPill(IEnumerable<string> detectedProcesses, SolidColorBrush accentBrush)
        {
            PillDot.Fill = accentBrush;
            PillText.Text = string.Join("  ·  ", detectedProcesses);
            PillText.Foreground = accentBrush;
            MonitorPill.Background = new SolidColorBrush(Color.FromArgb(40, 0x22, 0xc5, 0x5e));
        }

        private void SetIdleState(SolidColorBrush idleBrush)
        {
            PillDot.Fill = idleBrush;
            PillText.Text = GetString("String_PillIdle", "No ACE");
            PillText.Foreground = idleBrush;
            MonitorPill.Background = new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27));
            SetAceStatus(idleBrush, AceUiState.Idle, GetString("String_AceStatusIdle", "Waiting for ACE process..."));
        }

        private string BuildOptimizedDetail(IEnumerable<string> aceProcesses)
        {
            string template = GetString("String_AceDetailOptimized", "{0} — Priority Idle · Last Core");
            string processList = string.Join("  ·  ", aceProcesses);
            return string.Format(template, processList);
        }

        private string BuildBlockedDetail(IEnumerable<string> aceProcesses)
        {
            string resourceKey = _isElevated
                ? "String_AceDetailProtected"
                : "String_AceDetailAccessDenied";

            string fallback = _isElevated
                ? "{0} — ACE denied priority/affinity changes after the latest update."
                : "{0} — Access denied. Run ACE Optimizer as administrator.";

            string processList = string.Join("  ·  ", aceProcesses);
            return string.Format(GetString(resourceKey, fallback), processList);
        }

        private void PromptForElevation()
        {
            string title = GetString("String_ElevationPromptTitle", "Administrator access required");
            string message = GetString(
                "String_ElevationPromptMessage",
                "ACE Optimizer detected ACE, but Windows denied access. Restart as administrator now?");

            System.Windows.MessageBoxResult result = System.Windows.MessageBox.Show(
                message,
                title,
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            TryRestartElevated();
        }

        private void TryRestartElevated()
        {
            string executablePath = GetExecutablePath();

            if (_elevationService.TryRestartElevated(executablePath))
            {
                _isExitRequested = true;
                Application.Current.Shutdown();
                return;
            }

            _hasShownElevationPrompt = false;
        }

        private void SetAceStatus(SolidColorBrush color, AceUiState state, string detail)
        {
            AceStatusDot.Fill = color;
            
            string statusKey = state switch
            {
                AceUiState.Optimized => "String_AceStatusOptimized",
                AceUiState.Blocked => "String_AceStatusBlocked",
                _ => "String_AceStatusIdle"
            };
            
            string badgeKey = state switch
            {
                AceUiState.Optimized => "String_BadgeOptimized",
                AceUiState.Blocked => "String_BadgeBlocked",
                _ => "String_BadgeIdle"
            };

            AceStatusText.Text = GetString(statusKey, state.ToString());
            AceDetailText.Text = detail;
            
            AceBadgeLine1.Text = GetString(badgeKey, state.ToString());
            AceBadgeLine1.Foreground = color;
            AceBadgeIcon.Foreground = color;

            AceBadgeIcon.Symbol = state switch
            {
                AceUiState.Optimized => Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24,
                AceUiState.Blocked => Wpf.Ui.Controls.SymbolRegular.Prohibited24,
                _ => Wpf.Ui.Controls.SymbolRegular.CircleLine24,
            };

            AceBadgeBg.Color = state switch
            {
                AceUiState.Optimized => Color.FromArgb(40, 0x22, 0xc5, 0x5e),
                AceUiState.Blocked => Color.FromArgb(40, 0xef, 0x44, 0x44),
                _ => Color.FromRgb(0x11, 0x18, 0x27),
            };
        }

        private void CheckAutoStartStatus()
        {
            AutoStartToggle.IsChecked = _autoStartService.IsEnabled();
        }

        private void AutoStartToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded) return;
            try
            {
                _autoStartService.Enable(GetExecutablePath());
            }
            catch (Exception ex)
            {
                string title = GetString("String_AppTitle", "ACE Optimizer");
                string message = GetString("String_EnableAutoStartFailed", "Failed to enable auto-start: {0}");
                System.Windows.MessageBox.Show(string.Format(message, ex.Message), title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                AutoStartToggle.IsChecked = false;
            }
        }

        private void AutoStartToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded) return;
            try
            {
                _autoStartService.Disable();
            }
            catch (Exception ex)
            {
                string title = GetString("String_AppTitle", "ACE Optimizer");
                string message = GetString("String_DisableAutoStartFailed", "Failed to disable auto-start: {0}");
                System.Windows.MessageBox.Show(string.Format(message, ex.Message), title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                AutoStartToggle.IsChecked = true;
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized) ShowInTaskbar = false;
        }

        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e) => RestoreWindow();
        private void MenuItem_Open_Click(object sender, RoutedEventArgs e) => RestoreWindow();
        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            _isExitRequested = true;
            Application.Current.Shutdown();
        }

        private void RestoreWindow()
        {
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            Activate();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void UpdateService_ExitRequested()
        {
            Dispatcher.Invoke(() =>
            {
                _isExitRequested = true;
                Application.Current.Shutdown();
            });
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_isExitRequested)
            {
                _timer.Stop();
                _updateCheckCts.Cancel();
                _updateCheckCts.Dispose();
                _downloadCts?.Cancel();
                _downloadCts?.Dispose();
                _downloadCts = null;
                _updateService.ExitRequested -= UpdateService_ExitRequested;
                _updateService.Dispose();
                trayIcon.Dispose();
                return;
            }

            e.Cancel = true;
            WindowState = WindowState.Minimized;
        }

        private static string GetExecutablePath()
        {
            return Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? System.IO.Path.Combine(AppContext.BaseDirectory, "ACEOptimizer.exe");
        }
    }
}
