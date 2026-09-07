using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TreeGrid.Wpf.Columns
{
    /// <summary>
    /// A text box that only ever contains a number.
    /// <para>
    /// Input is rejected before it reaches the buffer rather than being corrected
    /// afterwards, so the caret never jumps and the user never sees text that is about
    /// to be thrown away. Paste is filtered through the same rule, since that is the
    /// usual way rubbish gets into a numeric field.
    /// </para>
    /// </summary>
    public class NumericTextBox : TextBox
    {
        public NumericTextBox()
        {
            DataObject.AddPastingHandler(this, OnPasting);
        }

        /// <summary>Permit a decimal separator. False means integers only.</summary>
        public bool AllowDecimals { get; set; } = true;

        /// <summary>Permit a leading negative sign.</summary>
        public bool AllowNegative { get; set; } = true;

        /// <summary>Digits allowed after the separator. Negative means unlimited.</summary>
        public int MaxDecimalDigits { get; set; } = -1;

        private static NumberFormatInfo Format => CultureInfo.CurrentCulture.NumberFormat;

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            base.OnPreviewTextInput(e);

            if (e.Handled)
                return;

            var input = TranslateInput(e.Text);

            if (input == null || !IsValid(Compose(input)))
                e.Handled = true;
            else if (input != e.Text)
            {
                // A typed '.' was translated to the culture separator; insert it
                // ourselves so the original character never lands in the buffer.
                e.Handled = true;
                Insert(input);
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            // Space arrives as text on some layouts and as a key on others.
            if (e.Key == Key.Space)
                e.Handled = true;
        }

        private void OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
            {
                e.CancelCommand();
                return;
            }

            var pasted = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
            var cleaned = Clean(pasted);

            if (string.IsNullOrEmpty(cleaned) || !IsValid(Compose(cleaned)))
            {
                e.CancelCommand();
                return;
            }

            if (cleaned == pasted)
                return;

            // Strip separators and stray characters rather than refusing the paste
            // outright: pasting "1,234.50" from a spreadsheet should work.
            e.CancelCommand();
            Insert(cleaned);
        }

        /// <summary>Maps a typed character to what should actually be inserted, or null to reject.</summary>
        private string TranslateInput(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            if (text.Length == 1 && char.IsDigit(text[0]))
                return text;

            var separator = Format.NumberDecimalSeparator;
            var negative = Format.NegativeSign;

            if (text == separator)
                return AllowDecimals ? text : null;

            // The numpad decimal key produces '.' regardless of culture.
            if (text == "." && separator != ".")
                return AllowDecimals ? separator : null;

            if (text == negative || text == "-")
                return AllowNegative ? negative : null;

            return null;
        }

        /// <summary>Removes group separators and anything else that is not part of a number.</summary>
        private string Clean(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var separator = Format.NumberDecimalSeparator;
            var negative = Format.NegativeSign;
            var builder = new System.Text.StringBuilder(text.Length);

            foreach (var ch in text.Trim())
            {
                if (char.IsDigit(ch))
                {
                    builder.Append(ch);
                    continue;
                }

                var s = ch.ToString();

                if ((s == separator || (s == "." && separator != ".")) && AllowDecimals)
                    builder.Append(separator);
                else if ((s == negative || s == "-") && AllowNegative && builder.Length == 0)
                    builder.Append(negative);
            }

            return builder.ToString();
        }

        private string Compose(string insertion)
        {
            var text = Text ?? string.Empty;
            var start = Math.Min(SelectionStart, text.Length);
            var length = Math.Min(SelectionLength, text.Length - start);

            return text.Substring(0, start) + insertion + text.Substring(start + length);
        }

        private void Insert(string insertion)
        {
            var text = Text ?? string.Empty;
            var start = Math.Min(SelectionStart, text.Length);
            var length = Math.Min(SelectionLength, text.Length - start);

            Text = text.Substring(0, start) + insertion + text.Substring(start + length);
            CaretIndex = start + insertion.Length;
        }

        /// <summary>
        /// Accepts partial input. "-" and "1." are not numbers yet but are valid on the
        /// way to one, and refusing them would make the field impossible to type in.
        /// </summary>
        public bool IsValid(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            var separator = Format.NumberDecimalSeparator;
            var negative = Format.NegativeSign;

            if (text.StartsWith(negative, StringComparison.Ordinal))
            {
                if (!AllowNegative)
                    return false;

                text = text.Substring(negative.Length);
            }

            // A sign anywhere but the front is never valid.
            if (text.Contains(negative) || text.Contains("-"))
                return false;

            var parts = text.Split(new[] { separator }, StringSplitOptions.None);

            if (parts.Length > 2 || (parts.Length == 2 && !AllowDecimals))
                return false;

            if (parts.Any(part => part.Any(ch => !char.IsDigit(ch))))
                return false;

            if (parts.Length == 2 && MaxDecimalDigits >= 0 && parts[1].Length > MaxDecimalDigits)
                return false;

            return true;
        }
    }
}
