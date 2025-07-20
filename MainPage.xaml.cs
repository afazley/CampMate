using CommunityToolkit.Maui.Alerts;
using Microsoft.Maui.Controls.Maps;
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
            Connectivity.ConnectivityChanged += Connectivity_ConnectivityChanged;
            LoadCampsites();
            BindingContext = this;
        }

        private async void LoadCampsites()
        {
            try
            {
                // Request location
                var location = await Geolocation.GetLastKnownLocationAsync();
                if (location == null)
                    location = await Geolocation.GetLocationAsync();

                if (location == null)
                {
                    await DisplayAlert("Location Error", "Unable to determine location.", "OK");
                    return;
                }

                double lat = location.Latitude;
                double lon = location.Longitude;

                // Create bounding box around user (0.5 deg is ~50km)
                double latMin = lat - 0.25;
                double latMax = lat + 0.25;
                double lonMin = lon - 0.25;
                double lonMax = lon + 0.25;

                string overpassUrl = $"https://overpass-api.de/api/interpreter?data=[out:json];node[\"tourism\"=\"camp_site\"]({latMin},{lonMin},{latMax},{lonMax});out;";

                using HttpClient client = new();
                string json = await client.GetStringAsync(overpassUrl);

                var root = JsonSerializer.Deserialize<OverpassResponse>(json);

                Campsites.Clear();
                CampsiteMap.Pins.Clear();  // Clear existing pins on the map

                foreach (var element in root.elements)
                {
                    var campsiteName = element.tags?.ContainsKey("name") == true ? element.tags["name"] : "Unnamed Site";
                    var campsiteLat = element.lat;
                    var campsiteLon = element.lon;

                    Campsites.Add(new Campsite
                    {
                        Name = campsiteName,
                        Location = $"Lat: {campsiteLat}, Lon: {campsiteLon}",
                        Latitude = campsiteLat,
                        Longitude = campsiteLon
                    });

                    // Create and add a pin to the map
                    var pin = new Pin
                    {
                        Label = campsiteName,
                        Address = $"Lat: {campsiteLat}, Lon: {campsiteLon}",
                        Location = new Location(campsiteLat, campsiteLon), // Maui Maps Location
                        Type = PinType.Place
                    };

                    CampsiteMap.Pins.Add(pin);
                }
            }
            catch (FeatureNotEnabledException)
            {
                await DisplayAlert("Location Disabled", "Location services are not enabled.", "OK");
            }
            catch (PermissionException)
            {
                await DisplayAlert("Permission Denied", "Location permission not granted.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not fetch campsites: {ex.Message}", "OK");
            }
        }

        private async void OnCacheNowClicked(object sender, EventArgs e)
        {
            try
            {
                var firstTen = Campsites.Take(10).ToList();
                string json = JsonSerializer.Serialize(firstTen);

                string fileName = "cached_campsites.json";
                string filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

                File.WriteAllText(filePath, json);

                await DisplayAlert("Offline Cache", "First 10 campsites have been cached locally.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to cache campsites: {ex.Message}", "OK");
            }
        }

        private async void Connectivity_ConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            if (e.NetworkAccess != NetworkAccess.Internet)
            {
                await CacheFirstTenCampsitesAsync();
                await Toast.Make("Internet connection lost. Cached campsites for offline use.").Show();
            }
        }

        private async Task CacheFirstTenCampsitesAsync()
        {
            try
            {
                var firstTen = Campsites.Take(10).ToList();
                string json = JsonSerializer.Serialize(firstTen);

                string fileName = "cached_campsites.json";
                string filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to auto-cache campsites: {ex.Message}", "OK");
            }
        }

        private async void OnViewCachedClicked(object sender, EventArgs e)
        {
            try
            {
                string filePath = Path.Combine(FileSystem.AppDataDirectory, "cached_campsites.json");
                if (File.Exists(filePath))
                {
                    string json = await File.ReadAllTextAsync(filePath);
                    var cachedSites = JsonSerializer.Deserialize<List<Campsite>>(json);
                    await DisplayAlert("Cache", $"Cached: {cachedSites?.Count} campsites", "OK");
                }
                else
                {
                    await DisplayAlert("Cache", "No cache file found.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
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

    public class OverpassResponse
    {
        public List<OverpassElement> elements { get; set; }
    }

    public class OverpassElement
    {
        public long id { get; set; }
        public double lat { get; set; }
        public double lon { get; set; }
        public Dictionary<string, string> tags { get; set; }
    }

}
