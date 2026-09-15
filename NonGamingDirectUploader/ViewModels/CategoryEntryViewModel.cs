using NonGamingDirectUploader.Models;

namespace NonGamingDirectUploader.ViewModels
{
    /// <summary>
    /// One editable "box" on an Online Gaming Uploader tab (e.g. "Solaire
    /// Online", "Table Games"). The category itself, its Provider (if any),
    /// and whether a derived Payout is shown are all fixed (see
    /// OnlineGamingCategoryDefinition) — the only things the person actually
    /// types are Wager and Win.
    /// </summary>
    public class CategoryEntryViewModel : BaseViewModel
    {
        public string DisplayName { get; }
        public string GameName { get; }
        public string? Provider { get; }

        /// <summary>True only for FUNaloMAX categories — shows the derived, read-only Payout row.</summary>
        public bool ShowPayout { get; }

        private string _wagerText = "";
        public string WagerText
        {
            get => _wagerText;
            set { if (Set(ref _wagerText, value)) OnPropertyChanged(nameof(PayoutDisplay)); }
        }

        private string _winText = "";
        public string WinText
        {
            get => _winText;
            set { if (Set(ref _winText, value)) OnPropertyChanged(nameof(PayoutDisplay)); }
        }

        /// <summary>Read-only, derived Wager - Win — this IS the "twist" for FUNaloMAX categories.</summary>
        public string PayoutDisplay
        {
            get
            {
                if (!ShowPayout) return "";
                double.TryParse(WagerText, out var wager);
                double.TryParse(WinText, out var win);
                return (wager - win).ToString("N2");
            }
        }

        public CategoryEntryViewModel(OnlineGamingCategoryDefinition definition, bool showPayout)
        {
            DisplayName = definition.DisplayName;
            GameName = definition.GameName;
            Provider = definition.Provider;
            ShowPayout = showPayout;
        }
    }
}