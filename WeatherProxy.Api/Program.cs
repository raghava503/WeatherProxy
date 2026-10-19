using System.Text.Json;
using WeatherProxy.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IWeatherService, WeatherService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

app.MapGet("/api/weather/{city}", async (string city, IWeatherService weather, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(city) || city.Length > 100)
        return Results.BadRequest(new { error = "Provide a city name (max 100 characters)." });

    try
    {
        var result = await weather.GetAsync(city, ct);
        return result is null ? Results.NotFound(new { error = $"City '{city}' not found." })
                              : Results.Ok(result);
    }
    catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException or JsonException)
                               && !ct.IsCancellationRequested)
    {
        return Results.Problem(statusCode: 502, title: "Weather provider unavailable");
    }
});

app.Run();