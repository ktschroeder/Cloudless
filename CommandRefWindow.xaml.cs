using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Cloudless.ReferenceData;

namespace Cloudless
{
    public partial class CommandRefWindow : Window
    {
        public CommandRefWindow()
        {
            InitializeComponent();
            LoadReferenceData();
        }

        private void LoadReferenceData()
        {
            var tabs = CommandReferenceData.GetTabs();

            if (TabControlReference != null)
            {
                TabControlReference.Items.Clear();

                double maxTabContentWidth = 0;
                double maxTabContentHeight = 0;

                foreach (var tabData in tabs)
                {
                    double col1Width = CalculateKeyColumnWidth(tabData.Items);
                    double col2Width = CalculateDescriptionColumnWidth(tabData.Items);

                    Grid measureGrid = new Grid();
                    ColumnDefinition measureCol1 = new ColumnDefinition { Width = new GridLength(col1Width, GridUnitType.Pixel) };
                    ColumnDefinition measureCol2 = new ColumnDefinition { Width = new GridLength(col2Width, GridUnitType.Pixel) };
                    measureGrid.ColumnDefinitions.Add(measureCol1);
                    measureGrid.ColumnDefinitions.Add(measureCol2);

                    int rowIndex = 0;
                    foreach (var item in tabData.Items)
                    {
                        RowDefinition row = new RowDefinition { Height = GridLength.Auto };
                        measureGrid.RowDefinitions.Add(row);

                        TextBlock keyBlock = new TextBlock 
                        { 
                            Text = item.Key,
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(0, 0, 12, 0)
                        };
                        Grid.SetColumn(keyBlock, 0);
                        Grid.SetRow(keyBlock, rowIndex);
                        measureGrid.Children.Add(keyBlock);

                        TextBlock descBlock = new TextBlock 
                        { 
                            Text = item.Description,
                            TextWrapping = TextWrapping.NoWrap
                        };
                        Grid.SetColumn(descBlock, 1);
                        Grid.SetRow(descBlock, rowIndex);
                        measureGrid.Children.Add(descBlock);

                        rowIndex++;
                    }

                    measureGrid.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                    maxTabContentWidth = Math.Max(maxTabContentWidth, measureGrid.DesiredSize.Width);
                }

                maxTabContentWidth += 28;

                // Second pass: build the actual tabs with calculated width
                foreach (var tabData in tabs)
                {
                    TabItem tabItem = new TabItem { Header = tabData.Header, Margin = new Thickness(1, 0, -1, 0) };

                    ScrollViewer scrollViewer = new ScrollViewer 
                    { 
                        Margin = new Thickness(12), 
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto 
                    };

                    StackPanel stackPanel = new StackPanel();

                    if (!string.IsNullOrEmpty(tabData.Description))
                    {
                        TextBlock descBlock = new TextBlock 
                        { 
                            Text = tabData.Description,
                            Margin = new Thickness(0, 0, 0, 10),
                            TextWrapping = TextWrapping.Wrap,
                            Width = maxTabContentWidth
                        };
                        stackPanel.Children.Add(descBlock);
                    }

                    Grid grid = new Grid();

                        
                    double col1Width = CalculateKeyColumnWidth(tabData.Items);
                    double col2Width = CalculateDescriptionColumnWidth(tabData.Items);

                    ColumnDefinition col1 = new ColumnDefinition { Width = new GridLength(col1Width, GridUnitType.Pixel) };
                    ColumnDefinition col2 = new ColumnDefinition { Width = new GridLength(col2Width, GridUnitType.Pixel) };
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);

                    int rowIndex = 0;
                    foreach (var item in tabData.Items)
                    {
                        RowDefinition row = new RowDefinition { Height = GridLength.Auto };
                        grid.RowDefinitions.Add(row);

                        TextBlock cmdBlock = new TextBlock 
                        { 
                            Text = item.Key,
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(0, 0, 12, 0)
                        };
                        Grid.SetColumn(cmdBlock, 0);
                        Grid.SetRow(cmdBlock, rowIndex);
                        grid.Children.Add(cmdBlock);

                        TextBlock cmdDescBlock = new TextBlock 
                        { 
                            Text = item.Description,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = maxTabContentWidth - col1Width - 24
                        };
                        Grid.SetColumn(cmdDescBlock, 1);
                        Grid.SetRow(cmdDescBlock, rowIndex);
                        grid.Children.Add(cmdDescBlock);

                        rowIndex++;
                    }

                    stackPanel.Children.Add(grid);

                        
                    if (!string.IsNullOrEmpty(tabData.Footer))
                    {
                        TextBlock footerBlock = new TextBlock 
                        { 
                            Text = tabData.Footer,
                            Margin = new Thickness(0, 10, 0, 1),
                            TextWrapping = TextWrapping.Wrap,
                            Width = maxTabContentWidth  
                        };
                        stackPanel.Children.Add(footerBlock);
                    }

                    scrollViewer.Content = stackPanel;
                    tabItem.Content = scrollViewer;
                    TabControlReference.Items.Add(tabItem);

                    scrollViewer.Measure(new System.Windows.Size(maxTabContentWidth + 24, double.PositiveInfinity));
                    maxTabContentHeight = Math.Max(maxTabContentHeight, scrollViewer.DesiredSize.Height);
                }

                if (ContentArea != null)
                {
                    ContentArea.Height = maxTabContentHeight;
                }

                const double horizontalPadding = 24; // 12px left + 12px right
                const double headerHeight = 40;
                const double verticalMargins = 20; // margins between elements
                const double closeButtonHeight = 30;

                double finalWidth = maxTabContentWidth + horizontalPadding;
                double finalHeight = maxTabContentHeight + headerHeight + verticalMargins + closeButtonHeight;

                System.Diagnostics.Debug.WriteLine($"CommandRefWindow - maxTabContentWidth: {maxTabContentWidth}, maxTabContentHeight: {maxTabContentHeight}");
                System.Diagnostics.Debug.WriteLine($"CommandRefWindow - finalWidth: {finalWidth}, finalHeight: {finalHeight}");

                this.Width = finalWidth;
                this.Height = finalHeight;
                this.SizeToContent = SizeToContent.Manual; // Lock the size
            }
        }

        private double CalculateKeyColumnWidth(List<ReferenceItem> items)
        {
            // Use a temporary TextBlock to measure text width
            double maxWidth = 0;
            var tempTb = new TextBlock { FontWeight = FontWeights.Bold };

            foreach (var item in items)
            {
                tempTb.Text = item.Key;
                tempTb.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                maxWidth = Math.Max(maxWidth, tempTb.DesiredSize.Width);
            }

            // Add some padding
            return maxWidth + 24;
        }

        private double CalculateDescriptionColumnWidth(List<ReferenceItem> items)
        {
            // Use a temporary TextBlock to measure text width
            double maxWidth = 0;
            var tempTb = new TextBlock { FontWeight = FontWeights.Bold };

            foreach (var item in items)
            {
                tempTb.Text = item.Description;
                tempTb.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                maxWidth = Math.Max(maxWidth, tempTb.DesiredSize.Width);
            }

            // Add some padding
            return maxWidth + 24;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { WindowHelper.HandleMouseDown(this, e); }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            WindowHelper.HandleKeyDown(this, e);
        }

        private void Close_Click(object sender, RoutedEventArgs e) { WindowHelper.Close_Click(this, e); }
    }
}
