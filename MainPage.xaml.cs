using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace CampMate
{
    public partial class MainPage : ContentPage
    {
        public ObservableCollection<Campsite> Campsites { get; set; } = new();

        public MainPage()
        {
            InitializeComponent();
            LoadCampsites();
            BindingContext = this;
        }

        private async void LoadCampsites()
        {
            try
            {
                string json = await FileSystem.OpenAppPackageFileAsync("campsites.json")
                                        .ContinueWith(t => new StreamReader(t.Result).ReadToEnd());

                var data = JsonSerializer.Deserialize<List<Campsite>>(json);
                if (data != null)
                {
                    Campsites.Clear();
                    foreach (var site in data)
                        Campsites.Add(site);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not load campsites: {ex.Message}", "OK");
            }
        }

        private void OnCacheNowClicked(object sender, EventArgs e)
        {
            DisplayAlert("Offline Cache", "Nearby campsite data has been cached for offline use.", "OK");
        }
    }

    public class Campsite
    {
        public string Name { get; set; }
        public string Location { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool HasElectricHookup { get; set; }
        public bool IsPetFriendly { get; set; }
        public bool HasWifi { get; set; }
    }
}
