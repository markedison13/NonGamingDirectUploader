using System;
using System.Data;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using NonGamingDirectUploader.Models;

namespace NonGamingDirectUploader.Helpers
{
    /// <summary>
    /// Reads a real .xlsx file into a DataTable for the automation import.
    /// Uses ClosedXML, which needs no Excel/Office installation.
    ///
    /// Reads the FIRST worksheet by column POSITION, not by matching header
    /// text — row 1 is always treated as a header and skipped, regardless of
    /// what it contains. Column N in the file maps to the Nth entry in the
    /// supplied column list.
    /// </summary>
    public static class ExcelTemplateService
    {
        public static DataTable ReadXlsxFile(string path, (string Field, string Header, ColumnDataType Type)[] columns)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("File not found.", path);

            var dt = new DataTable();
            foreach (var c in columns)
                dt.Columns.Add(c.Field, typeof(string));

            using var workbook = new XLWorkbook(path);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
                return dt; // empty workbook

            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
                return dt; // empty sheet

            bool isHeaderRow = true;
            foreach (var row in usedRange.RowsUsed())
            {
                if (isHeaderRow) { isHeaderRow = false; continue; } // skip row 1, whatever it says

                var dataRow = dt.NewRow();
                for (int c = 0; c < columns.Length; c++)
                {
                    var cell = row.Cell(c + 1); // ClosedXML columns are 1-based
                    dataRow[c] = ReadCellAsText(cell, columns[c].Type);
                }
                dt.Rows.Add(dataRow);
            }

            return dt;
        }

        /// <summary>
        /// Reads one cell's value as text, converting it appropriately for the
        /// column's expected type. Date columns get special handling: a cell
        /// can hold a real date (ClosedXML reports XLDataType.DateTime), OR a
        /// raw Excel serial-day NUMBER with no date formatting applied to the
        /// cell (common with data exported by another tool) — both are
        /// converted to the same "MM/dd/yyyy" text so downstream parsing works
        /// either way.
        /// </summary>
        private static object ReadCellAsText(IXLCell cell, ColumnDataType expectedType)
        {
            if (cell.IsEmpty())
                return DBNull.Value;

            string text;

            if (expectedType == ColumnDataType.Date)
            {
                if (cell.DataType == XLDataType.DateTime)
                {
                    text = cell.GetDateTime().ToString("MM/dd/yyyy");
                }
                else if (cell.DataType == XLDataType.Number)
                {
                    // Raw Excel serial date (e.g. 46224 = 4/6/2026) with no
                    // date format applied to the cell.
                    try
                    {
                        text = DateTime.FromOADate(cell.GetDouble()).ToString("MM/dd/yyyy");
                    }
                    catch
                    {
                        text = cell.GetString().Trim();
                    }
                }
                else
                {
                    text = cell.GetString().Trim();
                }
            }
            else if (cell.DataType == XLDataType.DateTime)
            {
                // A non-date column that happens to contain a date-typed cell
                // (rare, but read it as text rather than losing the value).
                text = cell.GetDateTime().ToString("MM/dd/yyyy");
            }
            else
            {
                text = cell.GetString().Trim();
            }

            return string.IsNullOrWhiteSpace(text) ? (object)DBNull.Value : text;
        }
    }
}