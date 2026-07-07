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
    public string? WindDirectionText { get; set; }
    public double? Pressure { get; set; }
    public double? Precipitation { get; set; }
    public int? CloudCover { get; set; }
    public double? UvIndex { get; set; }
    public int? RainProbability { get; set; }
    public string? Sunrise { get; set; }
    public string? Sunset { get; set; }
    public string? WeatherText { get; set; }
    public string? WeatherIcon { get; set; }
    public string? UpdateTime { get; set; }

    public List<ForecastDay> Forecast { get; set; } = new();
    public List<HourlyForecast> HourlyForecasts { get; set; } = new();

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
                $"&current=temperature_2m,relative_humidity_2m,apparent_temperature,is_day,precipitation,weather_code,cloud_cover,pressure_msl,wind_speed_10m,wind_direction_10m" +
                $"&hourly=temperature_2m,weather_code,precipitation_probability" +
                $"&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset,uv_index_max,precipitation_probability_max" +
                $"&forecast_days=7&timezone=auto";

            string weatherJson = await client.GetStringAsync(weatherUrl);
            using JsonDocument weatherDoc = JsonDocument.Parse(weatherJson);

            JsonElement current = weatherDoc.RootElement.GetProperty("current");

            Temperature = current.GetProperty("temperature_2m").GetDouble();
            FeelsLike = current.GetProperty("apparent_temperature").GetDouble();
            Humidity = current.GetProperty("relative_humidity_2m").GetInt32();
            WindSpeed = current.GetProperty("wind_speed_10m").GetDouble();
            Pressure = current.GetProperty("pressure_msl").GetDouble();
            Precipitation = current.GetProperty("precipitation").GetDouble();
            CloudCover = current.GetProperty("cloud_cover").GetInt32();

            int windDirection = current.GetProperty("wind_direction_10m").GetInt32();
            int weatherCode = current.GetProperty("weather_code").GetInt32();
            int isDay = current.GetProperty("is_day").GetInt32();

            WindDirectionText = GetWindDirectionText(windDirection);
            WeatherText = GetWeatherText(weatherCode);
            WeatherIcon = GetWeatherIcon(weatherCode, isDay);
            BackgroundClass = GetBackgroundClass(weatherCode, isDay);

            string timeText = current.GetProperty("time").GetString() ?? "";
            UpdateTime = FormatDateTime(timeText);

            JsonElement daily = weatherDoc.RootElement.GetProperty("daily");

            Sunrise = FormatOnlyTime(daily.GetProperty("sunrise")[0].GetString() ?? "");
            Sunset = FormatOnlyTime(daily.GetProperty("sunset")[0].GetString() ?? "");
            UvIndex = daily.GetProperty("uv_index_max")[0].GetDouble();
            RainProbability = daily.GetProperty("precipitation_probability_max")[0].GetInt32();

            JsonElement dates = daily.GetProperty("time");
            JsonElement maxTemps = daily.GetProperty("temperature_2m_max");
            JsonElement minTemps = daily.GetProperty("temperature_2m_min");
            JsonElement dailyCodes = daily.GetProperty("weather_code");

            for (int i = 0; i < dates.GetArrayLength(); i++)
            {
                int code = dailyCodes[i].GetInt32();

                Forecast.Add(new ForecastDay
                {
                    Date = FormatDayName(dates[i].GetString() ?? ""),
                    MaxTemp = maxTemps[i].GetDouble(),
                    MinTemp = minTemps[i].GetDouble(),
                    Icon = GetWeatherIcon(code, 1),
                    Text = GetWeatherText(code)
                });
            }

            JsonElement hourly = weatherDoc.RootElement.GetProperty("hourly");

            JsonElement hourlyTimes = hourly.GetProperty("time");
            JsonElement hourlyTemps = hourly.GetProperty("temperature_2m");
            JsonElement hourlyCodes = hourly.GetProperty("weather_code");
            JsonElement hourlyRain = hourly.GetProperty("precipitation_probability");

            int startIndex = FindCurrentHourIndex(hourlyTimes, timeText);

            for (int i = startIndex; i < hourlyTimes.GetArrayLength() && HourlyForecasts.Count < 12; i++)
            {
                int code = hourlyCodes[i].GetInt32();

                HourlyForecasts.Add(new HourlyForecast
                {
                    Time = FormatOnlyTime(hourlyTimes[i].GetString() ?? ""),
                    Temp = hourlyTemps[i].GetDouble(),
                    Icon = GetWeatherIcon(code, 1),
                    RainProbability = hourlyRain[i].GetInt32()
                });
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Hata: " + ex.Message;
        }
    }

    private int FindCurrentHourIndex(JsonElement hourlyTimes, string currentTime)
    {
        for (int i = 0; i < hourlyTimes.GetArrayLength(); i++)
        {
            string value = hourlyTimes[i].GetString() ?? "";

            if (string.Compare(value, currentTime, StringComparison.Ordinal) >= 0)
                return i;
        }

        return 0;
    }

    private string FormatDateTime(string value)
    {
        if (DateTime.TryParse(value, out DateTime date))
            return date.ToString("dd.MM.yyyy HH:mm");

        return value.Replace("T", " ");
    }

    private string FormatOnlyTime(string value)
    {
        if (DateTime.TryParse(value, out DateTime date))
            return date.ToString("HH:mm");

        return value;
    }

    private string FormatDayName(string value)
    {
        if (DateTime.TryParse(value, out DateTime date))
        {
            if (date.Date == DateTime.Now.Date)
                return "Bugün";

            return date.ToString("dddd", new CultureInfo("tr-TR"));
        }

        return value;
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

    private string GetWeatherIcon(int code, int isDay)
    {
        if (code == 0)
            return isDay == 1 ? "☀️" : "🌙";

        return code switch
        {
            1 or 2 => isDay == 1 ? "🌤️" : "☁️",
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

    private string GetBackgroundClass(int code, int isDay)
    {
        if (isDay == 0)
            return "night-bg";

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

private string GetWindDirectionText(int degree)
{
    if (degree >= 337 || degree < 23)
        return "Kuzey";
    else if (degree < 68)
        return "Kuzeydoğu";
    else if (degree < 113)
        return "Doğu";
    else if (degree < 158)
        return "Güneydoğu";
    else if (degree < 203)
        return "Güney";
    else if (degree < 248)
        return "Güneybatı";
    else if (degree < 293)
        return "Batı";
    else
        return "Kuzeybatı";
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

public class HourlyForecast
{
    public string Time { get; set; } = "";
    public double Temp { get; set; }
    public string Icon { get; set; } = "";
    public int RainProbability { get; set; }
}