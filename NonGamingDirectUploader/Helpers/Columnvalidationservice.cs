using System;
using System.Collections.Generic;
using System.Data;
using NonGamingDirectUploader.Models;

namespace NonGamingDirectUploader.Helpers
{
    /// <summary>
    /// Validates each row of a DataTable against a table's expected per-column
    /// data types (Date / Number / Text), regardless of what the source file's
    /// header row actually said. Used by the automation import, which reads
    /// files from another app whose headers won't necessarily match — content
    /// is what's checked, not column names.
    /// </summary>
    public static class ColumnValidationService
    {
        /// <summary>
        /// Returns a copy of <paramref name="data"/> containing only the rows
        /// that pass validation, plus a human-readable error per rejected row.
        /// </summary>
        public static (DataTable ValidRows, List<string> RowErrors) Validate(
            DataTable data, (string Field, string Header, ColumnDataType Type)[] columns)
        {
            var errors = new List<string>();
            var validTable = data.Clone();

            int rowNum = 1; // row 1 is the header in the source file, so data starts at row 2
            foreach (DataRow row in data.Rows)
            {
                rowNum++;
                var rowErrors = new List<string>();

                foreach (var col in columns)
                {
                    if (!data.Columns.Contains(col.Field)) continue;

                    var raw = row[col.Field];
                    var text = (raw == null || raw == DBNull.Value) ? "" : raw.ToString() ?? "";

                    switch (col.Type)
                    {
                        case ColumnDataType.Date:
                            if (string.IsNullOrWhiteSpace(text) || !DateTime.TryParse(text, out _))
                                rowErrors.Add($"{col.Header} ('{text}') is not a valid date");
                            break;

                        case ColumnDataType.Number:
                            if (string.IsNullOrWhiteSpace(text) || !double.TryParse(text, out _))
                                rowErrors.Add($"{col.Header} ('{text}') is not a valid number");
                            break;

                        case ColumnDataType.Text:
                        default:
                            // Any value, including blank, is acceptable for a text column.
                            break;
                    }
                }

                if (rowErrors.Count == 0)
                {
                    validTable.ImportRow(row);
                }
                else
                {
                    errors.Add($"Row {rowNum}: " + string.Join("; ", rowErrors));
                }
            }

            return (validTable, errors);
        }
    }
}