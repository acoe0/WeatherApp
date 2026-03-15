# NWS Weather Forecast Viewer

A C# console application that retrieves and displays weather forecast data from the [National Weather Service API](https://www.weather.gov/documentation/services-web-api).

## Features

| Feature | Description |
|---|---|
| State → Zone browsing | Enter a two-letter state code to list all NWS forecast zones, then pick one to view its daily + 24-hour hourly forecast |
| Coordinate → Grid forecast | Enter a lat/lon to get a NWS-resolved daily + 24-hour hourly forecast for that exact point |
| Color-coded temperatures | Blue → Cyan → White → Yellow → Red scaling from cold to hot |
| Daily H/L summary | Each day/night period shows the high or low clearly |
| Hourly table | Time, temperature, precipitation probability, humidity, wind, and condition |

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Running the App

```bash
cd NWSWeatherApp
dotnet run
```

## Project Structure

```
NWSWeatherApp/
├── Program.cs                  # Entry point, menu loop, two user flows
├── NWSWeatherApp.csproj
├── Models/
│   └── NWSModels.cs            # Typed models for all NWS API responses
├── Services/
│   └── NWSApiService.cs        # HttpClient wrapper (User-Agent, error handling)
└── Display/
    └── WeatherDisplay.cs       # All console formatting and color logic
```

## NWS API Endpoints Used

| Endpoint | Purpose |
|---|---|
| `GET /zones?area={state}&type=forecast` | List forecast zones for a state |
| `GET /zones/forecast/{zoneId}` | Zone metadata including polygon geometry |
| `GET /points/{lat},{lon}` | Resolve coordinates to an NWS grid |
| `GET /gridpoints/{wfo}/{x},{y}/forecast` | Daily 7-day forecast for a grid point |
| `GET /gridpoints/{wfo}/{x},{y}/forecast/hourly` | Hourly forecast for a grid point |

### NWS API Notes

- A descriptive `User-Agent` header is **required** — requests without it may be blocked or rate-limited.
- All responses are [GeoJSON](https://geojson.org/) `Feature` or `FeatureCollection` objects with forecast data inside the `properties` field.
- Coverage is the contiguous US, Alaska, Hawaii, and US territories.

## Design Decisions

**`User-Agent` header is required**
The NWS API actively blocks or rate-limits requests that do not include a descriptive `User-Agent` string with contact information. This is documented in the API and enforced in practice — it is not optional.

**Zone detail is fetched separately from the zone listing**
The `/zones?area={state}&type=forecast` listing endpoint returns `geometry: null` on every feature. To resolve a zone to a grid point (needed for daily and hourly forecasts), the app fetches the individual zone endpoint `/zones/forecast/{zoneId}`, which does include the polygon geometry. The centroid of that polygon is then used to call `/points/{lat},{lon}`.

**Both flows use the same grid-based display**
Rather than maintaining two separate display paths (one for zone text forecasts, one for grid forecasts), both the state→zone flow and the coordinate flow resolve to a grid point and use the same `ShowDailyForecast` / `ShowHourlyForecast` methods. This keeps the display logic in one place and ensures consistent output regardless of how the user entered their location.

**Daily and hourly forecasts are fetched with `Task.WhenAll`**
The daily and hourly grid forecast URLs are independent — neither depends on the other's response. Fetching them concurrently with `Task.WhenAll` roughly halves the wall-clock time for that step compared to sequential `await`.

**`updateTime` vs `updated` field names**
Grid forecast responses (`/gridpoints/.../forecast`) use `updateTime` for the timestamp, while zone responses use `updated`. Both fields are deserialized into nullable properties on `GridForecastProperties`, and an `EffectiveUpdated` computed property returns whichever is present.

**`relativeLocation` city reflects the nearest municipality, not the searched city**
The `/points` endpoint returns the name of the nearest incorporated municipality, which can differ from an intuitive place name. For example, coordinates inside the Glendale enclave within Denver resolve to "Glendale, CO" rather than "Denver, CO". This is a NWS API behavior, not an application bug.

## Example Interaction

```
╔══════════════════════════════════════════════════════════════════════════════════════════╗
║                        National Weather Service Forecast Viewer                        ║
║                            Data provided by api.weather.gov                            ║
╚══════════════════════════════════════════════════════════════════════════════════════════╝

  What would you like to do?
  1. Browse zones by state and get a forecast
  2. Enter coordinates (lat/lon) for daily + hourly forecast
  3. Exit

  Choice: 2

  Enter coordinates (must be within the contiguous US, Alaska, or Hawaii)
  Latitude  (e.g., 39.7550 for Denver): 39.7550
  Longitude (e.g., -104.9400 for Denver): -104.9400

  Resolving (39.7550, -104.9400) to an NWS grid point...
  Location: Denver, CO  (Grid: BOU 66,64)

  Fetching daily and hourly forecasts...

╔══════════════════════════════════════════════════════════════════════════════════════════╗
║                                    DAILY FORECAST                                      ║
║                                      Denver, CO                                        ║
║                          Updated Sun Mar 15, 2026  11:00 AM                            ║
╚══════════════════════════════════════════════════════════════════════════════════════════╝

  DATE / PERIOD      HIGH     LOW  PRECIP  WIND              CONDITION
  ──────────────────────────────────────────────────────────────────────────────────────────
  Sun Mar 15         34°F    16°F    27%   N 17 to 23 mph    Chance Light Snow
  Mon Mar 16         52°F    39°F     1%   ENE 3 to 8 mph    Mostly Sunny
  Tue Mar 17         72°F    46°F     —    W 6 to 13 mph     Mostly Sunny
  Wed Mar 18         80°F    47°F     —    W 6 to 12 mph     Sunny
  Thu Mar 19         85°F    50°F     —    W 6 to 13 mph     Sunny
  Fri Mar 20         86°F    49°F     —    WSW 6 to 13 mph   Sunny
  Sat Mar 21         86°F    46°F     1%   SSE 6 to 14 mph   Sunny

╔══════════════════════════════════════════════════════════════════════════════════════════╗
║                             HOURLY FORECAST  ·  Next 24 Hours                          ║
║                                      Denver, CO                                        ║
║                          Updated Sun Mar 15, 2026  11:00 AM                            ║
╚══════════════════════════════════════════════════════════════════════════════════════════╝

  24-hr range:  ▲ High   33°F at 4 PM     ▼ Low   16°F at 6 AM

  TIME              TEMP   PRECIP  HUMIDITY  WIND              CONDITION
  ──────────────────────────────────────────────────────────────────────────────────────────
  Sun 12:00 PM        29°F    27%       31%  N 23 mph          Chance Light Snow
  Sun 1:00 PM         31°F    27%       27%  N 23 mph          Chance Light Snow
  Sun 2:00 PM         32°F    27%       26%  N 22 mph          Chance Light Snow
  Sun 3:00 PM         32°F    27%       25%  N 20 mph          Chance Light Snow
▲ Sun 4:00 PM         33°F    27%       25%  N 18 mph          Chance Light Snow
  Sun 5:00 PM         32°F    27%       26%  N 17 mph          Chance Light Snow
  Sun 6:00 PM         32°F    20%       26%  N 15 mph          Slight Chance Light Snow
  Sun 7:00 PM         30°F    15%       28%  N 12 mph          Slight Chance Light Snow
  Sun 8:00 PM         28°F    10%       30%  N 10 mph          Mostly Cloudy
  Sun 9:00 PM         26°F     5%       33%  N 8 mph           Mostly Cloudy
  Sun 10:00 PM        24°F     0%       36%  N 6 mph           Partly Cloudy
  Sun 11:00 PM        22°F     0%       40%  N 5 mph           Mostly Clear
  Mon 12:00 AM        20°F     0%       44%  calm              Clear
  Mon 1:00 AM         19°F     0%       47%  calm              Clear
  Mon 2:00 AM         18°F     0%       50%  calm              Clear
  Mon 3:00 AM         18°F     0%       52%  calm              Clear
  Mon 4:00 AM         17°F     0%       54%  calm              Clear
  Mon 5:00 AM         17°F     0%       55%  calm              Clear
▼ Mon 6:00 AM         16°F     0%       55%  calm              Clear
  Mon 7:00 AM         17°F     0%       54%  calm              Sunny
  Mon 8:00 AM         19°F     0%       50%  E 3 mph           Sunny
  Mon 9:00 AM         22°F     0%       46%  E 5 mph           Sunny
  Mon 10:00 AM        25°F     0%       42%  ENE 5 mph         Sunny
  Mon 11:00 AM        28°F     0%       38%  ENE 7 mph         Sunny
  ──────────────────────────────────────────────────────────────────────────────────────────
```
