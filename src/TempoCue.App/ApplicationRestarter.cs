using System.Diagnostics;
using System.IO;

namespace TempoCue.App;

/// <summary>Перезапускает приложение, чтобы применить настройку.</summary>
/// <remarks>
/// Настройки распознавания применяются при создании сессии захвата, то есть один
/// раз за запуск. Подмена распознавателя на ходу усложнила бы её жизненный цикл
/// без пользы, а требовать от человека закрыть и открыть приложение руками —
/// перекладывать на него нашу работу.
/// </remarks>
public interface IApplicationRestarter
{
    void Restart();
}

/// <summary>
/// Запускает вторую копию и закрывает текущую.
/// </summary>
/// <remarks>
/// Порядок важен: сперва запускается новая копия, и только если это удалось,
/// закрывается старая. Обратный порядок оставил бы человека без приложения, если
/// запуск не удался.
///
/// Одиночного запуска у TempoCue нет, поэтому две копии на мгновение
/// сосуществуют. Это заметно только тем, что новое окно появляется до закрытия
/// старого.
/// </remarks>
public sealed class ApplicationRestarter : IApplicationRestarter
{
    public void Restart()
    {
        // Путь к собственному исполняемому файлу, а не к точке входа сборки:
        // для однофайловой публикации это разные вещи.
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable) || !File.Exists(executable))
        {
            Debug.WriteLine("Перезапуск невозможен: не найден исполняемый файл приложения.");
            return;
        }

        try
        {
            using var started = Process.Start(new ProcessStartInfo(executable)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(executable) ?? string.Empty
            });
            if (started is null)
            {
                Debug.WriteLine("Перезапуск не удался: процесс не создан.");
                return;
            }
        }
        catch (Exception exception)
        {
            // Неудачный перезапуск не должен закрывать приложение: человек
            // останется с работающей копией и прежней настройкой.
            Debug.WriteLine($"Перезапуск не удался: {exception}");
            return;
        }

        // Полное имя нарочно: короткое `Application` разрешается в
        // пространство имён TempoCue.Application, а не в тип WPF.
        System.Windows.Application.Current?.Shutdown();
    }
}
