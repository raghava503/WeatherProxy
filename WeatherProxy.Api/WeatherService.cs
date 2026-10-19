using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;

namespace WeatherProxy.Api;

public interface IWeatherService
{
    Task<WeatherResponse?> GetAsync(string city, CancellationToken ct = default);
}

public class WeatherService(HttpClient http, IMemoryCache cache) : IWeatherService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public async Task<WeatherResponse?> GetAsync(string city, CancellationToken ct = default)
    {
        string key = $"weather:{city.Trim().ToLowerInvariant()}";

        if (cache.TryGetValue(key, out WeatherResponse? cached) && cached is not null)
            return cached with { FromCache = true };

        // Step 1: city name -> coordinates
        var geoUrl = "https://geocoding-api.open-meteo.com/v1/search" +
                     $"?name={Uri.EscapeDataString(city)}&count=1";
        var geo = await http.GetFromJsonAsync<GeoResponse>(geoUrl, ct);

        var place = geo?.Results?.FirstOrDefault();
        if (place is null) return null;   // city not found

        // Step 2: coordinates -> current weather
        var forecastUrl = "https://api.open-meteo.com/v1/forecast" +
                          $"?latitude={place.Latitude}&longitude={place.Longitude}" +
                          "&current=temperature_2m,wind_speed_10m,weather_code";
        var forecast = await http.GetFromJsonAsync<ForecastResponse>(forecastUrl, ct);

        var current = forecast!.Current;
        var result = new WeatherResponse(
            place.Name,
            place.Country,
            current.Temperature,
            current.WindSpeed,
            Describe(current.WeatherCode),
            DateTime.UtcNow);

        cache.Set(key, result, CacheDuration);
        return result;
    }

    public static string Describe(int code) => code switch
    {
        0 => "Clear sky",
        1 or 2 or 3 => "Partly cloudy",
        45 or 48 => "Fog",
        >= 51 and <= 67 => "Rain",
        >= 71 and <= 77 => "Snow",
        >= 80 and <= 82 => "Rain showers",
        >= 95 => "Thunderstorm",
        _ => "Unknown"
    };
}