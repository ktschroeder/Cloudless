using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Cloudless.Models;
using Point = System.Windows.Point;

namespace Cloudless
{
    /// <summary>
    /// Handles crop-to-selection functionality including drawing and coordinate conversion.
    /// </summary>
    public partial class MainWindow
    {
        private bool isDrawingSelection = false;
        private Point selectionStart = new Point();
        private Point selectionCurrent = new Point();
        private Rectangle? selectionRectangleVisual = null;
        private Canvas? selectionCanvas = null;

        /// <summary>
        /// Enters selection mode for crop-to-selection. Called when user presses Ctrl+Q.
        /// </summary>
        public async Task EnterSelectionMode()
        {
            if (isDrawingSelection)
            {
                Message("Already in selection mode");
                return;
            }

            if (ImageDisplay.Source == null)
            {
                Message("No image loaded");
                return;
            }

            if (this.WindowState == WindowState.Maximized)
            {
                Message("Cannot create crop selection in maximized window.");
                return;
            }

            // Enter exploration mode first to ensure we have zoom/pan capability
            if (!isExplorationMode)
            {
                EnterExplorationMode(silent: true);
            }

            // Enter crop mode if not already
            if (!isCropMode)
            {
                await ToggleCropMode(true, silent: true);
            }

            isDrawingSelection = true;
            CreateSelectionCanvas();
            Message("Selection mode active - click and drag to select crop area");
        }

        /// <summary>
        /// Creates the canvas overlay for drawing the selection rectangle.
        /// </summary>
        private void CreateSelectionCanvas()
        {
            // Find or create canvas in the visual tree
            if (selectionCanvas == null)
            {
                selectionCanvas = new Canvas
                {
                    Background = Brushes.Transparent,
                    IsHitTestVisible = true
                };
                Grid.SetColumn(selectionCanvas, 0);
                Grid.SetRow(selectionCanvas, 0);
                MyGrid.Children.Add(selectionCanvas);
            }
            else
            {
                selectionCanvas.Children.Clear();
            }
        }

        /// <summary>
        /// Handles mouse down to start selection drawing.
        /// </summary>
        internal void SelectionMode_MouseDown(Point windowPoint)
        {
            if (!isDrawingSelection)
                return;

            selectionStart = windowPoint;
            selectionCurrent = windowPoint;

            // Create the visual rectangle
            if (selectionRectangleVisual != null)
            {
                selectionCanvas?.Children.Remove(selectionRectangleVisual);
            }

            selectionRectangleVisual = new Rectangle
            {
                Fill = Brushes.Transparent,
                Stroke = new SolidColorBrush(Colors.Red),
                StrokeThickness = 1,
                IsHitTestVisible = false
            };

            selectionCanvas?.Children.Add(selectionRectangleVisual);
            UpdateSelectionRectangleVisual();
        }

        /// <summary>
        /// Handles mouse move to update selection rectangle during drawing.
        /// </summary>
        internal void SelectionMode_MouseMove(Point windowPoint)
        {
            if (!isDrawingSelection || selectionRectangleVisual == null)
                return;

            selectionCurrent = windowPoint;
            UpdateSelectionRectangleVisual();
        }

        /// <summary>
        /// Handles mouse up to finalize selection and apply crop.
        /// </summary>
        internal async Task SelectionMode_MouseUp(Point windowPoint)
        {
            if (!isDrawingSelection)
                return;

            selectionCurrent = windowPoint;
            isDrawingSelection = false;

            // Get the final selection rectangle in window coordinates
            Rect windowSelectionRect = GetNormalizedRectangle(selectionStart, selectionCurrent);

            // Clear visual
            if (selectionRectangleVisual != null)
            {
                selectionCanvas?.Children.Remove(selectionRectangleVisual);
                selectionRectangleVisual = null;
            }

            // Convert to image coordinates and apply crop
            if (TryConvertWindowRectToImageCoordinates(windowSelectionRect, out SelectionRectangle selectionData))
            {
                await ApplyCropToSelection(windowSelectionRect);
                Message("Crop applied to selection");
            }
            else
            {
                Message("Selection is outside image bounds"); // TODO unclear edge case where we get here erroneously.
            }
        }

        /// <summary>
        /// Updates the visual representation of the selection rectangle.
        /// The stroke is positioned to project outward (external to the selection).
        /// </summary>
        private void UpdateSelectionRectangleVisual()
        {
            if (selectionRectangleVisual == null)
                return;

            Rect rect = GetNormalizedRectangle(selectionStart, selectionCurrent);

            // Position the rectangle, accounting for the 4px stroke that projects outward
            Canvas.SetLeft(selectionRectangleVisual, rect.Left);
            Canvas.SetTop(selectionRectangleVisual, rect.Top);
            selectionRectangleVisual.Width = rect.Width;
            selectionRectangleVisual.Height = rect.Height;
        }

        /// <summary>
        /// Gets a normalized rectangle from two points (handles negative widths/heights).
        /// </summary>
        private Rect GetNormalizedRectangle(Point point1, Point point2)
        {
            double x = Math.Min(point1.X, point2.X);
            double y = Math.Min(point1.Y, point2.Y);
            double width = Math.Abs(point2.X - point1.X);
            double height = Math.Abs(point2.Y - point1.Y);

            return new Rect(x, y, width, height);
        }

        /// <summary>
        /// Converts a rectangle in window coordinates to crop parameters.
        /// Uses the current rendering state to determine what portion of the image is selected.
        /// </summary>
        private bool TryConvertWindowRectToImageCoordinates(Rect windowRect, out SelectionRectangle result)
        {
            result = default;

            if (ImageDisplay.Source is not BitmapSource bitmap)
                return false;

            // Get ImageDisplay's bounds in window coordinates
            GeneralTransform transform = ImageDisplay.TransformToAncestor(this);
            Rect imageDisplayBounds = transform.TransformBounds(new Rect(0, 0, ImageDisplay.ActualWidth, ImageDisplay.ActualHeight));

            // Convert window selection to ImageDisplay-relative coordinates (in pixels)
            double selX = windowRect.X - imageDisplayBounds.Left;
            double selY = windowRect.Y - imageDisplayBounds.Top;
            double selWidth = windowRect.Width;
            double selHeight = windowRect.Height;

            // Clamp selection to ImageDisplay bounds
            double clampLeft = Math.Max(0, selX);
            double clampTop = Math.Max(0, selY);
            double clampRight = Math.Min(imageDisplayBounds.Width, selX + selWidth);
            double clampBottom = Math.Min(imageDisplayBounds.Height, selY + selHeight);

            if (clampLeft >= clampRight || clampTop >= clampBottom)
                return false;

            // Get current transforms
            double scaleX = imageScaleTransform?.ScaleX ?? 1.0;
            double scaleY = imageScaleTransform?.ScaleY ?? 1.0;
            double panX = imageTranslateTransform?.X ?? 0.0;
            double panY = imageTranslateTransform?.Y ?? 0.0;

            // The transforms are applied as: final_pixel = (source_pixel * scale) + pan
            // So to reverse: source_pixel = (final_pixel - pan) / scale
            double imagePixelLeft = (clampLeft - panX) / scaleX;
            double imagePixelTop = (clampTop - panY) / scaleY;
            double imagePixelRight = (clampRight - panX) / scaleX;
            double imagePixelBottom = (clampBottom - panY) / scaleY;

            // Clamp to actual image bounds
            imagePixelLeft = Math.Max(0, imagePixelLeft);
            imagePixelTop = Math.Max(0, imagePixelTop);
            imagePixelRight = Math.Min(bitmap.PixelWidth, imagePixelRight);
            imagePixelBottom = Math.Min(bitmap.PixelHeight, imagePixelBottom);

            if (imagePixelLeft >= imagePixelRight || imagePixelTop >= imagePixelBottom)
                return false;

            double cropPixelWidth = imagePixelRight - imagePixelLeft;
            double cropPixelHeight = imagePixelBottom - imagePixelTop;

            // For crop, we need to set ImageDisplay.Width/Height to the render dimensions
            // and set pan to show the selected region at the top-left
            result = new SelectionRectangle
            {
                WindowCoordinates = windowRect,
                ImagePixelCoordinates = new Rect(imagePixelLeft, imagePixelTop, cropPixelWidth, cropPixelHeight),
                CropRenderWidth = cropPixelWidth,
                CropRenderHeight = cropPixelHeight,
                CropPanX = -imagePixelLeft * scaleX,
                CropPanY = -imagePixelTop * scaleY
            };

            return result.IsValid;
        }

        /// <summary>
        /// Applies the crop based on the selection rectangle.
        /// </summary>
        private async Task ApplyCropToSelection(SelectionRectangle selection)
        {
            if (!selection.IsValid)
                return;

            if (imageScaleTransform == null || imageTranslateTransform == null)
                return;

            // Ensure we're in crop mode
            if (!isCropMode)
            {
                await ToggleCropMode(true, silent: true);
            }

            // Apply the crop parameters exactly as they are calculated
            ImageDisplay.Width = selection.CropRenderWidth;
            ImageDisplay.Height = selection.CropRenderHeight;
            imageTranslateTransform.X = selection.CropPanX;
            imageTranslateTransform.Y = selection.CropPanY;

            // Update crop mode info to lock in these values so SizeChanged doesn't override them
            UpdateCropModeInfo();

            Message($"Crop applied: render {selection.CropRenderWidth}x{selection.CropRenderHeight}, pan ({selection.CropPanX}, {selection.CropPanY})");
        }

        private async Task ApplyCropToSelection(Rect selection)
        {
            if (imageScaleTransform == null || imageTranslateTransform == null)
                return;

            // Ensure we're in crop mode
            if (!isCropMode)
            {
                await ToggleCropMode(true, silent: true);
            }

            this.Left += selection.Left;
            this.Width = selection.Width;

            this.Top += selection.Top;
            this.Height = selection.Height;

            await ToggleCropMode(false, silent: true);

            // Apply the crop parameters exactly as they are calculated
            //ImageDisplay.Width = selection.CropRenderWidth;
            //ImageDisplay.Height = selection.CropRenderHeight;
            //imageTranslateTransform.X = selection.CropPanX;
            //imageTranslateTransform.Y = selection.CropPanY;

            // Update crop mode info to lock in these values so SizeChanged doesn't override them
            //
            //
            //UpdateCropModeInfo();

            //Message($"Crop applied: render {selection.CropRenderWidth}x{selection.CropRenderHeight}, pan ({selection.CropPanX}, {selection.CropPanY})");
        }

        /// <summary>
        /// Exits selection mode without applying a crop.
        /// </summary>
        public void ExitSelectionMode()
        {
            if (!isDrawingSelection)
                return;

            isDrawingSelection = false;

            if (selectionRectangleVisual != null)
            {
                selectionCanvas?.Children.Remove(selectionRectangleVisual);
                selectionRectangleVisual = null;
            }

            Message("Selection mode cancelled");
        }
    }
}
