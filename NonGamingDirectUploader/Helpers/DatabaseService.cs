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
            UploaderType.Others => "Curr_Others",
            UploaderType.FnB => "Curr_FB",
            UploaderType.Hotel => "Curr_Hotel",
            UploaderType.Visitation => "Curr_Other_Stat",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        // ── CHECK EXIST (Daily) ───────────────────────────────────────────────
        /// <summary>Returns true if a record for the given date already exists.</summary>
        public static async Task<bool> DateExistsAsync(string dbPath, UploaderType type, DateTime date)
        {
            return await Task.Run(() =>
            {
                using var cn = new OleDbConnection(BuildConnectionString(dbPath));
                cn.Open();
                string sql = $"SELECT DTE FROM {TableName(type)} WHERE DTE = #{date:MM/dd/yyyy}#";
                using var cmd = new OleDbCommand(sql, cn);
                using var rdr = cmd.ExecuteReader();
                return rdr != null && rdr.HasRows;
            });
        }

        // ── DELETE DAILY ──────────────────────────────────────────────────────
        public static async Task DeleteDailyAsync(string dbPath, UploaderType type, DateTime date)
        {
            await Task.Run(() =>
            {
                using var cn = new OleDbConnection(BuildConnectionString(dbPath));
                cn.Open();
                string sql = $"DELETE * FROM {TableName(type)} WHERE DTE = #{date:MM/dd/yyyy}#";
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
                string sql = $"DELETE * FROM {TableName(type)} WHERE DTE Between #{start:MM/dd/yyyy}# And #{end:MM/dd/yyyy}#";
                using var cmd = new OleDbCommand(sql, cn);
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

        // ── FETCH DAILY (preview / verification) ─────────────────────────────
        public static async Task<DataTable> FetchDailyAsync(string dbPath, UploaderType type, DateTime date)
        {
            return await Task.Run(() =>
            {
                using var cn = new OleDbConnection(BuildConnectionString(dbPath));
                cn.Open();
                string sql = $"SELECT * FROM {TableName(type)} WHERE DTE = #{date:MM/dd/yyyy}#";
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
                string sql = $"SELECT * FROM {TableName(type)} WHERE DTE Between #{start:MM/dd/yyyy}# And #{end:MM/dd/yyyy}# ORDER BY DTE ASC";
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
