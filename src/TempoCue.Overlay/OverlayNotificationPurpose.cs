namespace TempoCue.Overlay;

/// <summary>Определяет роль уведомления в игровом потоке независимо от его оформления.</summary>
public enum OverlayNotificationPurpose
{
    /// <summary>Сигнал о текущем игровом состоянии или событии, требующем своевременной реакции.</summary>
    Operational,

    /// <summary>Несрочная рекомендация, закрепляющая выбранный навык или игровую привычку.</summary>
    Learning
}
