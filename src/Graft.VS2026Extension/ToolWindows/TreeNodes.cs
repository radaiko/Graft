using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Graft.VS2026Extension.ToolWindows
{
    internal sealed class StackNode : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _trunk = string.Empty;
        private bool _isActive;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string Trunk
        {
            get => _trunk;
            set { _trunk = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string DisplayName => $"{Name} (trunk: {Trunk})";

        public ObservableCollection<BranchNode> Branches { get; } = new ObservableCollection<BranchNode>();

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    internal sealed class BranchNode : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private ulong? _prNumber;
        private string? _prState;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public ulong? PrNumber
        {
            get => _prNumber;
            set { _prNumber = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string? PrState
        {
            get => _prState;
            set { _prState = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string DisplayName
        {
            get
            {
                if (PrNumber.HasValue)
                    return $"{Name} (#{PrNumber} {PrState ?? "open"})";
                return Name;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
