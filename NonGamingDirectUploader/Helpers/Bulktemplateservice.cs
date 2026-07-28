using System;
using System.Data;
using System.IO;
using ClosedXML.Excel;
using NonGamingDirectUploader.Models;

namespace NonGamingDirectUploader.Helpers
{
    /// <summary>
    /// Creates a per-table .xlsx template for the interactive Bulk Upload
    /// button — a real Excel workbook (via ClosedXML), so it opens correctly
    /// in Excel. Reading the filled-in template back is handled by
    /// ExcelTemplateService, which reads the same real .xlsx format (used by
    /// the automation import too).
    /// </summary>
    public static class BulkTemplateService
    {
        /// <summary>
        /// Creates an .xlsx template with the table's column headers and
        /// returns its path. If <paramref name="existingData"/> is supplied
        /// (e.g. from the Data Preview grid — vm.PreviewData) and has rows,
        /// those rows are written into the sheet under the header row so the
        /// user opens Excel already seeing what's currently loaded in the
        /// app, instead of a blank sheet. If it's null/empty, the sheet is
        /// just the header row, same as before.
        /// </summary>
        public static string CreateTemplate(
            UploaderType type,
            (string Field, string Header, ColumnDataType Type)[] columns,
            DataTable? existingData = null)
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

            if (existingData != null && existingData.Rows.Count > 0)
                WriteExistingRows(sheet, columns, existingData);

            workbook.SaveAs(tempPath);
            return tempPath;
        }

        /// <summary>
        /// Writes each row of <paramref name="existingData"/> (the DataGridView's
        /// current data) into the sheet, one row per Excel row starting at row 2,
        /// in the same column order as <paramref name="columns"/> — matching what
        /// ExcelTemplateService.ReadXlsxFile expects when reading position-based.
        /// </summary>
        private static void WriteExistingRows(
            IXLWorksheet sheet,
            (string Field, string Header, ColumnDataType Type)[] columns,
            DataTable existingData)
        {
            int excelRow = 2; // row 1 is the header
            foreach (DataRow row in existingData.Rows)
            {
                for (int c = 0; c < columns.Length; c++)
                {
                    var field = columns[c].Field;
                    if (!existingData.Columns.Contains(field))
                        continue; // this table doesn't have that column — leave cell blank

                    var value = row[field];
                    if (value == null || value == DBNull.Value)
                        continue;

                    var cell = sheet.Cell(excelRow, c + 1);

                    switch (columns[c].Type)
                    {
                        case ColumnDataType.Date:
                            if (value is DateTime dt)
                                cell.Value = dt;
                            else if (DateTime.TryParse(value.ToString(), out var parsedDate))
                                cell.Value = parsedDate;
                            else
                                cell.Value = value.ToString();
                            break;

                        case ColumnDataType.Number:
                            if (double.TryParse(value.ToString(), out var num))
                                cell.Value = num;
                            else
                                cell.Value = value.ToString();
                            break;

                        case ColumnDataType.Text:
                        default:
                            cell.Value = value.ToString();
                            break;
                    }
                }
                excelRow++;
            }
        }
    }
}