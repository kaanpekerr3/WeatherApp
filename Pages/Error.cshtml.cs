using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;
using System.Text.Json;

namespace WeatherApp.Pages;

public class IndexModel : PageModel
{
    [BindProperty]
    public string City { get; set; } = "";

    public string? ErrorMessage { get; set; }
    public string BackgroundClass { get; set; } = "default-bg";

    public string? CityName { get; set; }
    public string? Country { get; set; }
    public string? Region { get; set; }
    public double? Temperature { get; set; }
    public double? FeelsLike { get; set; }
    public int? Humidity { get; set; }
    public double? WindSpeed { get; set; }
    public string? WeatherText { get; set; }
    public string? WeatherIcon { get; set; }
    public string? UpdateTime { get; set; }

    public List<ForecastDay> Forecast { get; set; } = new();

    public async Task OnPostAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(City))
            {
                ErrorMessage = "Lütfen şehir veya ilçe adı gir.";
                return;
            }

            using HttpClient client = new();

            string encodedCity = Uri.EscapeDataString(City.Trim());
            string geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={encodedCity}&count=1&language=tr&format=json";

            string geoJson = await client.GetStringAsync(geoUrl);
            using JsonDocument geoDoc = JsonDocument.Parse(geoJson);

            if (!geoDoc.RootElement.TryGetProperty("results", out JsonElement results) || results.GetArrayLength() == 0)
            {
                ErrorMessage = "Konum bulunamadı. Örn: Istanbul, Kadikoy, Ankara";
                return;
            }

            JsonElement firstCity = results[0];

            double latitude = firstCity.GetProperty("latitude").GetDouble();
            double longitude = firstCity.GetProperty("longitude").GetDouble();

            CityName = firstCity.GetProperty("name").GetString() ?? City;
            Country = firstCity.TryGetProperty("country", out JsonElement countryElement) ? countryElement.GetString() : "";
            Region = firstCity.TryGetProperty("admin1", out JsonElement regionElement) ? regionElement.GetString() : "";

            string latText = latitude.ToString(CultureInfo.InvariantCulture);
            string lonText = longitude.ToString(CultureInfo.InvariantCulture);

            string weatherUrl =
                $"https://api.open-meteo.com/v1/forecast?latitude={latText}&longitude={lonText}" +
                $"&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m" +
                $"&daily=weather_code,temperature_2m_max,temperature_2m_min" +
                $"&forecast_days=7&timezone=auto";

            string weatherJson = await client.GetStringAsync(weatherUrl);
            using JsonDocument weatherDoc = JsonDocument.Parse(weatherJson);

            JsonElement current = weatherDoc.RootElement.GetProperty("current");

            Temperature = current.GetProperty("temperature_2m").GetDouble();
            FeelsLike = current.GetProperty("apparent_temperature").GetDouble();
            Humidity = current.GetProperty("relative_humidity_2m").GetInt32();
            WindSpeed = current.GetProperty("wind_speed_10m").GetDouble();

            int weatherCode = current.GetProperty("weather_code").GetInt32();

            string timeText = current.GetProperty("time").GetString() ?? "";
            UpdateTime = timeText.Replace("T", " ");

            WeatherText = GetWeatherText(weatherCode);
            WeatherIcon = GetWeatherIcon(weatherCode);
            BackgroundClass = GetBackgroundClass(weatherCode);

            JsonElement daily = weatherDoc.RootElement.GetProperty("daily");

            JsonElement dates = daily.GetProperty("time");
            JsonElement maxTemps = daily.GetProperty("temperature_2m_max");
            JsonElement minTemps = daily.GetProperty("temperature_2m_min");
            JsonElement codes = daily.GetProperty("weather_code");

            for (int i = 0; i < dates.GetArrayLength(); i++)
            {
                int code = codes[i].GetInt32();

                Forecast.Add(new ForecastDay
                {
                    Date = dates[i].GetString() ?? "",
                    MaxTemp = maxTemps[i].GetDouble(),
                    MinTemp = minTemps[i].GetDouble(),
                    Icon = GetWeatherIcon(code),
                    Text = GetWeatherText(code)
                });
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Hata: " + ex.Message;
        }
    }

    private string GetWeatherText(int code)
    {
        return code switch
        {
            0 => "Açık hava",
            1 => "Genelde açık",
            2 => "Parçalı bulutlu",
            3 => "Kapalı",
            45 or 48 => "Sisli",
            51 or 53 or 55 => "Çiseleme",
            61 or 63 or 65 => "Yağmurlu",
            71 or 73 or 75 => "Karlı",
            80 or 81 or 82 => "Sağanak yağışlı",
            95 => "Gök gürültülü",
            96 or 99 => "Dolu ve fırtına",
            _ => "Bilinmeyen"
        };
    }

    private string GetWeatherIcon(int code)
    {
        return code switch
        {
            0 => "☀️",
            1 or 2 => "🌤️",
            3 => "☁️",
            45 or 48 => "🌫️",
            51 or 53 or 55 => "🌦️",
            61 or 63 or 65 => "🌧️",
            71 or 73 or 75 => "❄️",
            80 or 81 or 82 => "🌧️",
            95 or 96 or 99 => "⛈️",
            _ => "🌍"
        };
    }

    private string GetBackgroundClass(int code)
    {
        return code switch
        {
            0 or 1 => "sunny-bg",
            2 or 3 => "cloudy-bg",
            45 or 48 => "foggy-bg",
            51 or 53 or 55 or 61 or 63 or 65 or 80 or 81 or 82 => "rainy-bg",
            71 or 73 or 75 => "snowy-bg",
            95 or 96 or 99 => "storm-bg",
            _ => "default-bg"
        };
    }
}

public class ForecastDay
{
    public string Date { get; set; } = "";
    public double MaxTemp { get; set; }
    public double MinTemp { get; set; }
    public string Icon { get; set; } = "";
    public string Text { get; set; } = "";
}