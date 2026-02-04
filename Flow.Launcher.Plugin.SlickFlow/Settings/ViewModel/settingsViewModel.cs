using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace Flow.Launcher.Plugin.SlickFlow.Settings.ViewModel;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly Settings _settings;

    public SettingsViewModel()
    {
        _settings = SettingsManager.Load();
        _dbFilePath = _settings.DbFilePath ?? string.Empty;
        SavedPath = _settings.DbFilePath ?? string.Empty;
        SaveCommand = new RelayCommand(_ => Save());
        BrowseCommand = new RelayCommand(_ => Browse());
    }

    private string _dbFilePath = string.Empty;
    public string DbFilePath
    {
        get => _dbFilePath;
        set
        {
            if (_dbFilePath == value) return;
            _dbFilePath = value;
            OnPropertyChanged(nameof(DbFilePath));
        }
    }

    private string _savedPath = string.Empty;
    public string SavedPath
    {
        get => _savedPath;
        private set
        {
            if (_savedPath == value) return;
            _savedPath = value;
            OnPropertyChanged(nameof(SavedPath));
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand BrowseCommand { get; }

    public void Save()
    {
        _settings.DbFilePath = DbFilePath ?? string.Empty;
        SettingsManager.Save(_settings);
        SavedPath = _settings.DbFilePath;
    }

    private void Browse()
    {
        try
        {
            using var dlg = new Forms.OpenFileDialog
            {
                Title = "Select SlickFlow.json",
                Filter = "JSON files (*.json)|*.json",
                FileName = "SlickFlow.json"
            };

            if (!string.IsNullOrWhiteSpace(DbFilePath))
            {
                dlg.InitialDirectory = Path.GetDirectoryName(DbFilePath);
            }

            if (dlg.ShowDialog() == Forms.DialogResult.OK)
            {
                DbFilePath = dlg.FileName;
            }
        }
        catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }
}
