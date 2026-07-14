using NonGamingDirectUploader.ViewModels;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NonGamingDirectUploader.Views
{
    public partial class UploaderPage : UserControl
    {
        private UploaderViewModel? _vm;
        private bool _isDaily = true;

        public UploaderPage()
        {
            InitializeComponent();
            DailyDatePicker.SelectedDate = DateTime.Today;
            MonthCombo.SelectedIndex = DateTime.Today.Month - 1;
        }

        public void SetViewModel(UploaderViewModel vm)
        {
            _vm = vm;
            TitleBlock.Text = $"{vm.DisplayTitle} Uploader";
            SubtitleBlock.Text = $"Upload daily or monthly data — Table: {vm.DisplayTitle switch
            {
                "Others" => "Curr_Others",
                "F&B" => "Curr_FnB",
                "Hotel" => "Curr_Hotel",
                "Visitation" => "Curr_Visitation",
                _ => "?"
            }}";

            // Bind accent color
            var color = (Color)ColorConverter.ConvertFromString(vm.AccentHex);
            AccentBar.Background = new SolidColorBrush(color);

            // Bind log
            LogList.ItemsSource = vm.LogLines;

            // Initial database display (auto-resolved from module + property)
            UpdateDbPathDisplay();

            // VM property-change → UI refresh
            vm.PropertyChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    switch (e.PropertyName)
                    {
                        case nameof(UploaderViewModel.StatusMessage):
                            StatusText.Text = vm.StatusMessage;
                            StatusText.Foreground = new SolidColorBrush(
                                (Color)ColorConverter.ConvertFromString(vm.StatusColor));
                            break;
                        case nameof(UploaderViewModel.IsConnected):
                            UpdateConnectionBadge(vm.IsConnected);
                            break;
                        case nameof(UploaderViewModel.IsBusy):
                            BusyOverlay.Visibility = vm.IsBusy ? Visibility.Visible : Visibility.Collapsed;
                            break;
                        case nameof(UploaderViewModel.PreviewData):
                            RefreshGrid(vm.PreviewData);
                            break;
                        case nameof(UploaderViewModel.TotalRows):
                            RowCountText.Text = $"{vm.TotalRows:N0} records";
                            RecordCountBadge.Text = $"{vm.TotalRows:N0} records";
                            break;
                        case nameof(UploaderViewModel.Progress):
                            UpdateProgress(vm.Progress);
                            break;
                        case nameof(UploaderViewModel.DbPathDisplay):
                            UpdateDbPathDisplay();
                            break;
                    }
                });
            };
        }

        private void UpdateDbPathDisplay()
        {
            if (_vm == null) return;
            DbPathText.Text = _vm.DbPathDisplay;
            DbPathText.Foreground = _vm.HasDb
                ? (SolidColorBrush)FindResource("TextSecondaryBrush")
                : (SolidColorBrush)FindResource("AccentRedBrush");
        }

        // ── DB ────────────────────────────────────────────────────────────────
        private async void TestConn_Click(object sender, RoutedEventArgs e)
        {
            if (_vm != null) await _vm.TestConnectionAsync();
        }

        // ── MODE TABS ─────────────────────────────────────────────────────────
        private void DailyTab_Click(object sender, MouseButtonEventArgs e)
        {
            _isDaily = true;
            if (_vm != null) _vm.Mode = Models.UploadMode.Daily;
            DailySection.Visibility = Visibility.Visible;
            MonthlySection.Visibility = Visibility.Collapsed;
            DailyTab.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_vm?.AccentHex ?? "#FF2F81F7"));
            MonthlyTab.Background = (SolidColorBrush)FindResource("BgElevatedBrush");
            ((TextBlock)DailyTab.Child).Foreground = Brushes.White;
            ((TextBlock)MonthlyTab.Child).Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush");
        }

        private void MonthlyTab_Click(object sender, MouseButtonEventArgs e)
        {
            _isDaily = false;
            if (_vm != null) _vm.Mode = Models.UploadMode.Monthly;
            DailySection.Visibility = Visibility.Collapsed;
            MonthlySection.Visibility = Visibility.Visible;
            MonthlyTab.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_vm?.AccentHex ?? "#FF2F81F7"));
            DailyTab.Background = (SolidColorBrush)FindResource("BgElevatedBrush");
            ((TextBlock)MonthlyTab.Child).Foreground = Brushes.White;
            ((TextBlock)DailyTab.Child).Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush");
        }

        // ── DATE / MONTH ──────────────────────────────────────────────────────
        private void DailyDate_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_vm != null && DailyDatePicker.SelectedDate.HasValue)
                _vm.SelectedDate = DailyDatePicker.SelectedDate.Value;
        }

        private void MonthCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || MonthCombo.SelectedItem is not ComboBoxItem item) return;
            int month = int.Parse(item.Tag?.ToString() ?? "1");
            int year = DateTime.Today.Year;
            _vm.MonthStart = new DateTime(year, month, 1);
            _vm.MonthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            _vm.SelectedMonth = item.Content?.ToString() ?? "";
            StartDatePicker.SelectedDate = _vm.MonthStart;
            EndDatePicker.SelectedDate = _vm.MonthEnd;
        }

        // ── ACTIONS ───────────────────────────────────────────────────────────
        private async void GetData_Click(object sender, RoutedEventArgs e)
        {
            if (_vm != null) await _vm.FetchDataAsync();
        }

        private async void Upload_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;

            // Use the currently loaded PreviewData as the upload payload.
            // In a real scenario you would load the DataTable from a staging
            // sheet / file; here we use whatever was fetched for demo purposes.
            if (_vm.PreviewData == null || _vm.PreviewData.Rows.Count == 0)
            {
                MessageBox.Show(
                    "No data to upload. Please load your data sheet first.",
                    _vm.DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            await _vm.UploadAsync(_vm.PreviewData);
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
            => _vm?.ClearLog();

        // ── ROW EDIT / DELETE ─────────────────────────────────────────────────
        private async void EditRow_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            if (sender is not Button btn || btn.Tag is not DataRowView rowView) return;

            var dialog = new EditRecordWindow(rowView.Row)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && dialog.Saved)
                await _vm.SaveRowEditAsync(rowView.Row);
        }

        private async void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            if (sender is not Button btn || btn.Tag is not DataRowView rowView) return;

            await _vm.DeleteRowAsync(rowView.Row);
        }

        // Appends a non-bound "Actions" column (Edit / Delete) after the
        // auto-generated data columns whenever a new preview table is bound.
        private void PreviewGrid_AutoGeneratedColumns(object? sender, EventArgs e)
        {
            if (PreviewGrid.Columns.Count == 0) return;

            // Remove any stale actions column from a previous bind
            for (int i = PreviewGrid.Columns.Count - 1; i >= 0; i--)
            {
                if (PreviewGrid.Columns[i] is DataGridTemplateColumn tc &&
                    (string)tc.Header == "Actions")
                {
                    PreviewGrid.Columns.RemoveAt(i);
                }
            }

            var actionsColumn = new DataGridTemplateColumn
            {
                Header = "Actions",
                CellTemplate = (DataTemplate)FindResource("RowActionsTemplate"),
                Width = new DataGridLength(140),
                CanUserResize = false,
                CanUserSort = false,
                CanUserReorder = false
            };
            PreviewGrid.Columns.Add(actionsColumn);
        }

        // ── HELPERS ───────────────────────────────────────────────────────────
        private void RefreshGrid(DataTable? dt)
        {
            if (dt == null || dt.Rows.Count == 0)
            {
                PreviewGrid.Visibility = Visibility.Collapsed;
                EmptyState.Visibility = Visibility.Visible;
                PreviewGrid.ItemsSource = null;
            }
            else
            {
                PreviewGrid.Visibility = Visibility.Visible;
                EmptyState.Visibility = Visibility.Collapsed;
                PreviewGrid.ItemsSource = dt.DefaultView;
            }
        }

        private void UpdateConnectionBadge(bool connected)
        {
            StatusDot.Fill = connected
                ? new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50))
                : (SolidColorBrush)FindResource("TextMutedBrush");
            StatusBadgeText.Text = connected ? "Connected" : "Not Connected";
            StatusBadgeText.Foreground = connected
                ? new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50))
                : (SolidColorBrush)FindResource("TextSecondaryBrush");
        }

        private void UpdateProgress(double pct)
        {
            // Animate progress bar width proportionally
            double maxWidth = ProgressFill.ActualWidth > 0
                ? ((FrameworkElement)ProgressFill.Parent).ActualWidth
                : 260;
            ProgressFill.Width = maxWidth * pct / 100.0;
        }
    }
}
