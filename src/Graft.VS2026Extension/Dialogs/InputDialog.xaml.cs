using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Graft.VS2026Extension.Dialogs
{
    public partial class InputDialog : Window, INotifyPropertyChanged
    {
        private string _inputText = string.Empty;
        private bool _isChecked;
        private bool _showCheckBox;
        private string _checkBoxText = string.Empty;
        private List<string>? _comboBoxItems;

        public string DialogTitle { get; }
        public string LabelText { get; }

        public string InputText
        {
            get => _inputText;
            set { _inputText = value; OnPropertyChanged(); }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; OnPropertyChanged(); }
        }

        public bool ShowCheckBox
        {
            get => _showCheckBox;
            set { _showCheckBox = value; OnPropertyChanged(); OnPropertyChanged(nameof(CheckBoxVisibility)); }
        }

        public string CheckBoxText
        {
            get => _checkBoxText;
            set { _checkBoxText = value; OnPropertyChanged(); }
        }

        public List<string>? ComboBoxItems
        {
            get => _comboBoxItems;
            set
            {
                _comboBoxItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowTextBox));
                OnPropertyChanged(nameof(ShowComboBox));
            }
        }

        public Visibility CheckBoxVisibility => ShowCheckBox ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ShowTextBox => ComboBoxItems == null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ShowComboBox => ComboBoxItems != null ? Visibility.Visible : Visibility.Collapsed;

        public InputDialog(string title, string label)
        {
            DialogTitle = title;
            LabelText = label;
            DataContext = this;
            InitializeComponent();
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
