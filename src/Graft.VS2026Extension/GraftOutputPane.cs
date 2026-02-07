using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Graft.VS2026Extension
{
    internal static class GraftOutputPane
    {
        private static IVsOutputWindowPane? _pane;
        private static readonly Guid PaneGuid = GraftGuids.OutputPaneGuid;

        public static IVsOutputWindowPane? GetPane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_pane != null)
                return _pane;

            var outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow == null)
                return null;

            var guid = PaneGuid;
            int hr = outputWindow.GetPane(ref guid, out _pane);
            if (hr != VSConstants.S_OK || _pane == null)
            {
                outputWindow.CreatePane(ref guid, "Graft", fInitVisible: 1, fClearWithSolution: 1);
                outputWindow.GetPane(ref guid, out _pane);
            }

            return _pane;
        }

        public static void Activate()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GetPane()?.Activate();
        }
    }
}
