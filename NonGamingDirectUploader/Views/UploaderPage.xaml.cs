using NonGamingDirectUploader.Helpers;
using NonGamingDirectUploader.ViewModels;
using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

            // Default Start/End to the current month, matching the VM's own defaults.
            var today = DateTime.Today;
            StartDatePicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
            EndDatePicker.SelectedDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
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

            // Fixed set of preview columns for this table (DTE, etc.)
            BuildPreviewColumns();

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

            // Push the initial Start/End picker values into the VM once it's attached.
            if (StartDatePicker.SelectedDate.HasValue) vm.MonthStart = StartDatePicker.SelectedDate.Value;
            if (EndDatePicker.SelectedDate.HasValue) vm.MonthEnd = EndDatePicker.SelectedDate.Value;
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

        // ── DATE / RANGE ──────────────────────────────────────────────────────
        private void DailyDate_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_vm != null && DailyDatePicker.SelectedDate.HasValue)
                _vm.SelectedDate = DailyDatePicker.SelectedDate.Value;
        }

        // Start/End date pickers drive the monthly query directly —
        // no Month dropdown in between.
        private void StartDate_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_vm != null && StartDatePicker.SelectedDate.HasValue)
                _vm.MonthStart = StartDatePicker.SelectedDate.Value;
        }

        private void EndDate_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_vm != null && EndDatePicker.SelectedDate.HasValue)
                _vm.MonthEnd = EndDatePicker.SelectedDate.Value;
        }

        // ── ACTIONS ───────────────────────────────────────────────────────────
        private async void GetData_Click(object sender, RoutedEventArgs e)
        {
            if (_vm != null) await _vm.FetchDataAsync();
        }

        /// <summary>
        /// Bulk upload flow: create a per-table .xlsx template (a real Excel
        /// workbook, opens in Excel via the default file association),
        /// PRE-FILLED with whatever is currently loaded in the Data Preview
        /// grid (vm.PreviewData) instead of a blank sheet, let the user
        /// review/edit / save / close, read it back with
        /// ExcelTemplateService, show it in a review window, then hand it to
        /// the normal upload pipeline (which does the key-scoped overwrite
        /// check).
        /// </summary>
        private async void BulkUpload_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;

            string templatePath;
            try
            {
                // Pass the grid's current data so the workbook opens already
                // populated with what's on screen, instead of just headers.
                templatePath = BulkTemplateService.CreateTemplate(_vm.UploaderType, _vm.PreviewColumns, _vm.PreviewData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not create the upload template: {ex.Message}",
                    _vm.DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(templatePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open the template automatically: {ex.Message}\n\nYou can open it manually from:\n{templatePath}",
                    _vm.DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var hasExistingRows = _vm.PreviewData != null && _vm.PreviewData.Rows.Count > 0;
            MessageBox.Show(
                hasExistingRows
                    ? $"The {_vm.DisplayTitle} upload file has opened in Excel, pre-filled with the {_vm.PreviewData!.Rows.Count} record(s) currently shown in the Data Preview grid.\n\n" +
                      "1. Review or edit the data as needed (do not change the header row).\n" +
                      "2. Save the file (Ctrl+S) and close Excel.\n" +
                      "3. Click OK below to load the data back into the app."
                    : $"A blank {_vm.DisplayTitle} upload template has opened in Excel (no data was currently loaded in the grid).\n\n" +
                      "1. Paste your data under the header row (do not change the header row).\n" +
                      "2. Save the file (Ctrl+S) and close Excel.\n" +
                      "3. Click OK below to load the data back into the app.",
                _vm.DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Information);

            DataTable dt;
            try
            {
                dt = ExcelTemplateService.ReadXlsxFile(templatePath, _vm.PreviewColumns);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not read the template file: {ex.Message}",
                    _vm.DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (dt.Rows.Count == 0)
            {
                MessageBox.Show("No data rows were found in the template.",
                    _vm.DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                TryDeleteFile(templatePath);
                return;
            }

            var preview = new BulkUploadPreviewWindow(dt, _vm.DisplayTitle)
            {
                Owner = Window.GetWindow(this)
            };

            if (preview.ShowDialog() == true && preview.Confirmed)
                await _vm.UploadAsync(preview.Data);

            TryDeleteFile(templatePath);
        }

        private static void TryDeleteFile(string path)
        {
            try { File.Delete(path); } catch { /* best-effort cleanup */ }
        }

        private async void Upload_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;

            // Uses whatever is currently loaded via "Get Data" or in-grid edits.
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

        // ── PREVIEW COLUMNS ──────────────────────────────────────────────────
        /// <summary>
        /// Builds the fixed, ordered set of columns for this uploader's Data
        /// Preview grid from vm.PreviewColumns, with the Actions column pinned
        /// first (frozen) and DTE formatted as date-only.
        /// </summary>
        private void BuildPreviewColumns()
        {
            if (_vm == null) return;
            PreviewGrid.Columns.Clear();

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

            foreach (var (field, header, _) in _vm.PreviewColumns)
            {
                var binding = new Binding($"[{field}]");
                if (string.Equals(field, "DTE", StringComparison.OrdinalIgnoreCase))
                    binding.StringFormat = "MM/dd/yyyy";

                PreviewGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = header,
                    Binding = binding,
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                });
            }
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