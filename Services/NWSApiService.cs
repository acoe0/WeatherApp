using System.Text.Json;
using NWSWeatherApp.Models;

namespace NWSWeatherApp.Services;

public class NWSApiService : IDisposable
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // NWS requires a descriptive User-Agent with contact info; requests without it may be blocked.
    private const string UserAgent = "NWSWeatherApp/1.0 (github.com/acoe0/WeatherApp)";

    public NWSApiService()
    {
        _client = new HttpClient
        {
            BaseAddress = new Uri("https://api.weather.gov"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _client.DefaultRequestHeaders.Accept.ParseAdd("application/geo+json");
    }

    /// <summary>Retrieves all forecast zones for a given two-letter state abbreviation.</summary>
    public async Task<ZoneFeatureCollection?> GetZonesByStateAsync(string state)
    {
        return await GetAsync<ZoneFeatureCollection>($"/zones?area={state}&type=forecast");
    }

    /// <summary>Retrieves zone metadata including geometry for a forecast zone.</summary>
    public async Task<ZoneFeature?> GetZoneDetailAsync(string zoneId)
    {
        return await GetAsync<ZoneFeature>($"/zones/forecast/{zoneId}");
    }

    /// <summary>Resolves a lat/lon to the NWS grid metadata for that point.</summary>
    public async Task<PointsResponse?> GetPointDataAsync(double lat, double lon)
    {
        return await GetAsync<PointsResponse>($"/points/{lat:F4},{lon:F4}");
    }

    /// <summary>Fetches a grid-based daily or hourly forecast from the absolute URL returned by /points.</summary>
    public async Task<GridForecastResponse?> GetGridForecastAsync(string url)
    {
        return await GetAsync<GridForecastResponse>(url);
    }

    // ── private helpers ───────────────────────────────────────────────────────

    private async Task<T?> GetAsync<T>(string url)
    {
        try
        {
            var response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(content, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            PrintError($"HTTP error ({ex.StatusCode}): {ex.Message}");
            return default;
        }
        catch (TaskCanceledException)
        {
            PrintError("Request timed out. Check your network connection.");
            return default;
        }
        catch (JsonException ex)
        {
            PrintError($"Failed to parse API response: {ex.Message}");
            return default;
        }
    }

    private static void PrintError(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  [ERROR] {message}");
        Console.ForegroundColor = prev;
    }

    public void Dispose() => _client.Dispose();
}
