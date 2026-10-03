using System.IO;
using System.Windows;
using System.Windows.Media;

namespace Cloudless
{
    public sealed class VideoThumbnailOverlay : FrameworkElement
    {
        private static readonly Brush RailBrush = CreateFrozenBrush(Color.FromArgb(215, 14, 14, 14));
        private static readonly Brush PerforationBrush = CreateFrozenBrush(Color.FromArgb(225, 225, 225, 215));

        public static readonly DependencyProperty IsVideoProperty = DependencyProperty.Register(
            nameof(IsVideo),
            typeof(bool),
            typeof(VideoThumbnailOverlay),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool IsVideo
        {
            get => (bool)GetValue(IsVideoProperty);
            set => SetValue(IsVideoProperty, value);
        }

        public VideoThumbnailOverlay()
        {
            IsHitTestVisible = false;
        }

        public static bool IsVlcVideoPath(string? path)
        {
            string extension = Path.GetExtension(path ?? string.Empty);
            return extension.Equals(".webm", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (!IsVideo || ActualWidth <= 0 || ActualHeight <= 0)
                return;

            double railHeight = Math.Min(10, ActualHeight * 0.18);
            double perforationHeight = railHeight * 0.5;
            double perforationWidth = Math.Min(8, Math.Max(3, ActualWidth / 22));
            int perforationCount = Math.Max(3, (int)(ActualWidth / 18));
            double spacing = ActualWidth / (perforationCount + 1);

            drawingContext.DrawRectangle(RailBrush, null, new Rect(0, 0, ActualWidth, railHeight));
            drawingContext.DrawRectangle(RailBrush, null, new Rect(0, ActualHeight - railHeight, ActualWidth, railHeight));

            for (int i = 1; i <= perforationCount; i++)
            {
                double x = i * spacing - perforationWidth / 2;
                double y = (railHeight - perforationHeight) / 2;
                var topPerforation = new Rect(x, y, perforationWidth, perforationHeight);
                var bottomPerforation = new Rect(x, ActualHeight - railHeight + y, perforationWidth, perforationHeight);

                drawingContext.DrawRoundedRectangle(PerforationBrush, null, topPerforation, 1, 1);
                drawingContext.DrawRoundedRectangle(PerforationBrush, null, bottomPerforation, 1, 1);
            }
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
