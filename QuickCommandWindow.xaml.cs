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
        private int _page = 1;
        private static int LastQuickCommandPage = 1; // global across windows
        public QuickCommandWindow(MainWindow mw)
        {
            InitializeComponent();

            _mw = mw;
            _page = LastQuickCommandPage;
            UpdateTabSelectionVisuals();
            UpdateQuickLabels();
        }
        private string? GetCommand(int globalCommandIndex)
        {
            _mw.LoadUserCommands();
            if (globalCommandIndex - 1 < 0 || globalCommandIndex - 1 >= _mw.UserCommands.Count)
                return null;
            return _mw.UserCommands[globalCommandIndex - 1];
        }
        private async void QuickButtonClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var textBlock = button.Content as TextBlock;
            string buttonName = textBlock.Name;
            int idx = int.Parse(buttonName[buttonName.Length - 1].ToString()); // 1..8

            int globalIndex = (_page - 1) * 8 + idx; // 1..24
            await _mw.RunUserCommand(globalIndex - 1);

            Close();
        }
        private string GetCommandForLabel(int commandIndex)
        {
            int globalIndex = ( _page - 1) * 8 + commandIndex;
            string? command = GetCommand(globalIndex);
            return string.IsNullOrEmpty(command) ? "(empty)" : command;
        }
        private void UpdateQuickLabels()
        {
            for (int i = 1; i <= 8; i++)
            {
                int globalIndex = (_page - 1) * 8 + i;
                string label = $"C{globalIndex}: " + GetCommandForLabel(i);
                switch (i)
                {
                    case 1: Quick1.Text = label; break;
                    case 2: Quick2.Text = label; break;
                    case 3: Quick3.Text = label; break;
                    case 4: Quick4.Text = label; break;
                    case 5: Quick5.Text = label; break;
                    case 6: Quick6.Text = label; break;
                    case 7: Quick7.Text = label; break;
                    case 8: Quick8.Text = label; break;
                }
            }
        }

        private void UpdateTabSelectionVisuals()
        {
            // simple visual state: disable the selected tab button to indicate active
            Tab1.IsEnabled = _page != 1;
            Tab2.IsEnabled = _page != 2;
            Tab3.IsEnabled = _page != 3;
        }

        private void SetPage(int p)
        {
            if (p < 1) p = 1;
            if (p > 3) p = 3;
            _page = p;
            LastQuickCommandPage = p;
            UpdateTabSelectionVisuals();
            UpdateQuickLabels();
        }

        private void TabButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b)
            {
                if (b == Tab1) SetPage(1);
                else if (b == Tab2) SetPage(2);
                else if (b == Tab3) SetPage(3);
            }
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) // wheel up -> previous
                SetPage(_page == 1 ? 3 : _page - 1);
            else
                SetPage(_page == 3 ? 1 : _page + 1);
            e.Handled = true;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // allow digit keys to switch pages when this window has focus
            if (e.Key == Key.D1 || e.Key == Key.NumPad1) { SetPage(1); e.Handled = true; return; }
            if (e.Key == Key.D2 || e.Key == Key.NumPad2) { SetPage(2); e.Handled = true; return; }
            if (e.Key == Key.D3 || e.Key == Key.NumPad3) { SetPage(3); e.Handled = true; return; }

            WindowHelper.HandleKeyDown(this, e);
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { WindowHelper.HandleMouseDown(this, e); }
        private void Close_Click(object sender, RoutedEventArgs e) { WindowHelper.Close_Click(this, e); }
    }
}
