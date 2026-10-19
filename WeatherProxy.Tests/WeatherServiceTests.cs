using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using WeatherProxy.Api;

namespace WeatherProxy.Tests;

public class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(responder(request));
    }
}

public class WeatherServiceTests
{
    private const string GeoJson =
        """{"results":[{"name":"London","country":"United Kingdom","latitude":51.5,"longitude":-0.12}]}""";
    private const string ForecastJson =
        """{"current":{"temperature_2m":12.5,"wind_speed_10m":9.0,"weather_code":3}}""";

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (WeatherService Service, StubHandler Handler) Create(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHandler(responder);
        var service = new WeatherService(new HttpClient(handler),
                                         new MemoryCache(new MemoryCacheOptions()));
        return (service, handler);
    }

    private static HttpResponseMessage HappyPath(HttpRequestMessage req) =>
        Json(req.RequestUri!.Host.Contains("geocoding") ? GeoJson : ForecastJson);

    [Fact]
    public async Task GetAsync_MapsProviderDataToOurResponse()
    {
        var (service, _) = Create(HappyPath);

        var result = await service.GetAsync("London");

        Assert.NotNull(result);
        Assert.Equal("London", result.City);
        Assert.Equal(12.5, result.TemperatureC);
        Assert.Equal("Partly cloudy", result.Condition);
        Assert.False(result.FromCache);
    }

    [Fact]
    public async Task GetAsync_UnknownCity_ReturnsNull()
    {
        var (service, _) = Create(_ => Json("{}"));

        Assert.Null(await service.GetAsync("Nowhereville"));
    }

    [Fact]
    public async Task GetAsync_SecondCall_ComesFromCache()
    {
        var (service, handler) = Create(HappyPath);

        await service.GetAsync("London");
        var second = await service.GetAsync("london");   // different casing, same key

        Assert.True(second!.FromCache);
        Assert.Equal(2, handler.CallCount);   // 1 geocode + 1 forecast, not 4
    }

    [Fact]
    public async Task GetAsync_ProviderError_ThrowsHttpRequestException()
    {
        var (service, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetAsync("London"));
    }

    [Theory]
    [InlineData(0, "Clear sky")]
    [InlineData(2, "Partly cloudy")]
    [InlineData(63, "Rain")]
    [InlineData(96, "Thunderstorm")]
    public void Describe_MapsWeatherCodes(int code, string expected) =>
        Assert.Equal(expected, WeatherService.Describe(code));
}