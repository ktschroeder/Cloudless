using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public sealed class BulkTagContextMenuTests
{
    [Fact]
    public void SelectionActionsPageTargetsAndSortOptions_ArePresentAndDispatchExpectedCallbacks()
    {
        RunOnStaThread(() =>
        {
            ResetWpfApplicationSingletonForTestIsolation();
            App.SuppressStartupForUiTests = true;
            var app = new App();
            app.InitializeComponent();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ThemeManager.ApplyThemeGlobalResources("Dark");

            try
            {
                int selectAllCalls = 0;
                int clearSelectionCalls = 0;
                int openInNewWindowsCalls = 0;
                int sendToFilmstripCalls = 0;
                int selectedCount = 2;
                int imageDetailsCalls = 0;
                var pageTargets = new List<string>();
                var actionCalls = new List<string>();
                var sortOrders = new List<MediaSortOrder>();
                var menu = BulkTagContextMenu.Create(
                    () => selectAllCalls++,
                    () => clearSelectionCalls++,
                    () =>
                    {
                        openInNewWindowsCalls++;
                        return Task.CompletedTask;
                    },
                    target =>
                    {
                        pageTargets.Add(target);
                        return Task.CompletedTask;
                    },
                    () => selectedCount == 1,
                    () => imageDetailsCalls++,
                    () =>
                    {
                        sendToFilmstripCalls++;
                        return Task.CompletedTask;
                    },
                    sortOrder =>
                    {
                        sortOrders.Add(sortOrder);
                        return Task.CompletedTask;
                    },
                    () => actionCalls.Add("edit tags"),
                    () => actionCalls.Add("add bookmarks"),
                    () => actionCalls.Add("remove bookmarks"));

                Assert.Equal("Select all", Assert.IsType<MenuItem>(menu.Items[0]).Header);
                Assert.Equal("Clear selection", Assert.IsType<MenuItem>(menu.Items[1]).Header);
                Assert.Equal("Open selected in new windows", Assert.IsType<MenuItem>(menu.Items[3]).Header);

                var openOnPage = Assert.IsType<MenuItem>(menu.Items[4]);
                Assert.Equal("Open selected on page", openOnPage.Header);
                Assert.Equal(
                    new[] { "Previous inactive", "Previous active", "Next active", "Next inactive", "Page index" },
                    openOnPage.Items.OfType<MenuItem>().Select(item => item.Header?.ToString()));

                var pageIndexMenu = Assert.IsType<MenuItem>(openOnPage.Items[4]);
                Assert.Equal("Page index", pageIndexMenu.Header);
                var pageIndexItems = pageIndexMenu.Items.OfType<MenuItem>().ToArray();
                Assert.Equal(20, pageIndexItems.Length);
                Assert.Equal(Enumerable.Range(1, 20).Select(index => index.ToString()),
                    pageIndexItems.Select(item => item.Header?.ToString()));

                AssertPageTarget(openOnPage, 0, "pi", pageTargets);
                AssertPageTarget(openOnPage, 1, "pa", pageTargets);
                AssertPageTarget(openOnPage, 2, "na", pageTargets);
                AssertPageTarget(openOnPage, 3, "ni", pageTargets);
                foreach (var pageItem in pageIndexItems)
                    pageItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, pageItem));

                Assert.Equal(new[] { "pi", "pa", "na", "ni" }.Concat(Enumerable.Range(1, 20).Select(i => i.ToString())), pageTargets);

                var imageDetailsItem = Assert.IsType<MenuItem>(menu.Items[5]);
                Assert.Equal("View image details", imageDetailsItem.Header);
                Assert.False(imageDetailsItem.IsEnabled);
                selectedCount = 1;
                menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
                Assert.True(imageDetailsItem.IsEnabled);
                imageDetailsItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, imageDetailsItem));
                Assert.Equal(1, imageDetailsCalls);
                selectedCount = 2;
                menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
                Assert.False(imageDetailsItem.IsEnabled);
                imageDetailsItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, imageDetailsItem));
                Assert.Equal(1, imageDetailsCalls);

                var sendToFilmstrip = Assert.IsType<MenuItem>(menu.Items[6]);
                Assert.Equal("Send selection to filmstrip", sendToFilmstrip.Header);
                AssertMenuAction(sendToFilmstrip, () => Assert.Equal(1, sendToFilmstripCalls));

                var sortMenu = Assert.IsType<MenuItem>(menu.Items[7]);
                Assert.Equal("Sort", sortMenu.Header);
                var sortItems = sortMenu.Items.OfType<MenuItem>().ToArray();
                Assert.Equal(
                    new[] { "Default", "Default (desc)", "Filename", "Filename (desc)", "Filetype", "Filetype (desc)", "CreatedOn", "CreatedOn (desc)", "ModifiedOn", "ModifiedOn (desc)", "Size", "Size (desc)" },
                    sortItems.Select(item => item.Header?.ToString()));
                foreach (var sortItem in sortItems)
                    sortItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, sortItem));
                Assert.Equal(Enum.GetValues<MediaSortOrder>(), sortOrders);

                AssertMenuAction(menu.Items[0], () => Assert.Equal(1, selectAllCalls));
                AssertMenuAction(menu.Items[1], () => Assert.Equal(1, clearSelectionCalls));
                AssertMenuAction(menu.Items[3], () => Assert.Equal(1, openInNewWindowsCalls));
                AssertMenuAction(menu.Items[9], () => Assert.Equal("Edit tags for selected", ((MenuItem)menu.Items[9]).Header));
                AssertMenuAction(menu.Items[11], () => Assert.Equal("Bookmark selected", ((MenuItem)menu.Items[11]).Header));
                AssertMenuAction(menu.Items[12], () => Assert.Equal("Remove bookmarks from selected", ((MenuItem)menu.Items[12]).Header));
                Assert.Equal(new[] { "edit tags", "add bookmarks", "remove bookmarks" }, actionCalls);
            }
            finally
            {
                app.Shutdown();
                App.SuppressStartupForUiTests = false;
                ResetWpfApplicationSingletonForTestIsolation();
            }
        });
    }

    private static void AssertPageTarget(MenuItem menu, int itemIndex, string expectedTarget, List<string> targets)
    {
        var item = Assert.IsType<MenuItem>(menu.Items[itemIndex]);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
        Assert.Equal(expectedTarget, targets[^1]);
    }

    private static void AssertMenuAction(object item, Action assertion)
    {
        var menuItem = Assert.IsType<MenuItem>(item);
        menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, menuItem));
        assertion();
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null)
            throw new Xunit.Sdk.XunitException($"STA selection context-menu test failed: {failure}");
    }

    private static void ResetWpfApplicationSingletonForTestIsolation()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        typeof(Application).GetField("_appInstance", flags)!.SetValue(null, null);
        typeof(Application).GetField("_appCreatedInThisAppDomain", flags)!.SetValue(null, false);
        typeof(Application).GetField("_isShuttingDown", flags)!.SetValue(null, false);
    }
}
