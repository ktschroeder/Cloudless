using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Cloudless
{
    public partial class PopOutCommandPaletteWindow : Window
    {
        private MainWindow _mw;

        // TODO lots of duplicate and near-duplicate code in here, compared to main command palette code. Could clean up probably.
        public PopOutCommandPaletteWindow(MainWindow owner)
        {
            InitializeComponent();
            _mw = owner;

            ResetCommandPalette();
        }
        private void ResetCommandPalette()
        {
            CommandTextBox.Text = ":";
            CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
            _mw.LoadCommandHistory();
            _mw.CommandHistoryIndex = _mw.CommandHistory.Count;
            TabScroll = false;
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { WindowHelper.HandleMouseDown(this, e); }
        //private void Window_KeyDown(object sender, KeyEventArgs e) { WindowHelper.HandleKeyDown(this, e); }
        private void Close_Click(object sender, RoutedEventArgs e) { WindowHelper.Close_Click(this, e); }

        private void CommandPalette_TextChanged(object sender, TextChangedEventArgs e)  // pass TextBox
        {
            // Executes every time a character is added or removed.
            // Ensure there is a colon at the start.
            string currentText = CommandTextBox.Text;
            if (string.IsNullOrEmpty(currentText))
                CommandTextBox.Text = ":";
            else if (currentText[0] != ':')
                CommandTextBox.Text = $":{currentText}";
        }

        private void CommandPalette_SelectionChanged(object sender, RoutedEventArgs e)  // pass TextBox
        {
            if (CommandTextBox.SelectionLength == 0 && CommandTextBox.CaretIndex == 0 && CommandTextBox.Text.Length > 0)
            {
                CommandTextBox.CaretIndex = 1;
            }

            // TODO: make Ctrl A select all but first char. make Shift Left do nothing when at index 1.
        }

        private bool TabScroll;  // whether next tab should try to find another autocomplete candidate
        private bool TabScrollCtrl;
        private LinkedList<string> AutocompleteCandidates = new LinkedList<string>();
        private LinkedList<string> AutocompleteCandidatesCtrl = new LinkedList<string>();
        private string PreviousTabScrollText = null;
        private void TabPressed(bool shiftPressed = false, bool controlPressed = false)
        {
            if (PreviousTabScrollText != CommandTextBox.Text)
            {
                TabScroll = false;
                TabScrollCtrl = false;
            }

            string foundCommandBase = null;
            string[] tabbableCommandBases = { "ws l", "ws load", "ws s", "ws save", "ws s!", "ws save!", "ws delete", "ws rename", "ws r", "ws merge", "ws m", "ws preview", "ws p" };
            foreach (string tcb in tabbableCommandBases)
            {
                if (CommandTextBox.Text.ToLower().StartsWith($":{tcb} "))
                {
                    foundCommandBase = tcb;
                    break;
                }
            }

            if (foundCommandBase != null)
            {
                string commandBase = $":{foundCommandBase} ";
                if (!TabScroll && !controlPressed)
                {
                    string query = CommandTextBox.Text.Length == commandBase.Length ? "" : CommandTextBox.Text.Substring(commandBase.Length);
                    var wsNames = Directory.GetFiles(MainWindow.workspaceFilesPath)?.Where(f => f.ToLower().EndsWith(".cloudless"))?.Select(f => Path.GetFileNameWithoutExtension(f))?.ToList();
                    wsNames ??= new List<string>();
                    wsNames = wsNames.Where(ws => !MainWindow.IsReservedWorkspaceName(ws)).ToList();  // filter out system/reserved workspace names
                    wsNames = wsNames.Where(ws => ws.ToLower().StartsWith(query.ToLower())).ToList();
                    AutocompleteCandidates.Clear();
                    foreach (var wsName in wsNames)
                    {
                        AutocompleteCandidates.AddLast(wsName);
                    }
                    TabScroll = true;
                }
                else if (!TabScrollCtrl && controlPressed)
                {
                    var wsNames = Directory.GetFiles(MainWindow.workspaceFilesPath)?.Where(f => f.ToLower().EndsWith(".cloudless"))?.Select(f => Path.GetFileNameWithoutExtension(f))?.ToList();
                    wsNames ??= new List<string>();
                    var recentNames = MainWindow.GetRecentlySavedAndLoadedWorkspaceNames();
                    wsNames = wsNames.Where(ws => recentNames.Contains(ws)).ToList();  // filter out names not present in recent history
                    wsNames = wsNames.Where(ws => !MainWindow.IsReservedWorkspaceName(ws)).ToList();  // filter out system/reserved workspace names
                    AutocompleteCandidatesCtrl.Clear();
                    foreach (var wsName in wsNames)
                    {
                        AutocompleteCandidatesCtrl.AddFirst(wsName);
                    }
                    TabScrollCtrl = true;
                }
                CycleToNextAutocompleteCandidate(commandBase, reverse: shiftPressed, recency: controlPressed);
            }
        }

        private void CycleToNextAutocompleteCandidate(string commandBase, bool reverse = false, bool recency = false)
        {
            LinkedList<string> candidates = recency ? AutocompleteCandidatesCtrl : AutocompleteCandidates;

            if (candidates.Count == 0)
                return;

            // get next candidate
            if (!reverse)
            {
                var next = candidates.First();
                candidates.RemoveFirst();
                CommandTextBox.Text = commandBase + next;
                if (CommandTextBox.Text.Equals(PreviousTabScrollText))  // ...then the user is "changing direction", so repeat the movement
                {
                    candidates.AddLast(next);
                    next = candidates.First();
                    candidates.RemoveFirst();
                    CommandTextBox.Text = commandBase + next;
                }
                PreviousTabScrollText = CommandTextBox.Text;
                candidates.AddLast(next);
                CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
            }
            else
            {
                var next = candidates.Last();
                candidates.RemoveLast();
                CommandTextBox.Text = commandBase + next;
                if (CommandTextBox.Text.Equals(PreviousTabScrollText))  // ...then the user is "changing direction" so repeat the movement
                {
                    candidates.AddFirst(next);
                    next = candidates.Last();
                    candidates.RemoveLast();
                    CommandTextBox.Text = commandBase + next;
                }
                PreviousTabScrollText = CommandTextBox.Text;
                candidates.AddFirst(next);
                CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
            }
        }

        private async void CommandTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            ModifierKeys modifiers = Keyboard.Modifiers;
            bool control = (modifiers & ModifierKeys.Control) != 0;
            bool alt = (modifiers & ModifierKeys.Alt) != 0;
            bool shift = (modifiers & ModifierKeys.Shift) != 0;

            if (e.Key == Key.Tab)
            {
                TabPressed(shift, control);
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Left && e.Key != Key.Right && !shift)
                TabScroll = false;

            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                CommandTextBox.IsEnabled = false;
                await _mw.ExecuteCommand(CommandTextBox.Text.Trim());
                ResetCommandPalette();
                CommandTextBox.IsEnabled = true;
                e.Handled = true;
                return;
            }
        }

        private void CommandPaletteTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_mw.CommandHistory.Count == 0)
                return;

            if (e.Key == Key.Up)
            {
                e.Handled = true;

                if (_mw.CommandHistoryIndex > 0)
                    _mw.CommandHistoryIndex--;

                CommandTextBox.Text = _mw.CommandHistory[_mw.CommandHistoryIndex];
                CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
            }
            else if (e.Key == Key.Down)
            {
                e.Handled = true;

                if (_mw.CommandHistoryIndex < _mw.CommandHistory.Count - 1)
                {
                    _mw.CommandHistoryIndex++;
                    CommandTextBox.Text = _mw.CommandHistory[_mw.CommandHistoryIndex];
                }
                else
                {
                    _mw.CommandHistoryIndex = _mw.CommandHistory.Count;
                    CommandTextBox.Text = ":";
                }

                CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
            }
        }
    }
}
