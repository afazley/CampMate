using CommunityToolkit.Maui.Alerts;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;





namespace CampMate
{



    public partial class MainPage : ContentPage
    {
        public ICommand SuggestionTappedCommand { get; }



        private readonly HttpClient _httpClient = new HttpClient();
        private const string GooglePlacesApiKey = "AIzaSyBahXlRuPXZdbQGHuK987bQgnBJZ7ZeEXk"; // Replace with your key
        private CancellationTokenSource _cts = new();
 

        public ObservableCollection<Campsite> Campsites { get; set; } = new();
        private bool isBusy;
        public bool IsBusy
        {
            get => isBusy;
            set
            {
                isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
            }
        }

        public MainPage()
        {
            InitializeComponent();

            Connectivity.ConnectivityChanged += Connectivity_ConnectivityChanged;

            LoadCampsites();

            BindingContext = this;

            SuggestionTappedCommand = new Command<object>(async (item) =>
            {
                dynamic suggestion = item;
                string address = suggestion?.Description;

                if (!string.IsNullOrEmpty(address))
                {
                    AddressSuggestions.IsVisible = false;
                    AddressEntry.Text = address;

                    var (lat, lng) = await GetCoordinatesFromAddress(address);
                    if (lat != 0 && lng != 0)
                    {
                        await LoadCampsites(lat, lng);
                    }
                }
            });

            _ = RefreshCampsitesAsync();
        }

        private async Task RefreshCampsitesAsync()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await DisplayAlert("Offline", "You're offline. Can't refresh now.", "OK");
                return;
            }

            try
            {
                IsBusy = true; // Show spinner

                CampsiteMap.Pins.Clear();
                Campsites.Clear();

                await LoadCampsites();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not refresh: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false; // Hide spinner
            }
        }

        private async Task LoadCampsites()
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

                    var tags = element.tags ?? new Dictionary<string, string>();

                    System.Diagnostics.Debug.WriteLine($"Tags for {campsiteName}:");
                    foreach (var tag in tags)
                    {
                        System.Diagnostics.Debug.WriteLine($"{tag.Key} = {tag.Value}");
                    }

                    var campsite = new Campsite
                    {
                        Name = campsiteName,
                        Location = $"Lat: {campsiteLat}, Lon: {campsiteLon}",
                        Latitude = campsiteLat,
                        Longitude = campsiteLon,
                        Tags = tags
                    };

                    Campsites.Add(campsite);

                    var pin = new Pin
                    {
                        Label = campsite.Name,
                        Address = campsite.Location,
                        Location = new Location(campsite.Latitude, campsite.Longitude),
                        Type = PinType.Place
                    };

                    pin.MarkerClicked += async (s, args) =>
                    {
                        args.HideInfoWindow = true;
                        await Navigation.PushAsync(new CampsiteDetailsPage(campsite));
                        CampsiteMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                            new Location(campsite.Latitude, campsite.Longitude),
                            Distance.FromKilometers(2)));
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

        private async Task LoadCampsites(double latitude, double longitude)
        {
            try
            {
                var location = await Geolocation.GetLastKnownLocationAsync();
                if (location == null)
                {
                    location = await Geolocation.GetLocationAsync(new GeolocationRequest
                    {
                        DesiredAccuracy = GeolocationAccuracy.Medium,
                        Timeout = TimeSpan.FromSeconds(10)
                    });
                }

                if (location != null)
                {
                    await LoadCampsites(location.Latitude, location.Longitude);
                }
                else
                {
                    await DisplayAlert("Error", "Could not get your location.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to get location: {ex.Message}", "OK");
            }
        }


        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await DisplayAlert("Offline", "You're offline. Can't refresh now.", "OK");
                return;
            }

            try
            {
                IsBusy = true; // Show spinner

                CampsiteMap.Pins.Clear();
                Campsites.Clear();

                await LoadCampsites();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not refresh: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false; // Hide spinner no matter what
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

        private async void OnCampsiteSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is Campsite selected)
            {
                await Navigation.PushAsync(new CampsiteDetailsPage(selected));

                CampsiteMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                    new Location(selected.Latitude, selected.Longitude),
                    Distance.FromKilometers(2)));
            } 
        }

        private async void OnUseGpsClicked(object sender, EventArgs e)
        {
            await LoadCampsites(); // Your existing location logic
        }

        private void OnChangeAddressClicked(object sender, EventArgs e)
        {
            AddressEntry.IsVisible = true;
            AddressSuggestions.IsVisible = true;
            AddressEntry.Focus();
        }

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.NewTextValue)) return;

            try
            {
                var uri = $"https://maps.googleapis.com/maps/api/place/autocomplete/json?input={Uri.EscapeDataString(e.NewTextValue)}&key={GooglePlacesApiKey}";
                var response = await _httpClient.GetFromJsonAsync<GooglePlaceAutocompleteResponse>(uri, cancellationToken: _cts.Token);

                if (response?.Predictions != null)
                {
                    AddressSuggestions.ItemsSource = response.Predictions;
                    AddressSuggestions.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Autocomplete error: {ex.Message}");
            }
        }

        private async void OnSuggestionSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is string selectedAddress)
            {
                AddressSuggestions.IsVisible = false;
                AddressEntry.Text = selectedAddress;

                // Fetch coordinates from the address
                var (lat, lng) = await GetCoordinatesFromAddress(selectedAddress);

                if (lat != 0 && lng != 0)
                {
                    await LoadCampsites(lat, lng);
                }

                // Clear the selection
                ((ListView)sender).SelectedItem = null;
            }
        }

        private async Task<(double, double)> GetCoordinatesFromAddress(string address)
        {
            var apiKey = "AIzaSyBahXlRuPXZdbQGHuK987bQgnBJZ7ZeEXk";
            var requestUri = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={apiKey}";

            using var httpClient = new HttpClient();
            var response = await httpClient.GetStringAsync(requestUri);
            var result = JsonSerializer.Deserialize<JsonElement>(response);

            if (result.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
            {
                var location = results[0].GetProperty("geometry").GetProperty("location");
                var lat = location.GetProperty("lat").GetDouble();
                var lng = location.GetProperty("lng").GetDouble();
                return (lat, lng);
            }

            return (0, 0); // fallback
        }



    }



    public class Campsite
    {
        public string Name { get; set; }
        public string Location { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public Dictionary<string, string> Tags { get; set; } = new();
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

    public class GooglePlaceAutocompleteResponse
    {
        [JsonPropertyName("predictions")]
        public List<Prediction> Predictions { get; set; }
    }

    public class Prediction
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("place_id")]
        public string PlaceId { get; set; }

        public override string ToString() => Description;
    }

    public class GooglePlaceDetailsResponse
    {
        [JsonPropertyName("result")]
        public PlaceResult Result { get; set; }
    }

    public class PlaceResult
    {
        [JsonPropertyName("geometry")]
        public Geometry Geometry { get; set; }
    }

    public class Geometry
    {
        [JsonPropertyName("location")]
        public LatLng Location { get; set; }
    }

    public class LatLng
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lng")]
        public double Lng { get; set; }
    }


}
