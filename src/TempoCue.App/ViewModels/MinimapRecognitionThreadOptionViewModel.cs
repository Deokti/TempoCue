using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Vision;

namespace TempoCue.App.ViewModels;

/// <summary>
/// Один вариант числа потоков распознавания. Список строится по машине: предлагать
/// больше потоков, чем у неё логических процессоров, бессмысленно — они всё равно
/// будут ждать друг друга.
/// </summary>
public sealed class MinimapRecognitionThreadOptionViewModel : ObservableObject, IDisposable
{
    /// <summary>Ноль означает «выбрать по машине», а не «не распознавать».</summary>
    public const int AutomaticThreads = 0;

    private readonly LocalizationService _localization;
    private bool _isDisposed;

    private MinimapRecognitionThreadOptionViewModel(
        LocalizationService localization,
        int threads)
    {
        _localization = localization;
        Threads = threads;
        _localization.PropertyChanged += Localization_PropertyChanged;
    }

    public int Threads { get; }

    /// <summary>
    /// Для автоматического варианта показывается и само вычисленное число: человек
    /// должен видеть, с чем сравнивает, выбирая значение вручную.
    /// </summary>
    public string DisplayName => Threads == AutomaticThreads
        ? _localization.Format(
            "Settings.Recognition.Threads.Automatic",
            DefaultMinimapFrameAnalyzer.DefaultDegreeOfParallelism)
        : Threads.ToString();

    /// <summary>
    /// Чего ждать от этого выбора. Числа в миллисекундах назвать нельзя — они
    /// зависят от машины, — поэтому подсказка говорит о последствиях: успеют ли
    /// уведомления и сколько процессора останется игре.
    /// </summary>
    public string HintText => _localization.Get(Threads switch
    {
        AutomaticThreads => "Settings.Recognition.Threads.Hint.Automatic",
        _ when Threads * 4 <= ProcessorCount => "Settings.Recognition.Threads.Hint.Frugal",
        _ when Threads * 2 <= ProcessorCount => "Settings.Recognition.Threads.Hint.Balanced",
        _ when Threads < ProcessorCount => "Settings.Recognition.Threads.Hint.Fast",
        _ => "Settings.Recognition.Threads.Hint.Maximum"
    });

    /// <summary>
    /// Предупреждение о выборе, при котором подтверждение видимости может не
    /// успевать. Разбор кадра на одном потоке длится дольше окна подтверждения,
    /// поэтому участник рискует не показаться вовсе, а не просто с опозданием.
    /// </summary>
    public bool IsRiskyForNotifications => Threads == 1;

    private static int ProcessorCount => Math.Max(1, Environment.ProcessorCount);

    public static IReadOnlyList<MinimapRecognitionThreadOptionViewModel> CreateForCurrentMachine(
        LocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        var processorCount = Math.Max(1, Environment.ProcessorCount);
        var threads = new List<int> { AutomaticThreads };
        for (var candidate = 1; candidate <= processorCount; candidate *= 2)
        {
            threads.Add(candidate);
        }

        // Число логических процессоров редко бывает степенью двойки на ноутбуках с
        // разнородными ядрами, поэтому оно добавляется отдельно.
        if (!threads.Contains(processorCount))
        {
            threads.Add(processorCount);
        }

        return threads
            .Select(count => new MinimapRecognitionThreadOptionViewModel(localization, count))
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
