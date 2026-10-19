using System.Text.Json.Serialization;

namespace WeatherProxy.Api;

// What WE return to our clients (clean and simple)
public record WeatherResponse(
    string City,
    string Country,
    double TemperatureC,
    double WindSpeedKmh,
    string Condition,
    DateTime FetchedAtUtc,
    bool FromCache = false);

// What Open-Meteo sends us (we only model the fields we need)
public record GeoResponse(List<GeoResult>? Results);
public record GeoResult(string Name, string Country, double Latitude, double Longitude);

public record ForecastResponse(CurrentWeather Current);
public record CurrentWeather(
    [property: JsonPropertyName("temperature_2m")] double Temperature,
    [property: JsonPropertyName("wind_speed_10m")] double WindSpeed,
    [property: JsonPropertyName("weather_code")] int WeatherCode);