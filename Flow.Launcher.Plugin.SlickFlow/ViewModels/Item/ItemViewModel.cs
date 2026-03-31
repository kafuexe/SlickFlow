using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Utils;

namespace Flow.Launcher.Plugin.SlickFlow.ViewModels.Item;

public class ItemViewModel : INotifyPropertyChanged
{
    private readonly ItemRepository _repository;

    private Items.Item _originalItem;
    private Items.Item _editingCopy;

    private bool _isEditing;

    public event PropertyChangedEventHandler? PropertyChanged;

    private string _newAliasInput = string.Empty;

    public ItemViewModel(Items.Item item, ItemRepository repository)
    {
        _repository = repository;
        _originalItem = item;
        _editingCopy = CloneItem(item);

        LoadIcon(_originalItem.IconPath);

        Aliases = new ObservableCollection<string>(_editingCopy.Aliases);
        Aliases.CollectionChanged += (s, e) => OnPropertyChanged(nameof(AliasesString));
        EditCommand = new RelayCommand(_ => BeginEdit(), _ => !IsEditing);
        SaveCommand = new RelayCommand(_ => Save(), _ => IsEditing);
        CancelCommand = new RelayCommand(_ => Cancel(), _ => IsEditing);
        AddAliasCommand = new RelayCommand(_ => AddAlias(), _ => IsEditing && !string.IsNullOrWhiteSpace(NewAliasInput));
        RemoveAliasCommand = new RelayCommand(obj => RemoveAlias(obj as string), _ => IsEditing);
        DeleteItemCommand = new RelayCommand(_ => DeleteItem());

    }
    
    // Raised after this item has been deleted from the repository
    public event Action? Deleted;

    #region Commands

    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AddAliasCommand { get; }
    public ICommand RemoveAliasCommand { get; }
    public ICommand DeleteItemCommand { get; }

    private void DeleteItem()
    {
        _repository.DeleteItem(_originalItem.Id);
        IsEditing = false;
        Deleted?.Invoke();

    }
    #endregion

    #region State

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            _isEditing = value;
            OnPropertyChanged();
            RefreshCommands();
        }
    }

    #endregion

    #region Exposed Properties

    public static List<KeyValuePair<int, string>> RunAsOptions { get; } = new()
    {
        new(0, "Normal"),
        new(1, "Administrator"),
    };

    public string AliasesString => string.Join(", ", Aliases);

    public string ArgsDisplay => string.IsNullOrWhiteSpace(Arguments) ? "" : "Args: " + Arguments;

    public string WorkingDirDisplay => string.IsNullOrWhiteSpace(WorkingDir) ? "" : "working dir: "+  WorkingDir;

    public int ExecCount => _editingCopy.ExecCount;

    public string FileName
    {
        get => _editingCopy.FileName;
        set
        {
            _editingCopy.FileName = value;
            OnPropertyChanged();
        }
    }

    public string Arguments
    {
        get => _editingCopy.Arguments;
        set
        {
            _editingCopy.Arguments = value;
            OnPropertyChanged();
        }
    }

    public string SubTitle
    {
        get => _editingCopy.SubTitle;
        set
        {
            _editingCopy.SubTitle = value;
            OnPropertyChanged();
        }
    }

    public int RunAs
    {
        get => _editingCopy.RunAs;
        set
        {
            _editingCopy.RunAs = value;
            OnPropertyChanged();
        }
    }

    public int StartMode
    {
        get => _editingCopy.StartMode;
        set
        {
            _editingCopy.StartMode = value;
            OnPropertyChanged();
        }
    }

    public string WorkingDir
    {
        get => _editingCopy.WorkingDir;
        set
        {
            _editingCopy.WorkingDir = value;
            OnPropertyChanged();
        }
    }

    public string IconPath
    {
        get => _editingCopy.IconPath;
        set
        {
            _editingCopy.IconPath = value;
            OnPropertyChanged();
        }
    }

    private ImageSource _icon;
    
    public ImageSource Icon
    {
        get => _icon;
        private set
        {
            if (_icon == value)
                return;

            _icon = value;
            OnPropertyChanged();
        }
}

    public ObservableCollection<string> Aliases { get; }

    public string NewAliasInput
    {
        get => _newAliasInput;
        set
        {
            _newAliasInput = value;
            OnPropertyChanged();
            RefreshCommands();
        }
    }

    #endregion

    #region Editing Flow

    private void BeginEdit()
    {
        _editingCopy = CloneItem(_originalItem);

        Aliases.Clear();
        foreach (var a in _editingCopy.Aliases)
            Aliases.Add(a);

        RefreshAllProperties();

        NewAliasInput = string.Empty;
        IsEditing = true;
    }

    private void Save()
    {
        _editingCopy.Aliases = Aliases.ToList();
        _repository.UpdateItem(_editingCopy);
        _originalItem = CloneItem(_editingCopy);
        RefreshAllProperties();
        LoadIcon(_originalItem.IconPath);
        IsEditing = false;

    }

    private void Cancel()
    {
        _editingCopy = CloneItem(_originalItem);

        Aliases.Clear();
        foreach (var a in _originalItem.Aliases)
            Aliases.Add(a);

        RefreshAllProperties();

        NewAliasInput = string.Empty;
        IsEditing = false;
    }

    private void AddAlias()
    {
        if (string.IsNullOrWhiteSpace(NewAliasInput))
            return;

        var alias = NewAliasInput.Trim();
        if (!Aliases.Any(a =>
            string.Equals(a, alias, StringComparison.OrdinalIgnoreCase)))
        {
            Aliases.Add(alias);
        }

        NewAliasInput = string.Empty;
    }

    private void RemoveAlias(string alias)
    {
        if (!string.IsNullOrWhiteSpace(alias))
            Aliases.Remove(alias);
    }

    #endregion

    #region Helpers

    private static Items.Item CloneItem(Items.Item src)
    {
        return new Items.Item
        {
            Id = src.Id,
            FileName = src.FileName,
            Arguments = src.Arguments,
            SubTitle = src.SubTitle,
            RunAs = src.RunAs,
            StartMode = src.StartMode,
            WorkingDir = src.WorkingDir,
            ExecCount = src.ExecCount,
            IconPath = src.IconPath,
            Aliases = new List<string>(src.Aliases)
        };
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    
    private void RefreshCommands()
    {
        (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddAliasCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemoveAliasCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }


    private void RefreshAllProperties()
    {
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(Arguments));
        OnPropertyChanged(nameof(SubTitle));
        OnPropertyChanged(nameof(WorkingDir));
        OnPropertyChanged(nameof(RunAs));
        OnPropertyChanged(nameof(StartMode));
        OnPropertyChanged(nameof(IconPath));
        OnPropertyChanged(nameof(ArgsDisplay));
        OnPropertyChanged(nameof(WorkingDirDisplay));
        OnPropertyChanged(nameof(ExecCount));

    }
    private void LoadIcon(string path)
    {
        if (!File.Exists(path))
        {
            Icon = null;
            return;
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad; // key part
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();

        Icon = bitmap;
    }

    #endregion
}
