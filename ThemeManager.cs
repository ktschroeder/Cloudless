using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Cloudless
{
    public static class ThemeManager
    {
        // themeName: "Light" or "Dark"
        public static void ApplyTheme(string themeName)
        {
            if (string.IsNullOrEmpty(themeName)) themeName = "Light";

            Color windowBg;
            Color secondaryBg;
            Color primaryFg;
            Color secondaryFg;
            Color buttonBg;
            Color buttonBorder;
            Color primaryBtnBg;
            Color primaryBtnBorder;
            Color buttonHover;
            Color buttonPressed;
            Color primaryBtnHover;
            Color primaryBtnPressed;

            if (themeName == "Dark")
            {
                windowBg = (Color)ColorConverter.ConvertFromString("#FF1E1E1E");
                secondaryBg = (Color)ColorConverter.ConvertFromString("#FF2A2A2A");
                primaryFg = (Color)ColorConverter.ConvertFromString("#FFF0F0F0");
                secondaryFg = (Color)ColorConverter.ConvertFromString("#FFCCCCCC");
                buttonBg = (Color)ColorConverter.ConvertFromString("#FF2A2A2A");
                buttonBorder = (Color)ColorConverter.ConvertFromString("#FF3A3A3A");
                primaryBtnBg = (Color)ColorConverter.ConvertFromString("#FF3A3F4A");
                primaryBtnBorder = (Color)ColorConverter.ConvertFromString("#FF4A5160");
                buttonHover = (Color)ColorConverter.ConvertFromString("#FF333333");
                buttonPressed = (Color)ColorConverter.ConvertFromString("#FF2A2A2A");
                primaryBtnHover = (Color)ColorConverter.ConvertFromString("#FF4A5160");
                primaryBtnPressed = (Color)ColorConverter.ConvertFromString("#FF404652");
            }
            else
            {
                windowBg = (Color)ColorConverter.ConvertFromString("#FFF9F9F9");
                secondaryBg = (Color)ColorConverter.ConvertFromString("#FFF0F0F0");
                primaryFg = (Color)ColorConverter.ConvertFromString("#FF222222");
                secondaryFg = (Color)ColorConverter.ConvertFromString("#FF555555");
                buttonBg = (Color)ColorConverter.ConvertFromString("#FFEDEDED");
                buttonBorder = (Color)ColorConverter.ConvertFromString("#FFD0D0D0");
                primaryBtnBg = (Color)ColorConverter.ConvertFromString("#FFDDE6F3");
                primaryBtnBorder = (Color)ColorConverter.ConvertFromString("#FFB5C7E3");
                buttonHover = (Color)ColorConverter.ConvertFromString("#FFE0E0E0");
                buttonPressed = (Color)ColorConverter.ConvertFromString("#FFD6D6D6");
                primaryBtnHover = (Color)ColorConverter.ConvertFromString("#FFC9D7F0");
                primaryBtnPressed = (Color)ColorConverter.ConvertFromString("#FFB7CBE8");
            }

            // Update Color resources so DynamicResource bindings trigger updates
            Application.Current.Resources["WindowBackgroundColor"] = windowBg;
            Application.Current.Resources["SecondaryBackgroundColor"] = secondaryBg;
            Application.Current.Resources["PrimaryForegroundColor"] = primaryFg;
            Application.Current.Resources["SecondaryForegroundColor"] = secondaryFg;
            Application.Current.Resources["ButtonBackgroundColor"] = buttonBg;
            Application.Current.Resources["ButtonBorderColor"] = buttonBorder;
            Application.Current.Resources["PrimaryButtonBackgroundColor"] = primaryBtnBg;
            Application.Current.Resources["PrimaryButtonBorderColor"] = primaryBtnBorder;
            Application.Current.Resources["ButtonHoverBackgroundColor"] = buttonHover;
            Application.Current.Resources["ButtonPressedBackgroundColor"] = buttonPressed;
            Application.Current.Resources["PrimaryButtonHoverBackgroundColor"] = primaryBtnHover;
            Application.Current.Resources["PrimaryButtonPressedBackgroundColor"] = primaryBtnPressed;

            // Create new brush instances with Freeze set to false to allow updates
            var windowBgBrush = new SolidColorBrush(windowBg);
            var secondaryBgBrush = new SolidColorBrush(secondaryBg);
            var primaryFgBrush = new SolidColorBrush(primaryFg);
            var secondaryFgBrush = new SolidColorBrush(secondaryFg);
            var buttonBgBrush = new SolidColorBrush(buttonBg);
            var buttonBorderBrush = new SolidColorBrush(buttonBorder);
            var primaryBtnBgBrush = new SolidColorBrush(primaryBtnBg);
            var primaryBtnBorderBrush = new SolidColorBrush(primaryBtnBorder);
            var buttonHoverBrush = new SolidColorBrush(buttonHover);
            var buttonPressedBrush = new SolidColorBrush(buttonPressed);
            var primaryBtnHoverBrush = new SolidColorBrush(primaryBtnHover);
            var primaryBtnPressedBrush = new SolidColorBrush(primaryBtnPressed);

            // Replace brushes in application resources
            Application.Current.Resources["WindowBackground"] = windowBgBrush;
            Application.Current.Resources["SecondaryBackground"] = secondaryBgBrush;
            Application.Current.Resources["PrimaryForeground"] = primaryFgBrush;
            Application.Current.Resources["SecondaryForeground"] = secondaryFgBrush;
            Application.Current.Resources["ButtonBackgroundBrush"] = buttonBgBrush;
            Application.Current.Resources["ButtonBorderBrush"] = buttonBorderBrush;
            Application.Current.Resources["PrimaryButtonBackgroundBrush"] = primaryBtnBgBrush;
            Application.Current.Resources["PrimaryButtonBorderBrush"] = primaryBtnBorderBrush;
            Application.Current.Resources["ButtonHoverBackgroundBrush"] = buttonHoverBrush;
            Application.Current.Resources["ButtonPressedBackgroundBrush"] = buttonPressedBrush;
            Application.Current.Resources["PrimaryButtonHoverBackgroundBrush"] = primaryBtnHoverBrush;
            Application.Current.Resources["PrimaryButtonPressedBackgroundBrush"] = primaryBtnPressedBrush;

            // Overlay and accent brushes
            Color overlayBg = (themeName == "Dark") ? (Color)ColorConverter.ConvertFromString("#CC1E1E1E") : (Color)ColorConverter.ConvertFromString("#CCFFFFFF");
            Color overlayFg = (themeName == "Dark") ? (Color)ColorConverter.ConvertFromString("#FFEAEAEA") : (Color)ColorConverter.ConvertFromString("#FF222222");
            Color accentBorder = (themeName == "Dark") ? (Color)ColorConverter.ConvertFromString("#66FFFFFF") : (Color)ColorConverter.ConvertFromString("#33000000");
            Color fadeBase = (themeName == "Dark") ? (Color)ColorConverter.ConvertFromString("#FF000000") : (Color)ColorConverter.ConvertFromString("#FFFFFFFF");

            Application.Current.Resources["OverlayBackground"] = new SolidColorBrush(overlayBg);
            Application.Current.Resources["OverlayForeground"] = new SolidColorBrush(overlayFg);
            Application.Current.Resources["AccentBorderBrush"] = new SolidColorBrush(accentBorder);
            Application.Current.Resources["FadeBaseColor"] = fadeBase;

            // Additional common keys to affect implicit controls
            Application.Current.Resources["ControlBackground"] = new SolidColorBrush(secondaryBg);
            Application.Current.Resources["ControlForeground"] = new SolidColorBrush(primaryFg);
            var textBoxBg = (themeName == "Dark") ? (Color)ColorConverter.ConvertFromString("#FF222222") : Colors.White;
            Application.Current.Resources["TextBoxBackground"] = new SolidColorBrush(textBoxBg);
            Application.Current.Resources["TextBoxForeground"] = new SolidColorBrush(primaryFg);
            Application.Current.Resources["ControlBorderBrush"] = new SolidColorBrush(buttonBorder);

            // Try to override some SystemColors so built-in controls pick up darker backgrounds
            try
            {
                Application.Current.Resources[SystemColors.ControlBrushKey] = new SolidColorBrush(secondaryBg);
                Application.Current.Resources[SystemColors.WindowBrushKey] = new SolidColorBrush(windowBg);
                Application.Current.Resources[SystemColors.ControlTextBrushKey] = new SolidColorBrush(primaryFg);
                Application.Current.Resources[SystemColors.WindowTextBrushKey] = new SolidColorBrush(primaryFg);
                // More system keys that can affect default control chrome (combo box toggle, etc.)
                Application.Current.Resources[SystemColors.ControlLightBrushKey] = new SolidColorBrush(Color.Multiply(secondaryBg, 1.08f));
                Application.Current.Resources[SystemColors.ControlDarkBrushKey] = new SolidColorBrush(Color.Multiply(secondaryBg, 0.9f));
                Application.Current.Resources[SystemColors.HighlightBrushKey] = new SolidColorBrush(primaryBtnBg);
                Application.Current.Resources[SystemColors.HighlightTextBrushKey] = new SolidColorBrush(primaryFg);
            }
            catch (Exception)
            {
                throw;
            }

            // Force refresh of all ContextMenus in the application by invalidating their visual state
            InvalidateContextMenus();

            // Rebind ContextMenu/MenuItem properties to DynamicResource so popups update
            RebindContextMenus();

            // Recreate ContextMenu instances to force fresh visual trees while preserving handlers
            RecreateContextMenus();

            // If a window is a MainWindow, ask it to rebuild its image context menu to ensure named MenuItems/fields are updated
            foreach (Window w in Application.Current.Windows)
            {
                if (w is MainWindow mw)
                {
                    mw.RebuildImageContextMenu();
                }
            }
        }

        private static readonly string _diagLogPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cloudless", "contextmenu-log.txt");

        private static void AppendLog(string msg)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(_diagLogPath);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(_diagLogPath, DateTime.Now.ToString("o") + " " + msg + "\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ThemeManager] Failed to write diag log: " + ex);
            }
        }

        private static void InvalidateContextMenus()
        {
            // Find all ContextMenu instances in all windows and force them to update
            foreach (Window window in Application.Current.Windows)
            {
                InvalidateContextMenusInVisualTree(window);
            }
        }

        private static void InvalidateContextMenusInVisualTree(DependencyObject obj)
        {
            if (obj is ContextMenu contextMenu)
            {
                // Force the ContextMenu to update its visual tree
                // by invalidating measure and arrange
                contextMenu.InvalidateMeasure();
                contextMenu.InvalidateArrange();
                contextMenu.InvalidateVisual();

                // Also invalidate all child elements
                int childCount = VisualTreeHelper.GetChildrenCount(contextMenu);
                for (int i = 0; i < childCount; i++)
                {
                    var child = VisualTreeHelper.GetChild(contextMenu, i) as DependencyObject;
                    if (child != null)
                    {
                        InvalidateVisualTree(child);
                    }
                }
            }

            // Recursively search for ContextMenus in the visual tree
            int count = VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                InvalidateContextMenusInVisualTree(child);
            }
        }

        private static void InvalidateVisualTree(DependencyObject obj)
        {
            if (obj is FrameworkElement element)
            {
                element.InvalidateMeasure();
                element.InvalidateArrange();
                element.InvalidateVisual();
            }

            int count = VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                InvalidateVisualTree(child);
            }
        }

        private static void RefreshBrush(string key)
        {
            if (Application.Current.Resources.Contains(key))
            {
                var obj = Application.Current.Resources[key];
                // If the resource is a SolidColorBrush backed by a Color resource, re-create it so bindings update
                if (obj is SolidColorBrush)
                {
                    var old = (SolidColorBrush)obj;
                    Application.Current.Resources[key] = new SolidColorBrush(old.Color);
                }
            }
        }

        private static void RebindContextMenus()
        {
            foreach (Window window in Application.Current.Windows)
            {
                RebindContextMenusInLogicalTree(window);
            }
        }

        private static void RebindContextMenusInLogicalTree(DependencyObject node)
        {
            if (node is FrameworkElement fe)
            {
                if (fe.ContextMenu != null)
                {
                    RebindContextMenu(fe.ContextMenu);
                }
            }

            // Recurse logical children where possible
            foreach (var child in LogicalTreeHelper.GetChildren(node))
            {
                try
                {
                    if (child is DependencyObject dob)
                    {
                        RebindContextMenusInLogicalTree(dob);
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        private static void RebindContextMenu(ContextMenu contextMenu)
        {
            // Rebind the ContextMenu itself
            contextMenu.SetResourceReference(Control.BackgroundProperty, "SecondaryBackground");
            contextMenu.SetResourceReference(Control.BorderBrushProperty, "PrimaryButtonBorderBrush");

            foreach (var item in contextMenu.Items)
            {
                if (item is MenuItem mi)
                {
                    RebindMenuItem(mi);
                }
                else if (item is Separator)
                {
                    // No-op
                }
            }

            // Attach separator overlay handler
            AttachSeparatorOverlayHandler(contextMenu);
            AppendLog($"RebindContextMenu: attached handlers for menu with {contextMenu.Items.Count} items");
        }

        private static void RebindMenuItem(MenuItem menuItem)
        {
            // Do not force Background to SecondaryBackground to avoid full-bleed boxy items.
            // Only ensure Foreground updates when resources change.
            menuItem.SetResourceReference(Control.ForegroundProperty, "PrimaryForeground");

            // Attach submenu handlers so submenus also render full-width separators
            if (menuItem.HasItems)
            {
                AttachSubmenuHandlers(menuItem);

                foreach (var sub in menuItem.Items)
                {
                    if (sub is MenuItem subMi)
                    {
                        RebindMenuItem(subMi);
                    }
                }
            }
        }

        private static void AttachSubmenuHandlers(MenuItem mi)
        {
            mi.SubmenuOpened -= MenuItem_SubmenuOpened_DrawSeparators;
            mi.SubmenuOpened += MenuItem_SubmenuOpened_DrawSeparators;
            mi.SubmenuClosed -= MenuItem_SubmenuClosed_ClearOverlay;
            mi.SubmenuClosed += MenuItem_SubmenuClosed_ClearOverlay;
        }

        private static void MenuItem_SubmenuClosed_ClearOverlay(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                try
                {
                    var popup = mi.Template.FindName("PART_Popup", mi) as Popup;
                    if (popup?.Child is Border b)
                    {
                        var overlay = FindCanvasInVisualTree(b);
                        if (overlay != null)
                            overlay.Children.Clear();
                    }
                }
                catch { }
            }
        }

        private static void MenuItem_SubmenuOpened_DrawSeparators(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                // Defer until layout complete so submenu item containers exist
                mi.Dispatcher.BeginInvoke((Action)(() =>
                {
                    try
                    {
                        var popup = mi.Template.FindName("PART_Popup", mi) as Popup;
                        if (popup == null)
                            return;

                        // Ensure popup child Border exists
                        if (!(popup.Child is Border border))
                            return;

                        // Ensure overlay canvas exists; if not, create and insert as first child
                        Canvas overlay = FindCanvasInVisualTree(border);
                        if (overlay == null)
                        {
                            var existingChild = border.Child;
                            var grid = new Grid();
                            overlay = new Canvas { Name = "SeparatorOverlay", IsHitTestVisible = false };
                            // Preserve original child
                            border.Child = null;
                            grid.Children.Add(overlay);
                            if (existingChild != null)
                                grid.Children.Add(existingChild);
                            border.Child = grid;
                        }

                        overlay.Children.Clear();

                        double menuWidth = border.ActualWidth;

                        var brush = TryFindResourceBrush(mi, "AccentBorderBrush") ?? TryFindResourceBrush(mi, "PrimaryForeground") ?? new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));

                        // iterate submenu items
                        for (int i = 0; i < mi.Items.Count; i++)
                        {
                            var item = mi.Items[i];
                            if (item is Separator || IsFullWidthSeparatorMenuItem(item))
                            {
                                var container = mi.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                                if (container == null) continue;
                                // compute position relative to border
                                Point topLeft = container.TranslatePoint(new Point(0, 0), border);
                                double y = topLeft.Y + container.ActualHeight / 2.0;

                                var rect = new Rectangle()
                                {
                                    Height = 1,
                                    Width = menuWidth,
                                    Fill = brush,
                                    IsHitTestVisible = false
                                };
                                Canvas.SetLeft(rect, 0);
                                Canvas.SetTop(rect, y - 0.5);
                                overlay.Children.Add(rect);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[ThemeManager] MenuItem_SubmenuOpened_DrawSeparators deferred failed: " + ex);
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        // Public diagnostic string you can inspect from debugger
        public static string LastContextMenuDiagnostic = string.Empty;

        private static void ContextMenu_Opened_DrawSeparators(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu cm)
            {
                // Build diagnostics string as before
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"ContextMenu Owner={GetOwnerName(cm)}");
                sb.AppendLine($"Template={(cm.Template?.GetType().FullName ?? "null")}");
                sb.AppendLine($"Style={(cm.Style?.GetType().FullName ?? "null")}");
                sb.AppendLine($"PlacementTarget={(cm.PlacementTarget?.GetType().FullName ?? "null")}");
                sb.AppendLine($"ActualWidth={cm.ActualWidth}");
                sb.AppendLine($"ActualHeight={cm.ActualHeight}");
                sb.AppendLine($"ItemsCount={cm.Items.Count}");
                sb.AppendLine($"HasSeparatorOverlay={(FindOverlayCanvas(cm) != null)}");

                LastContextMenuDiagnostic = sb.ToString();
                var _break_here_for_diagnostics = LastContextMenuDiagnostic;

                // Defer drawing until after layout so item containers exist
                cm.Dispatcher.BeginInvoke((Action)(() =>
                {
                    try
                    {
                        var overlay = FindOverlayCanvas(cm);
                        if (overlay == null)
                            return;

                        overlay.Children.Clear();

                        double menuWidth = cm.ActualWidth;
                        overlay.Width = menuWidth;
                        overlay.Height = cm.ActualHeight;

                        var brush = TryFindResourceBrush(cm, "AccentBorderBrush");
                        if (brush == null)
                            brush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));

                        // iterate items and find separators
                        for (int i = 0; i < cm.Items.Count; i++)
                        {
                            var item = cm.Items[i];
                            if (item is Separator || IsFullWidthSeparatorMenuItem(item))
                            {
                                // get container
                                var container = cm.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                                if (container == null) continue;
                                // compute position relative to context menu
                                Point topLeft = container.TranslatePoint(new Point(0, 0), cm);
                                double y = topLeft.Y + container.ActualHeight / 2.0; // center line within container

                                var rect = new Rectangle()
                                {
                                    Height = 1,
                                    Width = menuWidth,
                                    Fill = brush,
                                    IsHitTestVisible = false
                                };
                                Canvas.SetLeft(rect, 0);
                                Canvas.SetTop(rect, y - 0.5);
                                overlay.Children.Add(rect);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[ThemeManager] ContextMenu_Opened_DrawSeparators deferred failed: " + ex);
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private static string GetOwnerName(ContextMenu cm)
        {
            try
            {
                // Try to get placement target or owner
                if (cm.PlacementTarget is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
                    return fe.Name;
                if (cm.PlacementTarget != null)
                    return cm.PlacementTarget.ToString();
                return "(unknown)";
            }
            catch { return "(error)"; }
        }

        private static bool IsFullWidthSeparatorMenuItem(object item)
        {
            if (item is MenuItem mi)
            {
                // Prefer an explicit tag we set when creating full-width separators
                if (mi.Tag is string s && s == "FULL_WIDTH_SEPARATOR")
                    return true;
            }
            return false;
        }

        private static Canvas? FindOverlayCanvas(ContextMenu cm)
        {
            // The overlay Canvas should be named "SeparatorOverlay" inside the ContextMenu's template (Border -> Grid -> Canvas)
            try
            {
                // Try template-based lookup first
                var overlay = cm.Template.FindName("SeparatorOverlay", cm) as Canvas;
                if (overlay != null) return overlay;
            }
            catch { }

            // Fallback: search visual tree
            int count = VisualTreeHelper.GetChildrenCount(cm);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(cm, i) as DependencyObject;
                var found = FindCanvasInVisualTree(child);
                if (found != null) return found;
            }
            return null;
        }

        private static Canvas? FindCanvasInVisualTree(DependencyObject node)
        {
            if (node == null) return null;
            if (node is Canvas c && c.Name == "SeparatorOverlay") return c;
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                var found = FindCanvasInVisualTree(child);
                if (found != null) return found;
            }
            return null;
        }

        private static Brush? TryFindResourceBrush(FrameworkElement element, string key)
        {
            try
            {
                var resource = element.TryFindResource(key);
                if (resource is Brush b) return b;
            }
            catch { }
            return null;
        }

        private static void RecreateContextMenus()
        {
            foreach (Window window in Application.Current.Windows)
            {
                RecreateContextMenusInLogicalTree(window);
            }
        }

        private static void RecreateContextMenusInLogicalTree(DependencyObject node)
        {
            if (node is FrameworkElement fe)
            {
                var cm = fe.ContextMenu;
                if (cm != null)
                {
                    // Close if open
                    bool wasOpen = cm.IsOpen;
                    if (wasOpen)
                        cm.IsOpen = false;

                    // Detach and reattach the same ContextMenu instance to force WPF to rebuild its popup/visual tree
                    fe.ContextMenu = null;
                    fe.ContextMenu = cm;
                }
            }

            foreach (var child in LogicalTreeHelper.GetChildren(node))
            {
                try
                {
                    if (child is DependencyObject dob)
                    {
                        RecreateContextMenusInLogicalTree(dob);
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        private static void AttachSeparatorOverlayHandler(ContextMenu cm)
        {
            if (cm == null) return;
            cm.Opened -= ContextMenu_Opened_DrawSeparators;
            cm.Opened += ContextMenu_Opened_DrawSeparators;
            cm.Closed -= ContextMenu_Closed_ClearOverlay;
            cm.Closed += ContextMenu_Closed_ClearOverlay;
        }

        private static void ContextMenu_Closed_ClearOverlay(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu cm)
            {
                var overlay = FindOverlayCanvas(cm);
                if (overlay != null)
                    overlay.Children.Clear();
            }
        }
    }
}
