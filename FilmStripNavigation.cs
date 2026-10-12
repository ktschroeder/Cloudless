namespace Cloudless
{
    internal static class FilmStripNavigation
    {
        public static string? GetAdjacentPath(IReadOnlyList<string> paths, string? currentPath, int direction)
        {
            if (paths.Count == 0 || direction == 0)
                return null;

            int currentIndex = -1;
            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    if (string.Equals(paths[i], currentPath, StringComparison.OrdinalIgnoreCase))
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }

            if (currentIndex < 0)
                return direction > 0 ? paths[0] : paths[^1];

            int nextIndex = (currentIndex + Math.Sign(direction) + paths.Count) % paths.Count;
            return paths[nextIndex];
        }
    }
}
