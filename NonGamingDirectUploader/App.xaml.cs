using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NonGamingDirectUploader.Helpers;
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

            // Headless mode — launched by Windows Task Scheduler as:
            //   NonGamingDirectUploader.exe --auto-upload
            // Runs the folder import once, with no window shown, then exits.
            // This is the trigger for the daily 5:00 AM automated import.
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