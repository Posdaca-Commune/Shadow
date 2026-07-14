using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Shadow.Abstractions;

namespace Shadow.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    protected ViewModelBase()
    {
        ShadowLocalizer.Instance.PropertyChanged += Localizer_OnPropertyChanged;
    }

    [ObservableProperty]
    private ShadowLocalizationScope _localizer = new();

    private void Localizer_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Replace the binding source so Avalonia compiled bindings re-read all Localizer[key] paths.
        Localizer = new ShadowLocalizationScope();
        OnLocalizerChanged(e);
    }

    protected virtual void OnLocalizerChanged(PropertyChangedEventArgs e)
    {
    }
}
