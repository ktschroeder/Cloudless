using Accessibility;
using Microsoft.Win32.SafeHandles;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Cloudless
{
    public partial class QuickCommandWindow : Window
    {
        private MainWindow _mw;
        public QuickCommandWindow(MainWindow mw)
        {
            InitializeComponent();

            _mw = mw;

            Quick1.Text = "C1: " + GetCommandForLabel(1);
            Quick2.Text = "C2: " + GetCommandForLabel(2);
            Quick3.Text = "C3: " + GetCommandForLabel(3);
            Quick4.Text = "C4: " + GetCommandForLabel(4);
            Quick5.Text = "C5: " + GetCommandForLabel(5);
            Quick6.Text = "C6: " + GetCommandForLabel(6);
            Quick7.Text = "C7: " + GetCommandForLabel(7);
            Quick8.Text = "C8: " + GetCommandForLabel(8);
        }
        private string? GetCommand(int commandIndex)
        {
            _mw.LoadUserCommands();
            return _mw.UserCommands[commandIndex - 1];
        }
        private async void QuickButtonClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var textBlock = button.Content as TextBlock;
            string buttonName = textBlock.Name;
            int index = int.Parse(buttonName[buttonName.Length - 1].ToString());

            string? command = GetCommand(index);
            if (!string.IsNullOrEmpty(command))
                await _mw.ExecuteCommand(command);

            Close();
        }
        private string GetCommandForLabel(int commandIndex)
        {
            string? command = GetCommand(commandIndex);
            return string.IsNullOrEmpty(command) ? "(empty)" : command;
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { WindowHelper.HandleMouseDown(this, e); }
        private void Window_KeyDown(object sender, KeyEventArgs e) { WindowHelper.HandleKeyDown(this, e); }
        private void Close_Click(object sender, RoutedEventArgs e) { WindowHelper.Close_Click(this, e); }
    }
}
