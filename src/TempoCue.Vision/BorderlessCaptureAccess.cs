using System.Diagnostics;
using Windows.Graphics.Capture;
using Windows.Security.Authorization.AppCapabilityAccess;

namespace TempoCue.Vision;

/// <summary>Чем закончился запрос на захват без рамки.</summary>
public enum BorderlessCaptureState
{
    /// <summary>Ещё не спрашивали.</summary>
    Unknown,

    /// <summary>Система старше Windows 11: убрать рамку нечем.</summary>
    NotSupported,

    /// <summary>Разрешено — рамка снята.</summary>
    Allowed,

    /// <summary>Отказано пользователем или политикой; рамка остаётся.</summary>
    Denied
}

/// <summary>
/// Снимает жёлтую рамку, которой Windows обводит захватываемое окно.
/// </summary>
/// <remarks>
/// Рамка рисуется системой, а не нами, и убрать её из приложения нельзя. Windows 11
/// даёт единственный законный путь: спросить разрешение у самого пользователя через
/// системное окно согласия. Ответ система запоминает, поэтому спрашивается один раз.
///
/// Отказ — не ошибка. Рамка мешает, но ничему не препятствует, и приложение обязано
/// работать с ней ровно так же. Поэтому все отказы гасятся здесь, а наружу выходит
/// только состояние для диагностики.
/// </remarks>
public static class BorderlessCaptureAccess
{
    /// <summary>Windows 11: раньше ни запроса, ни свойства рамки не существует.</summary>
    private const int MinimumBuild = 22000;

    private static BorderlessCaptureState _state = BorderlessCaptureState.Unknown;

    public static BorderlessCaptureState State => _state;

    /// <summary>
    /// Спрашивает разрешение один раз за запуск. Повторные вызовы возвращают
    /// прежний ответ, не показывая окно согласия снова.
    /// </summary>
    public static async Task<BorderlessCaptureState> RequestAsync()
    {
        if (_state is not BorderlessCaptureState.Unknown)
        {
            return _state;
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, MinimumBuild))
        {
            _state = BorderlessCaptureState.NotSupported;
            return _state;
        }

        try
        {
            var status = await GraphicsCaptureAccess
                .RequestAccessAsync(GraphicsCaptureAccessKind.Borderless)
                .AsTask()
                .ConfigureAwait(false);
            _state = status is AppCapabilityAccessStatus.Allowed
                ? BorderlessCaptureState.Allowed
                : BorderlessCaptureState.Denied;
            Trace.TraceInformation("Захват без рамки: {0}", status);
        }
        catch (Exception exception)
        {
            // Приложение без пакетной идентичности система может не спрашивать
            // вовсе. Это не повод падать: рамка останется, всё остальное работает.
            Trace.TraceInformation(
                "Запрос захвата без рамки не удался ({0}): {1}",
                exception.GetType().Name,
                exception.Message);
            _state = BorderlessCaptureState.Denied;
        }

        return _state;
    }

    /// <summary>
    /// Убирает рамку у созданной сессии, если разрешение получено. Возвращает,
    /// удалось ли: вызывающей стороне это нужно только для диагностики.
    /// </summary>
    public static bool TryHideBorder(GraphicsCaptureSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (_state is not BorderlessCaptureState.Allowed)
        {
            return false;
        }

        try
        {
            session.IsBorderRequired = false;
            return true;
        }
        catch (Exception exception)
        {
            // Разрешение могло быть отозвано между запросом и созданием сессии.
            Trace.TraceInformation(
                "Снять рамку не удалось ({0}): {1}",
                exception.GetType().Name,
                exception.Message);
            _state = BorderlessCaptureState.Denied;
            return false;
        }
    }
}
