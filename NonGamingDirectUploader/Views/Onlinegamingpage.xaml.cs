using NonGamingDirectUploader.ViewModels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NonGamingDirectUploader.Views
{
    /// <summary>
    /// Combined page for all three Online Gaming modules (Table Games /
    /// "Virtual Games", SportsBook, FUNaloMAX). Replaces the generic
    /// UploaderPage + its editable Data Preview grid for these three modules
    /// with a per-module Data Viewer (read-only) and Uploader (fixed
    /// category boxes, Wager/Win only) — see MainWindow, which now routes
    /// all three Online Gaming nav buttons here and just switches which
    /// internal tab is selected (SelectModuleTab).
    /// </summary>
    public partial class OnlineGamingPage : UserControl
    {
        private VirtualGamesViewModel? _virtualGamesVm;
        private SportsBookViewModel? _sportsBookVm;
        private FUNaloMAXViewModel? _funaloVm;

        public OnlineGamingPage()
        {
            InitializeComponent();
        }

        /// <summary>Wires each module's ViewModel to its Data Viewer + Uploader panels and kicks off an initial refresh.</summary>
        public void SetViewModels(VirtualGamesViewModel virtualGamesVm, SportsBookViewModel sportsBookVm, FUNaloMAXViewModel funaloVm)
        {
            _virtualGamesVm = virtualGamesVm;
            _sportsBookVm = sportsBookVm;
            _funaloVm = funaloVm;

            VirtualGamesViewerPanel.DataContext = virtualGamesVm;
            VirtualGamesUploaderPanel.DataContext = virtualGamesVm;

            SportsBookViewerPanel.DataContext = sportsBookVm;
            SportsBookUploaderPanel.DataContext = sportsBookVm;

            FunaloViewerPanel.DataContext = funaloVm;
            FunaloUploaderPanel.DataContext = funaloVm;

            _ = RefreshAllSequentiallyAsync();
        }

        /// <summary>
        /// Refreshes all three Data Viewers ONE AT A TIME, never concurrently.
        /// Virtual Games, SportsBook and FUNaloMAX all point at the SAME
        /// Access database file (OtherGaming_DB.accdb — see DatabaseConfig).
        /// Firing all three refreshes unawaited ("_ = ...Async()") opened
        /// three simultaneous OLE DB connections to that one file at once,
        /// which is what was causing
        /// "SEHException: External component has thrown an exception" on
        /// cn.Open() — the ACE OLEDB/Jet engine does not reliably handle
        /// concurrent connection opens to the same .accdb. Awaiting them in
        /// sequence instead fixes it.
        /// </summary>
        private async Task RefreshAllSequentiallyAsync()
        {
            if (_virtualGamesVm != null) await _virtualGamesVm.RefreshViewerAsync();
            if (_sportsBookVm != null) await _sportsBookVm.RefreshViewerAsync();
            if (_funaloVm != null) await _funaloVm.RefreshViewerAsync();
        }

        /// <summary>Switches both the Data Viewer and Uploader TabControls to the module matching tag ("VirtualGames"/"SportsBook"/"FUNaloMAX").</summary>
        public void SelectModuleTab(string tag)
        {
            var index = tag switch
            {
                "SportsBook" => 1,
                "FUNaloMAX" => 2,
                _ => 0
            };
            DataViewerTabs.SelectedIndex = index;
            UploaderTabs.SelectedIndex = index;
        }

        // ── Data Viewer refresh buttons ──────────────────────────────────────
        private async void RefreshVirtualGamesViewer_Click(object sender, RoutedEventArgs e)
        {
            if (_virtualGamesVm != null) await _virtualGamesVm.RefreshViewerAsync();
        }

        private async void RefreshSportsBookViewer_Click(object sender, RoutedEventArgs e)
        {
            if (_sportsBookVm != null) await _sportsBookVm.RefreshViewerAsync();
        }

        private async void RefreshFunaloViewer_Click(object sender, RoutedEventArgs e)
        {
            if (_funaloVm != null) await _funaloVm.RefreshViewerAsync();
        }

        // ── Per-category Submit buttons ──────────────────────────────────────
        private async void SubmitVirtualGamesCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_virtualGamesVm != null && sender is Button btn && btn.Tag is CategoryEntryViewModel entry)
                await _virtualGamesVm.SubmitCategoryAsync(entry);
        }

        private async void SubmitSportsBookCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_sportsBookVm != null && sender is Button btn && btn.Tag is CategoryEntryViewModel entry)
                await _sportsBookVm.SubmitCategoryAsync(entry);
        }

        private async void SubmitFunaloCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_funaloVm != null && sender is Button btn && btn.Tag is CategoryEntryViewModel entry)
                await _funaloVm.SubmitCategoryAsync(entry);
        }

        // ── Submit All ────────────────────────────────────────────────────────
        private async void SubmitAllVirtualGames_Click(object sender, RoutedEventArgs e)
        {
            if (_virtualGamesVm != null) await _virtualGamesVm.SubmitAllAsync();
        }

        private async void SubmitAllSportsBook_Click(object sender, RoutedEventArgs e)
        {
            if (_sportsBookVm != null) await _sportsBookVm.SubmitAllAsync();
        }

        private async void SubmitAllFunalo_Click(object sender, RoutedEventArgs e)
        {
            if (_funaloVm != null) await _funaloVm.SubmitAllAsync();
        }
    }
}