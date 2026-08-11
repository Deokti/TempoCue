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
    {
        var session = new ActiveMatchSession(
            new SnapshotSource(result),
            new MatchupCoach(rules ?? []),
            TimeSpan.FromSeconds(1));
        var viewModel = new CurrentMatchViewModel(session, Localization);
        _viewModels.Add(viewModel);
        return viewModel;
    }

    public CurrentMatchViewModel CreateViewModel(
        MatchSnapshot snapshot,
        IEnumerable<CoachRuleDefinition>? rules = null) =>
        CreateViewModel(MatchSnapshotReadResult.Success(snapshot), rules);

    public void Dispose()
    {
        foreach (var viewModel in _viewModels)
        {
            viewModel.Dispose();
        }

        Localization.SetLanguage(_initialLanguage);
    }

    private sealed class SnapshotSource(MatchSnapshotReadResult result) : IMatchSnapshotSource
    {
        public Task<MatchSnapshotReadResult> ReadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
