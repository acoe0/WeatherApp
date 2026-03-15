using System.Text.Json;
using System.Text.Json.Serialization;

namespace NWSWeatherApp.Models;

// ── Zone listing ──────────────────────────────────────────────────────────────

public class ZoneFeatureCollection
{
    [JsonPropertyName("features")]
    public List<ZoneFeature> Features { get; set; } = new();
}

public class ZoneFeature
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("geometry")]
    public ZoneGeometry? Geometry { get; set; }

    [JsonPropertyName("properties")]
    public ZoneProperties? Properties { get; set; }

    /// <summary>
    /// Returns the centroid of the zone's GeoJSON polygon as (lat, lon).
    /// GeoJSON stores coordinates as [longitude, latitude].
    /// </summary>
    public (double Lat, double Lon)? GetRepresentativePoint()
    {
        if (Geometry is null) return null;
        try
        {
            JsonElement ring = Geometry.Type switch
            {
                "Polygon"      => Geometry.Coordinates[0],
                "MultiPolygon" => Geometry.Coordinates[0][0],
                _              => default
            };

            if (ring.ValueKind != JsonValueKind.Array) return null;

            double sumLon = 0, sumLat = 0;
            int count = ring.GetArrayLength();
            if (count == 0) return null;

            foreach (var point in ring.EnumerateArray())
            {
                sumLon += point[0].GetDouble(); // GeoJSON: [lon, lat]
                sumLat += point[1].GetDouble();
            }

            return (sumLat / count, sumLon / count);
        }
        catch
        {
            return null;
        }
    }
}

public class ZoneGeometry
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("coordinates")]
    public JsonElement Coordinates { get; set; }
}

public class ZoneProperties
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("forecastOffice")]
    public string ForecastOffice { get; set; } = string.Empty;
}

// ── Points endpoint ───────────────────────────────────────────────────────────

public class PointsResponse
{
    [JsonPropertyName("properties")]
    public PointProperties? Properties { get; set; }
}

public class PointProperties
{
    [JsonPropertyName("cwa")]
    public string Cwa { get; set; } = string.Empty;

    [JsonPropertyName("gridX")]
    public int GridX { get; set; }

    [JsonPropertyName("gridY")]
    public int GridY { get; set; }

    [JsonPropertyName("forecast")]
    public string ForecastUrl { get; set; } = string.Empty;

    [JsonPropertyName("forecastHourly")]
    public string ForecastHourlyUrl { get; set; } = string.Empty;

    [JsonPropertyName("relativeLocation")]
    public RelativeLocation? RelativeLocation { get; set; }
}

public class RelativeLocation
{
    [JsonPropertyName("properties")]
    public RelativeLocationProperties? Properties { get; set; }
}

public class RelativeLocationProperties
{
    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}

// ── Grid forecast (daily and hourly share the same structure) ─────────────────

public class GridForecastResponse
{
    [JsonPropertyName("properties")]
    public GridForecastProperties? Properties { get; set; }
}

public class GridForecastProperties
{
    // NWS uses "updated" on zone forecasts and "updateTime" on grid forecasts
    [JsonPropertyName("updated")]
    public DateTimeOffset? Updated { get; set; }

    [JsonPropertyName("updateTime")]
    public DateTimeOffset? UpdateTime { get; set; }

    public DateTimeOffset EffectiveUpdated => Updated ?? UpdateTime ?? DateTimeOffset.MinValue;

    [JsonPropertyName("units")]
    public string Units { get; set; } = string.Empty;

    [JsonPropertyName("periods")]
    public List<ForecastPeriod> Periods { get; set; } = new();
}

// ── Shared forecast period (used by zone, daily, and hourly) ──────────────────

public class ForecastPeriod
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("startTime")]
    public DateTimeOffset? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTimeOffset? EndTime { get; set; }

    [JsonPropertyName("isDaytime")]
    public bool IsDaytime { get; set; }

    [JsonPropertyName("temperature")]
    public int Temperature { get; set; }

    [JsonPropertyName("temperatureUnit")]
    public string TemperatureUnit { get; set; } = "F";

    [JsonPropertyName("temperatureTrend")]
    public string? TemperatureTrend { get; set; }

    [JsonPropertyName("windSpeed")]
    public string WindSpeed { get; set; } = string.Empty;

    [JsonPropertyName("windDirection")]
    public string WindDirection { get; set; } = string.Empty;

    [JsonPropertyName("shortForecast")]
    public string ShortForecast { get; set; } = string.Empty;

    [JsonPropertyName("detailedForecast")]
    public string DetailedForecast { get; set; } = string.Empty;

    [JsonPropertyName("probabilityOfPrecipitation")]
    public QuantitativeValue? ProbabilityOfPrecipitation { get; set; }

    [JsonPropertyName("relativeHumidity")]
    public QuantitativeValue? RelativeHumidity { get; set; }
}

public class QuantitativeValue
{
    [JsonPropertyName("unitCode")]
    public string UnitCode { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public double? Value { get; set; }
}
