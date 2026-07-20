using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using NonGamingDirectUploader.Models;

namespace NonGamingDirectUploader.Helpers
{
    /// <summary>
    /// Creates a per-table "Excel template" for bulk upload and reads it back
    /// into a DataTable once the user has pasted data and saved the file.
    ///
    /// The template is a .csv file — Excel opens .csv natively via the default
    /// file association (double-click behaves exactly like an .xlsx), so no
    /// Office automation / Interop / third-party libraries are required.
    /// </summary>
    public static class BulkTemplateService
    {
        /// <summary>Creates a blank template with the table's column headers and returns its path.</summary>
        public static string CreateTemplate(UploaderType type, (string Field, string Header)[] columns)
        {
            var tempPath = Path.Combine(Path.GetTempPath(),
                $"{type}_BulkUpload_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            var headerLine = string.Join(",", columns.Select(c => c.Field));
            // No BOM — keeps the file friendly to Excel's default CSV import.
            File.WriteAllText(tempPath, headerLine + Environment.NewLine, new UTF8Encoding(false));
            return tempPath;
        }

        /// <summary>Reads a filled-in template back into a DataTable (string-typed columns).</summary>
        public static DataTable ReadFilledTemplate(string path, (string Field, string Header)[] columns)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "Template file was not found. Did you save it before closing Excel?", path);

            var lines = File.ReadAllLines(path);

            var dt = new DataTable();
            foreach (var c in columns)
                dt.Columns.Add(c.Field, typeof(string));

            // Only a header row (or nothing) — return an empty table, caller decides how to react.
            if (lines.Length <= 1)
                return dt;

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                var cells = SplitCsvLine(lines[i]);
                var row = dt.NewRow();
                for (int c = 0; c < dt.Columns.Count; c++)
                {
                    row[c] = c < cells.Length && !string.IsNullOrWhiteSpace(cells[c])
                        ? cells[c].Trim()
                        : (object)DBNull.Value;
                }
                dt.Rows.Add(row);
            }

            return dt;
        }

        /// <summary>Minimal CSV line splitter that respects double-quoted fields containing commas.</summary>
        private static string[] SplitCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            foreach (char ch in line)
            {
                if (ch == '"') { inQuotes = !inQuotes; continue; }
                if (ch == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); continue; }
                current.Append(ch);
            }
            result.Add(current.ToString());
            return result.ToArray();
        }
    }
}