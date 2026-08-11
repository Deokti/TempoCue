using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Управляет локализацией и зависимостями тестов экрана текущего матча.</summary>
internal sealed class CurrentMatchViewModelTestContext : IDisposable
{
    private readonly string _initialLanguage;
    private readonly List<CurrentMatchViewModel> _viewModels = [];
    private readonly List<ActiveMatchMonitor> _monitors = [];

    public CurrentMatchViewModelTestContext()
    {
        Localization = LocalizationService.Instance;
        _initialLanguage = Localization.CurrentLanguageCode;
        Localization.SetLanguage("ru-RU");
    }

    public LocalizationService Localization { get; }

    public CurrentMatchViewModel CreateViewModel(
        MatchSnapshotReadResult result,
        IEnumerable<CoachRuleDefinition>? rules = null)
        => CreateViewModel(
            result,
            new TestMatchAdviceSelector(rules));

    public CurrentMatchViewModel CreateViewModel(
        MatchSnapshotReadResult result,
        IMatchAdviceSelector adviceSelector,
        IChampionCatalog? championCatalog = null)
    {
        var monitor = CurrentMatchViewModelTestData.CreateMonitor(
            TestMatchSnapshotSource.FromResult(result),
            adviceSelector);
        var viewModel = new CurrentMatchViewModel(
            monitor,
            championCatalog ?? TestChampionCatalog.Ready(),
            Localization);
        _monitors.Add(monitor);
        _viewModels.Add(viewModel);
        return viewModel;
    }

    public CurrentMatchViewModel CreateViewModel(
        MatchSnapshot snapshot,
        IEnumerable<CoachRuleDefinition>? rules = null) =>
        CreateViewModel(MatchSnapshotReadResult.Success(snapshot), rules);

    public CurrentMatchViewModel CreateViewModel(
        MatchSnapshot snapshot,
        IMatchAdviceSelector adviceSelector) =>
        CreateViewModel(MatchSnapshotReadResult.Success(snapshot), adviceSelector);

    public void Dispose()
    {
        foreach (var viewModel in _viewModels)
        {
            viewModel.Dispose();
        }

        foreach (var monitor in _monitors)
        {
            monitor.Dispose();
        }

        Localization.SetLanguage(_initialLanguage);
    }
}
