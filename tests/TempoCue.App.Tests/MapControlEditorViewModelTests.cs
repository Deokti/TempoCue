using TempoCue.App.Localization;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Tests;

/// <summary>Проверяет последовательность первой итерации редактора контроля карты.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MapControlEditorViewModelTests
{
    [Fact]
    public async Task NewEditor_UnlocksScopeOnlyAfterValidMainInformation()
    {
        var editor = CreateEditor();

        await editor.BeginNewAsync();

        Assert.Equal(1, editor.CurrentStep);
        Assert.False(editor.CanMoveNext);
        Assert.True(editor.CanNavigateToStep1);
        Assert.False(editor.CanNavigateToStep2);
        Assert.False(editor.NavigateToStep(3));

        editor.MainInformation.PackName = "Контроль объектов";
        editor.MainInformation.ShortDescription =
            "Напоминания о подготовке и появлении нейтральных объектов.";

        Assert.True(editor.MainInformation.IsComplete);
        Assert.True(editor.CanMoveNext);
        Assert.True(editor.MoveNext());
        Assert.Equal(2, editor.CurrentStep);
        Assert.True(editor.CanMoveNext);
        Assert.True(editor.CanNavigateToStep3);
    }

    [Fact]
    public void Scope_TracksMultipleRolesAndNeverAllowsEmptySelection()
    {
        var editor = CreateEditor();
        var scope = editor.Scope;

        Assert.True(scope.AllRolesSelected);
        Assert.Equal(5, scope.SelectedRoles.Count);

        scope.RoleOptions.Single(option => option.Role == TempoCue.Coaching.MapControlRole.Top)
            .IsSelected = true;
        scope.RoleOptions.Single(option => option.Role == TempoCue.Coaching.MapControlRole.Jungle)
            .IsSelected = true;

        Assert.False(scope.AllRolesSelected);
        Assert.Equal(2, scope.SelectedRoles.Count);

        foreach (var option in scope.RoleOptions.ToArray())
        {
            option.IsSelected = false;
        }

        Assert.True(scope.IsComplete);
        Assert.Single(scope.SelectedRoles);
    }

    [Fact]
    public async Task VisitedScope_AllowsReturnToCompletedMainInformation()
    {
        var editor = CreateEditor();
        await editor.BeginNewAsync();
        editor.MainInformation.PackName = "Контроль объектов";
        editor.MainInformation.ShortDescription =
            "Напоминания о подготовке и появлении нейтральных объектов.";
        Assert.True(editor.MoveNext());

        Assert.True(editor.NavigateToStep(1));
        Assert.Equal(1, editor.CurrentStep);
        Assert.True(editor.CanNavigateToStep2);
        Assert.True(editor.NavigateToStep(2));
    }

    private static MapControlEditorViewModel CreateEditor() => new(
        LocalizationService.Instance,
        TestGamePatchCatalog.Ready());
}
