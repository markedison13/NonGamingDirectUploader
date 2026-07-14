using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NonGamingDirectUploader.Views
{
    /// <summary>
    /// Generic "edit one record" dialog. Builds an editable field for every
    /// column in the row's table so it works for any of the four uploader
    /// schemas without dedicated per-module UI.
    /// </summary>
    public partial class EditRecordWindow : Window
    {
        private readonly DataRow _row;
        private readonly Dictionary<string, TextBox> _editors = new();

        public bool Saved { get; private set; }

        public EditRecordWindow(DataRow row)
        {
            InitializeComponent();
            _row = row;
            BuildFields();
        }

        private void BuildFields()
        {
            foreach (DataColumn col in _row.Table.Columns)
            {
                var label = new TextBlock
                {
                    Text = col.ColumnName,
                    Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush"),
                    FontSize = 11,
                    Margin = new Thickness(0, 10, 0, 4)
                };

                var box = new TextBox
                {
                    Text = _row[col]?.ToString() ?? "",
                    Tag = col
                };

                _editors[col.ColumnName] = box;
                FieldsPanel.Children.Add(label);
                FieldsPanel.Children.Add(box);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (DataColumn col in _row.Table.Columns)
                {
                    var text = _editors[col.ColumnName].Text;
                    _row[col] = ConvertValue(text, col.DataType);
                }
                Saved = true;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save: {ex.Message}", "Edit Record",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Saved = false;
            DialogResult = false;
        }

        private static object ConvertValue(string text, Type targetType)
        {
            if (string.IsNullOrWhiteSpace(text))
                return DBNull.Value;

            if (targetType == typeof(string))
                return text;

            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlying == typeof(DateTime))
                return DateTime.Parse(text);
            if (underlying == typeof(bool))
                return bool.Parse(text);

            // Numeric types
            return Convert.ChangeType(text, underlying);
        }
    }
}
