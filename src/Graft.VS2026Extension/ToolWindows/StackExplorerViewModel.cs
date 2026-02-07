using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Graft.VS2026Extension.Graft;
using Microsoft.VisualStudio.Shell;

namespace Graft.VS2026Extension.ToolWindows
{
    internal sealed class StackExplorerViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly GraftService? _service;
        private bool _isLoading;
        private string? _errorMessage;

        public ObservableCollection<StackNode> Stacks { get; } = new ObservableCollection<StackNode>();

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrEmpty(_errorMessage);

        public ICommand RefreshCommand { get; }

        public StackExplorerViewModel(GraftService? service)
        {
            _service = service;
            RefreshCommand = new RelayCommand(Refresh);

            if (_service != null)
            {
                _service.DataChanged += OnDataChanged;
                Refresh();
            }
            else
            {
                ErrorMessage = "Graft service not available. Open a solution that contains a git repository.";
            }
        }

        private void OnDataChanged(object? sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                Refresh();
            });
        }

        public void Refresh()
        {
            if (_service == null) return;

            IsLoading = true;
            ErrorMessage = null;

            try
            {
                var stacks = _service.LoadAllStacks();

                Stacks.Clear();
                foreach (var stack in stacks)
                {
                    var node = new StackNode
                    {
                        Name = stack.Name,
                        Trunk = stack.Trunk,
                        IsActive = stack.IsActive,
                    };

                    foreach (var branch in stack.Branches)
                    {
                        node.Branches.Add(new BranchNode
                        {
                            Name = branch.Name,
                            PrNumber = branch.PrNumber,
                            PrState = branch.PrState,
                        });
                    }

                    Stacks.Add(node);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load stacks: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            if (_service != null)
                _service.DataChanged -= OnDataChanged;
        }
    }
}
