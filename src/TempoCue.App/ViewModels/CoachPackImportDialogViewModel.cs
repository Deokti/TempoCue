using System.ComponentModel;
using System.IO;
using System.Text.Json;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Показывает проверенный предпросмотр файла и подтверждает его локальную установку.</summary>
public sealed class CoachPackImportDialogViewModel : ObservableObject, IDisposable
{
    private readonly LocalizationService _localization;
    private readonly ImportCoachPackUseCase _importCoachPack;
    private CoachPackImportPreview? _preview;
    private CoachPackImportDialogStatus _status;
    private bool _isOpen;
    private bool _isBusy;
    private bool _isDisposed;

    public CoachPackImportDialogViewModel(
        LocalizationService localization,
        ImportCoachPackUseCase importCoachPack)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _importCoachPack = importCoachPack ?? throw new ArgumentNullException(nameof(importCoachPack));
        _localization.PropertyChanged += Localization_PropertyChanged;
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanConfirm));
            }
        }
    }

    public CoachPack? Pack => _preview?.Pack;

    public bool HasPreview => Pack is not null;

    public string PackName => Pack?.Name ?? string.Empty;

    public string Description => string.IsNullOrWhiteSpace(Pack?.Description)
        ? _localization.Get("Coaching.Import.Preview.Description.Empty")
        : Pack.Description;

    public string MatchupLabel => Pack is null
        ? string.Empty
        : _localization.Format(
            "Coaching.Library.Matchup",
            EmptyToFallback(Pack.PlayerChampion, Pack.Rules.FirstOrDefault()?.PlayerChampion),
            EmptyToFallback(Pack.OpponentChampion, Pack.Rules.FirstOrDefault()?.OpponentChampion));

    public string RoleLabel => string.IsNullOrWhiteSpace(Pack?.LaneRole)
        ? _localization.Get("Coaching.Library.Role.Any")
        : Pack.LaneRole.ToUpperInvariant();

    public string AuthorLabel => string.IsNullOrWhiteSpace(Pack?.Author)
        ? _localization.Get("Coaching.Library.Author.TempoCue")
        : Pack.Author;

    public string LanguageLabel => string.IsNullOrWhiteSpace(Pack?.ContentLanguage)
        ? _localization.Get("Coaching.Library.Language.Unknown")
        : Pack.ContentLanguage;

    public string PatchLabel => Pack?.Patch ?? string.Empty;

    public string AdviceCountLabel => Pack is null
        ? string.Empty
        : _localization.Plural(
            "Count.Advice",
            Pack.Rules.Count + Pack.PersistentTips.Count);

    public string ItemBlockCountLabel => Pack is null
        ? string.Empty
        : _localization.Plural("Count.ItemBlock", Pack.ItemBlocks.Count);

    public bool CanConfirm => _preview?.CanImport == true && !IsBusy;

    public bool HasImportAction => _preview?.CanImport == true;

    public bool HasError => _status is CoachPackImportDialogStatus.InvalidFile
        or CoachPackImportDialogStatus.ReadFailed
        or CoachPackImportDialogStatus.SaveFailed
        or CoachPackImportDialogStatus.CreatedConflict
        or CoachPackImportDialogStatus.BuiltInConflict;

    public bool HasWarning => _status == CoachPackImportDialogStatus.Replacement;

    public string StatusTitle => _localization.Get(_status switch
    {
        CoachPackImportDialogStatus.Loading => "Coaching.Import.Status.Loading.Title",
        CoachPackImportDialogStatus.Ready => "Coaching.Import.Status.Ready.Title",
        CoachPackImportDialogStatus.Replacement => "Coaching.Import.Status.Replacement.Title",
        CoachPackImportDialogStatus.CreatedConflict => "Coaching.Import.Status.CreatedConflict.Title",
        CoachPackImportDialogStatus.BuiltInConflict => "Coaching.Import.Status.BuiltInConflict.Title",
        CoachPackImportDialogStatus.InvalidFile => "Coaching.Import.Status.Invalid.Title",
        CoachPackImportDialogStatus.ReadFailed => "Coaching.Import.Status.ReadFailed.Title",
        CoachPackImportDialogStatus.SaveFailed => "Coaching.Import.Status.SaveFailed.Title",
        _ => "Coaching.Import.Title"
    });

    public string StatusMessage => _localization.Format(_status switch
    {
        CoachPackImportDialogStatus.Loading => "Coaching.Import.Status.Loading.Message",
        CoachPackImportDialogStatus.Ready => "Coaching.Import.Status.Ready.Message",
        CoachPackImportDialogStatus.Replacement => "Coaching.Import.Status.Replacement.Message",
        CoachPackImportDialogStatus.CreatedConflict => "Coaching.Import.Status.CreatedConflict.Message",
        CoachPackImportDialogStatus.BuiltInConflict => "Coaching.Import.Status.BuiltInConflict.Message",
        CoachPackImportDialogStatus.InvalidFile => "Coaching.Import.Status.Invalid.Message",
        CoachPackImportDialogStatus.ReadFailed => "Coaching.Import.Status.ReadFailed.Message",
        CoachPackImportDialogStatus.SaveFailed => "Coaching.Import.Status.SaveFailed.Message",
        _ => "Coaching.Import.Subtitle"
    }, PackName);

    public string ConfirmActionLabel => _localization.Get(
        _preview?.RequiresReplacement == true
            ? "Coaching.Import.Action.Replace"
            : "Coaching.Import.Action.Import");

    public string DialogFilter => _localization.Get("Coaching.Import.Dialog.Filter");

    public async Task OpenAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(source);
        _preview = null;
        IsOpen = true;
        IsBusy = true;
        SetStatus(CoachPackImportDialogStatus.Loading);
        NotifyPreviewChanged();
        try
        {
            _preview = await _importCoachPack.InspectAsync(source, cancellationToken);
            SetStatus(GetPreviewStatus(_preview));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Close();
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            SetStatus(CoachPackImportDialogStatus.InvalidFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetStatus(CoachPackImportDialogStatus.ReadFailed);
        }
        finally
        {
            IsBusy = false;
            NotifyPreviewChanged();
        }
    }

    public void OpenFileFailure()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _preview = null;
        IsOpen = true;
        IsBusy = false;
        SetStatus(CoachPackImportDialogStatus.ReadFailed);
        NotifyPreviewChanged();
    }

    public async Task<bool> ConfirmAsync(CancellationToken cancellationToken = default)
    {
        if (_preview is null || !CanConfirm)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            var result = await _importCoachPack.ExecuteAsync(_preview, cancellationToken);
            _preview = result.Preview;
            if (!result.Succeeded)
            {
                SetStatus(GetPreviewStatus(_preview));
            }

            NotifyPreviewChanged();
            return result.Succeeded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            SetStatus(CoachPackImportDialogStatus.SaveFailed);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Close()
    {
        IsOpen = false;
        IsBusy = false;
        _preview = null;
        _status = CoachPackImportDialogStatus.None;
        NotifyPreviewChanged();
        NotifyStatusChanged();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _localization.PropertyChanged -= Localization_PropertyChanged;
    }

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyPreviewChanged();
        NotifyStatusChanged();
        OnPropertyChanged(nameof(DialogFilter));
    }

    private void SetStatus(CoachPackImportDialogStatus status)
    {
        if (_status == status)
        {
            return;
        }

        _status = status;
        NotifyStatusChanged();
    }

    private void NotifyPreviewChanged()
    {
        OnPropertyChanged(nameof(Pack));
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(PackName));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(MatchupLabel));
        OnPropertyChanged(nameof(RoleLabel));
        OnPropertyChanged(nameof(AuthorLabel));
        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(PatchLabel));
        OnPropertyChanged(nameof(AdviceCountLabel));
        OnPropertyChanged(nameof(ItemBlockCountLabel));
        OnPropertyChanged(nameof(CanConfirm));
        OnPropertyChanged(nameof(HasImportAction));
        OnPropertyChanged(nameof(ConfirmActionLabel));
    }

    private void NotifyStatusChanged()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasWarning));
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(StatusMessage));
    }

    private static CoachPackImportDialogStatus GetPreviewStatus(CoachPackImportPreview preview) =>
        preview.Conflict switch
        {
            CoachPackImportConflict.None => CoachPackImportDialogStatus.Ready,
            CoachPackImportConflict.ImportedPack => CoachPackImportDialogStatus.Replacement,
            CoachPackImportConflict.CreatedPack => CoachPackImportDialogStatus.CreatedConflict,
            CoachPackImportConflict.BuiltInPack => CoachPackImportDialogStatus.BuiltInConflict,
            _ => throw new ArgumentOutOfRangeException(nameof(preview))
        };

    private static string EmptyToFallback(string value, string? fallback) =>
        string.IsNullOrWhiteSpace(value)
            ? fallback ?? "—"
            : value;

    private enum CoachPackImportDialogStatus
    {
        None,
        Loading,
        Ready,
        Replacement,
        CreatedConflict,
        BuiltInConflict,
        InvalidFile,
        ReadFailed,
        SaveFailed
    }
}
