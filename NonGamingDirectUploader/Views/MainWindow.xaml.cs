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

        // Online Gaming — Data Viewers reuse the SAME UploaderPage as every
        // other module (Get Data, Edit, Delete — no bulk upload since
        // SupportsUpload is false for these three). Uploaders are their own
        // separate pages (fixed category boxes, Wager/Win only).
        private readonly UploaderPage _virtualGamesViewerPage = new();
        private readonly UploaderPage _sportsBookViewerPage = new();
        private readonly UploaderPage _funaloMaxViewerPage = new();

        private readonly OnlineGamingUploaderPage _virtualGamesUploaderPage = new();
        private readonly OnlineGamingUploaderPage _sportsBookUploaderPage = new();
        private readonly OnlineGamingUploaderPage _funaloMaxUploaderPage = new();

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

            _virtualGamesViewerPage.SetViewModel(_vm.VirtualGamesVM);
            _sportsBookViewerPage.SetViewModel(_vm.SportsBookVM);
            _funaloMaxViewerPage.SetViewModel(_vm.FUNaloMAXVM);

            _virtualGamesUploaderPage.SetViewModel(_vm.VirtualGamesVM);
            _sportsBookUploaderPage.SetViewModel(_vm.SportsBookVM);
            _funaloMaxUploaderPage.SetViewModel(_vm.FUNaloMAXVM);

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

            // NOTE: there is deliberately NO automatic "warm up every
            // database on launch" step here anymore. Every module's
            // Connected/Not Connected badge now only updates as a side
            // effect of an actual action — Get Data, Upload, Edit, Delete,
            // or clicking "Test Connection" explicitly — instead of the app
            // pinging all 10+ configured databases (including any that are
            // still placeholder paths) the instant the window opens.
        }

        // ── Automation ────────────────────────────────────────────────────────
        // Scoped to whichever business line (NonGaming/Gaming/Online Gaming)
        // is currently active in the UI, via _activeLine — set in
        // BusinessLineCombo_Changed below. Only imports files for the active
        // tab; scheduled/unattended runs should use the dedicated
        // --auto-upload-* launch args instead (see App.xaml.cs) so each line
        // runs independently.
        private async void RunAutomation_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            btn.IsEnabled = false;
            btn.Content = "⟳  Running…";
            try
            {
                var log = await AutomationService.RunImportAsync(_activeLine);
                var summary = string.Join(Environment.NewLine, log);
                MessageBox.Show(summary, $"Automation Run Complete ({_activeLine})", MessageBoxButton.OK, MessageBoxImage.Information);
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

        // ── Business line switch (NonGaming / Gaming / Online Gaming) ────────
        private void BusinessLineCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Same early-fire guard as SwitchTo — IsSelected="True" on the
            // ComboBoxItem triggers this during XAML parsing, before the
            // rest of the window (including PageHost) exists yet.
            if (PageHost == null) return;

            if (BusinessLineCombo.SelectedItem is not ComboBoxItem item) return;
            var content = item.Content?.ToString();

            BusinessLine selected = content switch
            {
                "Gaming" => BusinessLine.Gaming,
                "Online Gaming" => BusinessLine.OnlineGaming,
                _ => BusinessLine.NonGaming
            };

            _activeLine = selected;
            _vm.ActiveBusinessLine = selected;

            NonGamingNavPanel.Visibility = selected == BusinessLine.NonGaming ? Visibility.Visible : Visibility.Collapsed;
            GamingNavPanel.Visibility = selected == BusinessLine.Gaming ? Visibility.Visible : Visibility.Collapsed;
            OnlineGamingNavPanel.Visibility = selected == BusinessLine.OnlineGaming ? Visibility.Visible : Visibility.Collapsed;

            // Online Gaming is entered manually via the Uploader tabs and is
            // never automated (see AutomationService) — hide the button
            // entirely there instead of leaving a control that would just
            // run and report "nothing to do".
            bool isOnlineGaming = selected == BusinessLine.OnlineGaming;
            RunAutomationBtn.Visibility = isOnlineGaming ? Visibility.Collapsed : Visibility.Visible;
            AutomationNoteText.Visibility = isOnlineGaming ? Visibility.Collapsed : Visibility.Visible;

            // TEMPORARY: only SEC is configured for Online Gaming right now
            // (see DatabaseConfig — the SN entries for VirtualGames/
            // SportsBook/FUNaloMAX are commented out), so the SEC/SN picker
            // has nothing meaningful to switch between there. Hide it while
            // that's the case; bring it back once SN paths are added.
            PropertyPanel.Visibility = isOnlineGaming ? Visibility.Collapsed : Visibility.Visible;

            switch (selected)
            {
                case BusinessLine.NonGaming:
                    SwitchTo("Others");
                    break;
                case BusinessLine.Gaming:
                    SwitchTo("Mass");
                    break;
                case BusinessLine.OnlineGaming:
                    SwitchTo("VirtualGames");
                    break;
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

            // Reset all nav styles across ALL THREE panels
            NavOthers.Style = (Style)FindResource("NavButton");
            NavFnB.Style = (Style)FindResource("NavButton");
            NavHotel.Style = (Style)FindResource("NavButton");
            NavVisitation.Style = (Style)FindResource("NavButton");
            NavMass.Style = (Style)FindResource("NavButton");
            NavVIP.Style = (Style)FindResource("NavButton");
            NavJunket.Style = (Style)FindResource("NavButton");
            NavVirtualGames.Style = (Style)FindResource("NavButton");
            NavSportsBook.Style = (Style)FindResource("NavButton");
            NavFUNaloMAX.Style = (Style)FindResource("NavButton");
            NavVirtualGamesUploader.Style = (Style)FindResource("NavButton");
            NavSportsBookUploader.Style = (Style)FindResource("NavButton");
            NavFUNaloMAXUploader.Style = (Style)FindResource("NavButton");

            // Hide all dots
            DotOthers.Visibility = Visibility.Collapsed;
            DotFnB.Visibility = Visibility.Collapsed;
            DotHotel.Visibility = Visibility.Collapsed;
            DotVisitation.Visibility = Visibility.Collapsed;
            DotMass.Visibility = Visibility.Collapsed;
            DotVIP.Visibility = Visibility.Collapsed;
            DotJunket.Visibility = Visibility.Collapsed;
            DotVirtualGames.Visibility = Visibility.Collapsed;
            DotSportsBook.Visibility = Visibility.Collapsed;
            DotFUNaloMAX.Visibility = Visibility.Collapsed;
            DotVirtualGamesUploader.Visibility = Visibility.Collapsed;
            DotSportsBookUploader.Visibility = Visibility.Collapsed;
            DotFUNaloMAXUploader.Visibility = Visibility.Collapsed;

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

                // Online Gaming — Data Viewers (reuse UploaderPage, same as
                // Gaming/NonGaming: Get Data + Edit + Delete, no bulk upload).
                case "VirtualGames":
                    PageHost.Content = _virtualGamesViewerPage;
                    NavVirtualGames.Style = (Style)FindResource("NavButtonActive");
                    DotVirtualGames.Visibility = Visibility.Visible;
                    PageLabel.Text = "Online Gaming — Table Games Data Viewer";
                    _vm.ActiveUploader = UploaderType.VirtualGames;
                    break;
                case "SportsBook":
                    PageHost.Content = _sportsBookViewerPage;
                    NavSportsBook.Style = (Style)FindResource("NavButtonActive");
                    DotSportsBook.Visibility = Visibility.Visible;
                    PageLabel.Text = "Online Gaming — SportsBook Data Viewer";
                    _vm.ActiveUploader = UploaderType.SportsBook;
                    break;
                case "FUNaloMAX":
                    PageHost.Content = _funaloMaxViewerPage;
                    NavFUNaloMAX.Style = (Style)FindResource("NavButtonActive");
                    DotFUNaloMAX.Visibility = Visibility.Visible;
                    PageLabel.Text = "Online Gaming — FUNaloMAX Data Viewer";
                    _vm.ActiveUploader = UploaderType.FUNaloMAX;
                    break;

                // Online Gaming — Uploaders (separate pages, fixed category
                // boxes, Wager/Win only).
                case "VirtualGamesUploader":
                    PageHost.Content = _virtualGamesUploaderPage;
                    NavVirtualGamesUploader.Style = (Style)FindResource("NavButtonActive");
                    DotVirtualGamesUploader.Visibility = Visibility.Visible;
                    PageLabel.Text = "Online Gaming — Table Games Uploader";
                    _vm.ActiveUploader = UploaderType.VirtualGames;
                    break;
                case "SportsBookUploader":
                    PageHost.Content = _sportsBookUploaderPage;
                    NavSportsBookUploader.Style = (Style)FindResource("NavButtonActive");
                    DotSportsBookUploader.Visibility = Visibility.Visible;
                    PageLabel.Text = "Online Gaming — SportsBook Uploader";
                    _vm.ActiveUploader = UploaderType.SportsBook;
                    break;
                case "FUNaloMAXUploader":
                    PageHost.Content = _funaloMaxUploaderPage;
                    NavFUNaloMAXUploader.Style = (Style)FindResource("NavButtonActive");
                    DotFUNaloMAXUploader.Visibility = Visibility.Visible;
                    PageLabel.Text = "Online Gaming — FUNaloMAX Uploader";
                    _vm.ActiveUploader = UploaderType.FUNaloMAX;
                    break;
            }
        }

        // ── Property radio ────────────────────────────────────────────────────
        // Property (SEC/SN) applies across EVERY business line at once.
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

            _vm.VirtualGamesVM.Property = prop;
            _vm.SportsBookVM.Property = prop;
            _vm.FUNaloMAXVM.Property = prop;
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