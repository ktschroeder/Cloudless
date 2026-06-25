using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Cloudless.Controls
{
    public class FullWidthStackPanel : StackPanel
    {
        protected override Size ArrangeOverride(Size finalSize)
        {
            // Find ancestor ContextMenu to read its Padding
            Thickness menuPadding = new Thickness(0);
            DependencyObject parent = this;
            while (parent != null)
            {
                parent = VisualTreeHelper.GetParent(parent);
                if (parent is ContextMenu cm)
                {
                    menuPadding = cm.Padding;
                    break;
                }
            }

            double y = 0;
            foreach (UIElement child in InternalChildren)
            {
                if (child == null) continue;
                Size childDesired = child.DesiredSize;
                double childHeight = childDesired.Height;

                Rect rect;
                bool isSeparator = child is Separator || (child is MenuItem mi && !mi.HasItems && mi.Icon == null && string.IsNullOrEmpty(mi.Header as string) && !mi.IsEnabled);
                // If the child is a Separator (or our menu-item-as-separator), stretch full width ignoring menu padding
                if (child is Separator || (child is MenuItem && ((MenuItem)child).Style != null && !((MenuItem)child).IsEnabled && ((MenuItem)child).Padding.Left==0))
                {
                    double x = 0 - menuPadding.Left; // start at left edge of outer border
                    double width = finalSize.Width + menuPadding.Left + menuPadding.Right;
                    rect = new Rect(x, y, width, childHeight);
                }
                else
                {
                    rect = new Rect(0, y, finalSize.Width, childHeight);
                }

                child.Arrange(rect);
                y += childHeight;
            }

            return new Size(finalSize.Width, y);
        }
    }
}
