using System.CommandLine;
using Graft.Core.Scan;
using Graft.Core.Tui;

namespace Graft.Cli.Commands;

public static class CdCommand
{
    public static Command Create()
    {
        var nameArg = new Argument<string?>("name")
        {
            Description = "Repo or branch name to navigate to",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var command = new Command("cd", "Navigate to a repo or worktree");
        command.Add(nameArg);

        command.SetAction((parseResult) =>
        {
            var name = parseResult.GetValue(nameArg);
            var configDir = CliPaths.GetConfigDir();

            try
            {
                if (name != null)
                {
                    // Direct mode: find matches
                    var matches = RepoNavigator.FindByName(name, configDir);

                    if (matches.Count == 0)
                    {
                        Console.Error.WriteLine($"Error: No repo or worktree found matching '{name}'.");
                        Console.Error.WriteLine("Run 'graft scan add <directory>' to register scan paths, then try again.");
                        Environment.ExitCode = 1;
                        return;
                    }

                    if (matches.Count == 1)
                    {
                        Console.WriteLine(matches[0].Path);
                        return;
                    }

                    // Multiple matches — try interactive picker
                    if (!Console.IsInputRedirected)
                    {
                        var items = matches.Select(m => new PickerItem<NavigationResult>
                        {
                            Value = m,
                            Label = m.Name,
                            Description = m.Branch != null ? $"[{m.Branch}]" : m.Path,
                        }).ToList();

                        var selected = FuzzyPicker.Pick(items, "Select: ");
                        if (selected != null)
                        {
                            Console.WriteLine(selected.Path);
                            return;
                        }
                    }

                    // Non-interactive or cancelled: list matches on stderr
                    Console.Error.WriteLine($"Multiple matches for '{name}':");
                    foreach (var m in matches)
                    {
                        var detail = m.Branch != null ? $" [{m.Branch}]" : "";
                        Console.Error.WriteLine($"  {m.Name}{detail}  {m.Path}");
                    }
                    Environment.ExitCode = 1;
                }
                else
                {
                    // No args: open fuzzy picker with all repos
                    var items = RepoNavigator.GetAllAsPickerItems(configDir);

                    if (items.Count == 0)
                    {
                        Console.Error.WriteLine("No repos in cache. Run 'graft scan add <directory>' to register scan paths.");
                        Environment.ExitCode = 1;
                        return;
                    }

                    if (Console.IsInputRedirected)
                    {
                        Console.Error.WriteLine("Error: Interactive mode requires a terminal. Provide a name: graft cd <name>");
                        Environment.ExitCode = 1;
                        return;
                    }

                    var selected = FuzzyPicker.Pick(items, "Search: ");
                    if (selected != null)
                    {
                        Console.WriteLine(selected.Path);
                    }
                    else
                    {
                        Console.Error.WriteLine("No selection made. Provide a name: graft cd <name>");
                        Environment.ExitCode = 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        });

        return command;
    }

}
