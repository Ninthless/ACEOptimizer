using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace ACEOptimizer
{
    public partial class MainWindow : FluentWindow
    {
        // ---- Game profiles – all use ACE ----
        private record GameProfile(string Process, string DisplayName, System.Windows.Shapes.Ellipse? Dot = null);

        private static readonly string[] AceProcesses = { "SGuard64", "SGuardSvc64" };

        private List<GameProfile> _games = new();   // populated after InitializeComponent

        private readonly Dictionary<string, bool> _gameWasRunning = new();



        private readonly DispatcherTimer _timer;
        private nint _affinityMask;

        public MainWindow()
        {
            InitializeComponent();

            // Wire up game profiles with their UI dots
            _games = new List<GameProfile>
            {
                new("DeltaForceClient-Win64-Shipping", "三角洲行动", DeltaDot),
                new("VALORANT-Win64-Shipping",          "VALORANT",  ValorantDot),
            };

            foreach (var g in _games)
                _gameWasRunning[g.Process] = false;

            CalculateAffinityMask();
            CheckAutoStartStatus();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            
            // Initial call to set UI
            Timer_Tick(null, EventArgs.Empty);
        }

        // ---------------------------------------------------------------
        // Affinity mask
        // ---------------------------------------------------------------
        private void CalculateAffinityMask()
        {
            try
            {
                int cores = Environment.ProcessorCount;
                _affinityMask = (nint)(1L << (Math.Max(cores, 1) - 1));
            }
            catch { _affinityMask = 1; }
        }

        // ---------------------------------------------------------------
        // Timer tick – one unified ACE loop
        // ---------------------------------------------------------------
        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!this.IsLoaded) return;

            var green = (SolidColorBrush)FindResource("Green");
            var gray = (SolidColorBrush)FindResource("Gray");
            var amber = (SolidColorBrush)FindResource("Amber");

            // Detect which game (if any) is running
            GameProfile? activeGame = null;
            foreach (var g in _games)
            {
                bool running = Process.GetProcessesByName(g.Process).Length > 0;
                if (g.Dot != null)
                    g.Dot.Fill = running ? green : gray;
                if (running && activeGame == null)
                    activeGame = g;
                _gameWasRunning[g.Process] = running;
            }

            if (activeGame != null)
            {
                // Update live pill
                PillDot.Fill = green;
                PillText.Text = activeGame.DisplayName;
                PillText.Foreground = green;
                ActiveGamePill.Background = new SolidColorBrush(Color.FromArgb(40, 0x22, 0xc5, 0x5e));

                // Optimise ACE
                bool optimised = false;
                var actions = new List<string>();

                foreach (var aceName in AceProcesses)
                {
                    foreach (var proc in Process.GetProcessesByName(aceName))
                    {
                        try
                        {
                            if (proc.PriorityClass != ProcessPriorityClass.Idle)
                            {
                                proc.PriorityClass = ProcessPriorityClass.Idle;
                                optimised = true;
                            }
                            try
                            {
                                if (proc.ProcessorAffinity != _affinityMask)
                                {
                                    proc.ProcessorAffinity = _affinityMask;
                                    optimised = true;
                                }
                            }
                            catch (Win32Exception) { /* ACE self-protection, ignore */ }

                            actions.Add(aceName);
                        }
                        catch { }
                    }
                }

                if (optimised || actions.Count > 0)
                {
                    SetAceStatus(green, "OPTIMIZED",
                        $"{FindResource("String_BadgeOptimized")}: {string.Join("  ·  ", new HashSet<string>(actions))} — Priority Idle · Last Core");
                }
                else
                {
                    SetAceStatus(amber, "ACTIVE",
                        FindResource("String_AceStatusActive") as string ?? "Game running — waiting for ACE...");
                }
            }
            else
            {
                // No game running
                PillDot.Fill = gray;
                PillText.Text = FindResource("String_BadgeIdle") as string;
                PillText.Foreground = gray;
                ActiveGamePill.Background = new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27));
                SetAceStatus(gray, "IDLE", FindResource("String_AceStatusIdle") as string ?? "Waiting for a supported game...");
            }
        }

        private void SetAceStatus(SolidColorBrush color, string badgeState, string detail)
        {
            AceStatusDot.Fill = color;
            
            // Map state to localized string key
            string statusKey = badgeState switch
            {
                "OPTIMIZED" => "String_AceStatusOptimized",
                "ACTIVE"    => "String_AceStatusActive",
                _           => "String_AceStatusIdle"
            };
            
            string badgeKey = badgeState switch
            {
                "OPTIMIZED" => "String_BadgeOptimized",
                "ACTIVE"    => "String_BadgeActive",
                _           => "String_BadgeIdle"
            };

            AceStatusText.Text = FindResource(statusKey) as string;
            AceDetailText.Text = detail;
            
            AceBadgeLine1.Text = FindResource(badgeKey) as string;
            AceBadgeLine1.Foreground = color;
            AceBadgeIcon.Foreground = color;

            // Update icon and chip background tint per state
            AceBadgeIcon.Symbol = badgeState switch
            {
                "OPTIMIZED" => Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24,
                "ACTIVE"    => Wpf.Ui.Controls.SymbolRegular.Prohibited24,
                _           => Wpf.Ui.Controls.SymbolRegular.CircleLine24,
            };

            AceBadgeBg.Color = badgeState switch
            {
                "OPTIMIZED" => Color.FromArgb(40, 0x22, 0xc5, 0x5e),  // subtle green tint
                "ACTIVE"    => Color.FromArgb(40, 0xf5, 0x9e, 0x0b),  // subtle amber tint
                _           => Color.FromRgb(0x11, 0x18, 0x27),        // dark default
            };
        }

        // ---------------------------------------------------------------
        // Auto-Start via PowerShell Task Scheduler
        // ---------------------------------------------------------------
        private const string TaskName = "ACEOptimizer";

        private void CheckAutoStartStatus()
        {
            try
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NonInteractive -NoProfile -Command \"Get-ScheduledTask -TaskName '{TaskName}' -ErrorAction Stop\"",
                        UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardOutput = true, RedirectStandardError = true
                    }
                };
                p.Start(); p.WaitForExit();
                AutoStartToggle.IsChecked = (p.ExitCode == 0);
            }
            catch { AutoStartToggle.IsChecked = false; }
        }

        private void AutoStartToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded) return;
            try
            {
                string? exe = Process.GetCurrentProcess().MainModule?.FileName
                            ?? System.Reflection.Assembly.GetExecutingAssembly().Location;

                string ps = $"$a = New-ScheduledTaskAction -Execute '{exe.Replace("'", "''")}'; "
                          + $"$t = New-ScheduledTaskTrigger -AtLogon; "
                          + $"Register-ScheduledTask -TaskName '{TaskName}' -Action $a -Trigger $t -RunLevel Highest -Force";

                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NonInteractive -NoProfile -Command \"{ps}\"",
                        UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardOutput = true, RedirectStandardError = true
                    }
                };
                p.Start();
                string err = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0) throw new Exception(err);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"设置开机自启失败: {ex.Message}");
                AutoStartToggle.IsChecked = false;
            }
        }

        private void AutoStartToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded) return;
            try
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NonInteractive -NoProfile -Command \"Unregister-ScheduledTask -TaskName '{TaskName}' -Confirm:$false -ErrorAction SilentlyContinue\"",
                        UseShellExecute = false, CreateNoWindow = true
                    }
                };
                p.Start(); p.WaitForExit();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"取消开机自启失败: {ex.Message}");
                AutoStartToggle.IsChecked = true;
            }
        }

        // ---------------------------------------------------------------
        // Window / Tray
        // ---------------------------------------------------------------
        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized) ShowInTaskbar = false;
        }

        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e) => RestoreWindow();
        private void MenuItem_Open_Click(object sender, RoutedEventArgs e) => RestoreWindow();
        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

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

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
        }
    }
}
