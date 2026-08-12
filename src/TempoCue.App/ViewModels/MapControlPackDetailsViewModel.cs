using System.ComponentModel;
using System.Globalization;
using System.IO;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Coaching;

namespace TempoCue.App.ViewModels;

/// <summary>Готовит сохранённый Map Control Pack для просмотра и файловых действий.</summary>
public sealed class MapControlPackDetailsViewModel : ObservableObject, IDisposable
{
    private readonly LocalizationService _localization;
    private readonly ExportMapControlPackUseCase _exportMapControlPack;
    private MapControlPackCardViewModel? _card;
    private MapControlPackDetailsTab _currentTab;
    private IReadOnlyList<MapControlObjectiveRuleViewModel> _rules = [];
    private IReadOnlyList<MapControlTimedReminderViewModel> _timedReminders = [];
    private IReadOnlyList<MapControlPersistentTipViewModel> _persistentTips = [];
    private string _sourceLinkMessage = string.Empty;
    private ExportStatus _exportStatus;
    private bool _isExporting;
    private bool _isDisposed;

    public MapControlPackDetailsViewModel(
        LocalizationService localization,
        ExportMapControlPackUseCase exportMapControlPack)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _exportMapControlPack = exportMapControlPack
            ?? throw new ArgumentNullException(nameof(exportMapControlPack));
        _localization.PropertyChanged += Localization_PropertyChanged;
    }

    public MapControlPackCardViewModel? Card => _card;

    public MapControlPack? Pack => Card?.Pack;

    public string Title => Card?.Title ?? string.Empty;

    public string StateLabel => Card?.StateLabel ?? string.Empty;

    public bool IsEnabled => Card?.IsEnabled == true;

    public bool CanDelete => Card?.CanDelete == true;

    public bool IsImported => Card?.Origin == CoachingPackOrigin.Imported;

    public string EditActionLabel => Card?.CanEdit == true
        ? _localization.Get("Coaching.Details.Edit")
        : _localization.Get("Coaching.Details.CreateCopy");

    public string DeleteActionLabel => Card?.DeleteActionLabel ?? string.Empty;

    public string DeleteConfirmationTitle => IsImported
        ? _localization.Get("Coaching.Details.Delete.Confirmation.Imported.Title")
        : _localization.Get("Coaching.Details.Delete.Confirmation.Own.Title");

    public string DeleteConfirmationMessage => _localization.Format(
        IsImported
            ? "Coaching.Details.Delete.Confirmation.Imported.Message"
            : "Coaching.Details.Delete.Confirmation.Own.Message",
        Title);

    public string DeleteConfirmationActionLabel => IsImported
        ? _localization.Get("Coaching.Details.Delete.Confirmation.Imported.Action")
        : _localization.Get("Coaching.Details.Delete.Confirmation.Own.Action");

    public string DeleteFailureTitle =>
        _localization.Get("Coaching.Details.Delete.Failed.Title");

    public string DeleteFailureMessage =>
        _localization.Get("Coaching.Details.Delete.Failed.Message");

    public string HeaderDetails => Pack is null
        ? string.Empty
        : _localization.Format(
            "Coaching.MapControl.Details.Header",
            RolesLabel,
            AuthorLabel,
            Card?.OriginLabel ?? string.Empty);

    public string Description => string.IsNullOrWhiteSpace(Pack?.Description)
        ? _localization.Get("Coaching.Details.Overview.Description.Empty")
        : Pack.Description;

    public string AuthorLabel => string.IsNullOrWhiteSpace(Pack?.Author)
        ? _localization.Get("Coaching.Library.Author.TempoCue")
        : Pack.Author;

    public string ContentLanguageLabel => Card?.ContentLanguageLabel ?? string.Empty;

    public string PatchLabel => Pack?.Patch ?? string.Empty;

    public string VerifiedOnLabel => Pack?.VerifiedOn is DateOnly verifiedOn
        ? verifiedOn.ToString("D", CultureInfo.CurrentUICulture)
        : _localization.Get("Coaching.Details.Overview.NotSpecified");

    public string RolesLabel => Pack is null
        ? string.Empty
        : GetRolesLabel(Pack.Applicability.Roles);

    public string TeamSideLabel => Pack is null
        ? string.Empty
        : _localization.Get(Pack.Applicability.TeamSide switch
        {
            MapControlTeamSide.Blue => "Coaching.MapControlEditor.Scope.Summary.BlueSide",
            MapControlTeamSide.Red => "Coaching.MapControlEditor.Scope.Summary.RedSide",
            _ => "Coaching.MapControlEditor.Scope.Summary.AnySide"
        });

    public string SourceLabel => BuildSourceLabel(Pack);

    public Uri? SourceUri => BuildSourceUri(Pack);

    public bool HasSourceUri => SourceUri is not null;

    public string SourceLinkMessage
    {
        get => _sourceLinkMessage;
        private set
        {
            if (SetProperty(ref _sourceLinkMessage, value))
            {
                OnPropertyChanged(nameof(HasSourceLinkMessage));
            }
        }
    }

    public bool HasSourceLinkMessage => !string.IsNullOrWhiteSpace(SourceLinkMessage);

    public IReadOnlyList<MapControlObjectiveRuleViewModel> Rules
    {
        get => _rules;
        private set
        {
            if (SetProperty(ref _rules, value))
            {
                OnPropertyChanged(nameof(HasRules));
            }
        }
    }

    public bool HasRules => Rules.Count > 0;

    public IReadOnlyList<MapControlTimedReminderViewModel> TimedReminders
    {
        get => _timedReminders;
        private set
        {
            if (SetProperty(ref _timedReminders, value))
            {
                OnPropertyChanged(nameof(HasTimedReminders));
            }
        }
    }

    public bool HasTimedReminders => TimedReminders.Count > 0;

    public IReadOnlyList<MapControlPersistentTipViewModel> PersistentTips
    {
        get => _persistentTips;
        private set
        {
            if (SetProperty(ref _persistentTips, value))
            {
                OnPropertyChanged(nameof(HasPersistentTips));
            }
        }
    }

    public bool HasPersistentTips => PersistentTips.Count > 0;

    public int TotalAdviceCount => Pack is null
        ? 0
        : Pack.Rules.Count + Pack.TimedReminders.Count + Pack.PersistentTips.Count;

    public string ContentSummary => Pack is null
        ? string.Empty
        : _localization.Format(
            "Coaching.MapControl.Details.Overview.ContentValue",
            Pack.Rules.Count,
            Pack.TimedReminders.Count,
            Pack.PersistentTips.Count);

    public MapControlPackDetailsTab CurrentTab
    {
        get => _currentTab;
        private set
        {
            if (SetProperty(ref _currentTab, value))
            {
                OnPropertyChanged(nameof(IsOverviewTab));
                OnPropertyChanged(nameof(IsObjectivesTab));
                OnPropertyChanged(nameof(IsTimedRemindersTab));
                OnPropertyChanged(nameof(IsPersistentTipsTab));
            }
        }
    }

    public bool IsOverviewTab => CurrentTab == MapControlPackDetailsTab.Overview;

    public bool IsObjectivesTab => CurrentTab == MapControlPackDetailsTab.Objectives;

    public bool IsTimedRemindersTab => CurrentTab == MapControlPackDetailsTab.TimedReminders;

    public bool IsPersistentTipsTab => CurrentTab == MapControlPackDetailsTab.PersistentTips;

    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (SetProperty(ref _isExporting, value))
            {
                OnPropertyChanged(nameof(CanExport));
            }
        }
    }

    public bool CanExport => Pack is not null && !IsExporting;

    public string ExportMessage => _exportStatus switch
    {
        ExportStatus.Saved => _localization.Get("Coaching.Details.ExportPack.Status.Saved"),
        ExportStatus.Failed => _localization.Get("Coaching.Details.ExportPack.Status.Failed"),
        _ => string.Empty
    };

    public bool HasExportMessage => _exportStatus != ExportStatus.None;

    public bool HasExportError => _exportStatus == ExportStatus.Failed;

    public string ExportDialogFilter =>
        _localization.Get("Coaching.MapControl.Details.Export.Filter");

    public void Open(MapControlPackCardViewModel card)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(card);
        _card = card;
        CurrentTab = MapControlPackDetailsTab.Overview;
        SourceLinkMessage = string.Empty;
        SetExportStatus(ExportStatus.None);
        RebuildContent();
        NotifyPackChanged();
    }

    public void SelectForActions(MapControlPackCardViewModel card)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(card);
        _card = card;
        SourceLinkMessage = string.Empty;
        SetExportStatus(ExportStatus.None);
        NotifyPackChanged();
    }

    public void UpdateCard(MapControlPackCardViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (Pack?.Id != card.Pack.Id)
        {
            throw new ArgumentException("Карточка относится к другому Map Control Pack.", nameof(card));
        }

        _card = card;
        NotifyPackChanged();
    }

    public void ShowOverview() => CurrentTab = MapControlPackDetailsTab.Overview;

    public void ShowObjectives() => CurrentTab = MapControlPackDetailsTab.Objectives;

    public void ShowTimedReminders() => CurrentTab = MapControlPackDetailsTab.TimedReminders;

    public void ShowPersistentTips() => CurrentTab = MapControlPackDetailsTab.PersistentTips;

    public void ReportSourceLinkOpenFailure() =>
        SourceLinkMessage = _localization.Get("Coaching.Details.Source.OpenFailed");

    public async Task<bool> ExportAsync(
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (Pack is null || IsExporting)
        {
            return false;
        }

        IsExporting = true;
        SetExportStatus(ExportStatus.None);
        try
        {
            await _exportMapControlPack.ExecuteAsync(Pack, destination, cancellationToken);
            SetExportStatus(ExportStatus.Saved);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            SetExportStatus(ExportStatus.Failed);
            return false;
        }
        finally
        {
            IsExporting = false;
        }
    }

    public void ReportExportFileFailure() => SetExportStatus(ExportStatus.Failed);

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _localization.PropertyChanged -= Localization_PropertyChanged;
    }

    private void RebuildContent()
    {
        Rules = Pack?.Rules
            .Select(rule => new MapControlObjectiveRuleViewModel(rule, _localization))
            .ToArray() ?? [];
        TimedReminders = Pack?.TimedReminders
            .OrderBy(static reminder => reminder.GameTime)
            .Select(reminder => new MapControlTimedReminderViewModel(reminder, _localization))
            .ToArray() ?? [];
        PersistentTips = Pack?.PersistentTips
            .Select(tip => new MapControlPersistentTipViewModel(tip, _localization))
            .ToArray() ?? [];
    }

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RebuildContent();
        NotifyPackChanged();
        OnPropertyChanged(nameof(ExportMessage));
        OnPropertyChanged(nameof(ExportDialogFilter));
        if (HasSourceLinkMessage)
        {
            SourceLinkMessage = _localization.Get("Coaching.Details.Source.OpenFailed");
        }
    }

    private void NotifyPackChanged()
    {
        OnPropertyChanged(nameof(Card));
        OnPropertyChanged(nameof(Pack));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(IsImported));
        OnPropertyChanged(nameof(EditActionLabel));
        OnPropertyChanged(nameof(DeleteActionLabel));
        OnPropertyChanged(nameof(DeleteConfirmationTitle));
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
        OnPropertyChanged(nameof(DeleteConfirmationActionLabel));
        OnPropertyChanged(nameof(DeleteFailureTitle));
        OnPropertyChanged(nameof(DeleteFailureMessage));
        OnPropertyChanged(nameof(HeaderDetails));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(AuthorLabel));
        OnPropertyChanged(nameof(ContentLanguageLabel));
        OnPropertyChanged(nameof(PatchLabel));
        OnPropertyChanged(nameof(VerifiedOnLabel));
        OnPropertyChanged(nameof(RolesLabel));
        OnPropertyChanged(nameof(TeamSideLabel));
        OnPropertyChanged(nameof(SourceLabel));
        OnPropertyChanged(nameof(SourceUri));
        OnPropertyChanged(nameof(HasSourceUri));
        OnPropertyChanged(nameof(TotalAdviceCount));
        OnPropertyChanged(nameof(ContentSummary));
        OnPropertyChanged(nameof(CanExport));
    }

    private string GetRolesLabel(IReadOnlyList<MapControlRole> roles)
    {
        if (roles.Count == MapControlRoleValues.All.Count
            && MapControlRoleValues.All.All(roles.Contains))
        {
            return _localization.Get("Coaching.MapControlEditor.Scope.Summary.AllRoles");
        }

        return string.Join(", ", roles.Select(static role => role switch
        {
            MapControlRole.Top => "TOP",
            MapControlRole.Jungle => "JUNGLE",
            MapControlRole.Mid => "MID",
            MapControlRole.Adc => "ADC",
            MapControlRole.Support => "SUPPORT",
            _ => role.ToString().ToUpperInvariant()
        }));
    }

    private string BuildSourceLabel(MapControlPack? pack)
    {
        if (pack?.Source is null)
        {
            return _localization.Get("Coaching.Details.Overview.NotSpecified");
        }

        if (pack.Source.Kind == CoachPackSourceKind.PersonalExperience)
        {
            return _localization.Get("Coaching.Details.Source.PersonalExperience");
        }

        var title = string.IsNullOrWhiteSpace(pack.Source.Title)
            ? _localization.Get("Coaching.Details.Overview.NotSpecified")
            : pack.Source.Title;
        return string.IsNullOrWhiteSpace(pack.Source.Author)
            ? title
            : $"{title} — {pack.Source.Author}";
    }

    private static Uri? BuildSourceUri(MapControlPack? pack)
    {
        if (!Uri.TryCreate(pack?.Source?.Url, UriKind.Absolute, out var sourceUri))
        {
            return null;
        }

        return string.Equals(sourceUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sourceUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? sourceUri
            : null;
    }

    private void SetExportStatus(ExportStatus status)
    {
        if (_exportStatus == status)
        {
            return;
        }

        _exportStatus = status;
        OnPropertyChanged(nameof(ExportMessage));
        OnPropertyChanged(nameof(HasExportMessage));
        OnPropertyChanged(nameof(HasExportError));
    }

    private enum ExportStatus
    {
        None,
        Saved,
        Failed
    }
}
