using System;
using System.Windows.Controls;
using Graft.VS2026Extension.Graft;

namespace Graft.VS2026Extension.ToolWindows
{
    public partial class StackExplorerControl : UserControl, IDisposable
    {
        internal StackExplorerControl(GraftService? service)
        {
            InitializeComponent();
            DataContext = new StackExplorerViewModel(service);
            Unloaded += (s, e) => Dispose();
        }

        public void Dispose()
        {
            (DataContext as StackExplorerViewModel)?.Dispose();
        }
    }
}
