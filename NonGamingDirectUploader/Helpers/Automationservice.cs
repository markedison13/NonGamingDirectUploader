using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NonGamingDirectUploader.Models;
using NonGamingDirectUploader.ViewModels;

namespace NonGamingDirectUploader.Helpers
{
    /// <summary>
    /// Reads each module/property's configured .xlsx file (see
    /// AutomationConfig) and imports it into the matching database.
    ///
    /// Each file is a fixed, known path — not a folder to scan or a filename
    /// to parse. Any (module, property) pair with no configured path is
    /// simply skipped. Visitation is intentionally not configured and so is
    /// never touched by automation.
    ///
    /// Files are read by column POSITION, not by matching header text — row 1
    /// is always skipped regardless of content. Every row's values are then
    /// validated against each column's expected data type (Date/Number/Text)
    /// before upload; rows that fail are skipped individually and logged, the
    /// rest of the file still uploads.
    ///
    /// Runs fully unattended — conflicting DTE dates are overwritten
    /// automatically (via UploadSilentAsync), with no confirmation dialogs.
    /// Each source file is COPIED (never moved/deleted) into a Processed or
    /// Errors subfolder next to it, so the original is never touched — this
    /// matters since the same path is typically overwritten daily by another
    /// process (e.g. SAS).
    /// </summary>
    public static class AutomationService
    {
        /// <summary>Runs one full import pass across every configured module/property file.</summary>
        public static async Task<List<string>> RunImportAsync()
        {
            var log = new List<string> { $"=== Automation run started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===" };

            foreach (UploaderType type in Enum.GetValues(typeof(UploaderType)))
            {
                foreach (PropertyType prop in Enum.GetValues(typeof(PropertyType)))
                {
                    if (!AutomationConfig.TryGetFilePath(type, prop, out var filePath) || string.IsNullOrWhiteSpace(filePath))
                        continue; // not configured for this module/property

                    if (!File.Exists(filePath))
                    {
                        log.Add($"[{type}/{prop}] Skipped — file not found: {filePath}");
                        continue;
                    }

                    var vm = CreateViewModel(type);
                    vm.Property = prop;

                    var folder = Path.GetDirectoryName(filePath) ?? "";
                    var processedDir = Path.Combine(folder, "Processed");
                    var errorsDir = Path.Combine(folder, "Errors");
                    Directory.CreateDirectory(processedDir);
                    Directory.CreateDirectory(errorsDir);

                    var fileName = Path.GetFileName(filePath);

                    try
                    {
                        // Row 1 is always treated as a header and skipped — its actual text is never checked.
                        var rawTable = ExcelTemplateService.ReadXlsxFile(filePath, vm.PreviewColumns);
                        if (rawTable.Rows.Count == 0)
                        {
                            CopyFile(filePath, errorsDir);
                            log.Add($"[{type}/{prop}] {fileName}: no data rows found — copied to Errors. Original left in place.");
                            continue;
                        }

                        // Validate every row's values against each column's expected
                        // data type (Date/Number/Text) — content, not header names.
                        var (validRows, rowErrors) = ColumnValidationService.Validate(rawTable, vm.PreviewColumns);

                        foreach (var err in rowErrors)
                            log.Add($"[{type}/{prop}] {fileName}: SKIPPED — {err}");

                        if (validRows.Rows.Count == 0)
                        {
                            CopyFile(filePath, errorsDir);
                            log.Add($"[{type}/{prop}] {fileName}: no rows passed validation — copied to Errors. Original left in place.");
                            continue;
                        }

                        var (success, message, _) = await vm.UploadSilentAsync(validRows);
                        if (success)
                        {
                            CopyFile(filePath, processedDir);
                            var skippedNote = rowErrors.Count > 0 ? $" ({rowErrors.Count} row(s) skipped — see above)" : "";
                            log.Add($"[{type}/{prop}] {fileName}: {message}{skippedNote} — copied to Processed. Original left in place.");
                        }
                        else
                        {
                            CopyFile(filePath, errorsDir);
                            log.Add($"[{type}/{prop}] {fileName}: FAILED — {message} — copied to Errors. Original left in place.");
                        }
                    }
                    catch (Exception ex)
                    {
                        try { CopyFile(filePath, errorsDir); } catch { /* leave it in place if even that fails */ }
                        log.Add($"[{type}/{prop}] {fileName}: ERROR — {ex.Message}");
                    }
                }
            }

            log.Add($"=== Automation run finished {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            WriteLogFile(log);
            return log;
        }

        private static UploaderViewModel CreateViewModel(UploaderType type) => type switch
        {
            UploaderType.Others => new OthersViewModel(),
            UploaderType.FnB => new FnBViewModel(),
            UploaderType.Hotel => new HotelViewModel(),
            UploaderType.Visitation => new VisitationViewModel(),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        /// <summary>Copies a file into a Processed/Errors subfolder for a record, without touching the original.</summary>
        private static void CopyFile(string sourceFile, string destDir)
        {
            var destPath = Path.Combine(destDir,
                $"{Path.GetFileNameWithoutExtension(sourceFile)}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(sourceFile)}");
            File.Copy(sourceFile, destPath, overwrite: true);
        }

        private static void WriteLogFile(List<string> log)
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NonGamingDirectUploader", "Logs");
                Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, $"automation_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                File.WriteAllLines(logPath, log);
            }
            catch { /* logging is best-effort */ }
        }
    }
}