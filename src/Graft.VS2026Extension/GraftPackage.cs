using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Graft.VS2026Extension.Graft;
using Graft.VS2026Extension.StatusBar;
using Graft.VS2026Extension.ToolWindows;
using Task = System.Threading.Tasks.Task;

namespace Graft.VS2026Extension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(GraftGuids.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(StackExplorerToolWindow), Style = VsDockStyle.Tabbed,
        DockedWidth = 300, Window = "3ae79031-e1bc-11d0-8f78-00a0c9110057")]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string,
        PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class GraftPackage : AsyncPackage
    {
        internal GraftService? GraftService { get; private set; }
        internal GraftStatusBarManager? StatusBarManager { get; private set; }

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var solutionDir = await GetSolutionDirectoryAsync();
            if (solutionDir != null)
            {
                GraftService = new GraftService(solutionDir);
                StatusBarManager = new GraftStatusBarManager(this, GraftService);
                StatusBarManager.Initialize();
            }

            await Commands.StackCommands.InitializeAsync(this);
        }

        private async Task<string?> GetSolutionDirectoryAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution = await GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            if (solution == null)
                return null;

            solution.GetSolutionInfo(out string solutionDir, out _, out _);
            return string.IsNullOrEmpty(solutionDir) ? null : solutionDir;
        }

        public override IVsAsyncToolWindowFactory GetAsyncToolWindowFactory(Guid toolWindowType)
        {
            if (toolWindowType == typeof(StackExplorerToolWindow).GUID)
                return this;

            return base.GetAsyncToolWindowFactory(toolWindowType);
        }

        protected override string GetToolWindowTitle(Type toolWindowType, int id)
        {
            if (toolWindowType == typeof(StackExplorerToolWindow))
                return "Stack Explorer (Loading...)";

            return base.GetToolWindowTitle(toolWindowType, id);
        }

        protected override async Task<object?> InitializeToolWindowAsync(
            Type toolWindowType, int id, CancellationToken cancellationToken)
        {
            if (toolWindowType == typeof(StackExplorerToolWindow))
                return GraftService;

            return null;
        }
    }
}
