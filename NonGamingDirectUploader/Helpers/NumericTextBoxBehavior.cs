using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NonGamingDirectUploader.Helpers
{
    /// <summary>
    /// Attached behavior that restricts a TextBox to numeric input only
    /// (digits, one optional leading minus sign, one optional decimal
    /// point) AND auto-formats the integer part with thousands-separator
    /// commas as the user types (e.g. "1000" displays as "1,000").
    ///
    /// The commas are DISPLAY ONLY. Downstream code that reads
    /// WagerText/WinText (CategoryEntryViewModel.PayoutDisplay,
    /// UploaderViewModel.BuildSingleRowTable) already calls the default
    /// double.TryParse(string, out double) overload, which uses
    /// NumberStyles.Float | NumberStyles.AllowThousands by default — so
    /// "1,000" parses to 1000 exactly like "1000" does. The database only
    /// ever receives that parsed double, never this comma-formatted string,
    /// so no other file needs to change for this.
    ///
    /// Blocks disallowed keystrokes AND pasted text, so it can't be
    /// bypassed via Ctrl+V. Used on the Online Gaming Uploader's Wager/Win
    /// boxes (OnlineGamingPage.xaml / OnlineGamingUploaderPage.xaml).
    ///
    /// Usage in XAML (unchanged from before):
    ///   <TextBox Text="{Binding WagerText, UpdateSourceTrigger=PropertyChanged}"
    ///            helpers:NumericTextBoxBehavior.IsNumericOnly="True"/>
    /// </summary>
    public static class NumericTextBoxBehavior
    {
        public static readonly DependencyProperty IsNumericOnlyProperty =
            DependencyProperty.RegisterAttached(
                "IsNumericOnly",
                typeof(bool),
                typeof(NumericTextBoxBehavior),
                new PropertyMetadata(false, OnIsNumericOnlyChanged));

        public static bool GetIsNumericOnly(DependencyObject obj) => (bool)obj.GetValue(IsNumericOnlyProperty);
        public static void SetIsNumericOnly(DependencyObject obj, bool value) => obj.SetValue(IsNumericOnlyProperty, value);

        // Internal re-entrancy guard — prevents TextChanged from firing
        // again (and fighting the caret) while we're setting Text ourselves
        // during comma formatting.
        private static readonly DependencyProperty IsFormattingProperty =
            DependencyProperty.RegisterAttached(
                "IsFormatting", typeof(bool), typeof(NumericTextBoxBehavior), new PropertyMetadata(false));

        private static bool GetIsFormatting(DependencyObject obj) => (bool)obj.GetValue(IsFormattingProperty);
        private static void SetIsFormatting(DependencyObject obj, bool value) => obj.SetValue(IsFormattingProperty, value);

        private static void OnIsNumericOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox textBox) return;

            if ((bool)e.NewValue)
            {
                textBox.PreviewTextInput += TextBox_PreviewTextInput;
                textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
                textBox.TextChanged += TextBox_TextChanged;
                DataObject.AddPastingHandler(textBox, TextBox_Pasting);
            }
            else
            {
                textBox.PreviewTextInput -= TextBox_PreviewTextInput;
                textBox.PreviewKeyDown -= TextBox_PreviewKeyDown;
                textBox.TextChanged -= TextBox_TextChanged;
                DataObject.RemovePastingHandler(textBox, TextBox_Pasting);
            }
        }

        // Blocks the spacebar so it can never be inserted mid-number.
        private static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                e.Handled = true;
        }

        private static void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = (TextBox)sender;
            // Validate against the RAW (comma-stripped) text — commas are
            // auto-inserted by TextBox_TextChanged below, never typed
            // directly by the user, so a manually-typed "," is rejected
            // here same as a letter would be.
            var proposedRaw = StripCommas(GetProposedText(textBox, e.Text));
            e.Handled = !IsValidRawNumericText(proposedRaw);
        }

        private static void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            var textBox = (TextBox)sender;
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                var pastedText = (string)e.DataObject.GetData(typeof(string));
                var proposedRaw = StripCommas(GetProposedText(textBox, pastedText));
                if (!IsValidRawNumericText(proposedRaw))
                    e.CancelCommand();
                // A valid paste (e.g. "1000" or even "1,000" copied from
                // somewhere) is allowed through as-is; the very next
                // TextChanged pass below re-formats it with commas anyway.
            }
            else
            {
                e.CancelCommand();
            }
        }

        /// <summary>
        /// Re-formats the box's text with thousands-separator commas after
        /// every keystroke/paste, preserving caret position relative to the
        /// end of the text (the usual "typing forward" case stays exactly
        /// right; mid-string edits stay close enough to not be jarring).
        /// </summary>
        private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            if (GetIsFormatting(textBox)) return;

            var distanceFromEnd = textBox.Text.Length - textBox.CaretIndex;
            var formatted = FormatWithCommas(textBox.Text);
            if (formatted == textBox.Text) return;

            SetIsFormatting(textBox, true);
            textBox.Text = formatted;
            var newCaret = Math.Max(0, Math.Min(formatted.Length, formatted.Length - distanceFromEnd));
            textBox.CaretIndex = newCaret;
            SetIsFormatting(textBox, false);
        }

        private static string GetProposedText(TextBox textBox, string newText)
        {
            var text = textBox.Text;
            if (textBox.SelectionLength > 0)
                text = text.Remove(textBox.SelectionStart, textBox.SelectionLength);
            text = text.Insert(textBox.SelectionStart, newText);
            return text;
        }

        private static string StripCommas(string text) => text.Replace(",", "");

        // Allows: "", "-", "12", "12.", "12.5", "-12.5" — any valid
        // IN-PROGRESS state of typing a plain (comma-free) double.
        private static bool IsValidRawNumericText(string rawText)
        {
            if (string.IsNullOrEmpty(rawText)) return true;
            return Regex.IsMatch(rawText, @"^-?\d*\.?\d*$");
        }

        /// <summary>
        /// Inserts thousands-separator commas into the integer part of
        /// <paramref name="text"/> (display only — the underlying bound
        /// value is still comma-containing text, but see the class remarks
        /// for why that's fine). Leaves a trailing "-" or "." alone so the
        /// user can keep typing mid-number without the formatter fighting
        /// them — e.g. typing "1000." doesn't get the "." stripped back off
        /// before they type the decimal digits.
        /// </summary>
        private static string FormatWithCommas(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var raw = StripCommas(text);
            if (raw == "-" || raw == "." || raw == "-.") return raw;

            var isNegative = raw.StartsWith("-");
            if (isNegative) raw = raw.Substring(1);

            var dotIndex = raw.IndexOf('.');
            string integerPart, fractionPart;
            bool hasTrailingDot;

            if (dotIndex >= 0)
            {
                integerPart = raw.Substring(0, dotIndex);
                fractionPart = raw.Substring(dotIndex + 1);
                hasTrailingDot = true;
            }
            else
            {
                integerPart = raw;
                fractionPart = "";
                hasTrailingDot = false;
            }

            if (integerPart.Length == 0)
                integerPart = "0";

            // Group the integer part in 3s: "1000" -> "1,000".
            if (!long.TryParse(integerPart, NumberStyles.None, CultureInfo.InvariantCulture, out var intValue))
                return text; // shouldn't happen — PreviewTextInput already filtered to digits only

            var groupedInteger = intValue.ToString("#,##0", CultureInfo.InvariantCulture);

            var sb = new StringBuilder();
            if (isNegative) sb.Append('-');
            sb.Append(groupedInteger);
            if (hasTrailingDot)
            {
                sb.Append('.');
                sb.Append(fractionPart);
            }
            return sb.ToString();
        }
    }
}