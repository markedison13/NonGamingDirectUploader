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

        // NonGaming pages
        private readonly UploaderPage _othersPage = new();
        private readonly UploaderPage _fnBPage = new();
        private readonly UploaderPage _hotelPage = new();
        private readonly UploaderPage _visitationPage = new();

        // Gaming pages
        private readonly UploaderPage _massPage = new();
        private readonly UploaderPage _vipPage = new();
        private readonly UploaderPage _junketPage = new();

        private string _activeTag = "Others";
        private BusinessLine _activeLine = BusinessLine.NonGaming;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"MainWindow failed to load its XAML:\n\n{ex}",
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }

            DataContext = _vm;

            // Defer anything that touches named XAML elements until the
            // window has actually finished loading its visual tree. This
            // avoids "element was null right after InitializeComponent()"
            // issues that can happen with heavier windows.
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Bind each page to its view model
            _othersPage.SetViewModel(_vm.OthersVM);
            _fnBPage.SetViewModel(_vm.FnBVM);
            _hotelPage.SetViewModel(_vm.HotelVM);
            _visitationPage.SetViewModel(_vm.VisitationVM);

            _massPage.SetViewModel(_vm.MassVM);
            _vipPage.SetViewModel(_vm.VIPVM);
            _junketPage.SetViewModel(_vm.JunketVM);

            PageLabel.Text = "Others Uploader";

            // Defensive fallback: if the compiler-wired field is somehow
            // still null, try resolving it from the live visual tree
            // instead of crashing with a bare NullReferenceException.
            var host = PageHost ?? (ContentControl)FindName("PageHost");
            if (host == null)
            {
                MessageBox.Show(
                    "PageHost could not be resolved from MainWindow.xaml.\n\n" +
                    "This usually means the compiled XAML is out of sync with the source. " +
                    "Try closing Visual Studio, deleting the bin/obj folders and the .vs folder, " +
                    "then rebuilding.",
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            host.Content = _othersPage;

            // Warm up the OLE DB driver for every configured database (both
            // SEC and SN, across both business lines) right away, on app
            // launch — before any button is clicked.
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

            await _vm.MassVM.TestConnectionAsync();
            await _vm.VIPVM.TestConnectionAsync();
            await _vm.JunketVM.TestConnectionAsync();
        }

        // ── Automation ────────────────────────────────────────────────────────
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

        // ── Business line switch (NonGaming / Gaming) ────────────────────────
        private void BusinessLineCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Same early-fire guard as SwitchTo — IsSelected="True" on the
            // ComboBoxItem triggers this during XAML parsing, before the
            // rest of the window (including PageHost) exists yet.
            if (PageHost == null) return;

            if (BusinessLineCombo.SelectedItem is not ComboBoxItem item) return;
            var selected = item.Content?.ToString() == "Gaming" ? BusinessLine.Gaming : BusinessLine.NonGaming;

            _activeLine = selected;
            _vm.ActiveBusinessLine = selected;

            if (selected == BusinessLine.NonGaming)
            {
                NonGamingNavPanel.Visibility = Visibility.Visible;
                GamingNavPanel.Visibility = Visibility.Collapsed;
                SwitchTo("Others");
            }
            else
            {
                NonGamingNavPanel.Visibility = Visibility.Collapsed;
                GamingNavPanel.Visibility = Visibility.Visible;
                SwitchTo("Mass");
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
            // Guard: BusinessLineCombo's ComboBoxItem has IsSelected="True" in
            // XAML, which fires SelectionChanged -> SwitchTo DURING XAML
            // parsing, before InitializeComponent() has finished wiring up
            // every named element. PageHost is wired up last, so at that
            // moment it's still null. Ignore any call that happens that
            // early — the real initial page gets set in MainWindow_Loaded.
            if (PageHost == null)
                return;

            _activeTag = tag;

            // Reset all nav styles across BOTH panels
            NavOthers.Style = (Style)FindResource("NavButton");
            NavFnB.Style = (Style)FindResource("NavButton");
            NavHotel.Style = (Style)FindResource("NavButton");
            NavVisitation.Style = (Style)FindResource("NavButton");
            NavMass.Style = (Style)FindResource("NavButton");
            NavVIP.Style = (Style)FindResource("NavButton");
            NavJunket.Style = (Style)FindResource("NavButton");

            // Hide all dots
            DotOthers.Visibility = Visibility.Collapsed;
            DotFnB.Visibility = Visibility.Collapsed;
            DotHotel.Visibility = Visibility.Collapsed;
            DotVisitation.Visibility = Visibility.Collapsed;
            DotMass.Visibility = Visibility.Collapsed;
            DotVIP.Visibility = Visibility.Collapsed;
            DotJunket.Visibility = Visibility.Collapsed;

            // Activate selected
            switch (tag)
            {
                // NonGaming
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

                // Gaming
                case "Mass":
                    PageHost.Content = _massPage;
                    NavMass.Style = (Style)FindResource("NavButtonActive");
                    DotMass.Visibility = Visibility.Visible;
                    PageLabel.Text = "Mass Uploader";
                    _vm.ActiveUploader = UploaderType.Mass;
                    break;
                case "VIP":
                    PageHost.Content = _vipPage;
                    NavVIP.Style = (Style)FindResource("NavButtonActive");
                    DotVIP.Visibility = Visibility.Visible;
                    PageLabel.Text = "VIP Uploader";
                    _vm.ActiveUploader = UploaderType.VIP;
                    break;
                case "Junket":
                    PageHost.Content = _junketPage;
                    NavJunket.Style = (Style)FindResource("NavButtonActive");
                    DotJunket.Visibility = Visibility.Visible;
                    PageLabel.Text = "Junket Uploader";
                    _vm.ActiveUploader = UploaderType.Junket;
                    break;
            }
        }

        // ── Property radio ────────────────────────────────────────────────────
        // Property (SEC/SN) applies across BOTH business lines at once.
        private void Property_Checked(object sender, RoutedEventArgs e)
        {
            var prop = RadioSEC.IsChecked == true ? PropertyType.SEC : PropertyType.SN;
            _vm.OthersVM.Property = prop;
            _vm.FnBVM.Property = prop;
            _vm.HotelVM.Property = prop;
            _vm.VisitationVM.Property = prop;

            _vm.MassVM.Property = prop;
            _vm.VIPVM.Property = prop;
            _vm.JunketVM.Property = prop;
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