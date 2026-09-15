namespace NonGamingDirectUploader.Models
{
    /// <summary>
    /// Describes ONE fixed "box" on an Online Gaming module's Uploader tab
    /// (e.g. "Solaire Online", "Table Games") — see the mockup. These sets
    /// are fixed per module; the person only ever types Wager/Win for each
    /// one, never the category itself.
    ///
    /// DisplayName is what's shown as the box's label in the Uploader tab.
    /// GameName is the value actually written to the database's
    /// Brand/GameName-equivalent column, and shown that way in the Data
    /// Viewer grid — these two intentionally differ for the FUNaloMAX Bingo
    /// categories (uploader box says "Solaire Bingo" / "Solaire E-Bingo",
    /// but the stored/viewed value is "Bingo" / "E-Bingo", matching the
    /// sample FunaloMax table).
    /// </summary>
    public class OnlineGamingCategoryDefinition
    {
        public string DisplayName { get; }
        public string GameName { get; }
        public string? Provider { get; }

        public OnlineGamingCategoryDefinition(string displayName, string gameName, string? provider = null)
        {
            DisplayName = displayName;
            GameName = gameName;
            Provider = provider;
        }
    }

    /// <summary>
    /// The fixed category sets for each Online Gaming module, taken directly
    /// from the sample Virtual Games / SportsBook / FunaloMax tables and
    /// uploader mockup. Edit here if a category is renamed or a new one is
    /// added/removed — nothing else needs to change, since
    /// OnlineGamingUploaderViewModel builds its Categories collection from
    /// these lists.
    /// </summary>
    public static class OnlineGamingCategories
    {
        // "Virtual Games" — labeled "Table Games" on the Uploader tab per the
        // requested tab names.
        public static readonly OnlineGamingCategoryDefinition[] VirtualGames =
        {
            new("Solaire Online", "Solaire Online", "Evolution"),
            new("Virtual Games - GILAS", "Virtual Games - GILAS", "Ugames"),
            new("Solaire E-Bingo", "Solaire E-Bingo"),
        };

        // SportsBook has just one figure per day — no separate categories.
        public static readonly OnlineGamingCategoryDefinition[] SportsBook =
        {
            new("SportsBook", "SportsBook"),
        };

        // FUNaloMAX — every one of these gets a derived Payout (Wager - Win).
        // GameName values ("Table Games"/"E-Games"/"Bingo"/"E-Bingo") match
        // the sample FunaloMax data table exactly; the uploader box labels
        // for the two Bingo categories differ ("Solaire Bingo"/"Solaire
        // E-Bingo") per the mockup.
        public static readonly OnlineGamingCategoryDefinition[] FUNaloMAX =
        {
            new("Table Games", "Table Games"),
            new("E-Games", "E-Games"),
            new("Solaire Bingo", "Bingo"),
            new("Solaire E-Bingo", "E-Bingo"),
        };
    }
}