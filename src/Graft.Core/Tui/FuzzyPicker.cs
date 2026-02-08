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
        // Render goes to stderr, input from stdin — only bail if those are redirected
        if (Console.IsInputRedirected || Console.IsErrorRedirected || items.Count == 0)
            return null;

        var query = "";
        var selectedIndex = 0;
        var scrollOffset = 0;
        var filtered = new List<PickerItem<T>>(items);
        var totalCount = items.Count;
        var renderedLines = 0; // lines below prompt from last render

        // Hide cursor (via stderr)
        Err.Write("\x1b[?25l");

        try
        {
            renderedLines = Render(prompt, query, filtered, selectedIndex, scrollOffset, totalCount, renderedLines);

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

                renderedLines = Render(prompt, query, filtered, selectedIndex, scrollOffset, totalCount, renderedLines);
            }
        }
        finally
        {
            // Move past rendered content and show cursor
            if (renderedLines > 0)
                Err.Write($"\x1b[{renderedLines}B");
            Err.Write("\r\n");
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

    /// <summary>
    /// Renders the picker UI using relative ANSI cursor movement (no Console.CursorTop).
    /// Returns the number of lines below the prompt line that were rendered.
    /// </summary>
    private static int Render<T>(string prompt, string query, List<PickerItem<T>> filtered, int selectedIndex, int scrollOffset, int totalCount, int previousLines)
    {
        // Move cursor back to prompt line from wherever it ended up last render
        if (previousLines > 0)
            Err.Write($"\x1b[{previousLines}A");
        Err.Write("\r");

        // Prompt line (line 0)
        Err.Write($"\x1b[2K{prompt}{query}");

        // Items
        var end = Math.Min(scrollOffset + MaxVisible, filtered.Count);
        var linesBelow = 0;
        for (int i = scrollOffset; i < end; i++)
        {
            Err.Write("\r\n\x1b[2K");
            linesBelow++;

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
        Err.Write("\r\n\x1b[2K");
        linesBelow++;
        Err.Write($"\x1b[90m  {filtered.Count} / {totalCount} items\x1b[0m");

        // Clear leftover lines from previous render
        for (int i = linesBelow; i < previousLines; i++)
        {
            Err.Write("\r\n\x1b[2K");
            linesBelow++;
        }

        // Move back to prompt line
        if (linesBelow > 0)
            Err.Write($"\x1b[{linesBelow}A");

        // Position cursor at end of query text (1-based column)
        Err.Write($"\x1b[{prompt.Length + query.Length + 1}G");
        Err.Flush();

        return linesBelow;
    }
}
