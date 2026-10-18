using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Cloudless;

internal sealed class AnnotationControlWindow : Window
{
    private readonly AnnotationOverlayWindow _overlay;
    private readonly Button _eraserButton;

    internal AnnotationControlWindow(MainWindow owner, AnnotationOverlayWindow overlay)
    {
        Owner = owner;
        _overlay = overlay;
        Title = "Annotations";
        Width = 290;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = owner.Left + 20;
        Top = owner.Top + 20;
        SecondaryWindowOpacity.Apply(this);

        StackPanel windowContent = new();
        TextBlock title = new()
        {
            Text = "Annotations",
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryForeground");
        Border titleBar = new()
        {
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            Child = title
        };
        titleBar.SetResourceReference(Border.BackgroundProperty, "SecondaryBackground");
        titleBar.MouseDown += (_, e) => WindowHelper.HandleMouseDown(this, e);
        windowContent.Children.Add(titleBar);

        StackPanel panel = new() { Margin = new Thickness(12) };
        panel.Children.Add(new TextBlock { Text = "Color", Margin = new Thickness(0, 0, 0, 6) });

        WrapPanel colors = new() { Margin = new Thickness(0, 0, 0, 10) };
        AddColorButton(colors, Colors.Red);
        AddColorButton(colors, Colors.DodgerBlue);
        AddColorButton(colors, Colors.LimeGreen);
        AddColorButton(colors, Colors.Yellow);
        AddColorButton(colors, Colors.Magenta);
        AddColorButton(colors, Colors.White);
        AddColorButton(colors, Colors.Black);
        panel.Children.Add(colors);

        _eraserButton = new Button { Content = "Eraser: Off", Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(6) };
        _eraserButton.SetResourceReference(FrameworkElement.StyleProperty, "CloudlessButton");
        _eraserButton.Click += (_, _) =>
        {
            _overlay.SetEraseMode(!_overlay.IsEraseMode);
            UpdateEraserButton();
        };
        panel.Children.Add(_eraserButton);

        TextBlock sizeValue = new() { Text = "Size: 4 px", Margin = new Thickness(0, 0, 0, 2) };
        Slider size = new()
        {
            Minimum = 1,
            Maximum = 30,
            Value = 4,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            IsMoveToPointEnabled = true,
            SmallChange = 1,
            LargeChange = 1,
            Margin = new Thickness(0, 0, 0, 10)
        };
        size.ValueChanged += (_, e) =>
        {
            _overlay.SetStrokeSize(e.NewValue);
            sizeValue.Text = $"Size: {e.NewValue:0} px";
        };
        panel.Children.Add(sizeValue);
        panel.Children.Add(size);

        DockPanel actions = new();
        Button clearButton = new() { Content = "Clear", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 8, 0) };
        clearButton.SetResourceReference(FrameworkElement.StyleProperty, "CloudlessButton");
        clearButton.Click += (_, _) => _overlay.ClearAnnotations();
        Button closeButton = new() { Content = "Close", Padding = new Thickness(10, 5, 10, 5) };
        closeButton.SetResourceReference(FrameworkElement.StyleProperty, "CloudlessPrimaryButton");
        closeButton.Click += (_, _) => Close();
        DockPanel.SetDock(closeButton, Dock.Right);
        actions.Children.Add(closeButton);
        actions.Children.Add(clearButton);
        panel.Children.Add(actions);
        windowContent.Children.Add(panel);

        Border windowBorder = new()
        {
            CornerRadius = new CornerRadius(6),
            Child = windowContent,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 20,
                ShadowDepth = 0,
                Opacity = 0.55
            }
        };
        windowBorder.SetResourceReference(Border.BackgroundProperty, "WindowBackground");
        Grid contentRoot = new() { Margin = new Thickness(16) };
        contentRoot.Children.Add(windowBorder);
        Content = contentRoot;
        KeyDown += (_, e) => WindowHelper.HandleKeyDown(this, e);
    }

    private void AddColorButton(Panel panel, Color color)
    {
        Button button = new()
        {
            Width = 28,
            Height = 28,
            Margin = new Thickness(2),
            Padding = new Thickness(0),
            Background = new SolidColorBrush(color),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            ToolTip = color.ToString()
        };
        button.Click += (_, _) =>
        {
            _overlay.SetColor(color);
            UpdateEraserButton();
        };
        panel.Children.Add(button);
    }

    private void UpdateEraserButton()
    {
        _eraserButton.Content = _overlay.IsEraseMode ? "Draw" : "Erase";
    }

}
