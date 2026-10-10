using System.Windows;
using System.Windows.Input;

namespace Cloudless;

public partial class BulkTagEditWindow : Window
{
    private readonly TagManager _tagManager;
    private List<string> _autocompleteCandidates = new();
    private int _autocompleteCandidateIndex = -1;
    private string? _lastAutocompleteText;
    private int _lastAutocompleteCaret;
    private string _autocompletePrefix = "";
    private string _autocompleteSuffix = "";

    public IReadOnlyList<string> SelectedTags { get; private set; } = Array.Empty<string>();
    public bool RemoveFromFiles { get; private set; }

    public BulkTagEditWindow(IReadOnlyList<string> selectedFiles)
        : this(selectedFiles, TagManager.Instance)
    {
    }

    internal BulkTagEditWindow(IReadOnlyList<string> selectedFiles, TagManager tagManager)
    {
        InitializeComponent();
        _tagManager = tagManager;
        SecondaryWindowOpacity.Apply(this);
        int selectedFileCount = selectedFiles.Count;
        SelectedCountText.Text = $"Apply changes to {selectedFileCount} selected item{(selectedFileCount == 1 ? "" : "s")}. Double-click tags to add to input. Use Tab for autocomplete.";
        ExistingTagsList.ItemsSource = _tagManager.GetAllTags();
        if (selectedFileCount == 1)
        {
            CurrentTagsList.ItemsSource = _tagManager.GetTagsForFile(selectedFiles[0]);
            CurrentTagsPanel.Visibility = Visibility.Visible;
        }
        Loaded += (_, _) => TagsTextBox.Focus();
    }

    private void AddTags_Click(object sender, RoutedEventArgs e) => ApplyTags(remove: false);

    private void RemoveTags_Click(object sender, RoutedEventArgs e) => ApplyTags(remove: true);

    private void ApplyTags(bool remove)
    {
        string[] tags = TagsTextBox.Text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (tags.Length == 0)
        {
            ShowValidation("Enter at least one tag.");
            return;
        }

        string? reservedTag = tags.FirstOrDefault(tag => tag.Equals("and", StringComparison.OrdinalIgnoreCase) ||
                                                         tag.Equals("or", StringComparison.OrdinalIgnoreCase) ||
                                                         tag.Equals("not", StringComparison.OrdinalIgnoreCase));
        if (reservedTag != null)
        {
            ShowValidation($"'{reservedTag}' is reserved and cannot be used as a tag.");
            return;
        }

        SelectedTags = tags;
        RemoveFromFiles = remove;
        DialogResult = true;
    }

    private void ExistingTagsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ExistingTagsList.SelectedItem is not string tag)
            return;

        AppendTag(tag);
    }

    private void CurrentTagsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CurrentTagsList.SelectedItem is string tag)
            AppendTag(tag);
    }

    private void AppendTag(string tag)
    {
        string[] currentTags = TagsTextBox.Text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (currentTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            return;

        TagsTextBox.Text = string.Join(' ', currentTags.Append(tag));
        TagsTextBox.CaretIndex = TagsTextBox.Text.Length;
        TagsTextBox.Focus();
        ValidationText.Visibility = Visibility.Collapsed;
    }

    private void TagsTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            e.Handled = CycleTagAutocomplete(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            return;
        }

        if (e.Key == Key.Enter)
        {
            ApplyTags(remove: false);
            e.Handled = true;
        }
    }

    private bool CycleTagAutocomplete(bool reverse)
    {
        string text = TagsTextBox.Text;
        int caret = TagsTextBox.CaretIndex;
        bool continuingCycle = text == _lastAutocompleteText && caret == _lastAutocompleteCaret && _autocompleteCandidates.Count > 0;

        if (!continuingCycle)
        {
            int tokenStart = caret;
            while (tokenStart > 0 && !char.IsWhiteSpace(text[tokenStart - 1]))
                tokenStart--;

            int tokenEnd = caret;
            while (tokenEnd < text.Length && !char.IsWhiteSpace(text[tokenEnd]))
                tokenEnd++;

            string query = text[tokenStart..caret];
            _autocompletePrefix = text[..tokenStart];
            _autocompleteSuffix = text[tokenEnd..];
            _autocompleteCandidates = _tagManager.GetAllTags()
                .Where(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _autocompleteCandidateIndex = reverse ? 0 : -1;
        }

        if (_autocompleteCandidates.Count == 0)
            return false;

        _autocompleteCandidateIndex = (_autocompleteCandidateIndex + (reverse ? -1 : 1) + _autocompleteCandidates.Count)
            % _autocompleteCandidates.Count;
        string completion = _autocompleteCandidates[_autocompleteCandidateIndex];
        TagsTextBox.Text = _autocompletePrefix + completion + _autocompleteSuffix;
        TagsTextBox.CaretIndex = _autocompletePrefix.Length + completion.Length;
        _lastAutocompleteText = TagsTextBox.Text;
        _lastAutocompleteCaret = TagsTextBox.CaretIndex;
        return true;
    }

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationText.Visibility = Visibility.Visible;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e) => WindowHelper.HandleMouseDown(this, e);

    private void Window_KeyDown(object sender, KeyEventArgs e) => WindowHelper.HandleKeyDown(this, e);
}
