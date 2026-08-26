using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.App.Settings;

namespace TempoCue.App.ViewModels;

/// <summary>
/// Один вариант распознавателя миникарты в списке настроек.
/// </summary>
/// <remarks>
/// Подсказка висит на самом варианте, а не на выбранном значении: она нужна при
/// выборе, то есть до того, как вариант применён. Тот же приём, что и у списка
/// потоков распознавания.
/// </remarks>
public sealed class MinimapRecognizerOptionViewModel : ObservableObject, IDisposable
{
    private readonly LocalizationService _localization;
    private bool _isDisposed;

    private MinimapRecognizerOptionViewModel(
        LocalizationService localization,
        MinimapRecognizerChoice choice)
    {
        _localization = localization;
        Choice = choice;
        _localization.PropertyChanged += Localization_PropertyChanged;
    }

    public MinimapRecognizerChoice Choice { get; }

    public string DisplayName => _localization.Get(Choice switch
    {
        MinimapRecognizerChoice.OwnModel => "Settings.Recognizer.OwnModel",
        MinimapRecognizerChoice.DeeperLeague => "Settings.Recognizer.DeeperLeague",
        _ => "Settings.Recognizer.Default"
    });

    /// <summary>
    /// Чего ждать от этого выбора. Числа берутся из приёмки по корпусу и стоят
    /// в подсказке нарочно: без них выбор между тремя строчками ничем не подкреплён.
    /// </summary>
    public string HintText => _localization.Get(Choice switch
    {
        MinimapRecognizerChoice.OwnModel => "Settings.Recognizer.OwnModel.Hint",
        MinimapRecognizerChoice.DeeperLeague => "Settings.Recognizer.DeeperLeague.Hint",
        _ => "Settings.Recognizer.Default.Hint"
    });

    /// <summary>
    /// Знает ли распознаватель всех чемпионов. У сторонней модели ростер патча
    /// 13.14, и вышедших позже она не знает вовсе; предупреждение показывается
    /// только для неё.
    /// </summary>
    public bool HasUnsupportedChampions => Choice is MinimapRecognizerChoice.DeeperLeague;

    public static IReadOnlyList<MinimapRecognizerOptionViewModel> Create(
        LocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        return Enum.GetValues<MinimapRecognizerChoice>()
            .Select(choice => new MinimapRecognizerOptionViewModel(localization, choice))
            .ToArray();
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
        if (e.PropertyName is nameof(LocalizationService.CurrentLanguageCode))
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(HintText));
        }
    }
}
