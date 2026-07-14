using NonGamingDirectUploader.Models;
using NonGamingDirectUploader.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NonGamingDirectUploader.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new();
        private readonly UploaderPage _othersPage = new();
        private readonly UploaderPage _fnBPage = new();
        private readonly UploaderPage _hotelPage = new();
        private readonly UploaderPage _visitationPage = new();
        private string _activeTag = "Others";

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            // Bind each page to its view model
            _othersPage.SetViewModel(_vm.OthersVM);
            _fnBPage.SetViewModel(_vm.FnBVM);
            _hotelPage.SetViewModel(_vm.HotelVM);
            _visitationPage.SetViewModel(_vm.VisitationVM);

            PageLabel.Text = "Others Uploader";
            PageHost.Content = _othersPage;
        }

        // ── Navigation ────────────────────────────────────────────────────────
        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                SwitchTo(btn.Tag?.ToString() ?? "Others");
        }

        private void SwitchTo(string tag)
        {
            _activeTag = tag;

            // Reset all nav styles
            NavOthers.Style = (Style)FindResource("NavButton");
            NavFnB.Style = (Style)FindResource("NavButton");
            NavHotel.Style = (Style)FindResource("NavButton");
            NavVisitation.Style = (Style)FindResource("NavButton");

            // Hide all dots
            DotOthers.Visibility = Visibility.Collapsed;
            DotFnB.Visibility = Visibility.Collapsed;
            DotHotel.Visibility = Visibility.Collapsed;
            DotVisitation.Visibility = Visibility.Collapsed;

            // Activate selected
            switch (tag)
            {
                case "Others":
                    PageHost.Content = _othersPage;
                    NavOthers.Style = (Style)FindResource("NavButtonActive");
                    DotOthers.Visibility = Visibility.Visible;
                    PageLabel.Text = "Others Uploader";
                    _vm.ActiveUploader = UploaderType.Others;
                    break;
                case "FnB":
                    PageHost.Content = _fnBPage;
                    NavFnB.Style = (Style)FindResource("NavButtonActive");
                    DotFnB.Visibility = Visibility.Visible;
                    PageLabel.Text = "F&B Uploader";
                    _vm.ActiveUploader = UploaderType.FnB;
                    break;
                case "Hotel":
                    PageHost.Content = _hotelPage;
                    NavHotel.Style = (Style)FindResource("NavButtonActive");
                    DotHotel.Visibility = Visibility.Visible;
                    PageLabel.Text = "Hotel Uploader";
                    _vm.ActiveUploader = UploaderType.Hotel;
                    break;
                case "Visitation":
                    PageHost.Content = _visitationPage;
                    NavVisitation.Style = (Style)FindResource("NavButtonActive");
                    DotVisitation.Visibility = Visibility.Visible;
                    PageLabel.Text = "Visitation Uploader";
                    _vm.ActiveUploader = UploaderType.Visitation;
                    break;
            }
        }

        // ── Property radio ────────────────────────────────────────────────────
        private void Property_Checked(object sender, RoutedEventArgs e)
        {
            var prop = RadioSEC.IsChecked == true ? PropertyType.SEC : PropertyType.SN;
            _vm.OthersVM.Property = prop;
            _vm.FnBVM.Property = prop;
            _vm.HotelVM.Property = prop;
            _vm.VisitationVM.Property = prop;
        }

        // ── Custom window chrome ──────────────────────────────────────────────
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        private void MinBtn_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void MaxBtn_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
               ? WindowState.Normal : WindowState.Maximized;
    }
}
