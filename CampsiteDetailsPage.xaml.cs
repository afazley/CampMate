using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace CampMate;

public partial class CampsiteDetailsPage : ContentPage
{
    private Campsite _campsite;

    public CampsiteDetailsPage(Campsite campsite)
    {
        InitializeComponent();
        _campsite = campsite;
        BindingContext = campsite;
    }

    private async void OnOpenInMapsClicked(object sender, EventArgs e)
    {
        var url = $"https://www.google.com/maps/dir/?api=1&destination={_campsite.Latitude},{_campsite.Longitude}";
        await Launcher.Default.OpenAsync(url);
    }
}

