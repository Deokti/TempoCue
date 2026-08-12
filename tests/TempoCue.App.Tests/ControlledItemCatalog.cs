using TempoCue.Application;

namespace TempoCue.App.Tests;

/// <summary>
/// Позволяет тесту управлять порядком завершения запросов каталога предметов.
/// Отключение отмены имитирует внешний источник, который завершает устаревший запрос позднее нового.
/// </summary>
internal sealed class ControlledItemCatalog(bool honorCancellation) : IItemCatalog
{
    private readonly List<Request> _requests = [];

    public int RequestCount => _requests.Count;

    public Task<ItemCatalogLoadResult> LoadAsync(
        string gamePatch,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<ItemCatalogLoadResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _requests.Add(new Request(completion, cancellationToken));
        return honorCancellation
            ? completion.Task.WaitAsync(cancellationToken)
            : completion.Task;
    }

    public CancellationToken GetCancellationToken(int requestIndex) =>
        _requests[requestIndex].CancellationToken;

    public void Complete(int requestIndex, ItemCatalogLoadResult result) =>
        _requests[requestIndex].Completion.TrySetResult(result);

    private sealed record Request(
        TaskCompletionSource<ItemCatalogLoadResult> Completion,
        CancellationToken CancellationToken);
}
