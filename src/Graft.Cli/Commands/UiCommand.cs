using System.CommandLine;
using Graft.Cli.Server;

namespace Graft.Cli.Commands;

public static class UiCommand
{
    public static Command Create()
    {
        var cmd = new Command("ui", "Start the Graft web UI");

        var portOption = new Option<int>("--port")
        {
            Description = "Port to listen on (0 = random free port)",
            DefaultValueFactory = _ => 0,
            Validators = { result =>
                {
                    var value = result.GetValueOrDefault<int>();
                    if (value < 0 || value > 65535)
                        result.AddError("Port must be between 0 and 65535");
                }
            }
        };
        cmd.Add(portOption);

        cmd.SetAction((parseResult, ct) =>
        {
            var port = parseResult.GetValue(portOption);
            var repoPath = Directory.GetCurrentDirectory();

            using var server = new ApiServer(repoPath, port);
            server.Start();

            Console.WriteLine($"Graft UI running at http://localhost:{server.Port}");
            Console.WriteLine("Press Ctrl+C to stop.");

            using var exit = new ManualResetEventSlim();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                exit.Set();
            };

            ct.Register(() => exit.Set());
            exit.Wait();

            return Task.CompletedTask;
        });

        return cmd;
    }
}
