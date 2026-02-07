using System;
using System.ComponentModel.Design;
using System.Linq;
using Graft.VS2026Extension.Dialogs;
using Graft.VS2026Extension.Graft;
using Graft.VS2026Extension.ToolWindows;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Graft.VS2026Extension.Commands
{
    internal sealed class StackCommands
    {
        private readonly GraftPackage _package;
        private GraftService? Service => _package.GraftService;

        private StackCommands(GraftPackage package, OleMenuCommandService commandService)
        {
            _package = package;

            Register(commandService, CommandIds.InitStack, OnInitStack);
            Register(commandService, CommandIds.PushBranch, OnPushBranch);
            Register(commandService, CommandIds.PopBranch, OnPopBranch);
            Register(commandService, CommandIds.SyncStack, OnSyncStack);
            Register(commandService, CommandIds.SwitchStack, OnSwitchStack);
            Register(commandService, CommandIds.StackLog, OnStackLog);
            Register(commandService, CommandIds.OpenStackExplorer, OnOpenStackExplorer);
        }

        public static async Task InitializeAsync(GraftPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService))
                as OleMenuCommandService;
            if (commandService != null)
            {
                _ = new StackCommands(package, commandService);
            }
        }

        private static void Register(OleMenuCommandService commandService, int commandId,
            EventHandler handler)
        {
            var menuCommandId = new CommandID(GraftGuids.CommandSetGuid, commandId);
            var menuItem = new MenuCommand(handler, menuCommandId);
            commandService.AddCommand(menuItem);
        }

        private void OnInitStack(object sender, EventArgs e)
        {
            if (!EnsureService()) return;

            var dialog = new InputDialog("Initialize Stack", "Stack name:")
            {
                ShowCheckBox = false,
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                RunAsync($"Initializing stack '{dialog.InputText}'...", async () =>
                {
                    return await Service!.InitStackAsync(dialog.InputText).ConfigureAwait(false);
                });
            }
        }

        private void OnPushBranch(object sender, EventArgs e)
        {
            if (!EnsureService()) return;

            var dialog = new InputDialog("Push Branch", "Branch name:")
            {
                ShowCheckBox = true,
                CheckBoxText = "Create new branch",
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                RunAsync($"Pushing branch '{dialog.InputText}'...", async () =>
                {
                    return await Service!.PushBranchAsync(dialog.InputText, dialog.IsChecked)
                        .ConfigureAwait(false);
                });
            }
        }

        private void OnPopBranch(object sender, EventArgs e)
        {
            if (!EnsureService()) return;

            RunAsync("Popping top branch...", async () =>
            {
                return await Service!.PopBranchAsync().ConfigureAwait(false);
            });
        }

        private void OnSyncStack(object sender, EventArgs e)
        {
            if (!EnsureService()) return;

            RunAsync("Syncing stack...", async () =>
            {
                return await Service!.SyncStackAsync().ConfigureAwait(false);
            });
        }

        private void OnSwitchStack(object sender, EventArgs e)
        {
            if (!EnsureService()) return;

            var stackNames = Service!.ListStackNames();
            if (stackNames.Length == 0)
            {
                ShowMessage("No stacks found. Initialize a stack first.");
                return;
            }

            var dialog = new InputDialog("Switch Stack", "Select stack:")
            {
                ShowCheckBox = false,
                ComboBoxItems = stackNames.ToList(),
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                RunAsync($"Switching to stack '{dialog.InputText}'...", async () =>
                {
                    return await Service!.SwitchStackAsync(dialog.InputText).ConfigureAwait(false);
                });
            }
        }

        private void OnStackLog(object sender, EventArgs e)
        {
            if (!EnsureService()) return;

            RunAsync("Loading stack log...", async () =>
            {
                return await Service!.StackLogAsync().ConfigureAwait(false);
            });
        }

        private void OnOpenStackExplorer(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                var window = await _package.ShowToolWindowAsync(
                    typeof(StackExplorerToolWindow), 0, true,
                    _package.DisposalToken);

                if (window == null)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    ShowMessage("Failed to open Stack Explorer window.");
                }
            });
        }

        private bool EnsureService()
        {
            if (Service == null || !Service.IsAvailable)
            {
                ShowMessage("Graft CLI not found. Please install graft and ensure it's in your PATH.");
                return false;
            }
            return true;
        }

        private void RunAsync(string statusMessage, Func<Task<CliResult>> action)
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var outputPane = GraftOutputPane.GetPane();
                outputPane?.OutputStringThreadSafe($"> {statusMessage}\n");

                var result = await action().ConfigureAwait(false);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (!string.IsNullOrWhiteSpace(result.Stdout))
                    outputPane?.OutputStringThreadSafe(result.Stdout);

                if (!string.IsNullOrWhiteSpace(result.Stderr))
                    outputPane?.OutputStringThreadSafe($"[stderr] {result.Stderr}");

                if (!result.Success)
                {
                    outputPane?.OutputStringThreadSafe($"[exit code: {result.ExitCode}]\n");
                    ShowMessage($"Command failed: {result.Stderr.Trim()}");
                }
                else
                {
                    outputPane?.OutputStringThreadSafe("[done]\n");
                }
            });
        }

        private static void ShowMessage(string message)
        {
            VsShellUtilities.ShowMessageBox(
                ServiceProvider.GlobalProvider,
                message,
                "Graft",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
