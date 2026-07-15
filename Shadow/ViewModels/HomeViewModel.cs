using Shadow.Abstractions;

namespace Shadow.ViewModels;

public class HomeViewModel : ViewModelBase
{
    public HomeViewModel()
    {
        ShadowLocalizer.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Subtitle));
    }

    public string Title { get; } = "Shadow";

    public string Subtitle => Localizer["Shadow.Home.Subtitle"];
}
