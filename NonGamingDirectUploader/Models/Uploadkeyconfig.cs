using System.Collections.Generic;

namespace NonGamingDirectUploader.Models
{
    /// <summary>
    /// Defines, per module, which columns together identify "the same record"
    /// for overwrite purposes on upload. This is NOT an Access primary key —
    /// no schema change is required. It's purely an app-level rule used to
    /// scope deletes so that uploading a record only replaces the matching
    /// existing record(s), not every row for that date.
    ///
    /// IMPORTANT: FnB below is a BEST-GUESS default (DTE + Rev_Center) based
    /// on PreviewColumns in UploaderViewModel.cs. It has NOT been confirmed.
    /// If Rev_Center is not unique per date (e.g. multiple rows per outlet
    /// for different comp types/currencies), this key is too broad and will
    /// reproduce the same overwrite bug for F&amp;B specifically. Confirm and
    /// update before relying on this in production.
    ///
    /// The three Online Gaming entries below (VirtualGames/SportsBook/
    /// FUNaloMAX) are ALSO BEST-GUESS defaults — DTE + one category column —
    /// based on the placeholder PreviewColumns added for these modules.
    /// Confirm the real column names/uniqueness before relying on this.
    /// </summary>
    public static class UploadKeyConfig
    {
        private static readonly Dictionary<UploaderType, string[]> _map = new()
        {
            // ── NonGaming ────────────────────────────────────────────────────
            { UploaderType.Others,     new[] { "DTE", "Dept_Type", "Dept_Desc" } },
            { UploaderType.Hotel,      new[] { "DTE", "Area_Type", "Description_Type" } },
            { UploaderType.Visitation, new[] { "DTE" } },

            // NOT YET CONFIRMED — see warning above.
            { UploaderType.FnB,        new[] { "DTE", "Rev_Center" } },

            // ── Gaming ───────────────────────────────────────────────────────
            { UploaderType.Mass,       new[] { "DTE", "Pit", "PITID", "TABLE", "GAME", "Segment" } },
            { UploaderType.VIP,        new[] { "DTE", "VIP_Name", "Commission_Type", "Curr" } },
            { UploaderType.Junket,     new[] { "DTE", "VIP_Name", "Commission_Type", "Curr" } },

            // ── Online Gaming — NOT YET CONFIRMED, see warning above. ────────
            { UploaderType.VirtualGames, new[] { "Dte", "Brand", "Provider", "Wager", "Win" } },
            { UploaderType.SportsBook,   new[] { "Dte", "Wager", "Win" } },
            { UploaderType.FUNaloMAX,    new[] { "Dte", "GameType", "GameName", "Wager", "Win", "Payout" } },
        };

        /// <summary>
        /// Returns the logical key columns for a module. Falls back to just
        /// "DTE" if a module has no entry — callers should treat that
        /// fallback as a signal the config is incomplete, since a DTE-only
        /// key behaves like the old whole-day delete.
        /// </summary>
        public static string[] GetKeyColumns(UploaderType type)
            => _map.TryGetValue(type, out var cols) ? cols : new[] { "DTE" };
    }
}