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

    /// <summary>
    /// Shows an interactive fuzzy-search picker in the terminal.
    /// Returns the selected item's value, or default(T) if cancelled.
    /// Falls back to null if the console is not interactive.
    /// </summary>
    public static T? Pick<T>(List<PickerItem<T>> items, string prompt = "Search: ") where T : class
    {
        if (Console.IsInputRedirected || items.Count == 0)
            return null;

        var query = "";
        var selectedIndex = 0;
        var filtered = new List<PickerItem<T>>(items);

        // Save cursor position and hide cursor
        Console.Write("\x1b[?25l");
        var startRow = Console.CursorTop;

        try
        {
            var totalCount = items.Count;
            Render(prompt, query, filtered, selectedIndex, startRow, totalCount);

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
                        }
                        break;

                    default:
                        if (key.KeyChar >= 32 && key.KeyChar < 127)
                        {
                            query += key.KeyChar;
                            filtered = FilterItems(items, query);
                            selectedIndex = 0;
                        }
                        break;
                }

                Render(prompt, query, filtered, selectedIndex, startRow, totalCount);
            }
        }
        finally
        {
            // Show cursor and move past rendered content
            var totalLines = 1 + Math.Min(filtered.Count, MaxVisible) + 1;
            Console.SetCursorPosition(0, startRow + totalLines);
            Console.Write("\x1b[?25h");
        }
    }

    private static List<PickerItem<T>> FilterItems<T>(List<PickerItem<T>> items, string query)
    {
        if (string.IsNullOrEmpty(query))
            return new List<PickerItem<T>>(items);

        return FuzzyMatcher.Filter(query, items, i => i.Label);
    }

    private static void Render<T>(string prompt, string query, List<PickerItem<T>> filtered, int selectedIndex, int startRow, int totalCount)
    {
        Console.SetCursorPosition(0, startRow);

        // Prompt line
        Console.Write($"\x1b[2K{prompt}{query}");

        // Items
        var visibleCount = Math.Min(filtered.Count, MaxVisible);
        for (int i = 0; i < visibleCount; i++)
        {
            Console.SetCursorPosition(0, startRow + 1 + i);
            Console.Write("\x1b[2K");

            var item = filtered[i];
            if (i == selectedIndex)
            {
                // Highlighted: inverse video
                Console.Write($"\x1b[7m  {item.Label}");
                if (item.Description != null)
                    Console.Write($"  {item.Description}");
                Console.Write("\x1b[0m");
            }
            else
            {
                Console.Write($"  {item.Label}");
                if (item.Description != null)
                    Console.Write($"\x1b[90m  {item.Description}\x1b[0m");
            }
        }

        // Status line
        Console.SetCursorPosition(0, startRow + 1 + visibleCount);
        Console.Write($"\x1b[2K\x1b[90m  {filtered.Count} / {totalCount} items\x1b[0m");

        // Clear any leftover lines below
        for (int i = startRow + 2 + visibleCount; i < startRow + 2 + MaxVisible; i++)
        {
            Console.SetCursorPosition(0, i);
            Console.Write("\x1b[2K");
        }

        // Move cursor back to end of query
        Console.SetCursorPosition(prompt.Length + query.Length, startRow);
    }
}
