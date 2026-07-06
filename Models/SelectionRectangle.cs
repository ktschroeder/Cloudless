using System.Windows;

namespace Cloudless.Models
{
    /// <summary>
    /// Represents a selection rectangle for cropping, with multiple coordinate system representations.
    /// </summary>
    public struct SelectionRectangle
    {
        /// <summary>
        /// The selection rectangle in window display coordinate space (where user draws).
        /// </summary>
        public Rect WindowCoordinates { get; set; }

        /// <summary>
        /// The selection rectangle converted to actual image pixel coordinates.
        /// This represents the portion of the image that will be visible after cropping.
        /// </summary>
        public Rect ImagePixelCoordinates { get; set; }

        /// <summary>
        /// The width the ImageDisplay element should be set to after cropping.
        /// This defines the visible "render width" of the image.
        /// </summary>
        public double CropRenderWidth { get; set; }

        /// <summary>
        /// The height the ImageDisplay element should be set to after cropping.
        /// This defines the visible "render height" of the image.
        /// </summary>
        public double CropRenderHeight { get; set; }

        /// <summary>
        /// The X pan offset (imageTranslateTransform.X) after applying the crop.
        /// This positions the image so the selected area is visible.
        /// </summary>
        public double CropPanX { get; set; }

        /// <summary>
        /// The Y pan offset (imageTranslateTransform.Y) after applying the crop.
        /// This positions the image so the selected area is visible.
        /// </summary>
        public double CropPanY { get; set; }

        /// <summary>
        /// Gets whether this selection represents a valid crop (non-zero dimensions).
        /// </summary>
        public bool IsValid => CropRenderWidth > 0 && CropRenderHeight > 0;
    }
}
