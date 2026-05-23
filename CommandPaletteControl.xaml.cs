using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Cloudless
{
    public partial class CommandPaletteControl : UserControl
    {
        MainWindow _mw;
        public CommandPaletteControl(MainWindow mw)
        {
            InitializeComponent();
            _mw = mw;
        }

        // Expose the inner TextBox to allow MainWindow to interact with it
        public TextBox CommandTextBoxControl => CommandTextBox;

        // Forward events to MainWindow by default so existing logic can remain in MainWindow.CommandPalette.cs
        private void CommandTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
                _mw.CommandPalette_TextChanged(sender, e);
        }

        private void CommandTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
                _mw.CommandPalette_SelectionChanged(sender, e);
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
