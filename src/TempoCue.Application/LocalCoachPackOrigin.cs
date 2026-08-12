namespace TempoCue.Application;

/// <summary>
/// Происхождение локального Coach Pack определяет право редактирования, но не доверие к авторству.
/// Настоящая проверка издателя появится только вместе с серверной учётной записью.
/// </summary>
public enum LocalCoachPackOrigin
{
    CreatedByUser,
    Imported
}
