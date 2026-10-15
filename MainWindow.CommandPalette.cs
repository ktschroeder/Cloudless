using Cloudless.PluginBase;
using Cloudless.ReferenceData;
using System.Linq;
using System.Text.Json;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;


namespace Cloudless
{
    public partial class MainWindow : Window
    {
        // Current user-defined loop range for video playback. Null means use media bounds.
        private TimeSpan? _videoLoopStart = null;
        private TimeSpan? _videoLoopEnd = null;
        // Whether this window is a member of the sync group for its page
        private bool _isVideoSynced = false;
        public bool IsVideoSyncWaiting = false;
        private EventHandler<Cloudless.PluginBase.VideoTimeChangedEventArgs>? _syncTimeChangedHandler = null;
        private bool _pluginLoopTemporarilyDisabledForSync = false;
        // Number of times the trigger must be hit before the slideshow progresses (e.g., 1 = single hit, 3 = must loop 3 times)
        public int SlideshowTriggerCount = 0;
        // Current count of how many times the trigger has been hit
        private int _slideshowTriggerHitCount = 0;
        private EventHandler<Cloudless.PluginBase.VideoTimeChangedEventArgs>? _triggerTimeChangedHandler = null;
        private DateTime _lastTriggerFired = DateTime.MinValue;
        private bool _pluginLoopTemporarilyDisabled = false;
        private TextBox? GetCommandTextBox()
        {
            if (_commandPaletteWindow?.Control != null)
                return _commandPaletteWindow.Control.CommandTextBoxControl;
            var tb = this.FindName("CommandTextBox") as TextBox;
            return tb;
        }

        private void SetSync(bool enabled)
        {
            // If already set/unset, nothing to do
            if (enabled && _isVideoSynced && _syncTimeChangedHandler != null) return;
            if (!enabled && !_isVideoSynced) return;

            var vp = VideoHost.Content as Cloudless.PluginBase.IVideoPlayer;
            if (enabled)
            {
                // Subscribe to time updates
                if (vp != null && _syncTimeChangedHandler == null)
                {
                    _syncTimeChangedHandler = new EventHandler<Cloudless.PluginBase.VideoTimeChangedEventArgs>(OnSyncTimeChanged);
                    vp.TimeChanged += _syncTimeChangedHandler;
                    // Disable plugin auto-restart so host can coordinate synchronized restarts
                    vp.SetAutoRestartAllowed(false);
                }

                _isVideoSynced = true;
            }
            else
            {
                if (vp != null && _syncTimeChangedHandler != null)
                {
                    vp.TimeChanged -= _syncTimeChangedHandler;
                    _syncTimeChangedHandler = null;

                    // Re-enable plugin auto-restart when unsyncing
                    vp.SetAutoRestartAllowed(true);
                }

                _isVideoSynced = false;
            }
        }

        // Expose sync status for UI
        public bool VideoIsSynced => _isVideoSynced;
        public bool VideoSyncWaiting => IsVideoSyncWaiting;

        // Expose trigger status for UI
        public int GetSlideshowTriggerCount() => SlideshowTriggerCount;
        public int GetSlideshowTriggerHitCount() => _slideshowTriggerHitCount;


        // Increment and reset trigger hit count (used by both custom end and natural end paths)
        public void IncrementSlideshowTriggerHitCount() => _slideshowTriggerHitCount++;
        public void ResetSlideshowTriggerHitCount() => _slideshowTriggerHitCount = 0;

        // Called to set waiting state (used by OnSyncTimeChanged and VideoSyncManager)
        public void NotifySyncWaiting(bool waiting)
        {
            IsVideoSyncWaiting = waiting;
            // Ensure UI update runs on the UI thread
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateVideoControls();
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NotifySyncWaiting dispatch error: {ex.Message}");
                throw;
            }
        }

        private async void OnSyncTimeChanged(object? sender, Cloudless.PluginBase.VideoTimeChangedEventArgs e)
        {
            var vp = sender as Cloudless.PluginBase.IVideoPlayer;
            if (vp == null) return;

            long currentMs = e.TimeMilliseconds;
            TimeSpan? endTs = _videoLoopEnd;
            if (!endTs.HasValue)
            {
                var dur = vp.GetDuration();
                if (dur > TimeSpan.Zero)
                    endTs = dur;
            }

            if (!endTs.HasValue) return;

            long endMs = (long)endTs.Value.TotalMilliseconds;
            if (endMs <= 0) return;

            if (currentMs >= endMs - 550)  // 250 to accommodate VLC polling
            {
                // When synced, pause this window at loop start (if available) or pause at end.
                if (_videoLoopStart.HasValue)
                {
                    vp.SeekTo(_videoLoopStart.Value);
                    vp.TogglePause(true);
                }
                else
                {
                    vp.SeekTo(TimeSpan.Zero);
                    vp.TogglePause(true);
                }

                // Mark this window as waiting so UI may reflect it
                this.NotifySyncWaiting(true);

                // Evaluate whether all synced videos on this page are paused. If so, restart them all together.
                await this.Dispatcher.BeginInvoke(new Action(async () =>
                {
                    await Task.Delay(250);
                    var syncedWindows = Application.Current.Windows
                        .OfType<MainWindow>()
                        .Where(w => w.windowPageIndex == this.windowPageIndex && w.VideoIsSynced && !string.IsNullOrEmpty(w.currentlyDisplayedImagePath))
                        .ToList();

                    if (syncedWindows.Count == 0)
                        return;

                    // If any synced window is currently playing, do not restart yet
                    foreach (var win in syncedWindows)
                    {
                        var otherVp = win.VideoHost.Content as Cloudless.PluginBase.IVideoPlayer;
                        if (otherVp != null && (!otherVp.IsPaused() || !win.IsVideoSyncWaiting))
                            return;
                    }

                    // All synced windows are paused => restart all
                    foreach (var win in syncedWindows.Where(w => w.IsVideoSyncWaiting))
                    {
                        var otherVp = win.VideoHost.Content as Cloudless.PluginBase.IVideoPlayer;
                        if (otherVp == null) continue;
                        if (win.VideoLoopStart.HasValue)
                        {
                            otherVp.SeekTo(win.VideoLoopStart.Value);
                            otherVp.TogglePause(false);
                        }
                        else
                        {
                            otherVp.Restart();
                            otherVp.TogglePause(false);
                        }
                        win.NotifySyncWaiting(false);
                    }
                }));
            }
        }

        private void SetSlideshowTrigger(int triggerCount)
        {
            // Validate input: must be 0 (to disable) or greater than 0
            if (triggerCount < 0)
                triggerCount = 0;

            bool enabled = triggerCount > 0;

            // If already enabled and already subscribed, and count unchanged, nothing to do
            if (enabled && SlideshowTriggerCount == triggerCount && _triggerTimeChangedHandler != null)
                return;

            string ext = Path.GetExtension(currentlyDisplayedImagePath) ?? "";
            if (!FileTypeManager.IsVideoFile(ext))
            {
                Message($"Only windows that have a loaded video file can be marked as triggers.");
                return;
            }

            // If enabling and not already marked, enforce only one trigger per page
            // We also check whether workspace is loading: without the check, triggers can fail to get assigned because of timing of other windows briefly on-screen during load
            if (enabled && SlideshowTriggerCount <= 0 && !WorkspaceLoadInProgress)
            {
                var other = Application.Current.Windows
                    .OfType<MainWindow>()
                    .FirstOrDefault(w => w != this && w.windowPageIndex == this.windowPageIndex && w.SlideshowTriggerCount > 0);

                if (other != null)
                {
                    Message($"Cannot mark this window as a slideshow trigger because another window on page {this.windowPageIndex} is already a trigger.");
                    return;
                }
            }

            SlideshowTriggerCount = triggerCount;
            _slideshowTriggerHitCount = 0;

            try
            {
                if (enabled)
                {
                    // register page (idempotent)
                    SlideshowManager.RegisterTriggerPage(this.windowPageIndex);

                    // attempt to subscribe if plugin is present and not already subscribed
                    var vp = VideoHost.Content as Cloudless.PluginBase.IVideoPlayer;
                    if (vp != null && _triggerTimeChangedHandler == null)
                    {
                        _triggerTimeChangedHandler = new EventHandler<Cloudless.PluginBase.VideoTimeChangedEventArgs>(OnTriggerTimeChanged);
                        vp.TimeChanged += _triggerTimeChangedHandler;
                        // If a custom loop end is configured, disable plugin's internal loop behavior so host can handle triggers
                        try
                        {
                            if (_videoLoopEnd.HasValue)
                            {
                                vp.SetLoopRange(_videoLoopStart, null);
                                _pluginLoopTemporarilyDisabled = true;
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    // unregister page
                    SlideshowManager.UnregisterTriggerPage(this.windowPageIndex);

                    // unsubscribe if subscribed
                    var vp = VideoHost.Content as Cloudless.PluginBase.IVideoPlayer;
                    if (vp != null && _triggerTimeChangedHandler != null)
                    {
                        try { vp.TimeChanged -= _triggerTimeChangedHandler; } catch { }
                        _triggerTimeChangedHandler = null;
                        // If we previously disabled plugin loop, restore it
                        try
                        {
                            if (_pluginLoopTemporarilyDisabled && _videoLoopEnd.HasValue)
                            {
                                vp.SetLoopRange(_videoLoopStart, _videoLoopEnd);
                                _pluginLoopTemporarilyDisabled = false;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetSlideshowTrigger error: {ex.Message}");
            }
        }

        private void OnTriggerTimeChanged(object? sender, Cloudless.PluginBase.VideoTimeChangedEventArgs e)
        {
            try
            {
                // throttle repeated firings
                if ((DateTime.Now - _lastTriggerFired).TotalMilliseconds < 300)
                    return;

                var vp = VideoHost.Content as Cloudless.PluginBase.IVideoPlayer;
                if (vp == null) return;

                long currentMs = e.TimeMilliseconds;
                TimeSpan? endTs = _videoLoopEnd;
                if (!endTs.HasValue)
                {
                    var dur = vp.GetDuration();
                    if (dur > TimeSpan.Zero)
                        endTs = dur;
                }

                if (!endTs.HasValue) return; // unknown duration

                long endMs = (long)endTs.Value.TotalMilliseconds;
                if (endMs <= 0) return;

                if (currentMs >= endMs)  // This flow is only reached when there is a custom end. Not when there is no custom end.
                {
                    // Increment the hit counter
                    _slideshowTriggerHitCount++;

                    // Update UI immediately to show the hit
                    UpdateVideoControls();

                    // Only signal the manager if we've hit the required number of times
                    if (_slideshowTriggerHitCount >= SlideshowTriggerCount && SlideshowManager.HasSufficientTimePassedSinceLastTrigger_ConsumeIfYes())
                    {
                        // Signal manager to advance the slideshow
                        _lastTriggerFired = DateTime.Now;
                        SlideshowManager.SignalTriggerFired(this.windowPageIndex);
                        _slideshowTriggerHitCount = 0;
                    }
                    else
                    {
                        // Still waiting for more hits
                        _lastTriggerFired = DateTime.Now;
                    }

                    // restart the video to avoid repeated triggers
                    try
                    {
                        if (_videoLoopStart.HasValue)
                            vp.SeekTo(_videoLoopStart.Value);
                        else
                            vp.Restart();
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OnTriggerTimeChanged error: {ex.Message}");
            }
        }

        private bool IsCommandPaletteVisible()
        {
            return _commandPaletteWindow != null && _commandPaletteWindow.IsVisible;
        }

        private void OpenCommandPalette()
        {
            if (IsCommandPaletteVisible())
            {
                var tb = GetCommandTextBox();
                tb?.Focus();
                return;
            }

            //_commandPaletteWindow.Control = null;

            //_commandPaletteWindow.AlignToOwner(owner, 7, 7, desiredHeight);

            var textBox = GetCommandTextBox();
            if (textBox != null)
            {
                textBox.Text = "";
                textBox.CaretIndex = textBox.Text.Length;
            }

            _commandPaletteWindow.ShowAndFocus(textBox, this);

            LoadCommandHistory();
            CommandHistoryIndex = CommandHistory.Count;
            TabScroll = false;
        }

        

        private void CloseCommandPalette()
        {
            var textBox = GetCommandTextBox();
            if (textBox != null)
            {
                textBox.Text = "";
                textBox.CaretIndex = 0;
            }

            var win = _commandPaletteWindow;
            if (win != null && win.Control != null)
            {
                //win.Hide();
                win.Close();
            }

            _commandPaletteWindow = new CommandPaletteWindow(this);
            _commandPaletteWindow.AlignToOwner(this);
            _commandPaletteWindow.AttachOwnerHandlers(this);
            // keep it hidden until used
            _commandPaletteWindow.Hide();

            // Restore focus to the main window
            FocusManager.SetFocusedElement(this, this);
            Keyboard.Focus(this);
        }

        public async void CommandTextBox_KeyDown(object sender, KeyEventArgs e)
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
                var tb = GetCommandTextBox();
                tb.Text = "";
                CloseCommandPalette();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                var tb = GetCommandTextBox();
                string command = tb.Text.Trim();
                tb.Text = "";
                CloseCommandPalette();  // if this is closed after execution of command, we get some sporadic weirdness with unreleased focus in a window that has been sent to a different page.
                
                if (!string.IsNullOrEmpty(command))
                {
                    await ExecuteCommand(command);
                }

                e.Handled = true;
                return;
            }
        }

        public async Task ExecuteCommand(string cmd)
        {
            cmd = cmd.Trim().ToLower();

            CommandParseResult macroRecordParse = CommandSyntaxCatalog.Parse(cmd);
            if (macroRecordParse.DefinitionId == "macros" &&
                macroRecordParse.Operation == "macro-record" &&
                macroRecordParse.IsComplete)
            {
                SetUserMacro(macroRecordParse.GetArgument("name")!, macroRecordParse.GetArgument("macroCommand")!);
            }
            else if (macroRecordParse.DefinitionId == "command-chain")
            {
                for (int index = 0; index < macroRecordParse.SequenceCommands.Count; index++)
                {
                    CommandParseResult parsedCommand = macroRecordParse.Sequence[index];
                    if (parsedCommand.IsRecognized)
                        await ExecuteCatalogCommand(parsedCommand);
                    else
                        await ExecuteCommandInner(macroRecordParse.SequenceCommands[index]);
                }
            }
            else if (macroRecordParse.IsRecognized)
            {
                await ExecuteCatalogCommand(macroRecordParse);
            }
            else
            {
                await ExecuteCommandInner(cmd);
            }

            // Originally it seemed better to only commit valid commands, but I find it more convenient to commit all commands, at least for now.
            // ...especially when the user makes a slight typo and would rather not fully re-type a longer command.
            CommitCommand(cmd);
        }

        private bool TabScroll;  // whether next tab should try to find another autocomplete candidate
        private bool TabScrollCtrl;
        private LinkedList<string> AutocompleteCandidates = new LinkedList<string>();
        private LinkedList<string> AutocompleteCandidatesCtrl = new LinkedList<string>();
        private string PreviousTabScrollText = null;
        private string _commandSuggestionInput = "";
        private int _commandSuggestionCaretIndex = -1;
        private IReadOnlyList<string> _commandSuggestionCandidates = Array.Empty<string>();
        private int _commandSuggestionIndex;
        private static readonly string[] WorkspaceAutocompleteCommandBases =
        {
            "ws l", "ws load", "ws s", "ws save", "ws s!", "ws save!", "ws delete", "ws rename", "ws r", "ws merge", "ws m", "ws preview", "ws p", "fs ws", "fs preview", "fs p", "fs workspace", "filmstrip ws", "filmstrip preview", "filmstrip p", "filmstrip workspace"
        };

        internal static string? GetCommandCompletion(string text, int caretIndex)
        {
            return GetCommandCompletions(text, caretIndex).FirstOrDefault();
        }

        internal static IReadOnlyList<string> GetCommandCompletions(string text, int caretIndex)
        {
            if (caretIndex != text.Length)
                return Array.Empty<string>();

            if (CommandSyntaxCatalog.TryGetCommandSequenceCompletionContext(text, out string commandPrefix, out string command))
            {
                if (command.Length > 0)
                {
                    return GetCommandCompletions(command, command.Length)
                        .Select(completion => commandPrefix + completion)
                        .ToArray();
                }
            }

            if (CommandSyntaxCatalog.TryGetCommandValueContext(text, out string valuePrefix, out string commandValue))
            {
                return GetCommandCompletions(commandValue, commandValue.Length)
                    .Select(completion => valuePrefix + completion)
                    .ToArray();
            }

            if (IsTabbableCommandInput(text))
                return Array.Empty<string>();

            return CommandSuggestionProvider.GetSuggestions(text);
        }

        private static bool IsTabbableCommandInput(string text)
        {
            if (TryGetTagAutocompleteContext(text, out _, out _))
                return true;

            return WorkspaceAutocompleteCommandBases.Any(commandBase =>
                text.StartsWith(commandBase + " ", StringComparison.OrdinalIgnoreCase));
        }

        internal void UpdateCommandSuggestion()
        {
            var textBox = GetCommandTextBox();
            string text = textBox?.Text ?? "";
            int caretIndex = textBox?.CaretIndex ?? -1;
            if (_commandSuggestionInput != text || _commandSuggestionCaretIndex != caretIndex)
            {
                _commandSuggestionInput = text;
                _commandSuggestionCaretIndex = caretIndex;
                _commandSuggestionCandidates = textBox == null
                    ? Array.Empty<string>()
                    : GetCommandCompletions(text, textBox.CaretIndex);
                _commandSuggestionIndex = 0;
            }

            if (_commandSuggestionCandidates.Count == 0 && textBox != null)
                _commandSuggestionCandidates = GetCommandCompletions(text, textBox.CaretIndex);

            var suggestion = _commandSuggestionIndex < _commandSuggestionCandidates.Count
                ? _commandSuggestionCandidates[_commandSuggestionIndex]
                : null;
            _commandPaletteWindow?.Control?.SetCommandSuggestion(suggestion);
            _commandPaletteWindow?.Control?.SetCommandValidity(
                CommandSyntaxValidator.IsValidCommand(text),
                Cloudless.Properties.Settings.Default.CommandPaletteValidCommandIndicator);
        }

        private void CycleCommandSuggestion(bool reverse)
        {
            UpdateCommandSuggestion();
            if (_commandSuggestionCandidates.Count < 2)
                return;

            _commandSuggestionIndex = (_commandSuggestionIndex + (reverse ? -1 : 1) + _commandSuggestionCandidates.Count)
                % _commandSuggestionCandidates.Count;
            _commandPaletteWindow?.Control?.SetCommandSuggestion(_commandSuggestionCandidates[_commandSuggestionIndex]);
        }

        private void TabPressed(bool shiftPressed = false, bool controlPressed = false)
        {
            var _tb_for_prev = GetCommandTextBox();
            var _tbTextPrev = _tb_for_prev?.Text ?? "";
            if (PreviousTabScrollText != _tbTextPrev)
            {
                TabScroll = false;
                TabScrollCtrl = false;
            }

            if (TryGetTagAutocompleteContext(_tbTextPrev, out string tagPrefix, out string tagQuery))
            {
                if (!TabScroll)
                {
                    var matchingTags = TagManager.Instance.GetAllTags()
                        .Where(tag => tag.Contains(tagQuery, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(tag => tag)
                        .ToList();

                    AutocompleteCandidates.Clear();
                    foreach (var tag in matchingTags)
                        AutocompleteCandidates.AddLast(tag);

                    TabScroll = true;
                }

                // Ctrl+Tab has no recency behavior for tags; it cycles the same tag list.
                CycleToNextAutocompleteCandidate(tagPrefix, reverse: shiftPressed);
                return;
            }

            string foundCommandBase = null;

            // Check workspace commands first
            foreach (string tcb in WorkspaceAutocompleteCommandBases) 
            { 
                if (_tbTextPrev.StartsWith($"{tcb} ", StringComparison.OrdinalIgnoreCase))
                {
                    foundCommandBase = tcb;
                    break;
                }
            }

            if (foundCommandBase != null)
            {
                string commandBase = $"{foundCommandBase} ";

                if (!TabScroll && !controlPressed)
                {
                    var _tb_for_query = GetCommandTextBox();
                    string query = "";
                    if (_tb_for_query != null)
                        query = _tb_for_query.Text.Length == commandBase.Length ? "" : _tb_for_query.Text.Substring(commandBase.Length);

                    // For workspace commands, autocomplete with workspace names
                    var wsNames = Directory.GetFiles(workspaceFilesPath)?.Where(f => f.ToLower().EndsWith(".cloudless"))?.Select(f => Path.GetFileNameWithoutExtension(f))?.ToList();
                    wsNames ??= new List<string>();
                    wsNames = wsNames.Where(ws => !IsReservedWorkspaceName(ws) && ws.ToLower().Contains(query.ToLower())).Order().ToList();
                    AutocompleteCandidates.Clear();
                    foreach (var wsName in wsNames)
                    {
                        AutocompleteCandidates.AddLast(wsName);
                    }
                    TabScroll = true;
                }
                else if (!TabScrollCtrl && controlPressed)
                {
                    // Ctrl+Tab cycles workspace recency.
                    var wsNames = Directory.GetFiles(workspaceFilesPath)?.Where(f => f.ToLower().EndsWith(".cloudless"))?.Select(f => Path.GetFileNameWithoutExtension(f))?.ToList();
                    wsNames ??= new List<string>();
                    var recentNames = GetRecentlySavedAndLoadedWorkspaceNames();
                    wsNames = recentNames.Where(ws => wsNames.Contains(ws)).ToList();
                    wsNames = wsNames.Where(ws => !IsReservedWorkspaceName(ws)).ToList();
                    AutocompleteCandidatesCtrl.Clear();
                    foreach (var wsName in wsNames)
                    {
                        AutocompleteCandidatesCtrl.AddFirst(wsName);
                    }
                    TabScrollCtrl = true;
                }
                CycleToNextAutocompleteCandidate(commandBase, reverse: shiftPressed, recency: controlPressed);
                return;
            }

            var textBox = GetCommandTextBox();
            if (textBox != null && controlPressed)
            {
                CycleCommandSuggestion(reverse: shiftPressed);
                return;
            }

            string? completion = textBox != null && _commandSuggestionInput == _tbTextPrev &&
                                 _commandSuggestionCaretIndex == textBox.CaretIndex &&
                                 _commandSuggestionIndex < _commandSuggestionCandidates.Count
                ? _commandSuggestionCandidates[_commandSuggestionIndex]
                : textBox == null ? null : GetCommandCompletion(_tbTextPrev, textBox.CaretIndex);
            if (completion != null)
            {
                textBox!.Text = completion;
                textBox.CaretIndex = completion.Length;
                UpdateCommandSuggestion();
            }
        }

        internal static bool TryGetTagAutocompleteContext(string text, out string completionPrefix, out string query)
        {
            completionPrefix = "";
            query = "";

            foreach (string commandBase in CommandSyntaxCatalog.GetTagListAutocompleteCommandBases())
            {
                string prefix = commandBase + " ";
                if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string args = text.Substring(prefix.Length);
                int lastWhitespace = args.LastIndexOfAny(new[] { ' ', '\t', '\r', '\n' });
                if (args.Length == 0 || char.IsWhiteSpace(args[^1]))
                {
                    completionPrefix = text;
                }
                else
                {
                    completionPrefix = text.Substring(0, prefix.Length + lastWhitespace + 1);
                    query = args.Substring(lastWhitespace + 1);
                }

                return true;
            }

            foreach (string commandBase in CommandSyntaxCatalog.GetTagQueryAutocompleteCommandBases())
            {
                string prefix = commandBase + " ";
                if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string args = text.Substring(prefix.Length);
                string trimmedArgs = args.TrimEnd();
                bool hasTrailingWhitespace = trimmedArgs.Length < args.Length;
                if (trimmedArgs.Length == 0)
                {
                    completionPrefix = text;
                    return true;
                }

                int lastDelimiter = trimmedArgs.LastIndexOfAny(new[] { ' ', '\t', '\r', '\n', '(' });
                if (trimmedArgs.EndsWith('('))
                {
                    completionPrefix = text;
                    return true;
                }

                string lastToken = trimmedArgs.Substring(lastDelimiter + 1);
                bool isOperator = lastToken.Equals("AND", StringComparison.OrdinalIgnoreCase) ||
                                  lastToken.Equals("OR", StringComparison.OrdinalIgnoreCase) ||
                                  lastToken.Equals("NOT", StringComparison.OrdinalIgnoreCase);

                if (isOperator)
                {
                    completionPrefix = hasTrailingWhitespace ? text : text + " ";
                    return true;
                }

                if (hasTrailingWhitespace || lastToken.EndsWith(')'))
                    return false;

                completionPrefix = text.Substring(0, prefix.Length + lastDelimiter + 1);
                query = lastToken;
                return true;
            }

            return false;
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
                    var _tb_for_cycle = GetCommandTextBox();
                    if (_tb_for_cycle != null)
                        _tb_for_cycle.Text = commandBase + next;
                    if ((_tb_for_cycle?.Text ?? "").Equals(PreviousTabScrollText))  // ...then the user is "changing direction", so repeat the movement
                {
                    candidates.AddLast(next);
                    next = candidates.First();
                    candidates.RemoveFirst();
                        if (_tb_for_cycle != null)
                            _tb_for_cycle.Text = commandBase + next;
                }
                PreviousTabScrollText = _tb_for_cycle?.Text ?? PreviousTabScrollText;
                candidates.AddLast(next);
                if (_tb_for_cycle != null)
                    _tb_for_cycle.CaretIndex = _tb_for_cycle.Text.Length;
            }
            else
            {
                var next = candidates.Last();
                candidates.RemoveLast();
                var _tb_for_cycle2 = GetCommandTextBox();
                if (_tb_for_cycle2 != null)
                    _tb_for_cycle2.Text = commandBase + next;
                if ((_tb_for_cycle2?.Text ?? "").Equals(PreviousTabScrollText))  // ...then the user is "changing direction" so repeat the movement
                {
                    candidates.AddFirst(next);
                    next = candidates.Last();
                    candidates.RemoveLast();
                    if (_tb_for_cycle2 != null)
                        _tb_for_cycle2.Text = commandBase + next;
                }
                PreviousTabScrollText = _tb_for_cycle2?.Text ?? PreviousTabScrollText;
                candidates.AddFirst(next);
                if (_tb_for_cycle2 != null)
                    _tb_for_cycle2.CaretIndex = _tb_for_cycle2.Text.Length;
            }
        }

        private async Task SendCommandToOthers(string cmd)
        {
            var others = Application.Current.Windows
                .OfType<MainWindow>()
                .Where(w => w != this)
                .ToList();

            foreach (var w in others)
            {
                await w.ExecuteCommand(cmd);
            }
        }

        //private static bool TryGetCommandArguments(string command, out string arguments, params string[] commandNames)
        //{
        //    foreach (string commandName in commandNames)
        //    {
        //        if (command.Equals(commandName, StringComparison.OrdinalIgnoreCase))
        //        {
        //            arguments = string.Empty;
        //            return true;
        //        }

        //        if (command.StartsWith(commandName + " ", StringComparison.OrdinalIgnoreCase))
        //        {
        //            arguments = command[(commandName.Length + 1)..].Trim();
        //            return true;
        //        }
        //    }

        //    arguments = string.Empty;
        //    return false;
        //}

        private void HandleVideoTrackDelayCommand(string trackType, string arguments, bool isSubtitle)
        {
            if (VideoHost.Content is not IVideoPlayer videoPlayer)
            {
                Message($"The {trackType} delay command requires a loaded video.");
                return;
            }

            long currentDelay = isSubtitle ? videoPlayer.GetSubtitleDelay() : videoPlayer.GetAudioDelay();
            if (string.IsNullOrWhiteSpace(arguments))
            {
                Message($"Current {trackType}/video sync offset: {FormatVideoDelay(currentDelay)}");
                return;
            }

            if (!TryParseVideoDelay(arguments, out long requestedDelay))
            {
                Message($"Invalid {trackType}/video sync offset. Provide a time in seconds or milliseconds, e.g. {trackType} sync -0.25 or {trackType} sync 120ms.");
                return;
            }

            bool succeeded = isSubtitle
                ? videoPlayer.SetSubtitleDelay(requestedDelay)
                : videoPlayer.SetAudioDelay(requestedDelay);
            if (!succeeded)
            {
                Message($"Failed to set {trackType}/video sync offset.");
                return;
            }

            Message($"Set {trackType}/video sync offset to {FormatVideoDelay(requestedDelay)}");
            return;
        }

        internal static bool TryParseVideoDelay(string value, out long delayMicroseconds)
        {
            delayMicroseconds = 0;
            value = value.Trim();
            decimal multiplier = 1_000_000m;

            if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = 1_000m;
                value = value[..^2].Trim();
            }
            else if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                value = value[..^1].Trim();
            }

            if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal amount))
                return false;

            decimal microseconds;
            try
            {
                microseconds = decimal.Round(amount * multiplier, 0, MidpointRounding.AwayFromZero);
            }
            catch (OverflowException)
            {
                return false;
            }

            if (microseconds < long.MinValue || microseconds > long.MaxValue)
                return false;

            delayMicroseconds = (long)microseconds;
            return true;
        }

        internal static string FormatVideoDelay(long delayMicroseconds)
        {
            double seconds = delayMicroseconds / 1_000_000d;
            double milliseconds = delayMicroseconds / 1_000d;
            return $"{seconds.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture)} s ({milliseconds.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture)} ms)";
        }

        // returns whether successful (i.e. valid) command
        private async Task<bool> ExecuteCommandInner(string cmd)
        {
            cmd = cmd.Trim().ToLower();

            CommandParseResult catalogParse = CommandSyntaxCatalog.Parse(cmd);
            if (catalogParse.IsRecognized)
                return await ExecuteCatalogCommand(catalogParse);

            if (cmd.Equals("meteor shower"))
            {
                LaunchMeteorShower();
                return true;
            }

            if (cmd.Equals("meteor"))
            {
                LaunchShootingStar();
                return true;
            }

            if (cmd.StartsWith("echo ") && cmd.Length > 5)
            {
                string echo = cmd.Substring(5).Trim();
                Message("echo: " + echo);
                return true;
            }

            Message("Command not recognized");
            return false;
        }

        private int? GetPageIndexFromTargetToken(string targetToken)
        {
            int current = GetCurrentPageIndex();
            if (int.TryParse(targetToken, out int numericTarget))
            {
                return numericTarget;
            }
            else if (targetToken == "na")
            {
                var activePages = GetNonemptyPages();
                int nextActivePage = activePages?.Where(p => p > current)?.Order().FirstOrDefault() ?? 0;
                if (nextActivePage == 0)
                    nextActivePage = activePages?.Order().FirstOrDefault() ?? 0;
                return nextActivePage != 0 ? nextActivePage : null;
            }
            else if (targetToken == "pa")
            {
                var activePages = GetNonemptyPages();
                int prevActivePage = activePages?.Where(p => p < current)?.Order().LastOrDefault() ?? 0;
                if (prevActivePage == 0)
                    prevActivePage = activePages?.Order().LastOrDefault() ?? 0;
                return prevActivePage != 0 ? prevActivePage : null;
            }
            else if (targetToken == "ni")
            {
                var inactivePages = GetInactivePages();
                int nextInactivePage = inactivePages?.Where(p => p > current)?.Order().FirstOrDefault() ?? 0;
                if (nextInactivePage == 0)
                    nextInactivePage = inactivePages?.Order().FirstOrDefault() ?? 0;
                return nextInactivePage != 0 ? nextInactivePage : null;
            }
            else if (targetToken == "pi")
            {
                var inactivePages = GetInactivePages();
                int prevInactivePage = inactivePages?.Where(p => p < current)?.Order().LastOrDefault() ?? 0;
                if (prevInactivePage == 0)
                    prevInactivePage = inactivePages?.Order().LastOrDefault() ?? 0;
                return prevInactivePage != 0 ? prevInactivePage : null;
            }
            else if (targetToken == "next" || targetToken == "n")
                return current + 1 == 21 ? 1 : current + 1;
            else if (targetToken == "previous" || targetToken == "p" || targetToken == "prev")
                return current - 1 == 0 ? 20 : current - 1;
            else if (targetToken == "this" || targetToken == "t")
                return current;

            return null;
        }

        private async Task<bool> ExecuteCatalogCommand(CommandParseResult parsed)
        {
            if (!parsed.IsComplete)
            {
                Message(parsed.ErrorMessage ?? "Incomplete or invalid command. See command palette reference.");
                return false;
            }

            if (parsed.Operation == "clear-selection")
            {
                GlobalMouseModeManager.ClearSelection();
                return true;
            }

            if (parsed.Operation == "invert-selection")
            {
                GlobalMouseModeManager.InvertSelection();
                return true;
            }

            if (parsed.DefinitionId == "video-position")
                return ExecuteVideoPositionCommand(parsed);

            if (parsed.DefinitionId == "sort")
            {
                string? sortType = parsed.GetArgument("type");
                string? sortOrder = parsed.GetArgument("order");
                Cloudless.Properties.Settings.Default.ImageDirectorySortOrder = (sortType, sortOrder) switch
                {
                    ("name", "asc") => "FileNameAscending",
                    ("name", "desc") => "FileNameDescending",
                    ("date", "asc") => "DateModifiedAscending",
                    ("date", "desc") => "DateModifiedDescending",
                    _ => throw new InvalidOperationException("The catalog produced an invalid sort command.")
                };
                Cloudless.Properties.Settings.Default.Save();
                SortImageFilesArray();
                return true;
            }

            if (parsed.DefinitionId == "display-mode")
            {
                Cloudless.Properties.Settings.Default.DisplayMode = parsed.GetArgument("mode") switch
                {
                    "stretch" => "StretchToFit",
                    "zoom" => "ZoomToFill",
                    "best" => "BestFit",
                    "bestnozoom" => "BestFitWithoutZooming",
                    _ => throw new InvalidOperationException("The catalog produced an invalid display-mode command.")
                };
                Cloudless.Properties.Settings.Default.Save();
                ApplyDisplayMode();
                return true;
            }

            if (parsed.DefinitionId == "reverse-image-search")
            {
                await ReverseImageSearch(currentlyDisplayedImagePath, parsed.GetArgument("service") ?? "g");
                return true;
            }

            if (parsed.DefinitionId == "window-nudge")
            {
                if (parsed.Operation == "nudge-vector")
                {
                    NudgeWindow(int.Parse(parsed.GetArgument("x")!), int.Parse(parsed.GetArgument("y")!));
                    return true;
                }

                int count = int.TryParse(parsed.GetArgument("count"), out int parsedCount) ? parsedCount : 1;
                int dx = parsed.GetArgument("direction") switch
                {
                    "left" => -count,
                    "right" => count,
                    _ => 0
                };
                int dy = parsed.GetArgument("direction") switch
                {
                    "up" => -count,
                    "down" => count,
                    _ => 0
                };
                NudgeWindow(dx, dy);
                return true;
            }

            if (parsed.DefinitionId == "video-transport")
                return ExecuteVideoTransportCommand(parsed);

            if (parsed.DefinitionId == "filmstrip")
            {
                if (parsed.Operation == "filmstrip-tag-query")
                    return await HandleTagFilmstrip(parsed.GetArgument("query")!);
                return ExecuteFilmstripCommand(parsed);
            }

            if (parsed.DefinitionId == "gallery")
            {
                if (parsed.Operation == "gallery-tag-query")
                    return await HandleTagGallery(parsed.GetArgument("query")!);
                if (parsed.Operation == "gallery-directory")
                    return HandleGalleryDirectory(null);
                if (parsed.Operation == "gallery-directory-path")
                    return HandleGalleryDirectory(parsed.GetArgument("path")!);
            }

            if (parsed.DefinitionId == "window-dimensions")
                return ExecuteWindowDimensionsCommand(parsed);

            if (parsed.DefinitionId == "workspace")
                return await ExecuteWorkspaceCommand(parsed);

            if (parsed.DefinitionId == "hotkey")
            {
                string? keyToken = parsed.GetArgument("key");
                if (keyToken == null || !CommandSyntaxCatalog.TryResolveHotkeyKey(keyToken, out Key key))
                {
                    Message("Failed to parse hotkey name. Try names like 'left', 'space', 'f1' or a single character.");
                    return false;
                }

                IReadOnlyList<string> modifiers = parsed.GetArguments("modifier");
                bool control = modifiers.Contains("ctrl", StringComparer.OrdinalIgnoreCase);
                bool alt = modifiers.Contains("alt", StringComparer.OrdinalIgnoreCase);
                bool shift = modifiers.Contains("shift", StringComparer.OrdinalIgnoreCase);
                await SimulateKeyEvent(key, shift, control, alt);
                return true;
            }

            if (parsed.DefinitionId == "general-actions" && parsed.Operation == "selected-wrapper")
                return await ExecuteSelectedCommand(parsed.GetArgument("wrappedCommand")!);

            if (parsed.DefinitionId == "general-actions")
                return ExecuteGeneralAction(parsed.Operation);

            if (parsed.DefinitionId == "slideshow")
                return ExecuteSlideshowCommand(parsed);

            if (parsed.DefinitionId == "tags")
                return ExecuteTagCatalogCommand(parsed);

            if (parsed.DefinitionId == "image-navigation")
                return await ExecuteImageNavigationCommand(parsed);

            if (parsed.DefinitionId == "macros")
                return await ExecuteMacroCommand(parsed);

            if (parsed.DefinitionId == "custom-commands")
                return await ExecuteCustomCommand(parsed);

            if (parsed.DefinitionId == "command-wrappers")
                return await ExecuteCommandWrapper(parsed);

            if (parsed.DefinitionId == "open-path")
            {
                if (parsed.Operation == "open-tag-query")
                    return await HandleTagOpen(parsed.GetArgument("query")!, maxFiles: 10);
                if (parsed.Operation == "open-all-tag-query")
                    return await HandleTagOpen(parsed.GetArgument("query")!, maxFiles: int.MaxValue);

                string path = parsed.GetArgument("path")!;
                string resolvedPath = currentDirectory == null ? path : Path.GetFullPath(path, currentDirectory);
                if (Directory.Exists(resolvedPath))
                    await LoadImagesInDirectory(resolvedPath, parsed.Operation == "open-all-path");
                else if (File.Exists(resolvedPath))
                    await LoadImage(resolvedPath, true);
                return true;
            }

            return ExecutePageCommand(parsed);
        }

        private async Task<bool> ExecuteMacroCommand(CommandParseResult parsed)
        {
            switch (parsed.Operation)
            {
                case "macro-record":
                    Message("'macro record' commands are not valid inside macros or nested commands. To record a macro, use the command palette directly.");
                    return true;
                case "macro-run":
                    await RunUserMacro(parsed.GetArgument("name")!.Trim());
                    return true;
                case "macro-delete":
                    string name = parsed.GetArgument("name")!.Trim();
                    var macros = LoadUserMacros();
                    if (macros.Remove(name))
                    {
                        SaveUserMacros(macros);
                        Message($"Deleted macro '{name}'");
                    }
                    else
                        Message($"Macro not found: {name}");
                    return true;
                case "macro-list":
                    var savedMacros = LoadUserMacros();
                    if (savedMacros.Count == 0)
                        Message("No macros defined");
                    else
                    {
                        foreach (var macro in savedMacros)
                            Message($"{macro.Key}: {macro.Value}");
                    }
                    return true;
                default:
                    return false;
            }
        }

        private async Task<bool> ExecuteCustomCommand(CommandParseResult parsed)
        {
            int index = int.Parse(parsed.GetArgument("customIndex")![1..]) - 1;
            switch (parsed.Operation)
            {
                case "custom-command-set":
                    SetUserCommand(index, parsed.GetArgument("commandText")!);
                    return true;
                case "custom-command-view":
                    ViewUserCommand(index);
                    return true;
                case "custom-command-run":
                    await RunUserCommand(index);
                    return true;
                default:
                    return false;
            }
        }

        private async Task<bool> ExecuteCommandWrapper(CommandParseResult parsed)
        {
            string command = parsed.GetArgument("wrappedCommand")!;
            await SendCommandToOthers(command);
            if (parsed.Operation == "command-wrapper-all")
                await ExecuteCommand(command);
            return true;
        }

        private async Task<bool> ExecuteSelectedCommand(string command)
        {
            foreach (MainWindow window in GlobalMouseModeManager.GetSelectedWindows())
                await window.ExecuteCommand(command);
            return true;
        }

        private async Task<bool> ExecuteImageNavigationCommand(CommandParseResult parsed)
        {
            switch (parsed.Operation)
            {
                case "image-first":
                    if (imageFiles != null)
                        await JumpToIndex(0);
                    return true;
                case "image-last":
                    if (imageFiles != null)
                        await JumpToIndex(imageFiles.Length - 1);
                    return true;
                case "image-recent":
                    string? recentPath = recentFiles?.FirstOrDefault();
                    if (recentPath != null)
                        await OpenRecentFile(recentPath);
                    return true;
                case "image-recent-count":
                    return await OpenRecentImagesInNewWindows(int.Parse(parsed.GetArgument("count")!));
                case "image-index":
                    if (imageFiles == null)
                        return true;

                    int targetIndex = int.Parse(parsed.GetArgument("index")!) - 1;
                    if (targetIndex == -1)
                        Message("You are in 1-indexing mode, so the first image has index 1, not 0.");
                    targetIndex = Math.Max(0, Math.Min(targetIndex, imageFiles.Length - 1));
                    await JumpToIndex(targetIndex);
                    return true;
                case "image-forward-offset":
                case "image-backward-offset":
                    if (imageFiles == null)
                        return true;

                    int offset = int.Parse(parsed.GetArgument("offset")!);
                    targetIndex = currentImageIndex + offset;
                    int clampedIndex = Math.Max(0, Math.Min(targetIndex, imageFiles.Length - 1));
                    if (clampedIndex != targetIndex)
                        Message(clampedIndex == 0
                            ? "Relative jump was clamped to first image in directory"
                            : "Relative jump was clamped to final image in directory");
                    await JumpToIndex(clampedIndex);
                    return true;
                case "image-filename-search":
                    string query = parsed.GetArgument("query") ?? string.Empty;
                    await ExecuteFilenameSearch(query[1..]);
                    return true;
                default:
                    return false;
            }
        }

        private async Task<bool> OpenRecentImagesInNewWindows(int count)
        {
            var paths = recentFiles?.Take(count).ToArray() ?? Array.Empty<string>();
            foreach (var path in paths)
            {
                if (!File.Exists(path))
                    continue;

                try
                {
                    var newWindow = await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var window = new MainWindow(path, workspaceLoad: true);
                        window.WorkspaceLoadInProgress = true;
                        return window;
                    });

                    var loadTask = await Application.Current.Dispatcher.InvokeAsync(() => newWindow.LoadImage(path, true));
                    await loadTask;
                    await Application.Current.Dispatcher.InvokeAsync(() => newWindow.Show());
                    await newWindow.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                    if (newWindow.VideoHost.Content is Cloudless.PluginBase.IVideoPlayer)
                    {
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        while (stopwatch.ElapsedMilliseconds < 2000)
                        {
                            try
                            {
                                var dimensionsTask = await newWindow.Dispatcher.InvokeAsync(() =>
                                    (newWindow.VideoHost.Content as Cloudless.PluginBase.IVideoPlayer)?.GetDimensions());

                                if (dimensionsTask != null)
                                {
                                    var dimensions = await dimensionsTask.WaitAsync(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
                                    if (dimensions != null)
                                        break;
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Recent-media video dimension polling error: {ex}");
                            }

                            await Task.Delay(100).ConfigureAwait(false);
                        }
                    }

                    await newWindow.Dispatcher.InvokeAsync(() => newWindow.ResizeWindowToImage(), System.Windows.Threading.DispatcherPriority.Render);
                    await newWindow.Dispatcher.InvokeAsync(() => newWindow.CenterWindowOnCurrentScreen(), System.Windows.Threading.DispatcherPriority.Render);
                }
                catch (Exception ex)
                {
                    Message($"Failed to open recent media '{path}' in a new window: {ex.Message}");
                }
            }

            return true;
        }

        private bool ExecuteTagCatalogCommand(CommandParseResult parsed)
        {
            string tags = parsed.GetArgument("tags") ?? string.Empty;
            return parsed.Operation switch
            {
                "tag-add" => HandleTagAdd(tags),
                "tag-remove" => HandleTagRemove(tags),
                "tag-destroy" => HandleTagDestroy(tags),
                "tag-list" => HandleTagList(string.Empty),
                _ => false
            };
        }

        private bool ExecuteSlideshowCommand(CommandParseResult parsed)
        {
            switch (parsed.Operation)
            {
                case "slideshow-next":
                    SlideshowManager.NextSlideshowPage();
                    return true;
                case "slideshow-pause":
                    if (!SlideshowManager.IsRunning)
                        Message("No slideshow is running.");
                    else if (SlideshowManager.Pause())
                        Message("Slideshow paused.");
                    else
                        Message("Slideshow is already paused.");
                    return true;
                case "slideshow-resume":
                    if (!SlideshowManager.IsRunning)
                        Message("No slideshow is running.");
                    else if (SlideshowManager.Resume())
                        Message("Slideshow resumed.");
                    else
                        Message("Slideshow is not paused.");
                    return true;
                case "slideshow-status":
                    if (!SlideshowManager.IsRunning)
                    {
                        Message("No slideshow is running.");
                        return true;
                    }

                    string state = SlideshowManager.IsPaused ? "paused" : "running";
                    double intervalSeconds = SlideshowManager.CurrentIntervalSeconds;
                    string interval = intervalSeconds > 0
                        ? $"every {intervalSeconds:0.##} seconds"
                        : SlideshowManager.UseTriggers ? "trigger-based" : "without an interval";
                    string shuffle = SlideshowManager.IsShuffle ? "on" : "off";
                    string triggers = SlideshowManager.UseTriggers ? "on" : "off";
                    Message($"Slideshow is {state}; interval: {interval}; shuffle: {shuffle}; triggers: {triggers}.");
                    return true;
                case "slideshow-stop":
                    StopSlideshow();
                    return true;
                case "slideshow-start-triggers":
                case "slideshow-start-triggers-shuffle":
                    var activePages = GetNonemptyPages();
                    if (activePages.Count == 0)
                    {
                        Message("Cannot start slideshow: no active pages");
                        return true;
                    }

                    if (!SlideshowManager.AllPagesHaveTriggers(activePages))
                    {
                        Message("Cannot start trigger-only slideshow: not all active pages have a configured trigger");
                        return false;
                    }

                    StartSlideshow(0, parsed.Operation == "slideshow-start-triggers-shuffle", useTriggers: true);
                    return true;
                case "slideshow-start-interval":
                case "slideshow-start-interval-shuffle":
                    if (!double.TryParse(parsed.GetArgument("seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) || seconds <= 0)
                    {
                        Message("Invalid time format. Use a positive number (e.g., 'ss 5' for 5 seconds).");
                        return false;
                    }

                    StartSlideshow(seconds, parsed.Operation == "slideshow-start-interval-shuffle", useTriggers: false);
                    return true;
                default:
                    return false;
            }
        }

        private bool ExecuteGeneralAction(string? operation)
        {
            switch (operation)
            {
                case "close-window":
                    Close();
                    return true;
                case "close-all":
                    if (!SaveWorkspaceBeforeCloseAll())
                        return true;
                    CloseAllOtherInstances();
                    Close();
                    return true;
                case "close-others":
                    CloseAllOtherInstances();
                    return true;
                case "close-selected":
                    foreach (MainWindow selectedWindow in GlobalMouseModeManager.GetSelectedWindows())
                        selectedWindow.Close();
                    return true;
                case "close-empty":
                    CloseEmptyInstances();
                    return true;
                case "close-origin":
                    CloseWorkspaceOriginInstances();
                    return true;
                case "selection-count":
                    Message($"Selected windows: {GlobalMouseModeManager.SelectedWindowCount}");
                    return true;
                case "select-window":
                    GlobalMouseModeManager.SelectWindow(this);
                    return true;
                case "deselect-window":
                    GlobalMouseModeManager.DeselectWindow(this);
                    return true;
                case "minimize-all":
                    MinimizeAllOtherInstances();
                    MinimizeWindow();
                    return true;
                case "minimize-others":
                    MinimizeAllOtherInstances();
                    return true;
                case "minimize-origin":
                    MinimizeWorkspaceOriginInstances();
                    return true;
                case "unminimize-all":
                    UnminimizeAllOtherInstances();
                    return true;
                case "unminimize-origin":
                    UnminimizeWorkspaceOriginInstances();
                    return true;
                case "shutdown":
                    Shutdown();
                    return true;
                case "deflash":
                    Deflash();
                    return true;
                case "stack":
                    StackCurrentPageWindows();
                    return true;
                case "flatten":
                    FlattenPages();
                    return true;
                case "copy-image-path":
                    if (currentlyDisplayedImagePath == null)
                    {
                        Message("Cannot copy image path to clipboard because no image is loaded");
                        return true;
                    }

                    Clipboard.SetText(currentlyDisplayedImagePath);
                    Message("Copied image path to clipboard");
                    return true;
                case "reveal-image":
                    if (currentlyDisplayedImagePath == null)
                        Message("Cannot reveal image in Explorer because no image is loaded");
                    else
                        RevealImageInExplorer(currentlyDisplayedImagePath);
                    return true;
                case "help":
                    var window = CommandPaletteRef();
                    window.Activate();
                    window.Focus();
                    return true;
                default:
                    return false;
            }
        }

        private async Task<bool> ExecuteWorkspaceCommand(CommandParseResult parsed)
        {
            string workspaceName = parsed.GetArgument("workspace") ?? string.Empty;
            switch (parsed.Operation)
            {
                case "workspace-save":
                    SaveWorkspaceFromCommand(workspaceName, allowOverwrite: false);
                    return true;
                case "workspace-save-force":
                    SaveWorkspaceFromCommand(workspaceName, allowOverwrite: true);
                    return true;
                case "workspace-load":
                    await LoadWorkspace(workspaceName);
                    return true;
                case "workspace-merge":
                    if (await LoadWorkspace(workspaceName, true))
                        Message("Merged workspace: " + workspaceName);
                    return true;
                case "workspace-delete":
                    if (DeleteWorkspace(workspaceName))
                        Message("Deleted workspace: " + workspaceName);
                    return true;
                case "workspace-rename":
                    RenameWorkspace(new[] { parsed.GetArgument("oldName") ?? string.Empty, parsed.GetArgument("newName") ?? string.Empty });
                    return true;
                case "workspace-preview":
                    await PreviewWorkspace(workspaceName.Trim());
                    return true;
                case "workspace-origin":
                    RevealWorkspaceName();
                    return true;
                case "workspace-origin-save":
                    if (imageOriginalWorkspaceName == null)
                    {
                        Message("This window does not belong to a workspace");
                        return true;
                    }
                    if (IsReservedWorkspaceName(imageOriginalWorkspaceName))
                    {
                        Message($"Cannot save to origin: workspace origin is a system workspace: {imageOriginalWorkspaceName}");
                        return true;
                    }
                    SaveWorkspaceFromCommand(imageOriginalWorkspaceName, allowOverwrite: true);
                    return true;
                case "workspace-origin-load":
                    if (imageOriginalWorkspaceName == null)
                        Message("This window does not belong to a workspace");
                    else
                        await LoadWorkspace(imageOriginalWorkspaceName);
                    return true;
                case "workspace-list-current":
                    if (string.IsNullOrEmpty(currentlyDisplayedImagePath))
                    {
                        Message("No media is loaded.");
                        return true;
                    }
                    ListWorkspacesContainingPath(currentlyDisplayedImagePath);
                    return true;
                case "workspace-list-path":
                    string path = parsed.GetArgument("path")!.Trim();
                    if (!Path.IsPathRooted(path) && currentDirectory != null)
                        path = Path.GetFullPath(path, currentDirectory);
                    ListWorkspacesContainingPath(path);
                    return true;
                case "workspace-reveal":
                    RevealDirectoryInExplorer(workspaceFilesPath);
                    return true;
                case "workspace-undoload":
                    await UndoLoad();
                    return true;
                case "workspace-undocloseall":
                    await UndoCloseAll();
                    return true;
                case "workspace-quicksave":
                    Quicksave();
                    return true;
                case "workspace-quicksave-close":
                    if (Quicksave())
                    {
                        CloseAllOtherInstances();
                        Close();
                    }
                    return true;
                case "workspace-quickload":
                    await Quickload();
                    return true;
                case "workspace-quickmerge":
                    await Quickmerge();
                    return true;
                default:
                    return false;
            }
        }

        private void SaveWorkspaceFromCommand(string name, bool allowOverwrite)
        {
            (int windowCount, int pageCount, string? error) = SaveWorkspace(name, allowOverwrite);
            if (windowCount == -1)
                Message("Failed to save workspace due to unexpected error: " + error);
            else if (windowCount == -2)
                Message("A workspace by that name already exists. To overwrite it, use command 'ws s! [name]'");
            else if (windowCount == -3)
                Message("You're trying to save a workspace using a reserved name, which is not allowed. Did you mean to use a different command?");
            else
                Message($"Saved workspace {name} with {windowCount} windows and {pageCount} pages");
        }

        private bool ExecuteFilmstripCommand(CommandParseResult parsed)
        {
            string[] filesToShow;
            try
            {
                switch (parsed.Operation)
                {
                    case "filmstrip-current-directory":
                        filesToShow = FilmStripControl.HasRetainedContents
                            ? FilmStripControl.GetRetainedContents ?? Array.Empty<string>()
                            : imageFiles ?? Array.Empty<string>();
                        break;
                    case "filmstrip-directory":
                        filesToShow = imageFiles ?? Array.Empty<string>();
                        break;
                    case "filmstrip-directory-path":
                        string targetDirectory;
                        if (!TryResolveExplicitDirectoryPath(parsed.GetArgument("path")!, "Filmstrip", out targetDirectory))
                            return false;
                        filesToShow = GetSupportedMediaFilesInDirectory(targetDirectory);
                        break;
                    case "filmstrip-recent":
                        filesToShow = (recentFiles ?? new List<string>()).ToArray();
                        break;
                    case "filmstrip-bookmarks":
                        filesToShow = bookmarkManager?.GetBookmarks().ToArray() ?? Array.Empty<string>();
                        break;
                    case "filmstrip-preview":
                        string workspaceName = parsed.GetArgument("workspace")!.Trim();
                        string workspacePath = Path.Combine(workspaceFilesPath, workspaceName + ".cloudless");
                        if (!File.Exists(workspacePath))
                        {
                            Message("Workspace file not found: " + workspaceName);
                            return false;
                        }

                        string json = File.ReadAllText(workspacePath);
                        var workspace = JsonSerializer.Deserialize<CloudlessWorkspace>(json);
                        if (workspace == null)
                        {
                            Message("Invalid workspace file: " + workspaceName);
                            return false;
                        }
                        filesToShow = workspace.CloudlessWindows.Select(window => window.ImagePath).ToArray();
                        break;
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Message("Error preparing filmstrip: " + ex.Message);
                return false;
            }

            if (_filmStripWindow == null || !_filmStripWindow.IsVisible)
                ToggleFilmStrip(skipPopulation: true);

            if (_filmStripWindow == null)
            {
                Message("Failed to open filmstrip window.");
                return false;
            }

            NonstandardPopulateFilmStrip(filesToShow);
            return true;
        }

        private bool ExecuteWindowDimensionsCommand(CommandParseResult parsed)
        {
            if (parsed.Operation == "window-dimensions-query")
            {
                Message($"Current window dimensions: {Width:0.##} x {Height:0.##}");
                return true;
            }

            double width = Width;
            double height = Height;
            if (!TryApplyDimension(parsed.GetArgument("width")!, ref width) ||
                !TryApplyDimension(parsed.GetArgument("height")!, ref height))
            {
                Message(CommandSyntaxCatalog.GetDimensionUsageMessage());
                return false;
            }

            if (width <= 0 || height <= 0)
            {
                Message("Window dimensions must be greater than zero.");
                return false;
            }

            ResizeWindow(width, height);
            CenterWindowOnCurrentScreen();
            return true;
        }

        private static bool TryApplyDimension(string value, ref double dimension)
        {
            if (value == "_")
                return true;

            if (!int.TryParse(value, out int amount))
                return false;

            if (value.StartsWith('+') || value.StartsWith('-'))
                dimension += amount;
            else
                dimension = amount;

            return true;
        }

        private bool ExecuteVideoTransportCommand(CommandParseResult parsed)
        {
            switch (parsed.Operation)
            {
                case "video-mute":
                    ToggleMute(setTo: true);
                    return true;
                case "video-unmute":
                    ToggleMute(setTo: false);
                    return true;
                case "video-volume-query":
                    if (VideoHost.Content is IVideoPlayer volumePlayer)
                        Message($"Current volume: {(int)Math.Round(volumePlayer.GetVolume())}/100");
                    return true;
                case "video-volume-set":
                    if (!double.TryParse(parsed.GetArgument("number"), out double requestedVolume))
                    {
                        Message("Invalid volume value. Provide a number between 0 and 100.");
                        return true;
                    }
                    int volume = (int)Math.Round(requestedVolume);
                    if (volume < 0 || volume > 100)
                    {
                        Message("Volume must be from 0 to 100.");
                        return true;
                    }
                    if (VideoHost.Content is not IVideoPlayer volumeTarget)
                    {
                        Message("Command not recognized");
                        return false;
                    }
                    volumeTarget.SetVolume(volume);
                    _windowVideoVolume = volume;
                    Message($"Set volume to: {volume}/100");
                    return true;
                case "video-speed-query":
                case "video-speed-set":
                    if (VideoHost.Content is not IVideoPlayer speedPlayer)
                    {
                        Message("No video is loaded");
                        return true;
                    }
                    if (parsed.Operation == "video-speed-query")
                    {
                        double currentSpeed = speedPlayer.GetPlaybackSpeed();
                        Message($"Current playback speed: {currentSpeed.ToString("0.##", CultureInfo.InvariantCulture)}×");
                        return true;
                    }
                    double speed = double.Parse(parsed.GetArgument("multiplier")!, NumberStyles.Float, CultureInfo.InvariantCulture);
                    if (!speedPlayer.SetPlaybackSpeed(speed))
                    {
                        Message("The video player could not set that playback speed.");
                        return true;
                    }
                    Message($"Set playback speed to {speed.ToString("0.##", CultureInfo.InvariantCulture)}×");
                    return true;
                case "video-audio-sync-query":
                case "video-audio-sync-set":
                    HandleVideoTrackDelayCommand("audio", parsed.GetArgument("offset") ?? string.Empty, isSubtitle: false);
                    return true;
                case "video-subtitle-sync-query":
                case "video-subtitle-sync-set":
                    HandleVideoTrackDelayCommand("subtitle", parsed.GetArgument("offset") ?? string.Empty, isSubtitle: true);
                    return true;
                case "video-play":
                    return ExecuteVideoPlaybackToggle(pause: false);
                case "video-pause":
                    return ExecuteVideoPlaybackToggle(pause: true);
                case "video-sync":
                    if (VideoHost.Content is not IVideoPlayer)
                    {
                        Message("No video is loaded");
                        return false;
                    }
                    SetSync(true);
                    Message("Added this window to the page sync group");
                    return true;
                case "video-unsync":
                    SetSync(false);
                    Message("Removed this window from the page sync group");
                    return true;
                case "video-seek-status":
                case "video-seek-previous":
                case "video-seek-absolute":
                case "video-seek-forward":
                case "video-seek-backward":
                    return ExecuteSeekCommand(parsed);
                default:
                    return false;
            }
        }

        private bool ExecuteVideoPlaybackToggle(bool pause)
        {
            string commandName = pause ? "pause" : "play";
            string ext = Path.GetExtension(currentlyDisplayedImagePath) ?? string.Empty;
            if (!FileTypeManager.IsVideoFile(ext))
            {
                Message($"The '{commandName}' command only works when a video is loaded");
                return true;
            }

            if (VideoHost.Content is not IVideoPlayer videoPlayer)
            {
                Message("No video player available");
                return true;
            }

            try
            {
                bool isPaused = videoPlayer.IsPaused();
                if (pause && !isPaused)
                {
                    videoPlayer.TogglePause();
                    Message("Paused video playback");
                }
                else if (!pause && isPaused)
                {
                    videoPlayer.TogglePause();
                    Message("Resumed video playback");
                }
                else
                {
                    Message(pause ? "Video is already paused" : "Video is already playing");
                }
            }
            catch (Exception ex)
            {
                Message($"Failed to {commandName} video: {ex.Message}");
            }

            return true;
        }

        private bool ExecuteSeekCommand(CommandParseResult parsed)
        {
            string ext = Path.GetExtension(currentlyDisplayedImagePath)?.ToLowerInvariant() ?? string.Empty;
            bool isVideo = ext is ".webm" or ".mkv" or ".mp4" or ".avi" or ".mov";
            if (!isVideo)
            {
                Message("The 'seek' command only works when a video is loaded");
                return true;
            }

            if (VideoHost.Content is not IVideoPlayer videoPlayer)
            {
                Message("No video player available");
                return true;
            }

            if (parsed.Operation == "video-seek-previous")
            {
                TimeSpan? previousPosition = VideoPlaybackPositionStore.GetPosition(currentlyDisplayedImagePath);
                if (!previousPosition.HasValue)
                {
                    Message("No previous closed position is saved for this video.");
                    return true;
                }

                TimeSpan target = previousPosition.Value;
                TimeSpan duration = videoPlayer.GetDuration();
                if (duration > TimeSpan.Zero && target > duration)
                    target = duration;

                videoPlayer.SeekTo(target);
                Message($"Seeking to previous closed position: {FormatTimeSpan(target)}");
                return true;
            }

            if (parsed.Operation == "video-seek-status")
            {
                TimeSpan position = videoPlayer.GetPosition();
                TimeSpan duration = videoPlayer.GetDuration();
                Message($"Current position: {position}, Duration: {duration}");
                return true;
            }

            string timeText = parsed.GetArgument("time") ?? string.Empty;
            if (parsed.Operation is "video-seek-forward" or "video-seek-backward")
            {
                char sign = parsed.Operation == "video-seek-forward" ? '+' : '-';
                TimeSpan? delta = TryParseTimeString(timeText[1..].Trim());
                if (!delta.HasValue)
                {
                    Message($"Could not parse relative time '{timeText}'. Use formats like: '+90' (seconds), '+1:30' (m:s), '-30s'");
                    return true;
                }

                TimeSpan current = videoPlayer.GetPosition();
                TimeSpan target = sign == '+' ? current + delta.Value : current - delta.Value;
                if (target < TimeSpan.Zero)
                    target = TimeSpan.Zero;
                TimeSpan duration = videoPlayer.GetDuration();
                if (duration > TimeSpan.Zero && target > duration)
                    target = duration;
                videoPlayer.SeekTo(target);
                string direction = sign == '+' ? "forward" : "backward";
                Message($"Seeking {direction} by {FormatTimeSpan(delta.Value)} to {FormatTimeSpan(target)} (was {FormatTimeSpan(current)})");
                return true;
            }

            TimeSpan? targetTime = TryParseTimeString(timeText);
            if (!targetTime.HasValue)
            {
                Message($"Could not parse time '{timeText}'. Use formats like: '90' (seconds), '1:30' (m:s), '1:30:45' (h:m:s), or '1h30m45s'");
                return true;
            }
            if (targetTime.Value < TimeSpan.Zero)
            {
                Message("Time cannot be negative");
                return true;
            }

            TimeSpan durationAbs = videoPlayer.GetDuration();
            if (durationAbs > TimeSpan.Zero && targetTime.Value > durationAbs)
            {
                Message($"Seek time {FormatTimeSpan(targetTime.Value)} exceeds video duration of {FormatTimeSpan(durationAbs)}");
                return true;
            }
            videoPlayer.SeekTo(targetTime.Value);
            Message($"Seeking to {FormatTimeSpan(targetTime.Value)}");
            return true;
        }

        private bool ExecuteVideoPositionCommand(CommandParseResult parsed)
        {
            var videoPlayer = VideoHost.Content as IVideoPlayer;
            switch (parsed.Operation)
            {
                case "video-set-start":
                case "video-set-end":
                case "video-set-flag":
                    if (videoPlayer == null)
                    {
                        Message("No video is loaded");
                        return false;
                    }

                    TimeSpan position = videoPlayer.GetPosition();
                    if (parsed.Operation == "video-set-start")
                    {
                        _videoLoopStart = position;
                        videoPlayer.SetLoopRange(_videoLoopStart, _videoLoopEnd);
                        UpdateVideoControls();
                        Message($"Set loop start to {position}");
                    }
                    else if (parsed.Operation == "video-set-end")
                    {
                        _videoLoopEnd = position;
                        videoPlayer.SetLoopRange(_videoLoopStart, _videoLoopEnd);
                        UpdateVideoControls();
                        Message($"Set loop end to {position}");
                    }
                    else
                    {
                        _videoFlag = position;
                        UpdateVideoControls();
                        Message($"Set flag to {position}");
                    }
                    return true;

                case "video-clear-start":
                case "video-clear-end":
                    if (videoPlayer == null)
                    {
                        Message("No video is loaded");
                        return false;
                    }

                    if (parsed.Operation == "video-clear-start")
                    {
                        _videoLoopStart = null;
                        videoPlayer.SetLoopRange(_videoLoopStart, _videoLoopEnd);
                        Message("Reset loop start");
                    }
                    else
                    {
                        _videoLoopEnd = null;
                        videoPlayer.SetLoopRange(_videoLoopStart, _videoLoopEnd);
                        Message("Reset loop end");
                    }
                    UpdateVideoControls();
                    return true;

                case "video-clear-flag":
                    _videoFlag = null;
                    UpdateVideoControls();
                    Message("Cleared flag");
                    return true;

                case "video-goto-start":
                    if (videoPlayer == null)
                    {
                        Message("No video is loaded");
                        return true;
                    }
                    SeekToVideoPosition(videoPlayer, _videoLoopStart ?? TimeSpan.Zero, "start");
                    return true;

                case "video-goto-end":
                    if (videoPlayer == null)
                    {
                        Message("No video is loaded");
                        return true;
                    }

                    TimeSpan? endTarget = _videoLoopEnd;
                    if (!endTarget.HasValue)
                    {
                        try
                        {
                            TimeSpan duration = videoPlayer.GetDuration();
                            if (duration <= TimeSpan.Zero)
                            {
                                Message("Cannot determine video duration to seek to end");
                                return true;
                            }
                            endTarget = duration;
                        }
                        catch (Exception ex)
                        {
                            Message($"Failed to determine video duration: {ex.Message}");
                            return true;
                        }
                    }
                    SeekToVideoPosition(videoPlayer, endTarget.Value, "end");
                    return true;

                case "video-goto-flag":
                    if (videoPlayer == null)
                    {
                        Message("No video is loaded");
                        return false;
                    }
                    if (!_videoFlag.HasValue)
                    {
                        Message("No flag is set for this window");
                        return false;
                    }
                    try
                    {
                        videoPlayer.SeekTo(_videoFlag.Value);
                        Message($"Seeked to flag: {_videoFlag.Value}");
                    }
                    catch (Exception ex)
                    {
                        Message("Failed to seek to flag: " + ex.Message);
                    }
                    return true;

                case "video-set-trigger":
                    int triggerCount = int.TryParse(parsed.GetArgument("count"), out int parsedCount) ? parsedCount : 1;
                    SetSlideshowTrigger(triggerCount);
                    Message($"Marked this window as a slideshow trigger (requires {triggerCount} hit{(triggerCount > 1 ? "s" : "")})");
                    return true;

                case "video-clear-trigger":
                    SetSlideshowTrigger(0);
                    Message("Cleared slideshow trigger for this window");
                    return true;

                default:
                    return false;
            }
        }

        private void SeekToVideoPosition(IVideoPlayer videoPlayer, TimeSpan target, string positionName)
        {
            try
            {
                videoPlayer.SeekTo(target);
                Message($"Seeked to {positionName}: {FormatTimeSpan(target)}");
            }
            catch (Exception ex)
            {
                Message($"Failed to seek to {positionName}: {ex.Message}");
            }
        }

        private bool ExecutePageCommand(CommandParseResult parsed)
        {
            switch (parsed.Operation)
            {
                case "page-status":
                    Message($"On page {windowPageIndex}. Non-empty pages: " + string.Join(", ", GetNonemptyPages()));
                    return true;
                case "page-arrange":
                    BringActivePagesTogether();
                    return true;
            }

            string targetToken = parsed.GetArgument("target") ?? string.Empty;
            if (parsed.Operation == "page-view" && targetToken is "na" or "pa" or "ni" or "pi")
            {
                int? activePage = GetPageIndexFromTargetToken(targetToken);
                if (activePage.HasValue)
                    SwapViewToPage(activePage.Value);
                else if (targetToken is "na" or "pa")
                    Message("There are no other active pages.");
                else
                    Message("There are no other inactive pages.");
                return true;
            }

            int? resolvedTarget = GetPageIndexFromTargetToken(targetToken);
            if (!resolvedTarget.HasValue)
            {
                Message("Could not resolve the page target. See command palette reference.");
                return false;
            }

            int target = resolvedTarget.Value;
            switch (parsed.Operation)
            {
                case "page-view":
                    SwapViewToPage(target);
                    return true;
                case "page-send-window":
                    SendWindowToPage(target);
                    return true;
                case "page-bring-window":
                    SendWindowToPage(target);
                    SwapViewToPage(target);
                    return true;
                case "page-send-page":
                    SendPageToPage(target);
                    return true;
                case "page-bring-page":
                    SendPageToPage(target);
                    SwapViewToPage(target);
                    return true;
                case "page-clear":
                    ClearPage(target);
                    return true;
                case "page-swap":
                    int? secondTarget = GetPageIndexFromTargetToken(parsed.GetArgument("secondTarget") ?? string.Empty);
                    if (!secondTarget.HasValue)
                    {
                        Message("Swap command requires a valid second page target. Usage: p <page1> swap <page2>");
                        return false;
                    }
                    SwapPageWithPage(target, secondTarget.Value);
                    return true;
                default:
                    return false;
            }
        }

        private async Task ReverseImageSearch(string filePath, string serviceArg) {
            var validServiceArgs = new List<string>() { "google", "g", "bing", "b", "yandex", "y", "tineye", "t", "saucenao", "s" };
            if (!validServiceArgs.Contains(serviceArg))
            {
                Message("Invalid argument specifying reverse image search service.");
                return;
            }

            ShowLoadingOverlay($"Uploading file for reverse image search...");  // takes a couple seconds or so
            var hostedImageUrl = await ImgBB(filePath);
            HideLoadingOverlay();
            if (hostedImageUrl == null)
                return;

            string Encode(string url) => HttpUtility.UrlEncode(url);
            string finalUrl = null;

            switch (serviceArg)
            {
                case "google" or "g":
                    finalUrl = $"https://www.google.com/searchbyimage?image_url={Encode(hostedImageUrl)}";
                    break;
                case "bing" or "b":
                    finalUrl = $"https://www.bing.com/images/search?view=detailv2&iss=sbi&imgurl={Encode(hostedImageUrl)}";
                    break;
                case "yandex" or "y":
                    finalUrl = $"https://yandex.com/images/search?rpt=imageview&url={Encode(hostedImageUrl)}";
                    break;
                case "tineye" or "t":
                    finalUrl = $"https://tineye.com/search?url={Encode(hostedImageUrl)}";
                    break;
                case "saucenao" or "s":
                    finalUrl = $"https://saucenao.com/search.php?url={Encode(hostedImageUrl)}";
                    break;
            }

            OpenUrl(finalUrl ?? $"https://www.google.com/searchbyimage?image_url={Encode(hostedImageUrl)}");
        }

        public void LoadCommandHistory()
        {
            var stringCollection = Cloudless.Properties.Settings.Default.CommandHistory;
            if (stringCollection == null)
            {
                CommandHistory = new List<string>();
            }
            else
            {
                var list = stringCollection.Cast<string>().ToList();
                CommandHistory = list;
            }
        }

        private void CommitCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            LoadCommandHistory();

            // Avoid duplicate consecutive entries
            if (CommandHistory.Count == 0 || CommandHistory[^1] != command)
                CommandHistory.Add(command);

            const int HISTORY_MAX_SIZE = 100;
            StringCollection sc = new StringCollection();
            sc.AddRange(CommandHistory.TakeLast(HISTORY_MAX_SIZE).ToArray());
            Cloudless.Properties.Settings.Default.CommandHistory = sc;
            Cloudless.Properties.Settings.Default.Save();

            CommandHistoryIndex = CommandHistory.Count; // reset position
        }

        public void CommandPaletteTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (CommandHistory.Count == 0 && e.Key != Key.A)
                return;

            if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                var tb = GetCommandTextBox();
                if (tb != null && tb.Text.Length > 0)
                {
                    tb.SelectAll();
                    e.Handled = true;
                    return;
                }
            }

            if (CommandHistory.Count == 0)
                return;

            if (e.Key == Key.Up)
            {
                e.Handled = true;

                if (CommandHistoryIndex > 0)
                    CommandHistoryIndex--;

                var tb = GetCommandTextBox();
                if (tb != null)
                {
                    tb.Text = CommandHistory[CommandHistoryIndex];
                    tb.CaretIndex = tb.Text.Length;
                }
            }
            else if (e.Key == Key.Down)
            {
                e.Handled = true;

                if (CommandHistoryIndex < CommandHistory.Count - 1)
                {
                    CommandHistoryIndex++;
                    var tb = GetCommandTextBox();
                    if (tb != null)
                        tb.Text = CommandHistory[CommandHistoryIndex];
                }
                else
                {
                    CommandHistoryIndex = CommandHistory.Count;
                    var tb = GetCommandTextBox();
                    if (tb != null)
                        tb.Text = "";
                }

                var tbb = GetCommandTextBox();
                if (tbb != null)
                    tbb.CaretIndex = tbb.Text.Length;
            }

            //// TODO revisit
            //var textBox = sender as TextBox;
            //if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            //{
            //    if (textBox != null && textBox.Text.Length > 1)
            //    {
            //        textBox.Select(1, textBox.Text.Length - 1);

            //        // mark event handled to prevent default Ctrl+A
            //        e.Handled = true;
            //    }
            //}
            //if (textBox != null && e.Key == Key.Left &&
            //    (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            //{
            //    // If cursor is at index 1 (between 1st and 2nd char)
            //    if (textBox.CaretIndex == 1)
            //    {
            //        // Stop the default Shift+Left selection behavior
            //        e.Handled = true;
            //    }
            //}
        }

        private async Task JumpToIndex(int index)
        {
            if (index < 0 || imageFiles == null || index >= imageFiles.Count())
                return;

            currentImageIndex = index;
            await DisplayImage(index, true);
        }

        private async Task ExecuteFilenameSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                Message("Search string is empty");
                return;
            }

            var files = imageFiles ?? Array.Empty<string>();
            int count = files.Count();

            if (count == 0)
                return;

            string lowerQuery = query.ToLowerInvariant();

            // Start searching *after* the current image
            for (int offset = 1; offset < count - currentImageIndex; offset++)
            {
                int index = currentImageIndex + offset;
                string fileName = Path.GetFileName(files[index]).ToLowerInvariant();

                if (fileName.Contains(lowerQuery))
                {
                    await JumpToIndex(index);
                    return;
                }
            }

            // ...and if not found then check from the beginning of the list until the current image
            for (int index = 0; index < currentImageIndex; index++)
            {
                string fileName = Path.GetFileName(files[index]).ToLowerInvariant();

                if (fileName.Contains(lowerQuery))
                {
                    await JumpToIndex(index);
                    Message("Continuing search from start of directory");
                    return;
                }
            }

            Message($"No match for \"{query}\"");
        }

        private void SetUserCommand(int cIndex, string command)
        {
            LoadUserCommands();
            UserCommands[cIndex] = command;
            SaveUserCommands();
            Message($"Set command at index {cIndex + 1} to: {command}");
        }

        private void ViewUserCommand(int cIndex)
        {
            LoadUserCommands();
            string command = UserCommands[cIndex];
            if (string.IsNullOrEmpty(command))
            {
                Message($"No command found at index {cIndex}.");
            }
            else
            {
                Message($"{command}");
            }
        }

        private List<int> UserCommandsInProgress = new List<int>();  // to prevent infinite loops if user sets command 1 to "c1 run" for example
        public async Task RunUserCommand(int cIndex)
        {
            if (UserCommandsInProgress.Contains(cIndex))
            {
                Message($"Command recursion detected and prevented at index {cIndex + 1}. This likely means you have a recursive loop in your commands (e.g. command 1 is \"c1 run\"), which would cause the program to freeze if allowed to continue. To fix this, edit your commands to remove the loop.");
                return;
            }

            UserCommandsInProgress.Add(cIndex);

            LoadUserCommands();
            string command = UserCommands[cIndex];
            if (string.IsNullOrEmpty(command))
            {
                Message($"No command found at index {cIndex + 1}.");
            }
            else
            {
                await ExecuteCommand(command);
            }

            UserCommandsInProgress.Remove(cIndex);
        }

        public void LoadUserCommands()
        {
            var stringCollection = Cloudless.Properties.Settings.Default.UserCommands;
            if (stringCollection == null)
            {
                // initialize 24 empty command slots (3 pages x 8 commands)
                UserCommands = Enumerable.Repeat("", 24).ToList();
            }
            else
            {
                var list = stringCollection.Cast<string>().ToList();
                // pad or trim to 24 entries
                if (list.Count < 24)
                {
                    list.AddRange(Enumerable.Repeat("", 24 - list.Count));
                }
                else if (list.Count > 24)
                {
                    list = list.Take(24).ToList();
                }
                UserCommands = list;
            }
        }

        // --- User macros (saved to a file in workspaceFilesPath/user_macros.txt) ---
        private static readonly string userMacrosFileName = "user_macros.txt";

        private Dictionary<string, string> LoadUserMacros()
        {
            try
            {
                if (!Directory.Exists(workspaceFilesPath))
                    Directory.CreateDirectory(workspaceFilesPath);

                string path = Path.Combine(workspaceFilesPath, userMacrosFileName);
                if (!File.Exists(path))
                    return new Dictionary<string, string>();

                string json = File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return dict ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Message("Failed to load user macros: " + ex.Message);
                return new Dictionary<string, string>();
            }
        }

        private void SaveUserMacros(Dictionary<string, string> macros)
        {
            try
            {
                if (!Directory.Exists(workspaceFilesPath))
                    Directory.CreateDirectory(workspaceFilesPath);

                string path = Path.Combine(workspaceFilesPath, userMacrosFileName);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(macros, options);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Message("Failed to save user macros: " + ex.Message);
            }
        }

        private void SetUserMacro(string name, string command)
        {
            var macros = LoadUserMacros();
            macros[name] = command;
            SaveUserMacros(macros);
            Message($"Saved macro '{name}'");
        }

        private List<string> MacrosInProgress = new List<string>();
        private async Task<bool> RunUserMacro(string name)
        {
            var macros = LoadUserMacros();
            if (!macros.ContainsKey(name))
            {
                Message($"Macro not found: {name}");
                return false;
            }

            if (MacrosInProgress.Contains(name))
            {
                Message($"Macro recursion detected and prevented for: {name}");
                return false;
            }

            MacrosInProgress.Add(name);
            string command = macros[name];
            if (!string.IsNullOrEmpty(command))
            {
                await ExecuteCommand(command);
            }
            MacrosInProgress.Remove(name);
            return true;
        }

        private void SaveUserCommands()
        {
            StringCollection stringCollection = new StringCollection();
            stringCollection.AddRange(UserCommands.ToArray());
            Cloudless.Properties.Settings.Default.UserCommands = stringCollection;
            Cloudless.Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Parses a time string in various formats and returns a TimeSpan.
        /// Supported formats:
        /// - "90" or "90s" (seconds)
        /// - "1:30" (minutes:seconds)
        /// - "1:30:45" (hours:minutes:seconds)
        /// - "1h30m45s" (hours, minutes, seconds with labels)
        /// - "1h30m" (partial format)
        /// - "30m45s" (without hours)
        /// Returns null if parsing fails.
        /// </summary>
        internal static TimeSpan? TryParseTimeString(string timeStr)
        {
            if (string.IsNullOrWhiteSpace(timeStr))
                return null;

            timeStr = timeStr.Trim().ToLowerInvariant();

            // Try format with labels: "1h30m45s" or "1h30m" or "30m45s" etc.
            if (timeStr.Contains("h") || timeStr.Contains("m") || timeStr.Contains("s"))
            {
                return TryParseLabeled(timeStr);
            }

            // Try colon-separated format: "1:30:45" or "1:30"
            if (timeStr.Contains(":"))
            {
                return TryParseColonSeparated(timeStr);
            }

            // Try plain number (interpret as seconds)
            if (double.TryParse(timeStr, out double seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }

            return null;
        }

        private static TimeSpan? TryParseLabeled(string timeStr)
        {
            double hours = 0, minutes = 0, seconds = 0;
            string remaining = timeStr;

            var hMatch = Regex.Match(remaining, @"(\d+(?:\.\d+)?)\s*h");
            if (hMatch.Success)
            {
                hours = double.Parse(hMatch.Groups[1].Value);
                remaining = remaining.Substring(hMatch.Index + hMatch.Length);
            }

            var mMatch = Regex.Match(remaining, @"(\d+(?:\.\d+)?)\s*m");
            if (mMatch.Success)
            {
                minutes = double.Parse(mMatch.Groups[1].Value);
                remaining = remaining.Substring(mMatch.Index + mMatch.Length);
            }

            var sMatch = Regex.Match(remaining, @"(\d+(?:\.\d+)?)\s*s");
            if (sMatch.Success)
            {
                seconds = double.Parse(sMatch.Groups[1].Value);
                remaining = remaining.Substring(sMatch.Index + sMatch.Length);
            }

            // If something was parsed, return the result
            if (hours > 0 || minutes > 0 || seconds > 0)
            {
                return TimeSpan.FromSeconds(hours * 3600 + minutes * 60 + seconds);
            }

            return null;
        }

        private static TimeSpan? TryParseColonSeparated(string timeStr)
        {
            var parts = timeStr.Split(':');

            if (parts.Length == 2)
            {
                // MM:SS format
                if (int.TryParse(parts[0], out int minutes) && int.TryParse(parts[1], out int seconds))
                {
                    if (seconds >= 0 && seconds < 60)
                    {
                        return TimeSpan.FromSeconds(minutes * 60 + seconds);
                    }
                }
            }
            else if (parts.Length == 3)
            {
                // HH:MM:SS format
                if (int.TryParse(parts[0], out int hours) && 
                    int.TryParse(parts[1], out int minutes) && 
                    int.TryParse(parts[2], out int seconds))
                {
                    if (minutes >= 0 && minutes < 60 && seconds >= 0 && seconds < 60)
                    {
                        return TimeSpan.FromSeconds(hours * 3600 + minutes * 60 + seconds);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Formats a TimeSpan into a readable string like "1:30:45" or "1:23"
        /// </summary>
        private string FormatTimeSpan(TimeSpan ts)
        {
            int totalSeconds = (int)ts.TotalSeconds;
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;

            if (hours > 0)
            {
                return $"{hours}:{minutes:D2}:{seconds:D2}";
            }
            else
            {
                return $"{minutes}:{seconds:D2}";
            }
        }

        private void StartSlideshow(double intervalSeconds, bool shuffle = false, bool useTriggers = false)
        {
            StopSlideshow();

            var activePages = GetNonemptyPages();
            if (activePages.Count == 0)
            {
                Message("Cannot start slideshow: no active pages");
                return;
            }

            // Find starting page: current page if active, otherwise the next active page
            int currentPageIndex = GetCurrentPageIndex();
            int startPageIndex = activePages.Contains(currentPageIndex) ? currentPageIndex : activePages[0];
            int startingPageIndex = activePages.IndexOf(startPageIndex);

            // Initialize global slideshow and pass the tick handler
            SlideshowManager.Initialize(intervalSeconds, activePages, startingPageIndex, Dispatcher, SlideshowTick, shuffle, useTriggers);

            if (intervalSeconds == 0)
                Message($"Slideshow started: cycling through {activePages.Count} active page(s) when triggers fire" + (shuffle ? " (shuffle enabled)" : ""));
            else
                Message($"Slideshow started: cycling through {activePages.Count} active page(s) every {intervalSeconds} second(s)" + (shuffle ? " (shuffle enabled)" : ""));
        }

        /// <summary>
        /// Called by SlideshowManager on each timer tick to advance to the next page.
        /// </summary>
        private void SlideshowTick()
        {
            var pages = SlideshowManager.CurrentPages;
            if (pages == null || pages.Count == 0)
                return;

            // Prefer the manager-selected page if available (manager-driven choice)
            int nextPage;
            var sel = SlideshowManager.SelectedPage;
            if (sel.HasValue)
            {
                nextPage = sel.Value;
            }
            else
            {
                // Fallback: compute next page as before
                var allPages = GetNonemptyPages();
                if (allPages.Count == 0)
                {
                    StopSlideshow();
                    return;
                }

                int currentPageIndex = GetCurrentPageIndex();
                int currentPos = pages.IndexOf(currentPageIndex);
                if (currentPos < 0)
                    currentPos = 0;

                int nextPos = (currentPos + 1) % pages.Count;
                nextPage = pages[nextPos];
            }

            // Swap to next page (fromSlideshow=true prevents stopping the slideshow)
            SwapViewToPage(nextPage, skipHide: false, fromSlideshow: true);
        }
    }
}
