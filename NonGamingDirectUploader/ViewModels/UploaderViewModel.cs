using Microsoft.Win32;
using NonGamingDirectUploader.Helpers;
using NonGamingDirectUploader.Models;
using NonGamingDirectUploader.ViewModels;
using NonGamingDirectUploader.Helpers;
using NonGamingDirectUploader.Models;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace NonGamingDirectUploader.ViewModels
{
    /// <summary>
    /// Shared base for all four uploader panels.
    /// Mirrors the VBA pattern: browse DB → set date → get/upload.
    /// </summary>
    public abstract class UploaderViewModel : BaseViewModel
    {
        // ── Abstract ──────────────────────────────────────────────────────────
        public abstract UploaderType UploaderType { get; }
        public abstract string DisplayTitle { get; }
        public abstract string AccentHex { get; }

        // ── Database Path ─────────────────────────────────────────────────────
        private string _dbPath = "";
        public string DbPath
        {
            get => _dbPath;
            set { Set(ref _dbPath, value); OnPropertyChanged(nameof(DbPathDisplay)); }
        }
        public string DbPathDisplay => string.IsNullOrEmpty(_dbPath) ? "No database selected…" : _dbPath;

        // ── Property ──────────────────────────────────────────────────────────
        private PropertyType _property = PropertyType.SEC;
        public PropertyType Property
        {
            get => _property;
            set => Set(ref _property, value);
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
        public void BrowseDatabase()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Access Database (.accdb / .mdb)",
                Filter = "Access Database|*.accdb;*.mdb|All Files|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog() == true)
            {
                DbPath = dlg.FileName;
                Log($"Database path set: {DbPath}");
                _ = TestConnectionAsync();
            }
        }

        public async Task TestConnectionAsync()
        {
            if (string.IsNullOrEmpty(DbPath)) return;
            SetStatus("Testing connection…", "#FFFF8C00");
            IsBusy = true;
            try
            {
                IsConnected = await DatabaseService.TestConnectionAsync(DbPath);
                if (IsConnected)
                    SetStatus("✓ Connected to database.", "#FF3FB950");
                else
                    SetStatus("✗ Cannot connect. Check path or driver.", "#FFFF4444");
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
                    dt = await DatabaseService.FetchDailyAsync(DbPath, UploaderType, SelectedDate);
                else
                    dt = await DatabaseService.FetchMonthlyAsync(DbPath, UploaderType, MonthStart, MonthEnd);

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
        /// Main upload flow — mirrors VBA CheckExist → deleteData → toUpload.
        /// </summary>
        public async Task UploadAsync(DataTable uploadData)
        {
            if (!CheckDb()) return;
            if (uploadData == null || uploadData.Rows.Count == 0)
            {
                SetStatus("No data to upload.", "#FFFF8C00"); return;
            }

            IsBusy = true;
            Progress = 0;
            try
            {
                if (IsDailyMode)
                {
                    SetStatus("Checking for existing records…", "#FFFF8C00");
                    bool exists = await DatabaseService.DateExistsAsync(DbPath, UploaderType, SelectedDate);

                    if (exists)
                    {
                        var result = MessageBox.Show(
                            $"{DisplayTitle} Uploader: Data for {SelectedDate:MM/dd/yyyy} already exists. Do you want to update?",
                            "Update", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                        if (result != MessageBoxResult.OK) { SetStatus("Upload cancelled.", "#FF8B949E"); return; }

                        SetStatus("Deleting existing records…", "#FFFF8C00");
                        await DatabaseService.DeleteDailyAsync(DbPath, UploaderType, SelectedDate);
                        Log($"Deleted existing data for {SelectedDate:MM/dd/yyyy}.");
                    }
                    else
                    {
                        var result = MessageBox.Show(
                            $"{DisplayTitle} Uploader: Are you sure you want to upload data for {SelectedDate:MM/dd/yyyy}?",
                            "Upload", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                        if (result != MessageBoxResult.OK) { SetStatus("Upload cancelled.", "#FF8B949E"); return; }
                    }
                }
                else
                {
                    var result = MessageBox.Show(
                        $"Are you sure you want to upload {DisplayTitle} data for {SelectedMonth}?",
                        "Monthly Upload", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                    if (result != MessageBoxResult.OK) { SetStatus("Upload cancelled.", "#FF8B949E"); return; }

                    SetStatus("Deleting monthly records…", "#FFFF8C00");
                    await DatabaseService.DeleteMonthlyAsync(DbPath, UploaderType, MonthStart, MonthEnd);
                    Log($"Deleted monthly data for {MonthStart:MM/dd/yyyy} – {MonthEnd:MM/dd/yyyy}.");
                }

                SetStatus("Uploading records…", "#FFFF8C00");
                int uploaded = await DatabaseService.UploadRowsAsync(DbPath, UploaderType, uploadData);
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
            if (string.IsNullOrEmpty(DbPath))
            {
                SetStatus("Please select a database first.", "#FFFF4444");
                MessageBox.Show("Please select a database path first.", DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
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
    }

    public class FnBViewModel : UploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.FnB;
        public override string DisplayTitle => "F&B";
        public override string AccentHex => "#FF3FB950";
    }

    public class HotelViewModel : UploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.Hotel;
        public override string DisplayTitle => "Hotel";
        public override string AccentHex => "#FFFF8C00";
    }

    public class VisitationViewModel : UploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.Visitation;
        public override string DisplayTitle => "Visitation";
        public override string AccentHex => "#FF8957E5";
    }
}
