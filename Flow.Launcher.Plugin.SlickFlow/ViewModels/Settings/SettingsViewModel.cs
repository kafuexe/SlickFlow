using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Settings;
using Flow.Launcher.Plugin.SlickFlow.Utils;
using Flow.Launcher.Plugin.SlickFlow.ViewModels.Item;

namespace Flow.Launcher.Plugin.SlickFlow.ViewModels.Settings;
public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly Plugin.SlickFlow.Settings.Settings _settings;
    private readonly ItemRepository _repo;
    public ObservableCollection<ItemViewModel> Items { get; } = new();

    public SettingsViewModel(ItemRepository repo)
    {
        _settings = SettingsManager.Load();
        _repo = repo;

        // DB file
        _dbFilePath = _settings.DbFilePath;
        SavedDbPath = _settings.DbFilePath;

        // Icon directory
        _iconDirPath = _settings.IconDirPath ;
        SavedIconDirPath = _settings.IconDirPath ;

        // Commands
        SaveCommand = new RelayCommand(_ => Save());
        BrowseDbFolderCommand = new RelayCommand(_ => BrowseDbFile());
        BrowseIconFolderCommand = new RelayCommand(_ => BrowseIconFolder());
        ReloadItemsCommand = new RelayCommand(_ => ReloadItems());
        AddItemCommand = new RelayCommand(_ => AddItem());
        //Items:
        ReloadItems();

    }

    #region DB File Properties

    private string _dbFilePath;
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

    private string _savedDbPath;
    public string SavedDbPath
    {
        get => _savedDbPath;
        private set
        {
            if (_savedDbPath == value) return;
            _savedDbPath = value;
            OnPropertyChanged(nameof(SavedDbPath));
        }
    }

    #endregion

    #region Icon Directory Properties

    private string _iconDirPath;
    public string IconDirPath
    {
        get => _iconDirPath;
        set
        {
            if (_iconDirPath == value) return;
            _iconDirPath = value;
            OnPropertyChanged(nameof(IconDirPath));
        }
    }

    private string _savedIconDirPath;
    public string SavedIconDirPath
    {
        get => _savedIconDirPath;
        private set
        {
            if (_savedIconDirPath == value) return;
            _savedIconDirPath = value;
            OnPropertyChanged(nameof(SavedIconDirPath));
        }
    }

    #endregion

    #region Commands

    public ICommand SaveCommand { get; }
    public ICommand BrowseDbFolderCommand { get; }
    public ICommand BrowseIconFolderCommand { get; }
    public ICommand ReloadItemsCommand { get; }
    public ICommand AddItemCommand { get; }

    #endregion

    #region Methods

    public void Save()
    {
        _settings.DbFilePath = DbFilePath ?? string.Empty;
        _settings.IconDirPath = IconDirPath ?? string.Empty;

        SettingsManager.Save(_settings);

        SavedDbPath = _settings.DbFilePath;
        SavedIconDirPath = _settings.IconDirPath;
    }

    private void BrowseDbFile()
    {
        try
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select the db File",
                Filter = "JSON files (*.json)|*.json",
                FileName = "SlickFlow.json"
            };

            if (!string.IsNullOrWhiteSpace(DbFilePath))
                dlg.InitialDirectory = Path.GetDirectoryName(DbFilePath);

            if (dlg.ShowDialog() == DialogResult.OK)
                DbFilePath = dlg.FileName;
        }
        catch { }
    }

    private void BrowseIconFolder()
    {
        try
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select folder for icons"
            };

            if (!string.IsNullOrWhiteSpace(IconDirPath) && Directory.Exists(IconDirPath))
                dlg.SelectedPath = IconDirPath;

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                IconDirPath = dlg.SelectedPath;
                if (!Directory.Exists(IconDirPath))
                    Directory.CreateDirectory(IconDirPath);
            }
        }
        catch { }
    }

    private void ReloadItems()
    {
        Items.Clear();
        var tempItems = _repo.GetAllItems();
        tempItems.Reverse();
        foreach (var item in tempItems)
        {
            var vm = new ItemViewModel(item, _repo);
            vm.Deleted += ReloadItems;
            Items.Add(vm);
        }
    }

    private void AddItem()
    {
        var baseAlias = "New Item";
        var alias = baseAlias;
        var index = 2;
        
        while (_repo.GetItemByAlias(alias) != null)
        {
            if (index > 10000) // safety guard
                throw new InvalidOperationException(
                    "Failed to generate unique alias.");

            alias = $"{baseAlias} {index}";
            index++;
        }
        var newItem = new Items.Item()
        {
            Aliases = new List<string> { alias }
        };

        _repo.AddItem(newItem);
        ReloadItems();

    }

    #endregion

    #region INotifyPropertyChanged


    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    #endregion
}
