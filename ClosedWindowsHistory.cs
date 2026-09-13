using System;
using System.Collections.Generic;

namespace Cloudless
{
    public static class ClosedWindowsHistory
    {
        private static List<CloudlessWindowState> _closedWindows = new List<CloudlessWindowState>();
        private const int MaxHistorySize = 100;

        public static void RecordClosedWindow(CloudlessWindowState windowState)
        {
            if (windowState == null) return;

            _closedWindows.Add(windowState);

            // Maintain max history size by removing oldest entries
            if (_closedWindows.Count > MaxHistorySize)
            {
                _closedWindows = _closedWindows.TakeLast(MaxHistorySize).ToList();
            }
        }

        public static CloudlessWindowState? RestoreNextClosedWindow()
        {
            if (_closedWindows.Count == 0)
                return null;

            var toReturn = _closedWindows.Last(); ;
            _closedWindows.Remove(toReturn);
            return toReturn;
        }

        public static void ClearHistory()
        {
            _closedWindows.Clear();
        }

        public static int AvailableCount => _closedWindows.Count;
    }
}
