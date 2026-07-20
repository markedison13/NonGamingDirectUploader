using System.Data;
using System.Windows;

namespace NonGamingDirectUploader.Views
{
    /// <summary>
    /// Shows the data read back from a filled-in bulk-upload template so the
    /// user can review it before it's sent to the database.
    /// </summary>
    public partial class BulkUploadPreviewWindow : Window
    {
        public bool Confirmed { get; private set; }
        public DataTable Data { get; }

        public BulkUploadPreviewWindow(DataTable data, string displayTitle)
        {
            InitializeComponent();
            Data = data;
            TitleText.Text = $"{displayTitle} — Bulk Upload Preview";
            SubtitleText.Text = $"{data.Rows.Count} row(s) loaded. Review, then click Upload.";
            PreviewGrid.ItemsSource = data.DefaultView;
        }

        private void Upload_Click(object sender, RoutedEventArgs e)
        {
            if (Data.Rows.Count == 0)
            {
                MessageBox.Show("No rows to upload.", "Bulk Upload", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Confirmed = true;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
        }
    }
}