using System.Windows.Data;
using System.Windows.Markup;

namespace TempoCue.App.Localization;

/// <summary>
/// Позволяет писать в XAML коротко: {localization:Loc MatchState.Title}.
/// Внутри создаётся обычная Binding, поэтому текст обновляется при смене языка.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension(string key)
    {
        Key = key;
    }

    public string Key { get; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationService.Instance,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
