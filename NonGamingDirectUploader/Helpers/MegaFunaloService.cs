using System;
using System.Data;
using System.Data.OleDb;
using System.Threading.Tasks;

namespace NonGamingDirectUploader.Helpers
{
    /// <summary>
    /// MegaFunalo is a separate aggregate table holding ONE row per date with
    /// the TOTAL Wager / Win / Payout summed across every FUNaloMAX category
    /// (Table Games, E-Games, Bingo, E-Bingo) for that date. It is updated
    /// automatically whenever a FUNaloMAX category is submitted (see
    /// FUNaloMAXViewModel.AfterCategorySubmitAsync) — there is no separate
    /// manual upload step for it, and no user input beyond what's already
    /// typed into the FUNaloMAX category boxes.
    ///
    /// CONFIRMED schema (Mega_Funalo table, per Access Designer view):
    ///   Dte            Date/Time
    ///   Trans Country  Short Text   (column name HAS a space — must be bracketed in SQL)
    ///   Game Type      Long Text    (column name HAS a space — must be bracketed in SQL)
    ///   Game Name      Long Text    (column name HAS a space — must be bracketed in SQL)
    ///   Wager          Number
    ///   Payout         Number
    ///   Win            Number
    ///
    /// ASSUMPTIONS (please confirm before relying on this in production):
    ///   - MegaFunalo lives in the SAME database as FUNaloMAX
    ///     (OtherGaming_DB.accdb per DatabaseConfig).
    ///   - GameType ("Live Baccarat") and GameName ("Baccarat A") are FIXED
    ///     constant values, taken from the sample MegaFunalo row provided —
    ///     there is currently no other source that would tell this app what
    ///     they should be per-date.
    ///   - TransCountry has no known source yet and is left blank.
    ///
    /// FIX: the INSERT statement's column list uses "Trans Country" /
    /// "Game Type" / "Game Name" — these names are correct (confirmed
    /// against the real table), but Access/Jet SQL requires an identifier
    /// containing a space to be wrapped in square brackets, or the engine
    /// reads it as two separate tokens and throws
    /// "OleDbException: Syntax error in INSERT INTO statement." (exactly
    /// what was happening). Every column in the INSERT is now bracketed —
    /// this is always safe in Access SQL whether or not a name has a space,
    /// so it also protects against the same class of bug for Wager/Payout/
    /// Win/Dte even though those happen to be single words today.
    /// </summary>
    public static class MegaFunaloService
    {
        private const string TableName = "Mega_Funalo";
        private const string FixedGameType = "Live Baccarat";
        private const string FixedGameName = "Baccarat A";

        private static string BuildConnectionString(string dbPath)
            => $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={dbPath}";

        /// <summary>
        /// Replaces the single MegaFunalo row for <paramref name="date"/> with
        /// the given totals — same key-scoped (by Dte), overwrite-safe pattern
        /// used everywhere else in the app.
        /// </summary>
        public static async Task UpsertTotalsAsync(string dbPath, DateTime date, double totalWager, double totalWin, double totalPayout)
        {
            await Task.Run(() =>
            {
                using var cn = new OleDbConnection(BuildConnectionString(dbPath));
                cn.Open();

                using (var del = new OleDbCommand($"DELETE * FROM {TableName} WHERE Dte = #{date:MM/dd/yyyy}#", cn))
                    del.ExecuteNonQuery();

                var sql = $"INSERT INTO {TableName} ([Dte], [Trans Country], [Game Type], [Game Name], [Wager], [Payout], [Win]) " +
                          "VALUES (?, ?, ?, ?, ?, ?, ?)";
                using var cmd = new OleDbCommand(sql, cn);
                cmd.Parameters.Add(new OleDbParameter("Dte", date.Date));
                cmd.Parameters.Add(new OleDbParameter("TransCountry", DBNull.Value));
                cmd.Parameters.Add(new OleDbParameter("GameType", FixedGameType));
                cmd.Parameters.Add(new OleDbParameter("GameName", FixedGameName));
                cmd.Parameters.Add(new OleDbParameter("Wager", totalWager));
                cmd.Parameters.Add(new OleDbParameter("Payout", totalPayout));
                cmd.Parameters.Add(new OleDbParameter("Win", totalWin));
                cmd.ExecuteNonQuery();
            });
        }

        /// <summary>Fetches the MegaFunalo row(s) for one date, for the Data Viewer tab.</summary>
        public static async Task<DataTable> FetchDailyAsync(string dbPath, DateTime date)
        {
            return await Task.Run(() =>
            {
                using var cn = new OleDbConnection(BuildConnectionString(dbPath));
                cn.Open();
                string sql = $"SELECT * FROM {TableName} WHERE Dte = #{date:MM/dd/yyyy}#";
                using var adapter = new OleDbDataAdapter(sql, cn);
                var dt = new DataTable();
                adapter.Fill(dt);
                return dt;
            });
        }
    }
}