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
    /// Shared base for every uploader panel — NonGaming (Others/F&amp;B/Hotel/
    /// Visitation), Gaming (Mass/VIP/Junket), and Online Gaming (VirtualGames/
    /// SportsBook/FUNaloMAX) alike. Mirrors the VBA pattern: pick property →
    /// get/upload against the module's pre-assigned database → edit/delete
    /// individual records in-place.
    /// </summary>
    public abstract class UploaderViewModel : BaseViewModel
    {
        // ── Abstract ──────────────────────────────────────────────────────────
        public abstract UploaderType UploaderType { get; }
        public abstract string DisplayTitle { get; }
        public abstract string AccentHex { get; }

        /// <summary>
        /// Whether this module offers the Daily/Monthly mode toggle at all.
        /// Every module — including Online Gaming (VirtualGames/SportsBook/
        /// FUNaloMAX) — supports both Daily and Monthly fetch/upload, so this
        /// always defaults to true. (Previously the three Online Gaming
        /// modules overrode this to false, which silently forced Mode back to
        /// Daily any time the UI tried to switch to Monthly — even though the
        /// Monthly tab/date pickers stayed visible and clickable. That
        /// mismatch was why a Sept 1–30 monthly fetch only ever returned 1
        /// record: it was actually running a Daily fetch against whatever
        /// date sat in the Daily picker. Removing the override lets Monthly
        /// mode actually reach FetchMonthlyAsync for these modules too.)
        /// </summary>
        public virtual bool SupportsMonthlyMode => true;

        /// <summary>
        /// Whether this module shows the "⬆ Upload to Database" button on its
        /// UploaderPage. Defaults to true for every module. The three Online
        /// Gaming modules (VirtualGames/SportsBook/FUNaloMAX) override this to
        /// false — they now use the dedicated OnlineGamingPage (Data Viewer +
        /// fixed-category Uploader tabs) instead of the generic UploaderPage's
        /// editable Data Preview grid / bulk-Excel upload, so only the
        /// "Upload to Database" action is hidden here; "Get Data from
        /// Database" (used to drive the Data Viewer) still works normally.
        /// </summary>
        public virtual bool SupportsUpload => true;

        /// <summary>
        /// The columns for this table, in order: DB/DataTable field name,
        /// friendly display header, and expected data type. Used for the Data
        /// Preview grid, the Bulk Upload template, and — critically — for the
        /// automation import's content validation, since automated files are
        /// read by column position, not by matching header text.
        /// </summary>
        public abstract (string Field, string Header, ColumnDataType Type)[] PreviewColumns { get; }

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
                    // No automatic connection ping here anymore — switching
                    // SEC/SN just clears the stale badge/data; the NEXT real
                    // action (Get Data, Upload, or an explicit Test
                    // Connection click) is what re-establishes IsConnected.
                    // This also matters for Online Gaming specifically: all
                    // three modules there share one .accdb file, and firing
                    // an auto-ping for every module on toggle used to open
                    // several simultaneous connections to that same file.
                    IsConnected = false;
                    PreviewData = null;
                    TotalRows = 0;
                }
            }
        }

        // ── Mode ──────────────────────────────────────────────────────────────
        private UploadMode _mode = UploadMode.Daily;
        public UploadMode Mode
        {
            get => _mode;
            set
            {
                // Daily-only modules ignore any attempt to switch to Monthly —
                // belt-and-braces alongside the UI hiding the Monthly tab.
                if (value == UploadMode.Monthly && !SupportsMonthlyMode)
                    value = UploadMode.Daily;
                Set(ref _mode, value); OnPropertyChanged(nameof(IsDailyMode)); OnPropertyChanged(nameof(IsMonthlyMode));
            }
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
            catch (Exception ex)
            {
                IsConnected = false;
                SetStatus($"✗ Connection error: {ex.Message}", "#FFFF4444");
                Log($"ERROR: {ex.Message}");
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
                IsConnected = true; // a successful fetch IS proof of connectivity — no separate ping needed
                Log($"Fetched {TotalRows} row(s) from {TableName}.");
                SetStatus($"✓ {TotalRows} record(s) loaded.", "#FF3FB950");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                SetStatus($"✗ Fetch failed: {ex.Message}", "#FFFF4444");
                Log($"ERROR: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        /// <summary>
        /// Main upload flow, interactive. For each row in the upload, deletes
        /// only the existing record(s) that share the same logical key (see
        /// UploadKeyConfig) — NOT every existing record for that date — then
        /// inserts the uploaded rows. Prompts once to confirm before touching
        /// any dates that already have data.
        /// </summary>
        public Task UploadAsync(DataTable uploadData)
            => ExecuteUploadAsync(uploadData, interactive: true);

        /// <summary>
        /// Same upload pipeline as UploadAsync (key-scoped delete, then
        /// insert) but with no confirmation dialogs — any conflicting
        /// key-matched records are overwritten automatically. Used by
        /// unattended/automated imports (the folder-watching automation) and
        /// by the Online Gaming Uploader tabs (single-category submits),
        /// where no one is present to click a prompt each time. Returns a
        /// summary instead of showing a "done" MessageBox.
        /// </summary>
        public Task<(bool Success, string Message, int RowsUploaded)> UploadSilentAsync(DataTable uploadData)
            => ExecuteUploadAsync(uploadData, interactive: false);

        private async Task<(bool Success, string Message, int RowsUploaded)> ExecuteUploadAsync(DataTable uploadData, bool interactive)
        {
            if (!HasDb)
            {
                var msg = $"No database is configured for {DisplayTitle} / {Property} property.";
                SetStatus(msg, "#FFFF4444");
                if (interactive)
                    MessageBox.Show(msg, DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return (false, msg, 0);
            }

            if (uploadData == null || uploadData.Rows.Count == 0)
            {
                const string msg = "No data to upload.";
                SetStatus(msg, "#FFFF8C00");
                return (false, msg, 0);
            }

            if (!uploadData.Columns.Contains("DTE"))
            {
                const string msg = "Upload data must include a DTE (date) column.";
                SetStatus(msg, "#FFFF4444");
                if (interactive)
                    MessageBox.Show(msg, DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return (false, msg, 0);
            }

            // ── De-duplicate rows WITHIN this upload batch by logical key ──────
            // Bug fix: previously, the key-scoped delete below only deleted the
            // existing matching record ONCE per key (via a seenKeys check), but
            // every row of uploadData — including duplicates of the SAME key —
            // still got inserted afterward. So uploading a file with the same
            // record twice (e.g. same DTE/VIP_Name/Commission_Type/Curr for VIP)
            // deleted the 1 existing row but then inserted 2 rows back, silently
            // creating a duplicate instead of just replacing the existing record.
            //
            // Fix: collapse the upload batch to one row per logical key BEFORE
            // doing anything else. If the same key appears more than once, only
            // the LAST occurrence in the file is kept (matches "last value wins"
            // expectations for a spreadsheet edit), and the rest are logged as
            // skipped duplicates.
            var keyColumns = UploadKeyConfig.GetKeyColumns(UploaderType);
            var dedupedData = uploadData.Clone();
            var lastRowForKey = new Dictionary<string, DataRow>();
            int duplicateRowsInBatch = 0;

            foreach (DataRow row in uploadData.Rows)
            {
                var keyStr = string.Join("|", keyColumns.Select(k => row.Table.Columns.Contains(k) ? (row[k]?.ToString() ?? "") : ""));
                if (lastRowForKey.ContainsKey(keyStr))
                    duplicateRowsInBatch++;
                lastRowForKey[keyStr] = row; // last occurrence for this key wins
            }
            foreach (var row in lastRowForKey.Values)
                dedupedData.ImportRow(row);

            if (duplicateRowsInBatch > 0)
            {
                Log($"⚠ {duplicateRowsInBatch} duplicate row(s) within this upload shared the same key " +
                    $"({string.Join("+", keyColumns)}) — only the last occurrence of each was kept, the rest were skipped.");
            }

            uploadData = dedupedData; // everything below now operates on the deduped set

            var dates = ExtractDistinctDates(uploadData);
            if (dates.Count == 0)
            {
                const string msg = "No valid DTE (date) values were found in the data to upload.";
                SetStatus(msg, "#FFFF4444");
                if (interactive)
                    MessageBox.Show(msg, DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return (false, msg, 0);
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

                if (interactive)
                {
                    string prompt;
                    string caption;
                    MessageBoxImage icon;
                    if (existingDates.Count > 0)
                    {
                        var dateList = string.Join(", ", existingDates.OrderBy(d => d).Select(d => d.ToString("MM/dd/yyyy")));
                        prompt = $"{DisplayTitle} Uploader: {existingDates.Count} date(s) in this upload already have data:\n{dateList}\n\n" +
                                 "Only the specific record(s) matching this upload (same key fields — see UploadKeyConfig) " +
                                 "will be replaced. Other existing records for these dates will be kept." +
                                 (duplicateRowsInBatch > 0
                                     ? $"\n\nNote: {duplicateRowsInBatch} duplicate row(s) within this file were detected and collapsed — see the log after upload."
                                     : "") +
                                 "\n\nDo you want to continue?";
                        caption = "Overwrite Matching Data";
                        icon = MessageBoxImage.Warning;
                    }
                    else
                    {
                        var dateList = string.Join(", ", dates.OrderBy(d => d).Select(d => d.ToString("MM/dd/yyyy")));
                        prompt = $"{DisplayTitle} Uploader: are you sure you want to upload data for {dateList}?" +
                                 (duplicateRowsInBatch > 0
                                     ? $"\n\nNote: {duplicateRowsInBatch} duplicate row(s) within this file were detected and collapsed — see the log after upload."
                                     : "");
                        caption = "Upload";
                        icon = MessageBoxImage.Question;
                    }

                    var result = MessageBox.Show(prompt, caption, MessageBoxButton.OKCancel, icon);
                    if (result != MessageBoxResult.OK)
                    {
                        SetStatus("Upload cancelled.", "#FF8B949E");
                        return (false, "Upload cancelled by user.", 0);
                    }
                }
                else if (existingDates.Count > 0)
                {
                    var dateList = string.Join(", ", existingDates.OrderBy(d => d).Select(d => d.ToString("MM/dd/yyyy")));
                    Log($"Automated import: replacing matching-key record(s) for {dateList}.");
                }

                // ── Key-scoped replace ───────────────────────────────────────
                // uploadData is already deduped to one row per logical key (see
                // above), so this loop deletes at most one existing matching
                // record per row — no separate seenKeys check needed anymore.
                // This is what makes uploading a single record (or a partial
                // file) safe: it replaces just the record(s) that share the
                // same logical key, leaving every other existing record for
                // that date untouched. Segment filter is still applied for
                // VIP/Junket so they never touch each other's rows.
                SetStatus("Removing matching existing records…", "#FFFF8C00");
                int matchesChecked = 0;

                foreach (DataRow row in uploadData.Rows)
                {
                    await DatabaseService.DeleteMatchingRowAsync(ResolvedDbPath, UploaderType, row, keyColumns);
                    matchesChecked++;
                }

                if (matchesChecked > 0)
                    Log($"Checked {matchesChecked} distinct record key(s) in this upload (key: {string.Join("+", keyColumns)}); any matching existing record(s) were replaced.");

                SetStatus("Uploading records…", "#FFFF8C00");
                int uploaded = await DatabaseService.UploadRowsAsync(ResolvedDbPath, UploaderType, uploadData);
                Progress = 100;
                TotalRows = uploaded;
                IsConnected = true; // a successful upload IS proof of connectivity
                Log($"✓ Upload complete — {uploaded} row(s) inserted.");
                var successMsg = duplicateRowsInBatch > 0
                    ? $"{uploaded} record(s) uploaded ({duplicateRowsInBatch} duplicate row(s) in the file were skipped)."
                    : $"{uploaded} record(s) uploaded.";
                SetStatus($"✓ Done! {successMsg}", "#FF3FB950");
                if (interactive)
                    MessageBox.Show("Done uploading the file.", DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                return (true, successMsg, uploaded);
            }
            catch (Exception ex)
            {
                IsConnected = false;
                SetStatus($"✗ Upload failed: {ex.Message}", "#FFFF4444");
                Log($"ERROR: {ex.Message}");
                return (false, ex.Message, 0);
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
                IsConnected = true;
                Log("✓ Row updated.");
                SetStatus("✓ Row updated.", "#FF3FB950");
            }
            catch (Exception ex)
            {
                IsConnected = false;
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
                IsConnected = true;
                Log("✓ Row deleted.");
                SetStatus("✓ Row deleted.", "#FF3FB950");
            }
            catch (Exception ex)
            {
                IsConnected = false;
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
            UploaderType.Mass => "Curr_Mass",
            UploaderType.VIP => "Curr_VIP",
            UploaderType.Junket => "Curr_Junket",
            UploaderType.VirtualGames => "Virtual_Games",
            UploaderType.SportsBook => "SportsBook",
            UploaderType.FUNaloMAX => "FUNaloMAX",
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

    // ── Online Gaming base ───────────────────────────────────────────────────
    /// <summary>
    /// Base for the three Online Gaming modules (VirtualGames/SportsBook/
    /// FUNaloMAX). Adds the fixed-category "Uploader" panel (see the
    /// mockup: a handful of named boxes, each taking just a Wager/Win pair)
    /// plus a matching read-only "Data Viewer" for the selected date, on top
    /// of everything UploaderViewModel already provides. Replaces the
    /// generic UploaderPage's editable Data Preview grid for these three
    /// modules — see OnlineGamingPage.
    /// </summary>
    public abstract class OnlineGamingUploaderViewModel : UploaderViewModel
    {
        /// <summary>The fixed set of categories shown as individual boxes on this module's Uploader tab.</summary>
        protected abstract OnlineGamingCategoryDefinition[] CategoryDefinitions { get; }

        /// <summary>True for FUNaloMAX — every category there shows a derived, read-only Payout (Wager - Win).</summary>
        public virtual bool CategoriesHavePayout => false;

        /// <summary>Constant value written to a "GameType" column for every category row, if this module's table has one (FUNaloMAX: "FunaloMax"). Null if not applicable.</summary>
        protected virtual string? FixedGameTypeValue => null;

        public ObservableCollection<CategoryEntryViewModel> Categories { get; }

        // Defaults to yesterday, per the requested design — the person can
        // still pick any other date via the DatePicker. Changing it also
        // drives the Data Viewer grid for this module.
        private DateTime _uploadDate = DateTime.Today.AddDays(-1);
        public DateTime UploadDate
        {
            get => _uploadDate;
            set
            {
                if (Set(ref _uploadDate, value))
                {
                    SelectedDate = value; // keep the inherited daily-fetch date in sync
                    _ = RefreshViewerAsync();
                }
            }
        }

        protected OnlineGamingUploaderViewModel()
        {
            Categories = new ObservableCollection<CategoryEntryViewModel>();
            foreach (var def in CategoryDefinitions)
                Categories.Add(new CategoryEntryViewModel(def, CategoriesHavePayout));

            SelectedDate = _uploadDate;
            Mode = UploadMode.Daily;
        }

        /// <summary>Re-fetches the Data Viewer grid for UploadDate. Virtual so FUNaloMAX can also refresh its MegaFunalo summary.</summary>
        public virtual Task RefreshViewerAsync() => FetchDataAsync();

        /// <summary>
        /// Uploads ONE category's Wager/Win (plus a derived Payout, for
        /// modules where CategoriesHavePayout is true) for UploadDate. Reuses
        /// the same key-scoped, silent (no confirmation dialog) upload
        /// pipeline as the automation import, so re-submitting the same
        /// category/date just replaces that one record instead of adding a
        /// duplicate.
        /// </summary>
        public async Task<bool> SubmitCategoryAsync(CategoryEntryViewModel entry)
        {
            double.TryParse(entry.WagerText, out var wager);
            double.TryParse(entry.WinText, out var win);
            var payout = wager - win;

            var dt = BuildSingleRowTable(entry, wager, win, payout);
            var (success, message, _) = await UploadSilentAsync(dt);

            if (success)
            {
                entry.WagerText = "";
                entry.WinText = "";
                await RefreshViewerAsync();
                await AfterCategorySubmitAsync();
            }
            else
            {
                MessageBox.Show(message, DisplayTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return success;
        }

        /// <summary>Submits every category that has a Wager and/or Win entered; blank categories are left untouched.</summary>
        public async Task SubmitAllAsync()
        {
            foreach (var entry in Categories.ToList())
            {
                if (string.IsNullOrWhiteSpace(entry.WagerText) && string.IsNullOrWhiteSpace(entry.WinText))
                    continue;
                await SubmitCategoryAsync(entry);
            }
        }

        /// <summary>
        /// Hook for modules that need to do something after each successful
        /// category submission. FUNaloMAX overrides this to recompute and
        /// push the MegaFunalo totals (see FUNaloMAXViewModel).
        /// </summary>
        protected virtual Task AfterCategorySubmitAsync() => Task.CompletedTask;

        private DataTable BuildSingleRowTable(CategoryEntryViewModel entry, double wager, double win, double payout)
        {
            var dt = new DataTable();
            foreach (var c in PreviewColumns)
                dt.Columns.Add(c.Field, typeof(object));

            var row = dt.NewRow();
            foreach (var c in PreviewColumns)
            {
                row[c.Field] = c.Field.ToUpperInvariant() switch
                {
                    "DTE" => UploadDate.Date,
                    "BRAND" => entry.GameName,
                    "PROVIDER" => (object?)entry.Provider ?? DBNull.Value,
                    "GAMETYPE" => (object?)FixedGameTypeValue ?? DBNull.Value,
                    "GAMENAME" => entry.GameName,
                    "WAGER" => wager,
                    "WIN" => win,
                    "PAYOUT" => payout,
                    _ => DBNull.Value
                };
            }
            dt.Rows.Add(row);
            return dt;
        }
    }

    // ── Concrete ViewModels — NonGaming ─────────────────────────────────────
    public class OthersViewModel : UploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.Others;
        public override string DisplayTitle => "Others";
        public override string AccentHex => "#FF2F81F7";

        public override (string Field, string Header, ColumnDataType Type)[] PreviewColumns => new[]
        {
            ("DTE",       "DTE",       ColumnDataType.Date),
            ("Dept_Type", "Dept Type", ColumnDataType.Text),
            ("Dept_Desc", "Dept Desc", ColumnDataType.Text),
            ("Revenue",   "Revenue",   ColumnDataType.Number),
            ("Comp",      "Comp",      ColumnDataType.Number),
        };
    }

    public class FnBViewModel : UploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.FnB;
        public override string DisplayTitle => "F&B";
        public override string AccentHex => "#FF3FB950";

        public override (string Field, string Header, ColumnDataType Type)[] PreviewColumns => new[]
        {
            ("DTE",         "DTE",         ColumnDataType.Date),
            ("Rev_Center",  "Rev Center",  ColumnDataType.Text),
            ("Net_Sales",   "Net Sales",   ColumnDataType.Number),
            ("Covers",      "Covers",      ColumnDataType.Number),
            ("Comp_Rev",    "Comp Rev",    ColumnDataType.Number),
            ("Comp_Covers", "Comp Covers", ColumnDataType.Number),
        };
    }

    public class HotelViewModel : UploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.Hotel;
        public override string DisplayTitle => "Hotel";
        public override string AccentHex => "#FFFF8C00";

        public override (string Field, string Header, ColumnDataType Type)[] PreviewColumns => new[]
        {
            ("DTE",              "DTE",              ColumnDataType.Date),
            ("Area_Type",        "Area Type",        ColumnDataType.Text),
            ("Description_Type", "Description Type", ColumnDataType.Text),
            ("Total_Revenue",    "Total Revenue",     ColumnDataType.Number),
            ("Comp_Revenue",     "Comp Revenue",      ColumnDataType.Number),
            ("Occupied_Rooms",    "Occupied Room",     ColumnDataType.Number),
        };
    }

    public class VisitationViewModel : UploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.Visitation;
        public override string DisplayTitle => "Visitation";
        public override string AccentHex => "#FF8957E5";

        public override (string Field, string Header, ColumnDataType Type)[] PreviewColumns => new[]
        {
            ("DTE",        "DTE",        ColumnDataType.Date),
            ("Visitation", "Visitation", ColumnDataType.Number),
            ("SRC",        "SRC",        ColumnDataType.Text),
        };
    }

    // ── Concrete ViewModels — Gaming ────────────────────────────────────────
    // PLACEHOLDER column sets below — edit these to match your real Mass/VIP/
    // Junket Access table columns (field names, order, and data types).

    public class MassViewModel : UploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.Mass;
        public override string DisplayTitle => "Mass";
        public override string AccentHex => "#FFE3B341";

        public override (string Field, string Header, ColumnDataType Type)[] PreviewColumns => new[]
        {
            ("DTE",        "DTE",        ColumnDataType.Date),
            ("Pit",        "Pit",        ColumnDataType.Number),
            ("PITID",      "PitID",      ColumnDataType.Text),
            ("TABLE",      "Table",      ColumnDataType.Text),
            ("GAME",       "Game",       ColumnDataType.Text),
            ("Game_Name",  "Game Name",  ColumnDataType.Text),
            ("Curr",       "Currency",   ColumnDataType.Text),
            ("Revenue",    "Revenue",    ColumnDataType.Number),
            ("Drop",       "Drop",       ColumnDataType.Number),
            ("Segment",    "Segment",    ColumnDataType.Text),
            ("Limit",      "Limit",      ColumnDataType.Number),
            ("Saved_by",   "Saved By",   ColumnDataType.Text),
            ("Saved_Date", "Saved Date", ColumnDataType.Date),
        };
    }

    public class VIPViewModel : UploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.VIP;
        public override string DisplayTitle => "VIP";
        public override string AccentHex => "#FFDB61A2";

        public override (string Field, string Header, ColumnDataType Type)[] PreviewColumns => new[]
        {
            ("DTE",             "DTE",              ColumnDataType.Date),
            ("Segment",         "Segment",          ColumnDataType.Text),
            ("VIP_Name",        "VIP Name",         ColumnDataType.Text),
            ("Commission_Type", "Commission Type",  ColumnDataType.Text),
            ("Curr",            "Currency",         ColumnDataType.Text),
            ("Turnover",        "Turnover",         ColumnDataType.Number),
            ("Revenue",         "Revenue",          ColumnDataType.Number),
            ("Table_Count",     "Table Count",      ColumnDataType.Number),
        };
    }

    public class JunketViewModel : UploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.Junket;
        public override string DisplayTitle => "Junket";
        public override string AccentHex => "#FF39C5CF";

        public override (string Field, string Header, ColumnDataType Type)[] PreviewColumns => new[]
        {
            ("DTE",             "DTE",              ColumnDataType.Date),
            ("Segment",         "Segment",          ColumnDataType.Text),
            ("VIP_Name",        "VIP Name",         ColumnDataType.Text),
            ("Commission_Type", "Commission Type",  ColumnDataType.Text),
            ("Curr",            "Currency",         ColumnDataType.Text),
            ("Turnover",        "Turnover",         ColumnDataType.Number),
            ("Revenue",         "Revenue",          ColumnDataType.Number),
            ("Table_Count",     "Table Count",      ColumnDataType.Number),
        };
    }

    // ── Concrete ViewModels — Online Gaming ─────────────────────────────────
    // These now derive from OnlineGamingUploaderViewModel, which adds the
    // fixed-category Uploader panel (Categories) and the UploadDate-driven
    // Data Viewer (RefreshViewerAsync/PreviewData) used by OnlineGamingPage.
    // SupportsUpload stays false — uploading happens per-category (or via
    // "Submit All") from that page, not through the generic UploaderPage.

    public class VirtualGamesViewModel : OnlineGamingUploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.VirtualGames;
        public override string DisplayTitle => "Virtual Games";
        public override string AccentHex => "#FF00C2A8";
        public override bool SupportsUpload => false;

        protected override OnlineGamingCategoryDefinition[] CategoryDefinitions => OnlineGamingCategories.VirtualGames;

        public override (string Field, string Header, ColumnDataType Type)[] PreviewColumns => new[]
        {
            ("Dte",       "Dte",        ColumnDataType.Date),
            ("Brand",     "Brand",      ColumnDataType.Text),
            ("Provider",  "Provider",   ColumnDataType.Text),
            ("Wager",     "Wager",      ColumnDataType.Number),
            ("Win",       "Win",        ColumnDataType.Number),
        };
    }

    public class SportsBookViewModel : OnlineGamingUploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.SportsBook;
        public override string DisplayTitle => "SportsBook";
        public override string AccentHex => "#FF5B8DEF";
        public override bool SupportsUpload => false;

        protected override OnlineGamingCategoryDefinition[] CategoryDefinitions => OnlineGamingCategories.SportsBook;

        public override (string Field, string Header, ColumnDataType Type)[] PreviewColumns => new[]
        {
            ("Dte",   "Dte",   ColumnDataType.Date),
            ("Wager", "Wager", ColumnDataType.Number),
            ("Win",   "Win",   ColumnDataType.Number),
        };
    }

    public class FUNaloMAXViewModel : OnlineGamingUploaderViewModel
    {
        public override UploaderType UploaderType => UploaderType.FUNaloMAX;
        public override string DisplayTitle => "FUNaloMAX";
        public override string AccentHex => "#FFF2994A";
        public override bool SupportsUpload => false;

        // The "twist": every FUNaloMAX category derives a read-only Payout
        // (Wager - Win) shown right in its Uploader box, and GameType is
        // always the fixed value "FunaloMax" for every row.
        public override bool CategoriesHavePayout => true;
        protected override string? FixedGameTypeValue => "FunaloMax";
        protected override OnlineGamingCategoryDefinition[] CategoryDefinitions => OnlineGamingCategories.FUNaloMAX;

        public override (string Field, string Header, ColumnDataType Type)[] PreviewColumns => new[]
        {
            ("DTE",      "DTE",       ColumnDataType.Date),
            ("GameType", "GameType",  ColumnDataType.Text),
            ("GameName", "GameName",  ColumnDataType.Text),
            ("Wager",    "Wager",     ColumnDataType.Number),
            ("Win",      "Win",       ColumnDataType.Number),
            ("Payout",   "Payout",    ColumnDataType.Number),
        };

        // ── MegaFunalo (daily total across every FUNaloMAX category) ────────
        private DataTable? _megaFunaloData;
        public DataTable? MegaFunaloData
        {
            get => _megaFunaloData;
            set => Set(ref _megaFunaloData, value);
        }

        public override async Task RefreshViewerAsync()
        {
            await base.RefreshViewerAsync(); // fetches the FUNaloMAX rows into PreviewData
            if (!HasDb) return;
            try { MegaFunaloData = await MegaFunaloService.FetchDailyAsync(ResolvedDbPath, UploadDate); }
            catch { /* viewer-only, best effort */ }
        }

        /// <summary>
        /// Recomputes MegaFunalo's totals from EVERY FUNaloMAX row currently
        /// on file for UploadDate (not just the category(ies) just
        /// submitted), so the aggregate stays correct however the categories
        /// were submitted — one at a time or via "Submit All".
        /// </summary>
        protected override async Task AfterCategorySubmitAsync()
        {
            if (!HasDb || PreviewData == null) return;

            double totalWager = 0, totalWin = 0, totalPayout = 0;
            foreach (DataRow row in PreviewData.Rows)
            {
                totalWager += ToDouble(row["Wager"]);
                totalWin += ToDouble(row["Win"]);
                totalPayout += ToDouble(row["Payout"]);
            }

            try
            {
                await MegaFunaloService.UpsertTotalsAsync(ResolvedDbPath, UploadDate, totalWager, totalWin, totalPayout);
                MegaFunaloData = await MegaFunaloService.FetchDailyAsync(ResolvedDbPath, UploadDate);
                Log($"✓ MegaFunalo totals updated for {UploadDate:MM/dd/yyyy} — Wager {totalWager:N2}, Win {totalWin:N2}, Payout {totalPayout:N2}.");
            }
            catch (Exception ex)
            {
                Log($"ERROR updating MegaFunalo totals: {ex.Message}");
            }
        }

        private static double ToDouble(object? v) => v == null || v == DBNull.Value ? 0 : Convert.ToDouble(v);
    }
}