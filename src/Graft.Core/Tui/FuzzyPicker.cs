namespace Graft.Core.Tui;

public sealed class PickerItem<T>
{
    public required T Value { get; init; }
    public required string Label { get; init; }
    public string? Description { get; init; }
}

public static class FuzzyPicker
{
    private const int MaxVisible = 15;

    // All TUI rendering goes to stderr so stdout is clean for shell capture (cd $(graft cd))
    private static readonly TextWriter Err = Console.Error;

    /// <summary>
    /// Shows an interactive fuzzy-search picker in the terminal.
    /// Returns the selected item's value, or default(T) if cancelled.
    /// Falls back to null if the console is not interactive.
    /// </summary>
    public static T? Pick<T>(List<PickerItem<T>> items, string prompt = "Search: ") where T : class
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected || items.Count == 0)
            return null;

        var query = "";
        var selectedIndex = 0;
        var scrollOffset = 0;
        var filtered = new List<PickerItem<T>>(items);
        var totalCount = items.Count;

        // Hide cursor (via stderr)
        Err.Write("\x1b[?25l");
        // Get current cursor row via stderr — use CursorTop which reads terminal state
        var startRow = Console.CursorTop;

        try
        {
            Render(prompt, query, filtered, selectedIndex, scrollOffset, startRow, totalCount);

            while (true)
            {
                var key = Console.ReadKey(intercept: true);

                switch (key.Key)
                {
                    case ConsoleKey.Escape:
                        return null;

                    case ConsoleKey.Enter:
                        if (filtered.Count > 0 && selectedIndex < filtered.Count)
                            return filtered[selectedIndex].Value;
                        return null;

                    case ConsoleKey.UpArrow:
                        if (selectedIndex > 0)
                            selectedIndex--;
                        break;

                    case ConsoleKey.DownArrow:
                        if (selectedIndex < filtered.Count - 1)
                            selectedIndex++;
                        break;

                    case ConsoleKey.Backspace:
                        if (query.Length > 0)
                        {
                            query = query[..^1];
                            filtered = FilterItems(items, query);
                            selectedIndex = 0;
                            scrollOffset = 0;
                        }
                        break;

                    default:
                        if (key.KeyChar >= 32 && key.KeyChar < 127)
                        {
                            query += key.KeyChar;
                            filtered = FilterItems(items, query);
                            selectedIndex = 0;
                            scrollOffset = 0;
                        }
                        break;
                }

                // Keep selection in viewport
                if (selectedIndex < scrollOffset)
                    scrollOffset = selectedIndex;
                if (selectedIndex >= scrollOffset + MaxVisible)
                    scrollOffset = selectedIndex - MaxVisible + 1;

                Render(prompt, query, filtered, selectedIndex, scrollOffset, startRow, totalCount);
            }
        }
        finally
        {
            // Show cursor and move past rendered content (via stderr)
            var visibleCount = Math.Min(filtered.Count - scrollOffset, MaxVisible);
            if (visibleCount < 0) visibleCount = 0;
            var totalLines = 1 + visibleCount + 1;
            Err.Write($"\x1b[{startRow + totalLines + 1};1H");
            Err.Write("\x1b[?25h");
            Err.Flush();
        }
    }

    private static List<PickerItem<T>> FilterItems<T>(List<PickerItem<T>> items, string query)
    {
        if (string.IsNullOrEmpty(query))
            return new List<PickerItem<T>>(items);

        return FuzzyMatcher.Filter(query, items, i => i.Label);
    }

    private static void Render<T>(string prompt, string query, List<PickerItem<T>> filtered, int selectedIndex, int scrollOffset, int startRow, int totalCount)
    {
        // Move to start row (ANSI rows are 1-based)
        Err.Write($"\x1b[{startRow + 1};1H");

        // Prompt line
        Err.Write($"\x1b[2K{prompt}{query}");

        // Items
        var end = Math.Min(scrollOffset + MaxVisible, filtered.Count);
        var visibleCount = end - scrollOffset;
        for (int i = scrollOffset; i < end; i++)
        {
            var row = startRow + 1 + (i - scrollOffset);
            Err.Write($"\x1b[{row + 1};1H\x1b[2K");

            var item = filtered[i];
            if (i == selectedIndex)
            {
                // Highlighted: inverse video
                Err.Write($"\x1b[7m  {item.Label}");
                if (item.Description != null)
                    Err.Write($"  {item.Description}");
                Err.Write("\x1b[0m");
            }
            else
            {
                Err.Write($"  {item.Label}");
                if (item.Description != null)
                    Err.Write($"\x1b[90m  {item.Description}\x1b[0m");
            }
        }

        // Status line
        var statusRow = startRow + 1 + visibleCount;
        Err.Write($"\x1b[{statusRow + 1};1H\x1b[2K\x1b[90m  {filtered.Count} / {totalCount} items\x1b[0m");

        // Clear any leftover lines below
        for (int i = 0; i < MaxVisible - visibleCount; i++)
        {
            var clearRow = statusRow + 1 + i;
            Err.Write($"\x1b[{clearRow + 1};1H\x1b[2K");
        }

        // Move cursor back to end of query on the prompt line
        Err.Write($"\x1b[{startRow + 1};{prompt.Length + query.Length + 1}H");
        Err.Flush();
    }
}
