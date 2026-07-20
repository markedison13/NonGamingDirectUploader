using NonGamingDirectUploader.Helpers;
using NonGamingDirectUploader.Models;
using NonGamingDirectUploader.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NonGamingDirectUploader.ViewModels
{
    /// <summary>
    /// Shared base for all four uploader panels.
    /// Mirrors the VBA pattern: pick property → get/upload against the module's
    /// pre-assigned database → edit/delete individual records in-place.
    /// </summary>
    public abstract class UploaderViewModel : BaseViewModel
    {
        // ── Abstract ──────────────────────────────────────────────────────────
        public abstract UploaderType UploaderType { get; }
        public abstract string DisplayTitle { get; }
        public abstract string AccentHex { get; }

        /// <summary>
        /// The columns shown in the Data Preview grid (and used as the header
        /// row of the bulk-upload template), in display order.
        /// Field = actual DB/DataTable column name. Header = friendly label.
        /// </summary>
        public abstract (string Field, string Header)[] PreviewColumns { get; }

        // ── Database (resolved from backend config, not user-selected) ─────────
        /// <summary>
        /// The database designated for this module + property combination.
        /// Every (module, property) pair has a fixed, pre-assigned database —
        /// there is no manual browse/select step.
        /// </summary>
        public string ResolvedDbPath => DatabaseConfig.GetPathOrEmpty(UploaderType, Property);

        public string DbPathDisplay => string.IsNullOrEmpty(ResolvedDbPath)
            ? "No database configured for this module/property."
            : ResolvedDbPath;

        public bool HasDb => !string.IsNullOrEmpty(ResolvedDbPath);

        // ── Property ──────────────────────────────────────────────────────────
        private PropertyType _property = PropertyType.SEC;
        public PropertyType Property
        {
            get => _property;
            set
            {
                if (Set(ref _property, value))
                {
                    OnPropertyChanged(nameof(ResolvedDbPath));
                    OnPropertyChanged(nameof(DbPathDisplay));
                    OnPropertyChanged(nameof(HasDb));
                    IsConnected = false;
                    PreviewData = null;
                    TotalRows = 0;
                    _ = TestConnectionAsync();
                }
            }
        }

        // ── Mode ──────────────────────────────────────────────────────────────
        private UploadMode _mode = UploadMode.Daily;
        public UploadMode Mode
        {
            get => _mode;
            set { Set(ref _mode, value); OnPropertyChanged(nameof(IsDailyMode)); OnPropertyChanged(nameof(IsMonthlyMode)); }
        }
        public bool IsDailyMode => Mode == UploadMode.Daily;
        public bool IsMonthlyMode => Mode == UploadMode.Monthly;

        // ── Dates ─────────────────────────────────────────────────────────────
        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set => Set(ref _selectedDate, value);
        }

        private DateTime _monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        public DateTime MonthStart
        {
            get => _monthStart;
            set => Set(ref _monthStart, value);
        }

        private DateTime _monthEnd = new DateTime(DateTime.Today.Year, DateTime.Today.Month,
            DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month));
        public DateTime MonthEnd
        {
            get => _monthEnd;
            set => Set(ref _monthEnd, value);
        }

        private string _selectedMonth = DateTime.Today.ToString("MMMM");
        public string SelectedMonth
        {
            get => _selectedMonth;
            set => Set(ref _selectedMonth, value);
        }

        // ── Status / Log ──────────────────────────────────────────────────────
        private string _statusMessage = "Ready.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }

        private string _statusColor = "#FF8B949E";
        public string StatusColor
        {
            get => _statusColor;
            set => Set(ref _statusColor, value);
        }

        private ObservableCollection<string> _logLines = new();
        public ObservableCollection<string> LogLines
        {
            get => _logLines;
            set => Set(ref _logLines, value);
        }

        // ── Preview Data ──────────────────────────────────────────────────────
        private DataTable? _previewData;
        public DataTable? PreviewData
        {
            get => _previewData;
            set => Set(ref _previewData, value);
        }

        private int _totalRows;
        public int TotalRows
        {
            get => _totalRows;
            set => Set(ref _totalRows, value);
        }

        // ── Busy ──────────────────────────────────────────────────────────────
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { Set(ref _isBusy, value); OnPropertyChanged(nameof(IsNotBusy)); }
        }
        public bool IsNotBusy => !_isBusy;

        private double _progress;
        public double Progress
        {
            get => _progress;
            set => Set(ref _progress, value);
        }

        // ── DB Connected ──────────────────────────────────────────────────────
        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set => Set(ref _isConnected, value);
        }

        // ── Commands (called from code-behind) ────────────────────────────────
        public async Task TestConnectionAsync()
        {
            if (!HasDb)
            {
                IsConnected = false;
                SetStatus("No database configured for this module/property.", "#FFFF4444");
                return;
            }
            SetStatus("Testing connection…", "#FFFF8C00");
            IsBusy = true;
            try
            {
                IsConnected = await DatabaseService.TestConnectionAsync(ResolvedDbPath);
                if (IsConnected)
                    SetStatus("✓ Connected to database.", "#FF3FB950");
                else
                    SetStatus("✗ Cannot connect. Check the configured database path/driver.", "#FFFF4444");
                Log(IsConnected ? "Connection successful." : "Connection failed.");
            }
            finally { IsBusy = false; }
        }

        /// <summary>
        /// Fetch existing data from the DB for preview (mirrors VBA GetDataFromADO).
        /// </summary>
        public async Task FetchDataAsync()
        {
            if (!CheckDb()) return;
            IsBusy = true;
            SetStatus("Fetching data from database…", "#FFFF8C00");
            try
            {
                DataTable dt;
                if (IsDailyMode)
                    dt = await DatabaseService.FetchDailyAsync(ResolvedDbPath, UploaderType, SelectedDate);
                else
                    dt = await DatabaseService.FetchMonthlyAsync(ResolvedDbPath, UploaderType, MonthStart, MonthEnd);

                PreviewData = dt;
                TotalRows = dt.Rows.Count;
                Log($"Fetched {TotalRows} row(s) from {TableName}.");
                SetStatus($"✓ {TotalRows} record(s) loaded.", "#FF3FB950");
            }
            catch (Exception ex)
            {
                SetStatus($"✗ Fetch failed: {ex.Message}", "#FFFF4444");
                Log($"ERROR: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        /// <summary>
        /// Main upload flow. Always inspects the DTE column of the data being
        /// uploaded (whether it came from Get Data, an in-grid edit, or the
        /// bulk-upload Excel template) and — for every distinct date already
        /// present in the target table — prompts once to confirm overwrite
        /// before deleting and re-inserting.
        /// </summary>
        public async Task UploadAsync(DataTable uploadData)
        {
            if (!CheckDb()) return;
            if (uploadData == null || uploadData.Rows.Count == 0)
            {
                SetStatus("No data to upload.", "#FFFF8C00"); return;
            }

            if (!uploadData.Columns.Contains("DTE"))
            {
                MessageBox.Show(
                    "Upload data must include a DTE (date) column.",
                    DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dates = ExtractDistinctDates(uploadData);
            if (dates.Count == 0)
            {
                MessageBox.Show(
                    "No valid DTE (date) values were found in the data to upload.",
                    DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            Progress = 0;
            try
            {
                SetStatus("Checking for existing records…", "#FFFF8C00");
                var existingDates = new List<DateTime>();
                foreach (var d in dates)
                {
                    if (await DatabaseService.DateExistsAsync(ResolvedDbPath, UploaderType, d))
                        existingDates.Add(d);
                }

                string prompt;
                string caption;
                MessageBoxImage icon;
                if (existingDates.Count > 0)
                {
                    var dateList = string.Join(", ", existingDates.OrderBy(d => d).Select(d => d.ToString("MM/dd/yyyy")));
                    prompt = $"{DisplayTitle} Uploader: data already exists for {existingDates.Count} date(s):\n{dateList}\n\n" +
                             "Do you want to overwrite the existing data for these dates?";
                    caption = "Overwrite Existing Data";
                    icon = MessageBoxImage.Warning;
                }
                else
                {
                    var dateList = string.Join(", ", dates.OrderBy(d => d).Select(d => d.ToString("MM/dd/yyyy")));
                    prompt = $"{DisplayTitle} Uploader: are you sure you want to upload data for {dateList}?";
                    caption = "Upload";
                    icon = MessageBoxImage.Question;
                }

                var result = MessageBox.Show(prompt, caption, MessageBoxButton.OKCancel, icon);
                if (result != MessageBoxResult.OK) { SetStatus("Upload cancelled.", "#FF8B949E"); return; }

                if (existingDates.Count > 0)
                {
                    SetStatus("Deleting existing records…", "#FFFF8C00");
                    foreach (var d in existingDates)
                    {
                        await DatabaseService.DeleteDailyAsync(ResolvedDbPath, UploaderType, d);
                        Log($"Deleted existing data for {d:MM/dd/yyyy}.");
                    }
                }

                SetStatus("Uploading records…", "#FFFF8C00");
                int uploaded = await DatabaseService.UploadRowsAsync(ResolvedDbPath, UploaderType, uploadData);
                Progress = 100;
                TotalRows = uploaded;
                Log($"✓ Upload complete — {uploaded} row(s) inserted.");
                SetStatus($"✓ Done! {uploaded} record(s) uploaded.", "#FF3FB950");
                MessageBox.Show("Done uploading the file.", DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetStatus($"✗ Upload failed: {ex.Message}", "#FFFF4444");
                Log($"ERROR: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        /// <summary>Extracts the distinct DTE dates (date part only) present in a DataTable.</summary>
        private static List<DateTime> ExtractDistinctDates(DataTable data)
        {
            var dates = new List<DateTime>();
            foreach (DataRow row in data.Rows)
            {
                var raw = row["DTE"];
                if (raw == null || raw == DBNull.Value) continue;

                DateTime d;
                if (raw is DateTime dt) d = dt.Date;
                else if (!DateTime.TryParse(raw.ToString(), out d)) continue;
                else d = d.Date;

                if (!dates.Contains(d)) dates.Add(d);
            }
            return dates;
        }

        /// <summary>Persists an in-place edit of a single preview row back to the database.</summary>
        public async Task SaveRowEditAsync(DataRow row)
        {
            if (!CheckDb()) return;
            if (row.RowState != DataRowState.Modified)
            {
                Log("No changes to save for this row.");
                return;
            }

            IsBusy = true;
            SetStatus("Saving row changes…", "#FFFF8C00");
            try
            {
                await DatabaseService.UpdateRowAsync(ResolvedDbPath, UploaderType, row);
                row.AcceptChanges();
                Log("✓ Row updated.");
                SetStatus("✓ Row updated.", "#FF3FB950");
            }
            catch (Exception ex)
            {
                SetStatus($"✗ Update failed: {ex.Message}", "#FFFF4444");
                Log($"ERROR: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        /// <summary>Deletes a single preview row from the database and removes it from the grid.</summary>
        public async Task DeleteRowAsync(DataRow row)
        {
            if (!CheckDb()) return;

            var result = MessageBox.Show(
                "Delete this record from the database? This cannot be undone.",
                DisplayTitle, MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result != MessageBoxResult.OK) return;

            IsBusy = true;
            SetStatus("Deleting row…", "#FFFF8C00");
            try
            {
                await DatabaseService.DeleteRowAsync(ResolvedDbPath, UploaderType, row);
                row.Table?.Rows.Remove(row);
                TotalRows = PreviewData?.Rows.Count ?? 0;
                Log("✓ Row deleted.");
                SetStatus("✓ Row deleted.", "#FF3FB950");
            }
            catch (Exception ex)
            {
                SetStatus($"✗ Delete failed: {ex.Message}", "#FFFF4444");
                Log($"ERROR: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        protected string TableName => UploaderType switch
        {
            UploaderType.Others => "Curr_Others",
            UploaderType.FnB => "Curr_FnB",
            UploaderType.Hotel => "Curr_Hotel",
            UploaderType.Visitation => "Curr_Visitation",
            _ => ""
        };

        private bool CheckDb()
        {
            if (!HasDb)
            {
                SetStatus("No database configured for this module/property.", "#FFFF4444");
                MessageBox.Show(
                    $"No database is configured for {DisplayTitle} / {Property} property. Please contact your administrator.",
                    DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void SetStatus(string msg, string color)
        {
            StatusMessage = msg;
            StatusColor = color;
        }

        protected void Log(string msg)
            => LogLines.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");

        public void ClearLog() => LogLines.Clear();
    }

    // ── Concrete ViewModels ───────────────────────────────────────────────────
    public class OthersViewModel : UploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.Others;
        public override string DisplayTitle => "Others";
        public override string AccentHex => "#FF2F81F7";

        public override (string Field, string Header)[] PreviewColumns => new[]
        {
            ("DTE",       "DTE"),
            ("Dept_Type", "Dept Type"),
            ("Dept_Desc", "Dept Desc"),
            ("Revenue",   "Revenue"),
            ("Comp",      "Comp"),
        };
    }

    public class FnBViewModel : UploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.FnB;
        public override string DisplayTitle => "F&B";
        public override string AccentHex => "#FF3FB950";

        public override (string Field, string Header)[] PreviewColumns => new[]
        {
            ("DTE",         "DTE"),
            ("Rev_Center",  "Rev Center"),
            ("Net_Sales",   "Net Sales"),
            ("Covers",      "Covers"),
            ("Comp_Rev",    "Comp Rev"),
            ("Comp_Covers", "Comp Covers"),
        };
    }

    public class HotelViewModel : UploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.Hotel;
        public override string DisplayTitle => "Hotel";
        public override string AccentHex => "#FFFF8C00";

        public override (string Field, string Header)[] PreviewColumns => new[]
        {
            ("DTE",              "DTE"),
            ("Area_Type",        "Area Type"),
            ("Description_Type", "Description Type"),
            ("Total_Revenue",    "Total Revenue"),
            ("Comp_Revenue",     "Comp Revenue"),
            ("Occupied_Room",    "Occupied Room"),
        };
    }

    public class VisitationViewModel : UploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.Visitation;
        public override string DisplayTitle => "Visitation";
        public override string AccentHex => "#FF8957E5";

        public override (string Field, string Header)[] PreviewColumns => new[]
        {
            ("DTE",        "DTE"),
            ("Visitation", "Visitation"),
            ("SRC",        "SRC"),
        };
    }
}