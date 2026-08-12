using TempoCue.App.Localization;
using TempoCue.App.Settings;
using TempoCue.Overlay;

namespace TempoCue.App.Tests;

/// <summary>Проверяет возврат сохранённого runtime после интерактивного предпросмотра.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class OverlaySetupSessionTests
{
    [Fact]
    public void Close_RestoresSavedPresentationSettings()
    {
        using var presenter = new RecordingOverlayPresenter();
        var saved = new OverlaySettingsState(new OverlayUserSettings
        {
            Width = 300,
            BackgroundOpacityPercent = 60
        });
        using var session = new OverlaySetupSession(
            presenter,
            saved,
            LocalizationService.Instance);

        session.Open(new OverlayUserSettings
        {
            Width = 400,
            BackgroundOpacityPercent = 100
        });
        session.Close();

        Assert.False(session.IsActive);
        Assert.Equal(OverlayInteractionMode.Game, presenter.InteractionMode);
        Assert.Equal(300, presenter.Settings.Width);
        Assert.Equal(0.6, presenter.Settings.BackgroundOpacity);
    }

    [Fact]
    public void PresenterDismissal_RestoresSavedSettingsAndNotifiesViewModelBoundary()
    {
        using var presenter = new RecordingOverlayPresenter();
        var saved = new OverlaySettingsState(new OverlayUserSettings { Width = 340 });
        using var session = new OverlaySetupSession(
            presenter,
            saved,
            LocalizationService.Instance);
        var dismissalCount = 0;
        session.Dismissed += (_, _) => dismissalCount++;
        session.Open(new OverlayUserSettings { Width = 400 });

        presenter.DismissSetup();

        Assert.Equal(1, dismissalCount);
        Assert.Equal(340, presenter.Settings.Width);
    }
}
