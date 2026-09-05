using System;

namespace Geohash
{
    internal static class GeographicMath
    {
        internal static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        // Caller must validate that longitude is finite.
        internal static double NormalizeLongitude(double longitude)
        {
            // Preserve already-normalized coordinates exactly. In particular,
            // do not add 180 and round a point across a cell boundary.
            if (longitude >= -180.0 && longitude < 180.0)
                return longitude;

            double result = longitude % 360.0;

            if (result >= 180.0)
                result -= 360.0;
            else if (result < -180.0)
                result += 360.0;

            return result;
        }
    }
}