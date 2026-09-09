using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NonGamingDirectUploader.Helpers;
using NonGamingDirectUploader.Models;
using NonGamingDirectUploader.Views;

namespace NonGamingDirectUploader
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Safety net: a native/COM-level fault (e.g. from the ACE OLEDB
            // driver) should never be allowed to take the whole app down.
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += App_DomainUnhandledException;
            TaskScheduler.UnobservedTaskException += App_UnobservedTaskException;

            // Headless mode — launched by Windows Task Scheduler. Four
            // possible args, so each business line can run as a fully
            // independent scheduled task (different times, independent
            // success/failure) instead of always running together:
            //
            //   NonGamingDirectUploader.exe --auto-upload-nongaming
            //   NonGamingDirectUploader.exe --auto-upload-gaming
            //   NonGamingDirectUploader.exe --auto-upload-onlinegaming
            //   NonGamingDirectUploader.exe --auto-upload            (legacy: runs ALL)
            //
            // Runs the folder import once, with no window shown, then exits.
            if (Array.Exists(e.Args, a => string.Equals(a, "--auto-upload-nongaming", StringComparison.OrdinalIgnoreCase)))
            {
                await AutomationService.RunImportAsync(BusinessLine.NonGaming);
                Shutdown();
                return;
            }

            if (Array.Exists(e.Args, a => string.Equals(a, "--auto-upload-gaming", StringComparison.OrdinalIgnoreCase)))
            {
                await AutomationService.RunImportAsync(BusinessLine.Gaming);
                Shutdown();
                return;
            }

            if (Array.Exists(e.Args, a => string.Equals(a, "--auto-upload-onlinegaming", StringComparison.OrdinalIgnoreCase)))
            {
                await AutomationService.RunImportAsync(BusinessLine.OnlineGaming);
                Shutdown();
                return;
            }

            // Legacy arg — kept for backward compatibility with any existing
            // scheduled task. Runs ALL business lines in one pass, same as
            // the original behavior. Prefer the scoped args above for new
            // Task Scheduler setups.
            if (Array.Exists(e.Args, a => string.Equals(a, "--auto-upload", StringComparison.OrdinalIgnoreCase)))
            {
                await AutomationService.RunImportAsync();
                Shutdown();
                return;
            }

            // Normal interactive launch.
            new MainWindow().Show();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\n" +
                "This is often caused by a missing or mismatched Microsoft Access Database " +
                "Engine (ACE OLEDB) driver — make sure its bitness (32-bit/64-bit) matches this app. " +
                "The application will keep running.",
                "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void App_DomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show(
                    $"A critical error occurred:\n\n{ex.Message}",
                    "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void App_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
        }
    }
}