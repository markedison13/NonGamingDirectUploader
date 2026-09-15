using NonGamingDirectUploader.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NonGamingDirectUploader.Views
{
    /// <summary>
    /// One Online Gaming module's Uploader ONLY — fixed category boxes
    /// (Wager/Win, plus a derived Payout for FUNaloMAX), Submit and Submit
    /// All. No data grid here; that's the separate Data Viewer's job (the
    /// same UploaderPage used by every other module — see MainWindow).
    /// One instance of this page per module (Table Games / SportsBook /
    /// FUNaloMAX), each bound to its own OnlineGamingUploaderViewModel.
    /// </summary>
    public partial class OnlineGamingUploaderPage : UserControl
    {
        private OnlineGamingUploaderViewModel? _vm;

        public OnlineGamingUploaderPage()
        {
            InitializeComponent();
        }

        public void SetViewModel(OnlineGamingUploaderViewModel vm)
        {
            _vm = vm;
            DataContext = vm;

            TitleBlock.Text = $"{vm.DisplayTitle} Uploader";
            SubtitleBlock.Text = $"Enter Wager/Win for each {vm.DisplayTitle} category. Date defaults to yesterday but can be changed below.";

            var color = (Color)ColorConverter.ConvertFromString(vm.AccentHex);
            AccentBar.Background = new SolidColorBrush(color);

            LogList.ItemsSource = vm.LogLines;

            // Only FUNaloMAX also updates the MegaFunalo aggregate on submit.
            FooterNoteText.Visibility = vm.CategoriesHavePayout ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void SubmitCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_vm != null && sender is Button btn && btn.Tag is CategoryEntryViewModel entry)
                await _vm.SubmitCategoryAsync(entry);
        }

        private async void SubmitAll_Click(object sender, RoutedEventArgs e)
        {
            if (_vm != null) await _vm.SubmitAllAsync();
        }
    }
}