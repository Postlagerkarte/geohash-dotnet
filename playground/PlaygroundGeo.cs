using System.Globalization;

namespace Geohash.Playground;

/// <summary>
/// Demo-side helpers: viewport grid enumeration, preset shapes and formatting.
/// All actual geohash math is the library's job.
/// </summary>
public static class PlaygroundGeo
{
    private static readonly Geohasher Hasher = new();

    public record GridCell(string Hash, double S, double W, double N, double E);

    /// <summary>
    /// Picks the finest precision whose grid covers the viewport with at most
    /// <paramref name="maxCells"/> cells.
    /// </summary>
    public static int PickGridPrecision(double s, double w, double n, double e, int maxCells)
    {
        int best = 1;
        for (int p = 1; p <= Geohasher.MaxPrecision; p++)
        {
            if (CountCells(s, w, n, e, p) > maxCells) break;
            best = p;
        }
        return best;
    }

    private static long CountCells(double s, double w, double n, double e, int precision)
    {
        var (latStep, lngStep) = Steps(precision);
        double lngSpan = Math.Min(e - w, 360.0);
        long latCount = (long)Math.Ceiling((n - s) / latStep) + 1;
        long lngCount = (long)Math.Ceiling(lngSpan / lngStep) + 1;
        return latCount * lngCount;
    }

    /// <summary>
    /// Enumerates the geohash grid covering a viewport. Longitudes are kept
    /// unnormalized so cells land on the world copy the user is looking at;
    /// <see cref="Geohasher.Encode"/> normalizes internally for the hash itself.
    /// </summary>
    public static List<GridCell> EnumerateGrid(double s, double w, double n, double e, int precision, int maxCells)
    {
        var (latStep, lngStep) = Steps(precision);
        var result = new List<GridCell>();

        if (e - w >= 360.0) { w = -180.0; e = 180.0; }

        int latStart = (int)Math.Floor(Math.Max(s, -90.0) / latStep);
        int latEnd = (int)Math.Ceiling(Math.Min(n, 90.0) / latStep);
        int lngStart = (int)Math.Floor(w / lngStep);
        int lngEnd = (int)Math.Ceiling(e / lngStep);

        for (int li = latStart; li < latEnd; li++)
        {
            double cs = li * latStep;
            double cn = cs + latStep;
            if (cn <= -90.0 || cs >= 90.0) continue;

            for (int gi = lngStart; gi < lngEnd; gi++)
            {
                if (result.Count >= maxCells) return result;
                double cw = gi * lngStep;
                string hash = Hasher.Encode(cs + latStep * 0.5, cw + lngStep * 0.5, precision);
                result.Add(new GridCell(hash, cs, cw, cn, cw + lngStep));
            }
        }

        return result;
    }

    private static (double latStep, double lngStep) Steps(int precision)
    {
        int totalBits = 5 * precision;
        return (180.0 / (1L << (totalBits / 2)), 360.0 / (1L << ((totalBits + 1) / 2)));
    }

    // ── Preset shapes ───────────────────────────────────────────

    /// <summary>Parametric heart centered on a point, ~<paramref name="heightMeters"/> tall.</summary>
    public static List<(double Lat, double Lng)> Heart(double centerLat, double centerLng, double heightMeters)
    {
        const int steps = 72;
        var pts = new List<(double, double)>(steps + 1);

        double latRadius = heightMeters / 2.0 / 111_195.0;
        double lngRadius = latRadius / Math.Cos(centerLat * Math.PI / 180.0);

        for (int i = 0; i < steps; i++)
        {
            double t = 2.0 * Math.PI * i / steps;
            double x = 16.0 * Math.Pow(Math.Sin(t), 3);
            double y = 13.0 * Math.Cos(t) - 5.0 * Math.Cos(2 * t) - 2.0 * Math.Cos(3 * t) - Math.Cos(4 * t);
            pts.Add((centerLat + y / 17.0 * latRadius, centerLng + x / 17.0 * lngRadius));
        }

        pts.Add(pts[0]);
        return pts;
    }

    /// <summary>
    /// A loop around the Fiji archipelago. Longitudes intentionally run past 180°
    /// — the library splits antimeridian-crossing polygons automatically.
    /// </summary>
    public static List<(double Lat, double Lng)> Fiji() => new()
    {
        (-15.4, 176.6), (-15.2, 178.5), (-15.6, 180.6), (-16.5, 181.9),
        (-18.2, 182.1), (-19.7, 181.3), (-20.3, 179.5), (-19.9, 177.4),
        (-18.5, 176.2), (-16.8, 176.0), (-15.4, 176.6),
    };

    // ── Formatting (invariant culture; the app ships with InvariantGlobalization) ──

    public static string Meters(double m) => m switch
    {
        >= 1_000_000 => (m / 1000).ToString("N0", CultureInfo.InvariantCulture) + " km",
        >= 10_000 => (m / 1000).ToString("0.#", CultureInfo.InvariantCulture) + " km",
        >= 1_000 => (m / 1000).ToString("0.##", CultureInfo.InvariantCulture) + " km",
        >= 100 => m.ToString("N0", CultureInfo.InvariantCulture) + " m",
        >= 1 => m.ToString("0.#", CultureInfo.InvariantCulture) + " m",
        _ => (m * 100).ToString("0.#", CultureInfo.InvariantCulture) + " cm",
    };

    public static string CellSize(int precision, double latitude)
    {
        var (w, h) = RadiusHasher.GetCellSizeMeters(precision, latitude);
        return $"{Meters(w)} × {Meters(h)}";
    }

    public static string Duration(double ms) => ms switch
    {
        < 0.0005 => "<1 µs",
        < 1 => (ms * 1000).ToString("N0", CultureInfo.InvariantCulture) + " µs",
        < 100 => ms.ToString("0.#", CultureInfo.InvariantCulture) + " ms",
        < 10_000 => (ms / 1000).ToString("0.##", CultureInfo.InvariantCulture) + " s",
        _ => (ms / 1000).ToString("N0", CultureInfo.InvariantCulture) + " s",
    };

    public static string Num(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

    public static string Inv(double v, string fmt) => v.ToString(fmt, CultureInfo.InvariantCulture);

    /// <summary>Linear blend of the playground accent ramp, coarse → fine.</summary>
    public static string LevelColor(double t)
    {
        (int r, int g, int b) a = (0x22, 0xd3, 0xee), b2 = (0xa7, 0x8b, 0xfa), c = (0xf4, 0x72, 0xb6);
        (int r, int g, int b) lo, hi;
        double f;
        if (t <= 0.5) { lo = a; hi = b2; f = t * 2; }
        else { lo = b2; hi = c; f = (t - 0.5) * 2; }
        int rr = (int)(lo.r + (hi.r - lo.r) * f);
        int gg = (int)(lo.g + (hi.g - lo.g) * f);
        int bb = (int)(lo.b + (hi.b - lo.b) * f);
        return $"#{rr:x2}{gg:x2}{bb:x2}";
    }
}
