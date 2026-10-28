namespace Cloudless.ReferenceData
{
    public static class MouseModeReferenceData
    {
        public static List<ReferenceTab> GetTabs()
        {
            return new List<ReferenceTab>
            {
                new ReferenceTab("All", new List<ReferenceItem>
                {
                    new ReferenceItem("Right-click", "Open the context menu."),
                    new ReferenceItem("Middle-click", "Open the quick command display."),
                    new ReferenceItem("Double middle-click", "Toggle crop mode."),
                    new ReferenceItem("Long-hold middle-click", "Open the mouse mode selector. Angle of release determines choice of mode."),
                    new ReferenceItem("Double left-click", "Toggle maximized state."),
                    new ReferenceItem("(modes that support dragging window): Shift + drag", "Constrain the window movement to a horizontal or vertical line.")
                }, "These inputs work as described across all mouse modes."),
                new ReferenceTab("Standard", new List<ReferenceItem>
                {
                    new ReferenceItem("Left-click + drag", "Move the window."),
                    new ReferenceItem("Ctrl + left-click + drag", "Pan the image or video."),
                    new ReferenceItem("Ctrl + scroll up/down", "Zoom in or out."),
                    new ReferenceItem("Ctrl + Alt + scroll up/down", "Finely zoom in or out."),
                    new ReferenceItem("Scroll up/down", "Load the previous/next image in the current directory/sequence."),
                }, "The default mode, suitable for general use."),
                new ReferenceTab("Exploration Control", new List<ReferenceItem>
                {
                    new ReferenceItem("Left-click + drag", "Pan the image or video."),
                    new ReferenceItem("Scroll up/down", "Zoom in or out."),
                    new ReferenceItem("Hold right-click + scroll up/down", "Finely zoom in or out."),
                }, "Explore an image or video directly with the mouse only."),
                new ReferenceTab("Selection", new List<ReferenceItem>
                {
                    new ReferenceItem("Click", "Select or deselect the window."),
                    new ReferenceItem("Click + drag", "Move the selected window or selected group of windows."),
                    new ReferenceItem("Ctrl + click + drag", "Draw a marquee to select windows within the rectangle."),
                    new ReferenceItem("Scroll", "Cycle the Z-order of Cloudless windows under the cursor."),
                    new ReferenceItem("Ctrl + scroll", "Cycle through pages."),
                }, "Select one or more Cloudless windows, for group operations (commands using the 'sel' keyword act on these windows). Selected windows can be moved together in this mode."),
                new ReferenceTab("Page Control", new List<ReferenceItem>
                {
                    new ReferenceItem("Scroll up/down", "Go forward/backward along pages."),
                    new ReferenceItem("Left-click + hold/drag", "Bring the held window (or selection of windows) with you as you switch pages by scrolling. If a selection contains windows on different pages, their page-dimension proximity is retained. Position movement applies only to the window that was clicked on."),
                }, "Use the mouse wheel to navigate pages while keeping the pointer over a window. Useful for intuitive movement of windows across pages without needing to use commands."),
                new ReferenceTab("Arrangement", new List<ReferenceItem>
                {
                    new ReferenceItem("Click + drag", "Move the window."),
                    new ReferenceItem("Ctrl + click", "Toggle collision avoidance for the window (only collides with other windows that are also opted-in to collisions)."),
                    new ReferenceItem("Ctrl + click and drag", "Move the window with temporary global 2-way collision avoidance, regardless of its saved setting."),
                    new ReferenceItem("Scroll up/down", "Scale the window and its content up or down from its center."),
                    new ReferenceItem("Alt + scroll", "Scale more finely."),
                    new ReferenceItem("Shift + scroll", "Scale more coarsely."),
                }, "Arrange and resize windows. Collision avoidance keeps this window from overlapping other Cloudless windows or exceeding display bounds, making it easy to create precise, gapless arrangements."),
                new ReferenceTab("Annotation", new List<ReferenceItem>
                {
                    new ReferenceItem("Left-click and drag", "Draw/erase an annotation on the window."),
                    new ReferenceItem("Long-hold right-click", "Open annotation controls window to choose a color or size, use the eraser, clear annotations, or close the controls."),
                }, "Draw directly over the window. Annotations remain visible while using other mouse modes, but are lost once a window is closed."),
                new ReferenceTab("Spotlight", new List<ReferenceItem>
                {
                    new ReferenceItem("Scroll up/down", "Increase/decrease the spotlight diameter."),
                    new ReferenceItem("Alt + scroll", "Adjust the spotlight size more finely."),
                    new ReferenceItem("Shift + scroll", "Adjust the spotlight size more coarsely."),
                    new ReferenceItem("Left-click + drag", "Move the window as in Standard mode."),
                    new ReferenceItem("Ctrl left-click + drag", "Pan media as in Standard mode."),
                    new ReferenceItem("Ctrl + scroll", "Zoom media as in Standard mode."),
                }, "Dim the desktop outside a cursor-centered spotlight."),
                new ReferenceTab("Magnifier", new List<ReferenceItem>
                {
                    new ReferenceItem("Scroll up/down", "Increase/decrease desktop magnification."),
                    new ReferenceItem("Hold right-click + scroll up/down", "Increase/decrease the magnifier lens size."),
                    new ReferenceItem("Alt + scroll", "Adjust magnification or lens size more finely."),
                    new ReferenceItem("Shift + scroll", "Adjust magnification or lens size more coarsely."),
                }, "Magnify everything (global, desktop inclusive) around the cursor. Useful to get larger views of UI elements, for instance."),
            };
        }
    }
}
