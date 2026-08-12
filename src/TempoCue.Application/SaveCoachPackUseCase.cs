using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>Проверяет и сохраняет готовый Coach Pack в выбранном локальном состоянии.</summary>
public sealed class SaveCoachPackUseCase
{
    private readonly ICoachPackStore _store;

    public SaveCoachPackUseCase(ICoachPackStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task<CoachPackSaveResult> ExecuteAsync(
        CoachPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        CoachPackValidator.Validate(pack);
        return _store.SaveAsync(pack, state, cancellationToken);
    }
}
