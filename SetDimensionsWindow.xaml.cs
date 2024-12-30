using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Cloudless
{
    public partial class SetDimensionsWindow : Window
    {
        public int NewWidth;
        public int NewHeight;

        public SetDimensionsWindow(double w, double h)
        {
            InitializeComponent();
            FillDimensionInfo(w, h);
        }
        public void FillDimensionInfo(double w, double h)  // could also just send in dimensions as parameters here
        {
            var currentWidth = (int)w;  // could have off-by-one issues? then could round.
            var currentHeight = (int)h;
            CurrentDimensionsText.Text = $"{currentWidth} X {currentHeight}";

            WidthTextBox.Text = currentWidth.ToString();
            HeightTextBox.Text = currentHeight.ToString();
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { WindowHelper.HandleMouseDown(this, e); }
        private void Window_KeyDown(object sender, KeyEventArgs e) { WindowHelper.HandleKeyDown(this, e); }
        private void Cancel_Click(object sender, RoutedEventArgs e) { WindowHelper.Close_Click(this, e); }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(!int.TryParse(WidthTextBox.Text.Trim(), out int newWidth))
                    throw new Exception("Could not parse width: " + WidthTextBox.Text.Trim() + ". Use an integer such as 500.");
                if (!int.TryParse(HeightTextBox.Text.Trim(), out int newHeight))
                    throw new Exception("Could not parse height: " + HeightTextBox.Text.Trim() + ". Use an integer such as 500.");
                if (newWidth < 25 || newHeight < 25)
                    throw new Exception("Width and height should both be at least 25 pixels.");
                if (newWidth >= 20000 || newHeight >= 20000)
                    throw new Exception("Width and height should both be less than 20000 pixels.");

                NewWidth = newWidth;
                NewHeight = newHeight;

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
