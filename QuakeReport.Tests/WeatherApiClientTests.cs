using System.Net;
using System.Net.Http.Json;
using QuakeReport.Web;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public class WeatherApiClientTests
{
    [TestMethod]
    public async Task GetWeatherReturnsEmptyArrayForEmptyResponse()
    {
        using var client = CreateClient("[]");
        var apiClient = new WeatherApiClient(client);

        var result = await apiClient.GetWeatherAsync();

        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public async Task GetWeatherSkipsNullEntriesAndHonorsMaximum()
    {
        using var client = CreateClient("[null,{\"date\":\"2026-08-10\",\"temperatureC\":20,\"summary\":\"Warm\"},{\"date\":\"2026-08-11\",\"temperatureC\":21,\"summary\":\"Mild\"}]");
        var apiClient = new WeatherApiClient(client);

        var result = await apiClient.GetWeatherAsync(maxItems: 1);

        Assert.AreEqual(1, result.Length);
        Assert.AreEqual(new DateOnly(2026, 8, 10), result[0].Date);
        Assert.AreEqual(20, result[0].TemperatureC);
    }

    [TestMethod]
    public async Task GetWeatherReturnsNoItemsWhenMaximumIsZero()
    {
        using var client = CreateClient("[{\"date\":\"2026-08-10\",\"temperatureC\":20,\"summary\":\"Warm\"}]");
        var apiClient = new WeatherApiClient(client);

        var result = await apiClient.GetWeatherAsync(maxItems: 0);

        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public void WeatherForecastConvertsCelsiusToFahrenheit()
    {
        Assert.AreEqual(32, new WeatherForecast(default, 0, null).TemperatureF);
        Assert.AreEqual(211, new WeatherForecast(default, 100, null).TemperatureF);
        Assert.AreEqual(-39, new WeatherForecast(default, -40, null).TemperatureF);
    }

    private static HttpClient CreateClient(string json)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
        return new HttpClient(new TestHttpMessageHandler(response))
        {
            BaseAddress = new Uri("http://localhost"),
        };
    }
}
