using NWSWeatherApp.Models;
using NWSWeatherApp.Services;
using NWSWeatherApp.Display;

// UTF-8 needed for weather glyphs (☀ ☽) on all platforms
Console.OutputEncoding = System.Text.Encoding.UTF8;

using var api = new NWSApiService();
var display = new WeatherDisplay();

display.ShowBanner();

bool running = true;
while (running)
{
    display.ShowMainMenu();
    var choice = Console.ReadLine()?.Trim();

    switch (choice)
    {
        case "1":
            await HandleStateSearchAsync(api, display);
            break;
        case "2":
            await HandleCoordinateSearchAsync(api, display);
            break;
        case "3":
        case "q":
        case "Q":
            running = false;
            Console.WriteLine("\n  Goodbye!\n");
            break;
        default:
            Console.WriteLine("  Invalid choice — please enter 1, 2, or 3.");
            break;
    }
}

// ── State → Zone → Forecast flow ─────────────────────────────────────────────

static async Task HandleStateSearchAsync(NWSApiService api, WeatherDisplay display)
{
    Console.Write("\n  Enter state abbreviation (e.g., CO): ");
    var state = Console.ReadLine()?.Trim().ToUpper();
    if (string.IsNullOrWhiteSpace(state))
    {
        Console.WriteLine("  No state entered.");
        return;
    }

    Console.WriteLine($"\n  Fetching forecast zones for {state}...");
    var zoneCollection = await api.GetZonesByStateAsync(state);

    if (zoneCollection is null || zoneCollection.Features.Count == 0)
    {
        Console.WriteLine($"  No forecast zones found for \"{state}\". Verify the two-letter state code.");
        return;
    }

    display.ShowZoneList(zoneCollection.Features, state);

    Console.Write("\n  Enter zone number (or 0 to go back): ");
    if (!int.TryParse(Console.ReadLine()?.Trim(), out int zoneNum) || zoneNum == 0)
        return;

    if (zoneNum < 1 || zoneNum > zoneCollection.Features.Count)
    {
        Console.WriteLine("  Invalid zone number.");
        return;
    }

    var selected = zoneCollection.Features[zoneNum - 1];
    var zoneId = selected.Properties?.Id ?? string.Empty;
    var zoneName = selected.Properties?.Name ?? zoneId;

    Console.WriteLine($"\n  Fetching forecast for {zoneName} ({zoneId})...");

    // Resolve zone geometry to a grid point so we can use the same grid-based
    // daily + hourly display as the coordinate flow
    var zoneDetail = await api.GetZoneDetailAsync(zoneId);
    var point = zoneDetail?.GetRepresentativePoint();

    if (!point.HasValue)
    {
        Console.WriteLine("  Could not determine a grid point for this zone.");
        return;
    }

    var pointData = await api.GetPointDataAsync(point.Value.Lat, point.Value.Lon);
    if (pointData?.Properties is null)
    {
        Console.WriteLine("  Could not resolve zone to an NWS grid.");
        return;
    }

    var gridProps = pointData.Properties;
    Console.WriteLine($"  Grid: {gridProps.Cwa} {gridProps.GridX},{gridProps.GridY}\n");

    Console.WriteLine("  Fetching daily and hourly forecasts...");
    var dailyTask  = api.GetGridForecastAsync(gridProps.ForecastUrl);
    var hourlyTask = api.GetGridForecastAsync(gridProps.ForecastHourlyUrl);
    await Task.WhenAll(dailyTask, hourlyTask);

    if (dailyTask.Result?.Properties is not null)
        display.ShowDailyForecast(dailyTask.Result.Properties, zoneName);
    else
        Console.WriteLine("  Could not retrieve daily forecast.");

    if (hourlyTask.Result?.Properties is not null)
        display.ShowHourlyForecast(hourlyTask.Result.Properties, zoneName);
    else
        Console.WriteLine("  Could not retrieve hourly forecast.");

    Pause();
}

// ── Lat/Lon → Grid forecast flow ──────────────────────────────────────────────

static async Task HandleCoordinateSearchAsync(NWSApiService api, WeatherDisplay display)
{
    Console.WriteLine("\n  Enter coordinates (must be within the contiguous US, Alaska, or Hawaii)");

    Console.Write("  Latitude  (e.g., 39.7550 for Denver): ");
    if (!double.TryParse(Console.ReadLine()?.Trim(), out double lat))
    {
        Console.WriteLine("  Invalid latitude.");
        return;
    }

    Console.Write("  Longitude (e.g., -104.9400 for Denver): ");
    if (!double.TryParse(Console.ReadLine()?.Trim(), out double lon))
    {
        Console.WriteLine("  Invalid longitude.");
        return;
    }

    Console.WriteLine($"\n  Resolving ({lat:F4}, {lon:F4}) to an NWS grid point...");
    var pointData = await api.GetPointDataAsync(lat, lon);

    if (pointData?.Properties is null)
    {
        Console.WriteLine("  Could not resolve coordinates. Confirm they are inside NWS coverage area.");
        return;
    }

    var props = pointData.Properties;
    var location = props.RelativeLocation?.Properties is { } loc
        ? $"{loc.City}, {loc.State}"
        : $"{lat:F4}, {lon:F4}";

    Console.WriteLine($"  Location: {location}  (Grid: {props.Cwa} {props.GridX},{props.GridY})\n");

    // Fetch daily and hourly in parallel to save time
    Console.WriteLine("  Fetching daily and hourly forecasts...");
    var dailyTask  = api.GetGridForecastAsync(props.ForecastUrl);
    var hourlyTask = api.GetGridForecastAsync(props.ForecastHourlyUrl);
    await Task.WhenAll(dailyTask, hourlyTask);

    var daily  = dailyTask.Result;
    var hourly = hourlyTask.Result;

    if (daily?.Properties is not null)
        display.ShowDailyForecast(daily.Properties, location);
    else
        Console.WriteLine("  Could not retrieve daily forecast.");

    if (hourly?.Properties is not null)
        display.ShowHourlyForecast(hourly.Properties, location);
    else
        Console.WriteLine("  Could not retrieve hourly forecast.");

    Pause();
}

static void Pause()
{
    Console.WriteLine();
    Console.Write("  Press Enter to return to the menu...");
    Console.ReadLine();
}
