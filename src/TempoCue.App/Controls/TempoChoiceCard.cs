using System.Windows;
using System.Windows.Controls;

namespace TempoCue.App.Controls;

/// <summary>Переиспользуемый выбор с заголовком, описанием и необязательной плашкой.</summary>
public sealed class TempoChoiceCard : RadioButton
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(TempoChoiceCard),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(TempoChoiceCard),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty NoticeProperty = DependencyProperty.Register(
        nameof(Notice),
        typeof(string),
        typeof(TempoChoiceCard),
        new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string Notice
    {
        get => (string)GetValue(NoticeProperty);
        set => SetValue(NoticeProperty, value);
    }
}
