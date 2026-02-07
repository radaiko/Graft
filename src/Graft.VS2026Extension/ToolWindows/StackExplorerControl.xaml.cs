using System.Windows.Controls;
using Graft.VS2026Extension.Graft;

namespace Graft.VS2026Extension.ToolWindows
{
    public partial class StackExplorerControl : UserControl
    {
        internal StackExplorerControl(GraftService? service)
        {
            InitializeComponent();
            DataContext = new StackExplorerViewModel(service);
        }
    }
}
