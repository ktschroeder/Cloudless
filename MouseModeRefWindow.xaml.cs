using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Cloudless.ReferenceData;

namespace Cloudless
{
    public partial class MouseModeRefWindow : Window
    {
        public MouseModeRefWindow()
        {
            InitializeComponent();
            SecondaryWindowOpacity.Apply(this);
            LoadReferenceData();
        }

        private void LoadReferenceData()
        {
            List<ReferenceTab> tabs = MouseModeReferenceData.GetTabs();
            const double contentWidth = 700;
            double maxContentHeight = 0;

            foreach (ReferenceTab tabData in tabs)
            {
                double keyWidth = CalculateKeyColumnWidth(tabData.Items);
                double descriptionWidth = contentWidth - keyWidth;
                Grid grid = new();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(keyWidth) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(descriptionWidth) });

                StackPanel content = new();
                if (!string.IsNullOrWhiteSpace(tabData.Description))
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = tabData.Description,
                        Margin = new Thickness(0, 0, 0, 12),
                        TextWrapping = TextWrapping.Wrap,
                        Width = contentWidth
                    });
                }

                int rowIndex = 0;
                foreach (ReferenceItem item in tabData.Items)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    TextBlock inputBlock = new()
                    {
                        Text = item.Key,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 12, 6),
                        TextWrapping = TextWrapping.Wrap
                    };
                    Grid.SetColumn(inputBlock, 0);
                    Grid.SetRow(inputBlock, rowIndex);
                    grid.Children.Add(inputBlock);

                    TextBlock descriptionBlock = new()
                    {
                        Text = item.Description,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 6),
                        MaxWidth = descriptionWidth
                    };
                    Grid.SetColumn(descriptionBlock, 1);
                    Grid.SetRow(descriptionBlock, rowIndex);
                    grid.Children.Add(descriptionBlock);
                    rowIndex++;
                }

                content.Children.Add(grid);
                ScrollViewer scrollViewer = new()
                {
                    Margin = new Thickness(12),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = content
                };
                TabItem tabItem = new()
                {
                    Header = tabData.Header,
                    Margin = new Thickness(1, 0, -1, 0),
                    Content = scrollViewer
                };
                TabControlReference.Items.Add(tabItem);

                scrollViewer.Measure(new Size(contentWidth + 24, double.PositiveInfinity));
                maxContentHeight = Math.Max(maxContentHeight, scrollViewer.DesiredSize.Height);
            }

            ContentArea.Height = maxContentHeight + 20;
            Width = contentWidth + 85;
            Height = maxContentHeight + 140;
            SizeToContent = SizeToContent.Manual;
        }

        private static double CalculateKeyColumnWidth(List<ReferenceItem> items)
        {
            double maxWidth = 0;
            TextBlock measureBlock = new() { FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap };
            foreach (ReferenceItem item in items)
            {
                measureBlock.Text = item.Key;
                measureBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                maxWidth = Math.Max(maxWidth, measureBlock.DesiredSize.Width);
            }

            return maxWidth + 24;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) => WindowHelper.HandleMouseDown(this, e);

        private void Window_KeyDown(object sender, KeyEventArgs e) => WindowHelper.HandleKeyDown(this, e);

        private void Close_Click(object sender, RoutedEventArgs e) => WindowHelper.Close_Click(this, e);

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e) => WindowHelper.HandleMouseWheel(this, e);
    }
}
