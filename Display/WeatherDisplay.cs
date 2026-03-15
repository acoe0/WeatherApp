using NWSWeatherApp.Models;

namespace NWSWeatherApp.Display;

public class WeatherDisplay
{
    private const int Width = 90;
    private static readonly string HRule = new('─', Width);
    private static readonly string DRule = new('═', Width);

    // ── Public entry points ───────────────────────────────────────────────────

    public void ShowBanner()
    {
        Console.Clear();
        SetColor(ConsoleColor.Cyan);
        Console.WriteLine($"╔{DRule}╗");
        Console.WriteLine(Center("National Weather Service Forecast Viewer", Width));
        Console.WriteLine(Center("Data provided by api.weather.gov", Width));
        Console.WriteLine($"╚{DRule}╝");
        ResetColor();
    }

    public void ShowMainMenu()
    {
        Console.WriteLine();
        SetColor(ConsoleColor.Yellow);
        Console.WriteLine("  What would you like to do?");
        ResetColor();
        Console.WriteLine("  1. Browse zones by state and get a forecast");
        Console.WriteLine("  2. Enter coordinates (lat/lon) for daily + hourly forecast");
        Console.WriteLine("  3. Exit");
        Console.WriteLine();
        Console.Write("  Choice: ");
    }

    public void ShowZoneList(List<ZoneFeature> features, string state)
    {
        Console.WriteLine();
        SetColor(ConsoleColor.Cyan);
        Console.WriteLine($"  Forecast Zones in {state} ({features.Count} found)");
        Console.WriteLine($"  {HRule}");
        ResetColor();

        for (int i = 0; i < features.Count; i++)
        {
            var props = features[i].Properties;
            var id   = props?.Id   ?? "???";
            var name = props?.Name ?? "(unnamed)";
            Console.WriteLine($"  {(i + 1),4}. {id,-10} {name}");
        }
    }

    // ── Grid daily forecast ───────────────────────────────────────────────────

    public void ShowDailyForecast(GridForecastProperties props, string location)
    {
        Console.WriteLine();
        PrintBoxHeader("DAILY FORECAST", location,
            $"Updated {props.EffectiveUpdated.ToLocalTime():ddd MMM d, yyyy  h:mm tt}");

        PrintDailySummaryTable(props.Periods);
        PrintFooter();
    }

    // ── Hourly forecast ───────────────────────────────────────────────────────

    public void ShowHourlyForecast(GridForecastProperties props, string location)
    {
        // Show next 24 hours only to keep output manageable
        var periods = props.Periods.Take(24).ToList();

        Console.WriteLine();
        PrintBoxHeader("HOURLY FORECAST  ·  Next 24 Hours", location,
            $"Updated {props.EffectiveUpdated.ToLocalTime():ddd MMM d, yyyy  h:mm tt}");

        // Identify the single highest and lowest temperature periods
        var high = periods.MaxBy(p => p.Temperature)!;
        var low  = periods.MinBy(p => p.Temperature)!;

        // Summary line
        Console.Write("  24-hr range:  ▲ High ");
        PrintTemp(high.Temperature, high.TemperatureUnit);
        var highTime = high.StartTime.HasValue ? high.StartTime.Value.ToLocalTime().ToString("h tt") : "";
        Console.Write($" at {highTime}     ▼ Low ");
        PrintTemp(low.Temperature, low.TemperatureUnit);
        var lowTime = low.StartTime.HasValue ? low.StartTime.Value.ToLocalTime().ToString("h tt") : "";
        Console.WriteLine($" at {lowTime}");
        Console.WriteLine();

        // Column header
        SetColor(ConsoleColor.DarkGray);
        Console.WriteLine($"  {"TIME",-17} {"TEMP",6}  {"PRECIP",6}  {"HUMIDITY",8}  {"WIND",-14}  CONDITION");
        Console.WriteLine($"  {HRule}");
        ResetColor();

        foreach (var p in periods)
        {
            var time = p.StartTime.HasValue
                ? p.StartTime.Value.ToLocalTime().ToString("ddd h:mm tt")
                : p.Name;

            var pop = p.ProbabilityOfPrecipitation?.Value is double pv
                ? $"{(int)pv,3}%"
                : "  —";
            var humidity = p.RelativeHumidity?.Value is double hv
                ? $"{(int)hv,3}%"
                : "  —";

            var wind = Truncate($"{p.WindDirection} {p.WindSpeed}", 14);

            // Mark the high/low rows
            string marker = "  ";
            if (p == high) { SetColor(ConsoleColor.Red);    marker = "▲ "; }
            else if (p == low)  { SetColor(ConsoleColor.Cyan);   marker = "▼ "; }

            Console.Write($"  {time,-17} {marker}");
            ResetColor();
            PrintTemp(p.Temperature, p.TemperatureUnit);
            Console.Write($"  {pop,4}  {humidity,8}  {wind,-14}  {p.ShortForecast}");
            Console.WriteLine();
        }

        PrintFooter();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void PrintBoxHeader(string title, string subtitle, string updated)
    {
        SetColor(ConsoleColor.Cyan);
        Console.WriteLine($"╔{DRule}╗");
        Console.WriteLine(Center(title, Width));
        Console.WriteLine(Center(subtitle, Width));
        SetColor(ConsoleColor.DarkGray);
        Console.WriteLine(Center(updated, Width));
        SetColor(ConsoleColor.Cyan);
        Console.WriteLine($"╚{DRule}╝");
        ResetColor();
    }

    private void PrintFooter()
    {
        SetColor(ConsoleColor.DarkGray);
        Console.WriteLine($"  {HRule}");
        ResetColor();
    }

    private void PrintDailySummaryTable(List<ForecastPeriod> periods)
    {
        Console.WriteLine();
        SetColor(ConsoleColor.DarkGray);
        Console.WriteLine($"  {"DATE / PERIOD",-16} {"HIGH",6}  {"LOW",6}  {"PRECIP",5}  {"WIND",-16}  CONDITION");
        Console.WriteLine($"  {HRule}");
        ResetColor();

        int i = 0;

        // First period may be a night period (e.g., app run after midday)
        if (i < periods.Count && !periods[i].IsDaytime)
        {
            var p = periods[i++];
            Console.Write($"  {PeriodLabel(p),-16} {"—",6}  ");
            PrintTemp(p.Temperature, p.TemperatureUnit);
            Console.WriteLine($"  {PopLabel(p),5}  {Truncate($"{p.WindDirection} {p.WindSpeed}", 16),-16}  {p.ShortForecast}");
        }

        while (i < periods.Count)
        {
            var day   = periods[i];
            var night = (i + 1 < periods.Count && !periods[i + 1].IsDaytime) ? periods[i + 1] : null;

            Console.Write($"  {PeriodLabel(day),-16} ");
            PrintTemp(day.Temperature, day.TemperatureUnit);
            Console.Write("  ");
            if (night is not null)
                PrintTemp(night.Temperature, night.TemperatureUnit);
            else
                Console.Write($"{"—",6}");

            Console.WriteLine($"  {PopLabel(day),5}  {Truncate($"{day.WindDirection} {day.WindSpeed}", 16),-16}  {day.ShortForecast}");

            i += night is not null ? 2 : 1;
        }

        Console.WriteLine();
    }

    private static string PeriodLabel(ForecastPeriod p) =>
        p.StartTime.HasValue
            ? p.StartTime.Value.ToLocalTime().ToString("ddd MMM d")
            : p.Name;

    private static string PopLabel(ForecastPeriod p) =>
        p.ProbabilityOfPrecipitation?.Value is double v && v > 0 ? $"{(int)v}%" : "—";

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";

    private static void PrintTemp(int temp, string unit)
    {
        var color = temp switch
        {
            <= 32 => ConsoleColor.Cyan,
            <= 45 => ConsoleColor.Blue,
            <= 65 => ConsoleColor.White,
            <= 80 => ConsoleColor.Yellow,
            _     => ConsoleColor.Red
        };
        SetColor(color);
        Console.Write($"{temp,4}°{unit}");
        ResetColor();
    }

    private static IEnumerable<string> WordWrap(string text, int maxWidth)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new System.Text.StringBuilder();

        foreach (var word in words)
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > maxWidth)
            {
                yield return line.ToString();
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }

        if (line.Length > 0)
            yield return line.ToString();
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width) return text;
        int pad = (width - text.Length) / 2;
        return $"║{new string(' ', pad)}{text}{new string(' ', width - pad - text.Length)}║";
    }

    private static void SetColor(ConsoleColor color) => Console.ForegroundColor = color;
    private static void ResetColor() => Console.ResetColor();
}
