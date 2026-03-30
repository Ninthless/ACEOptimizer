using System.Windows;
using Wpf.Ui.Appearance;

namespace ACEOptimizer
{
    public partial class App : Application
    {
        private static System.Threading.Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Single Instance Check
            bool createdNew;
            _mutex = new System.Threading.Mutex(true, "ACE_Optimizer_SingleInstance_Mutex", out createdNew);

            if (!createdNew)
            {
                // App is already running. Need to load strings first to show localized message.
                string culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
                string resPath = culture.StartsWith("zh") ? "Resources/Strings.zh-CN.xaml" : "Resources/Strings.en-US.xaml";
                try
                {
                    var dict = new ResourceDictionary { Source = new Uri($"pack://application:,,,/{resPath}", UriKind.Absolute) };
                    string? msg = dict["String_AlreadyRunning"] as string;
                    MessageBox.Show(msg ?? "ACE Optimizer is already running.", "ACE Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch { MessageBox.Show("ACE Optimizer is already running."); }
                
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);
            
            // Language detection
            string currentCulture = System.Globalization.CultureInfo.CurrentUICulture.Name;
            string resourcePath = currentCulture.StartsWith("zh") 
                ? "Resources/Strings.zh-CN.xaml" 
                : "Resources/Strings.en-US.xaml";

            try
            {
                // Use Pack URI format for compiled resources
                var dict = new ResourceDictionary 
                { 
                    Source = new Uri($"pack://application:,,,/{resourcePath}", UriKind.Absolute) 
                };
                this.Resources.MergedDictionaries.Add(dict);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load i18n resources: {ex.Message}");
            }

            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // 0x80263001: Desktop composition is disabled (e.g. in some RDP sessions).
            // WPF-UI's FluentWindow/WindowChrome will throw this when DWM is unavailable.
            // Silently ignore it — the window will render without Mica/glass effects.
            if (e.Exception is System.Runtime.InteropServices.COMException comEx
                && (uint)comEx.HResult == 0x80263001)
            {
                e.Handled = true;
                return;
            }

            MessageBox.Show($"UI Thread Exception: {e.Exception.Message}\n\n{e.Exception.StackTrace}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
            Environment.Exit(1);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show($"Background Thread Exception: {ex.Message}\n\n{ex.StackTrace}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Environment.Exit(1);
        }
    }
}
