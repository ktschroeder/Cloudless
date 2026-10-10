using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Cloudless
{
    public partial class CommandPaletteControl : UserControl
    {
        private static readonly Brush ValidCommandGreen = CreateValidCommandGreenBrush();

        private readonly MainWindow _mw;

        private static Brush CreateValidCommandGreenBrush()
        {
            var brush = new SolidColorBrush(Color.FromRgb(0, 200, 83));
            brush.Freeze();
            return brush;
        }

        public CommandPaletteControl(MainWindow mw)
        {
            InitializeComponent();
            _mw = mw;
        }

        public TextBox CommandTextBoxControl => CommandTextBox;

        private void CommandTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _mw.UpdateCommandSuggestion();
        }

        private void CommandTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            _mw.UpdateCommandSuggestion();
        }

        public void SetCommandSuggestion(string? suggestion)
        {
            if (string.IsNullOrEmpty(suggestion))
            {
                CommandSuggestionTextBlock.Visibility = Visibility.Collapsed;
                CommandSuggestionTextBlock.Text = string.Empty;
                return;
            }

            CommandSuggestionTextBlock.Inlines.Clear();
            CommandSuggestionTextBlock.Inlines.Add(new Run(CommandTextBox.Text) { Foreground = Brushes.Transparent });
            CommandSuggestionTextBlock.Inlines.Add(new Run(suggestion[CommandTextBox.Text.Length..]));
            CommandSuggestionTextBlock.Visibility = Visibility.Visible;
        }

        public void SetCommandValidity(bool isValid, string indicator)
        {
            bool showIndicator = isValid && indicator != "None";
            CommandTextBox.SetResourceReference(TextBox.ForegroundProperty, "OverlayForeground");
            CommandValidityBorder.SetResourceReference(Border.BorderBrushProperty, "CommandPaletteBackground");
            CommandValidityBorder.Visibility = Visibility.Collapsed;
            CommandValidityCheckmark.Visibility = Visibility.Collapsed;

            if (!showIndicator)
                return;

            switch (indicator)
            {
                case "TextGreen":
                    CommandTextBox.Foreground = ValidCommandGreen;
                    break;
                case "GreenBorder":
                    CommandValidityBorder.BorderBrush = ValidCommandGreen;
                    CommandValidityBorder.Visibility = Visibility.Visible;
                    break;
                case "Checkmark":
                    CommandValidityCheckmark.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void CommandTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            _mw.CommandTextBox_KeyDown(sender, e);
        }

        private void CommandTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            _mw.CommandPaletteTextBox_PreviewKeyDown(sender, e);
        }
    }
}
