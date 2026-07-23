using System;
using System.IO;
using ClosedXML.Excel;
using NonGamingDirectUploader.Models;

namespace NonGamingDirectUploader.Helpers
{
    /// <summary>
    /// Creates a per-table blank .xlsx template for the interactive Bulk
    /// Upload button — a real Excel workbook (via ClosedXML), so it opens
    /// correctly in Excel. Reading the filled-in template back is handled by
    /// ExcelTemplateService, which reads the same real .xlsx format (used by
    /// the automation import too).
    /// </summary>
    public static class BulkTemplateService
    {
        /// <summary>Creates a blank .xlsx template with the table's column headers and returns its path.</summary>
        public static string CreateTemplate(UploaderType type, (string Field, string Header, ColumnDataType Type)[] columns)
        {
            var tempPath = Path.Combine(Path.GetTempPath(),
                $"{type}_BulkUpload_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Upload");

            for (int i = 0; i < columns.Length; i++)
            {
                var col = i + 1; // ClosedXML columns are 1-based
                sheet.Cell(1, col).Value = columns[i].Field;
                sheet.Cell(1, col).Style.Font.Bold = true;
                sheet.Column(col).Width = 18;

                // Pre-format the date column so anything typed/pasted in is
                // stored as a real date rather than plain text or a raw
                // serial number.
                if (columns[i].Type == ColumnDataType.Date)
                    sheet.Column(col).Style.DateFormat.Format = "MM/dd/yyyy";
            }

            workbook.SaveAs(tempPath);
            return tempPath;
        }
    }
}