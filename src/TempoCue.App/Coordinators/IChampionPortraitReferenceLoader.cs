using TempoCue.Application;
using TempoCue.Vision;

namespace TempoCue.App.Coordinators;

/// <summary>Преобразует портрет Data Dragon в маленький эталон для Vision.</summary>
internal interface IChampionPortraitReferenceLoader
{
    Task<MinimapChampionReference?> LoadAsync(
        ChampionCatalogEntry champion,
        MinimapMarkerAffiliation affiliation,
        CancellationToken cancellationToken = default);
}
