using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Graft.VS2026Extension.Graft;

namespace Graft.VS2026Extension.ToolWindows
{
    [Guid(GraftGuids.StackExplorerToolWindowGuidString)]
    public sealed class StackExplorerToolWindow : ToolWindowPane
    {
        public StackExplorerToolWindow() : base(null)
        {
            Caption = "Stack Explorer";
        }

        public StackExplorerToolWindow(GraftService? service) : base(null)
        {
            Caption = "Stack Explorer";
            Content = new StackExplorerControl(service);
        }

        protected override void Initialize()
        {
            base.Initialize();

            if (Content == null)
            {
                var package = (GraftPackage?)Package;
                Content = new StackExplorerControl(package?.GraftService);
            }
        }
    }
}
