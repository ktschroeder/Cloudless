using System.Windows;
using System.Windows.Controls;

namespace Cloudless;

internal static class BulkTagContextMenu
{
    public static ContextMenu Create(Action selectAll, Action clearSelection, Action editTags, Action addBookmarks, Action removeBookmarks)
    {
        var menu = new ContextMenu();

        var selectAllItem = new MenuItem { Header = "Select all" };
        selectAllItem.Click += (_, _) => selectAll();
        menu.Items.Add(selectAllItem);

        var clearSelectionItem = new MenuItem { Header = "Clear selection" };
        clearSelectionItem.Click += (_, _) => clearSelection();
        menu.Items.Add(clearSelectionItem);

        var separator = new MenuItem
        {
            IsEnabled = false,
            Padding = new Thickness(0),
            Tag = "FULL_WIDTH_SEPARATOR",
            Style = (Style)Application.Current.FindResource("ContextMenuFullWidthSeparator")
        };
        menu.Items.Add(separator);

        var editTagsItem = new MenuItem { Header = "Edit tags for selected" };
        editTagsItem.Click += (_, _) => editTags();
        menu.Items.Add(editTagsItem);

        var bookmarkSeparator = new MenuItem
        {
            IsEnabled = false,
            Padding = new Thickness(0),
            Tag = "FULL_WIDTH_SEPARATOR",
            Style = (Style)Application.Current.FindResource("ContextMenuFullWidthSeparator")
        };
        menu.Items.Add(bookmarkSeparator);

        var addBookmarksItem = new MenuItem { Header = "Bookmark selected" };
        addBookmarksItem.Click += (_, _) => addBookmarks();
        menu.Items.Add(addBookmarksItem);

        var removeBookmarksItem = new MenuItem { Header = "Remove bookmarks from selected" };
        removeBookmarksItem.Click += (_, _) => removeBookmarks();
        menu.Items.Add(removeBookmarksItem);

        menu.ItemsPanel = (ItemsPanelTemplate)Application.Current.FindResource("FullWidthItemsPanelTemplate");
        menu.SetResourceReference(Control.BackgroundProperty, "SecondaryBackground");
        menu.SetResourceReference(Control.BorderBrushProperty, "PrimaryButtonBorderBrush");
        return menu;
    }
}
