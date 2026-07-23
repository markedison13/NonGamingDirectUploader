using NonGamingDirectUploader.Helpers;
using NonGamingDirectUploader.Models;
using NonGamingDirectUploader.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NonGamingDirectUploader.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new();
        private readonly UploaderPage _othersPage = new();
        private readonly UploaderPage _fnBPage = new();
        private readonly UploaderPage _hotelPage = new();
        private readonly UploaderPage _visitationPage = new();
        private string _activeTag = "Others";

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            // Bind each page to its view model
            _othersPage.SetViewModel(_vm.OthersVM);
            _fnBPage.SetViewModel(_vm.FnBVM);
            _hotelPage.SetViewModel(_vm.HotelVM);
            _visitationPage.SetViewModel(_vm.VisitationVM);

            PageLabel.Text = "Others Uploader";
            PageHost.Content = _othersPage;

            // Warm up the OLE DB driver for every configured database (both
            // SEC and SN) right away, on app launch. The native ACE OLEDB
            // provider is typically only ever loaded once per process — if
            // that first load happens while the user is interacting with the
            // UI (e.g. clicking the SN radio button), a driver-level fault can
            // surface as a crash. Doing it here, before any button is clicked,
            // moves that risk to startup where it's silent and harmless.
            _ = WarmUpDatabaseDriversAsync();
        }

        private async System.Threading.Tasks.Task WarmUpDatabaseDriversAsync()
        {
            var paths = new HashSet<string>();
            foreach (UploaderType type in Enum.GetValues(typeof(UploaderType)))
                foreach (PropertyType prop in Enum.GetValues(typeof(PropertyType)))
                    if (DatabaseConfig.TryGetPath(type, prop, out var path))
                        paths.Add(path);

            foreach (var path in paths)
            {
                try { await DatabaseService.TestConnectionAsync(path); }
                catch { /* silent warm-up only */ }
            }

            await _vm.OthersVM.TestConnectionAsync();
            await _vm.FnBVM.TestConnectionAsync();
            await _vm.HotelVM.TestConnectionAsync();
            await _vm.VisitationVM.TestConnectionAsync();
        }

        // ── Automation ────────────────────────────────────────────────────────
        /// <summary>
        /// Manually triggers one full automation import pass — the same logic
        /// that runs headlessly at 5:00 AM via Task Scheduler — so you can test
        /// it without waiting or configuring the schedule first.
        /// </summary>
        private async void RunAutomation_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            btn.IsEnabled = false;
            btn.Content = "⟳  Running…";
            try
            {
                var log = await AutomationService.RunImportAsync();
                var summary = string.Join(Environment.NewLine, log);
                MessageBox.Show(summary, "Automation Run Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Automation run failed: {ex.Message}", "Automation Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btn.IsEnabled = true;
                btn.Content = "▶  Run Automation Now";
            }
        }

        // ── Navigation ────────────────────────────────────────────────────────
        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                SwitchTo(btn.Tag?.ToString() ?? "Others");
        }

        private void SwitchTo(string tag)
        {
            _activeTag = tag;

            // Reset all nav styles
            NavOthers.Style = (Style)FindResource("NavButton");
            NavFnB.Style = (Style)FindResource("NavButton");
            NavHotel.Style = (Style)FindResource("NavButton");
            NavVisitation.Style = (Style)FindResource("NavButton");

            // Hide all dots
            DotOthers.Visibility = Visibility.Collapsed;
            DotFnB.Visibility = Visibility.Collapsed;
            DotHotel.Visibility = Visibility.Collapsed;
            DotVisitation.Visibility = Visibility.Collapsed;

            // Activate selected
            switch (tag)
            {
                case "Others":
                    PageHost.Content = _othersPage;
                    NavOthers.Style = (Style)FindResource("NavButtonActive");
                    DotOthers.Visibility = Visibility.Visible;
                    PageLabel.Text = "Others Uploader";
                    _vm.ActiveUploader = UploaderType.Others;
                    break;
                case "FnB":
                    PageHost.Content = _fnBPage;
                    NavFnB.Style = (Style)FindResource("NavButtonActive");
                    DotFnB.Visibility = Visibility.Visible;
                    PageLabel.Text = "F&B Uploader";
                    _vm.ActiveUploader = UploaderType.FnB;
                    break;
                case "Hotel":
                    PageHost.Content = _hotelPage;
                    NavHotel.Style = (Style)FindResource("NavButtonActive");
                    DotHotel.Visibility = Visibility.Visible;
                    PageLabel.Text = "Hotel Uploader";
                    _vm.ActiveUploader = UploaderType.Hotel;
                    break;
                case "Visitation":
                    PageHost.Content = _visitationPage;
                    NavVisitation.Style = (Style)FindResource("NavButtonActive");
                    DotVisitation.Visibility = Visibility.Visible;
                    PageLabel.Text = "Visitation Uploader";
                    _vm.ActiveUploader = UploaderType.Visitation;
                    break;
            }
        }

        // ── Property radio ────────────────────────────────────────────────────
        private void Property_Checked(object sender, RoutedEventArgs e)
        {
            var prop = RadioSEC.IsChecked == true ? PropertyType.SEC : PropertyType.SN;
            _vm.OthersVM.Property = prop;
            _vm.FnBVM.Property = prop;
            _vm.HotelVM.Property = prop;
            _vm.VisitationVM.Property = prop;
        }

        // ── Custom window chrome ──────────────────────────────────────────────
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        private void MinBtn_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void MaxBtn_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
               ? WindowState.Normal : WindowState.Maximized;
    }
}