using System.Globalization;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Cloudless
{
    public sealed class AnimatedThumbnailOverlay : FrameworkElement
    {
        private static readonly Brush BadgeBrush = CreateFrozenBrush(Color.FromArgb(215, 14, 14, 14));
        private static readonly Brush TextBrush = CreateFrozenBrush(Colors.White);

        public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
            nameof(IsAnimated),
            typeof(bool),
            typeof(AnimatedThumbnailOverlay),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool IsAnimated
        {
            get => (bool)GetValue(IsAnimatedProperty);
            set => SetValue(IsAnimatedProperty, value);
        }

        public AnimatedThumbnailOverlay()
        {
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (!IsAnimated || ActualWidth <= 0 || ActualHeight <= 0)
                return;

            double fontSize = Math.Clamp(ActualHeight * 0.075, 8, 10);
            var text = new FormattedText(
                "? ANIM",
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Semibold"),
                fontSize,
                TextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            const double horizontalPadding = 5;
            const double verticalPadding = 2;
            double badgeWidth = text.WidthIncludingTrailingWhitespace + horizontalPadding * 2;
            double badgeHeight = text.Height + verticalPadding * 2;
            double left = Math.Max(2, ActualWidth - badgeWidth - 4);
            var badge = new Rect(left, 4, Math.Min(badgeWidth, ActualWidth - left - 2), badgeHeight);

            drawingContext.DrawRoundedRectangle(BadgeBrush, null, badge, 4, 4);
            drawingContext.DrawText(text, new Point(badge.Left + horizontalPadding, badge.Top + verticalPadding));
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
