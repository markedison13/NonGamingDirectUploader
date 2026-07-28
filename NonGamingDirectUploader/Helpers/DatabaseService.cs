using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Threading.Tasks;
using NonGamingDirectUploader.Models;

namespace NonGamingDirectUploader.Helpers
{
    /// <summary>
    /// Wraps all ADODB operations translated from the VBA macros.
    /// Uses Microsoft.ACE.OLEDB.12.0 — same provider as the original VBA.
    /// </summary>
    public static class DatabaseService
    {
        // ── CONNECTION ────────────────────────────────────────────────────────
        private static string BuildConnectionString(string dbPath)
            => $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={dbPath}";

        // ── TABLE NAME MAP ────────────────────────────────────────────────────
        private static string TableName(UploaderType type) => type switch
        {
            // NonGaming
            UploaderType.Others => "Curr_Others",
            UploaderType.FnB => "Curr_FB",
            UploaderType.Hotel => "Curr_Hotel",
            UploaderType.Visitation => "Curr_Other_Stat",

            // Gaming
            UploaderType.Mass => "Curr_Mass",

            // VIP and Junket share the SAME table (Curr_VIP) — they're told
            // apart by the Segment column via SegmentFilterClause() below,
            // not by separate tables.
            UploaderType.VIP => "Curr_VIP",
            UploaderType.Junket => "Curr_VIP",

            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        // ── SEGMENT FILTER (VIP vs Junket) ───────────────────────────────────
        /// <summary>
        /// VIP and Junket share the same underlying database (VIP.accdb —
        /// see DatabaseConfig) and are told apart by the Segment column:
        /// VIP records have Segment = 'Premium'; every other Segment value
        /// belongs to Junket. Returns an extra " AND ..." clause to append
        /// to any query for these two types, or "" for types that don't need
        /// this distinction.
        /// </summary>
        private static string SegmentFilterClause(UploaderType type) => type switch
        {
            UploaderType.VIP => " AND Segment = 'Premium'",
            UploaderType.Junket => " AND Segment <> 'Premium'",
            _ => ""
        };

        // ── CHECK EXIST (Daily) ───────────────────────────────────────────────
        /// <summary>Returns true if a record for the given date already exists.</summary>
        public static async Task<bool> DateExistsAsync(string dbPath, UploaderType type, DateTime date)
        {
            return await Task.Run(() =>
            {
                using var cn = new OleDbConnection(BuildConnectionString(dbPath));
                cn.Open();
                string sql = $"SELECT DTE FROM {TableName(type)} WHERE DTE = #{date:MM/dd/yyyy}#{SegmentFilterClause(type)}";
                using var cmd = new OleDbCommand(sql, cn);
                using var rdr = cmd.ExecuteReader();
                return rdr != null && rdr.HasRows;
            });
        }

        // ── DELETE DAILY ──────────────────────────────────────────────────────
        // NOTE: No longer used by the upload pipeline (UploaderViewModel now
        // calls DeleteMatchingRowAsync, scoped to each upload row's logical
        // key, instead of wiping every row for a date). Left in place in case
        // it's needed elsewhere — a full manual "clear this whole day" tool,
        // for example — but do not wire this back into ExecuteUploadAsync
        // without re-introducing the whole-day overwrite bug.
        public static async Task DeleteDailyAsync(string dbPath, UploaderType type, DateTime date)
        {
            await Task.Run(() =>
            {
                using var cn = new OleDbConnection(BuildConnectionString(dbPath));
                cn.Open();
                string sql = $"DELETE * FROM {TableName(type)} WHERE DTE = #{date:MM/dd/yyyy}#{SegmentFilterClause(type)}";
                using var cmd = new OleDbCommand(sql, cn);
                cmd.ExecuteNonQuery();
            });
        }

        // ── DELETE MONTHLY ────────────────────────────────────────────────────
        public static async Task DeleteMonthlyAsync(string dbPath, UploaderType type, DateTime start, DateTime end)
        {
            await Task.Run(() =>
            {
                using var cn = new OleDbConnection(BuildConnectionString(dbPath));
                cn.Open();
                string sql = $"DELETE * FROM {TableName(type)} WHERE DTE Between #{start:MM/dd/yyyy}# And #{end:MM/dd/yyyy}#{SegmentFilterClause(type)}";
                using var cmd = new OleDbCommand(sql, cn);
                cmd.ExecuteNonQuery();
            });
        }

        // ── DELETE MATCHING ROW (key-scoped, replaces whole-day delete) ─────
        /// <summary>
        /// Deletes only the existing row(s) whose values across
        /// <paramref name="keyColumns"/> match <paramref name="uploadRow"/> —
        /// NOT every row for that date. This is what makes uploading a single
        /// record (or a partial file) safe: it replaces just the record(s)
        /// that share the same logical key, leaving every other existing
        /// record for that date untouched. Segment filter is still applied
        /// for VIP/Junket so they never touch each other's rows.
        /// </summary>
        public static async Task DeleteMatchingRowAsync(string dbPath, UploaderType type, DataRow uploadRow, string[] keyColumns)
        {
            await Task.Run(() =>
            {
                using var cn = new OleDbConnection(BuildConnectionString(dbPath));
                cn.Open();

                var clauses = new List<string>();
                var parameters = new List<OleDbParameter>();

                foreach (var col in keyColumns)
                {
                    var value = uploadRow[col];

                    if (string.Equals(col, "DTE", StringComparison.OrdinalIgnoreCase))
                    {
                        DateTime d = value is DateTime dt ? dt : DateTime.Parse(value?.ToString() ?? "");
                        clauses.Add($"DTE = #{d:MM/dd/yyyy}#");
                    }
                    else if (value == null || value == DBNull.Value)
                    {
                        clauses.Add($"[{col}] IS NULL");
                    }
                    else
                    {
                        clauses.Add($"[{col}] = ?");
                        parameters.Add(new OleDbParameter(col, value));
                    }
                }

                string sql = $"DELETE * FROM {TableName(type)} WHERE {string.Join(" AND ", clauses)}{SegmentFilterClause(type)}";
                using var cmd = new OleDbCommand(sql, cn);
                cmd.Parameters.AddRange(parameters.ToArray());
                cmd.ExecuteNonQuery();
            });
        }

        // ── UPLOAD ROWS (generic DataTable) ──────────────────────────────────
        /// <summary>
        /// Inserts every row in <paramref name="data"/> into the target table.
        /// Column order must match table schema (same as VBA .Fields(0..N)).
        /// </summary>
        public static async Task<int> UploadRowsAsync(string dbPath, UploaderType type, DataTable data)
        {
            return await Task.Run(() =>
            {
                int count = 0;
                using var cn = new OleDbConnection(BuildConnectionString(dbPath));
                cn.Open();

                // Build parameterised INSERT once
                var colNames = new List<string>();
                var paramNames = new List<string>();
                foreach (DataColumn col in data.Columns)
                {
                    colNames.Add($"[{col.ColumnName}]");
                    paramNames.Add("?");
                }
                string sql = $"INSERT INTO {TableName(type)} ({string.Join(",", colNames)}) VALUES ({string.Join(",", paramNames)})";

                using var cmd = new OleDbCommand(sql, cn);
                foreach (DataColumn col in data.Columns)
                    cmd.Parameters.Add(new OleDbParameter(col.ColumnName, OleDbType.VarChar));

                foreach (DataRow row in data.Rows)
                {
                    for (int i = 0; i < data.Columns.Count; i++)
                        cmd.Parameters[i].Value = row[i] ?? DBNull.Value;
                    cmd.ExecuteNonQuery();
                    count++;
                }
                return count;
            });
        }

        // ── UPDATE SINGLE ROW (row-level edit from the preview grid) ────────
        /// <summary>
        /// Updates the row in the database that matches <paramref name="row"/>'s
        /// ORIGINAL (pre-edit) values, setting every column to the row's current
        /// (edited) values. Call before <c>row.AcceptChanges()</c>.
        /// </summary>
        public static async Task UpdateRowAsync(string dbPath, UploaderType type, DataRow row)
        {
            await Task.Run(() =>
            {
                using var cn = new OleDbConnection(BuildConnectionString(dbPath));
                cn.Open();

                var setClauses = new List<string>();
                var parameters = new List<OleDbParameter>();
                foreach (DataColumn col in row.Table.Columns)
                {
                    setClauses.Add($"[{col.ColumnName}] = ?");
                    parameters.Add(new OleDbParameter(col.ColumnName, row[col] ?? DBNull.Value));
                }

                var (whereClause, whereParams) = BuildRowWhereClause(row);
                string sql = $"UPDATE {TableName(type)} SET {string.Join(",", setClauses)} WHERE {whereClause}";

                using var cmd = new OleDbCommand(sql, cn);
                cmd.Parameters.AddRange(parameters.ToArray());
                cmd.Parameters.AddRange(whereParams.ToArray());
                cmd.ExecuteNonQuery();
            });
        }

        // ── DELETE SINGLE ROW (row-level delete from the preview grid) ──────
        public static async Task DeleteRowAsync(string dbPath, UploaderType type, DataRow row)
        {
            await Task.Run(() =>
            {
                using var cn = new OleDbConnection(BuildConnectionString(dbPath));
                cn.Open();

                var (whereClause, whereParams) = BuildRowWhereClause(row);
                string sql = $"DELETE FROM {TableName(type)} WHERE {whereClause}";

                using var cmd = new OleDbCommand(sql, cn);
                cmd.Parameters.AddRange(whereParams.ToArray());
                cmd.ExecuteNonQuery();
            });
        }

        /// <summary>Builds a WHERE clause that matches a row's ORIGINAL values across every column.</summary>
        private static (string Clause, List<OleDbParameter> Parameters) BuildRowWhereClause(DataRow row)
        {
            var clauses = new List<string>();
            var parameters = new List<OleDbParameter>();
            foreach (DataColumn col in row.Table.Columns)
            {
                var value = row.RowState == DataRowState.Detached
                    ? row[col]
                    : row[col, DataRowVersion.Original];

                if (value == null || value == DBNull.Value)
                {
                    clauses.Add($"[{col.ColumnName}] IS NULL");
                }
                else
                {
                    clauses.Add($"[{col.ColumnName}] = ?");
                    parameters.Add(new OleDbParameter(col.ColumnName, value));
                }
            }
            return (string.Join(" AND ", clauses), parameters);
        }

        // ── FETCH DAILY (preview / verification) ─────────────────────────────
        public static async Task<DataTable> FetchDailyAsync(string dbPath, UploaderType type, DateTime date)
        {
            return await Task.Run(() =>
            {
                using var cn = new OleDbConnection(BuildConnectionString(dbPath));
                cn.Open();
                string sql = $"SELECT * FROM {TableName(type)} WHERE DTE = #{date:MM/dd/yyyy}#{SegmentFilterClause(type)}";
                using var adapter = new OleDbDataAdapter(sql, cn);
                var dt = new DataTable();
                adapter.Fill(dt);
                return dt;
            });
        }

        // ── FETCH MONTHLY ─────────────────────────────────────────────────────
        public static async Task<DataTable> FetchMonthlyAsync(string dbPath, UploaderType type, DateTime start, DateTime end)
        {
            return await Task.Run(() =>
            {
                using var cn = new OleDbConnection(BuildConnectionString(dbPath));
                cn.Open();
                string sql = $"SELECT * FROM {TableName(type)} WHERE DTE Between #{start:MM/dd/yyyy}# And #{end:MM/dd/yyyy}#{SegmentFilterClause(type)} ORDER BY DTE ASC";
                using var adapter = new OleDbDataAdapter(sql, cn);
                var dt = new DataTable();
                adapter.Fill(dt);
                return dt;
            });
        }

        // ── TEST CONNECTION ───────────────────────────────────────────────────
        public static async Task<bool> TestConnectionAsync(string dbPath)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var cn = new OleDbConnection(BuildConnectionString(dbPath));
                    cn.Open();
                    return cn.State == ConnectionState.Open;
                });
            }
            catch { return false; }
        }
    }
}