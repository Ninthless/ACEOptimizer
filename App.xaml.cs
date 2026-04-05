using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using ACEOptimizer.Services;

namespace ACEOptimizer
{
    public partial class App : Application
    {
        private const string SingleInstanceMutexName = "ACE_Optimizer_SingleInstance_Mutex";

        private static Mutex? _mutex;
        private readonly LocalizationService _localizationService = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            if (!EnsureSingleInstance())
            {
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);
            LoadLocalizationResources();
            RegisterUnhandledExceptionHandlers();
        }

        private bool EnsureSingleInstance()
        {
            _mutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
            if (createdNew)
                return true;

            ShowAlreadyRunningMessage();
            return false;
        }

        private void ShowAlreadyRunningMessage()
        {
            string title = _localizationService.GetStringForCurrentCulture("String_AppTitle", "ACE Optimizer");
            string message = _localizationService.GetStringForCurrentCulture("String_AlreadyRunning", "ACE Optimizer is already running.");
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadLocalizationResources()
        {
            try
            {
                _localizationService.LoadApplicationResources(this);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load i18n resources: {ex.Message}");
            }
        }

        private void RegisterUnhandledExceptionHandlers()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            if (IsDesktopCompositionUnavailable(e.Exception))
            {
                e.Handled = true;
                return;
            }

            ShowFatalError("String_UIThreadException", "UI Thread Exception", e.Exception);
            e.Handled = true;
            Environment.Exit(1);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                ShowFatalError("String_BackgroundThreadException", "Background Thread Exception", ex);
            }

            Environment.Exit(1);
        }

        private static bool IsDesktopCompositionUnavailable(Exception exception)
        {
            return exception is COMException comException
                && (uint)comException.HResult == 0x80263001;
        }

        private void ShowFatalError(string messageKey, string fallbackMessage, Exception exception)
        {
            string title = GetString("String_FatalErrorTitle", "Fatal Error");
            string prefix = GetString(messageKey, fallbackMessage);
            string body = $"{prefix}: {exception.Message}\n\n{exception.StackTrace}";
            MessageBox.Show(body, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private string GetString(string resourceKey, string fallback)
        {
            return _localizationService.GetStringFromApplicationResources(this, resourceKey, fallback);
        }
    }
}
