using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Предмет, разрешённый через каталог Data Dragon для режима чтения.</summary>
public sealed record CoachingPackItemDetailsViewModel(
    int Id,
    string DisplayName,
    Uri? ImageUri);

/// <summary>Фаза матчапа, подготовленная для read-only представления.</summary>
public sealed record CoachingPackPhaseDetailsViewModel(
    string Id,
    string Title,
    string PrimaryAdvice,
    string WhyNow,
    string NextGoal,
    PowerState PowerState,
    string PowerStateLabel,
    string LevelRange,
    IReadOnlyList<CoachingPackItemDetailsViewModel> RecommendedItems)
{
    public bool HasWhyNow => !string.IsNullOrWhiteSpace(WhyNow);

    public bool HasNextGoal => !string.IsNullOrWhiteSpace(NextGoal);

    public bool HasRecommendedItems => RecommendedItems.Count > 0;
}

/// <summary>Постоянная подсказка с уже разрешённой областью применимости.</summary>
public sealed record CoachingPackPersistentTipDetailsViewModel(
    string Title,
    string Message,
    string ScopeLabel);

/// <summary>Авторский блок предметов в режиме чтения.</summary>
public sealed record CoachingPackItemBlockDetailsViewModel(
    string Id,
    string Name,
    string WhenToChoose,
    string ItemCountLabel,
    IReadOnlyList<CoachingPackItemDetailsViewModel> Items)
{
    public bool HasItems => Items.Count > 0;
}

/// <summary>Полный локализованный снимок содержимого Coach Pack для режима чтения.</summary>
internal sealed record CoachingPackDetailsContent(
    IReadOnlyList<CoachingPackPhaseDetailsViewModel> Phases,
    IReadOnlyList<CoachingPackPersistentTipDetailsViewModel> PersistentTips,
    IReadOnlyList<CoachingPackItemBlockDetailsViewModel> ItemBlocks);
