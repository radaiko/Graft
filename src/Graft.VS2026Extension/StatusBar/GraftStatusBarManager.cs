using System;
using Graft.VS2026Extension.Graft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Graft.VS2026Extension.StatusBar
{
    internal sealed class GraftStatusBarManager : IDisposable
    {
        private readonly GraftService _service;

        public GraftStatusBarManager(GraftService service)
        {
            _service = service;
        }

        public void Initialize()
        {
            _service.DataChanged += OnDataChanged;
            UpdateStatusBar();
        }

        private void OnDataChanged(object? sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                UpdateStatusBar();
            });
        }

        private void UpdateStatusBar()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var statusBar = (IVsStatusbar?)Package.GetGlobalService(typeof(SVsStatusbar));
            if (statusBar == null) return;

            var activeStack = _service.GetActiveStackName();
            if (activeStack == null)
            {
                statusBar.SetText("Graft: (no active stack)");
                return;
            }

            var stacks = _service.LoadAllStacks();
            var active = stacks.Find(s => s.IsActive);
            if (active != null && active.Branches.Count > 0)
            {
                var topBranch = active.Branches[active.Branches.Count - 1].Name;
                statusBar.SetText($"Graft: {activeStack} | {topBranch}");
            }
            else
            {
                statusBar.SetText($"Graft: {activeStack}");
            }
        }

        public void Dispose()
        {
            _service.DataChanged -= OnDataChanged;
        }
    }
}
