using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Geohash
{
    /// <summary>
    /// Generates geohashes covering a circle (point + radius) on a spherical Earth model.
    /// Handles antimeridian wrapping and circles covering the poles.
    /// All members are thread-safe and the class is stateless.
    /// </summary>
    public class RadiusHasher
    {
        /// <summary>Mean Earth radius (IUGG) in meters.</summary>
        public const double EarthRadiusMeters = 6_371_008.8;

        private const double DegToRad = Math.PI / 180.0;
        private const double MetersPerDegree = EarthRadiusMeters * DegToRad; // ≈ 111,195 m

        private static readonly Geohasher Hasher = new Geohasher();

        /// <summary>
        /// Returns all geohashes of the given precision matching the circle, with the
        /// precision chosen automatically so the circle is covered by a reasonable
        /// number of cells (cell size ≈ radius / 2 or finer).
        /// </summary>
        public HashSet<string> GetHashes(
            double latitude,
            double longitude,
            double radiusMeters,
            GeohashInclusionCriteria criteria = GeohashInclusionCriteria.Intersects,
            CancellationToken cancellationToken = default)
        {
            int precision = GetPrecisionForRadius(radiusMeters, latitude);
            return GetHashes(latitude, longitude, radiusMeters, precision, criteria,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Returns all geohashes of the given precision matching the circle.
        /// </summary>
        /// <param name="latitude">Circle center latitude [-90, 90].</param>
        /// <param name="longitude">Circle center longitude; wrapped into [-180, 180).</param>
        /// <param name="radiusMeters">Circle radius in meters (≥ 0).</param>
        /// <param name="geohashPrecision">1–12. Higher precision = exponentially more cells.</param>
        /// <param name="criteria">
        /// <see cref="GeohashInclusionCriteria.Intersects"/>: cell touches the circle (default).
        /// <see cref="GeohashInclusionCriteria.Contains"/>: cell lies entirely inside the circle.
        /// </param>
        /// <param name="maxCandidateCells">
        /// Safety limit on the number of grid cells examined. Guards against accidental
        /// "precision 12 + 500 km radius" requests that would enumerate billions of cells.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public HashSet<string> GetHashes(
            double latitude,
            double longitude,
            double radiusMeters,
            int geohashPrecision,
            GeohashInclusionCriteria criteria = GeohashInclusionCriteria.Intersects,
            long maxCandidateCells = 10_000_000,
            CancellationToken cancellationToken = default)
        {
            // --- Validation ---
            if (double.IsNaN(latitude) || double.IsNaN(longitude) || double.IsNaN(radiusMeters))
                throw new ArgumentException("Inputs must not be NaN.");
            if (latitude < -90.0 || latitude > 90.0)
                throw new ArgumentOutOfRangeException(nameof(latitude), latitude,
                    "Latitude must be between -90 and 90.");
            if (radiusMeters < 0 || double.IsInfinity(radiusMeters))
                throw new ArgumentOutOfRangeException(nameof(radiusMeters), radiusMeters,
                    "Radius must be a finite, non-negative number of meters.");
            if (geohashPrecision < 1 || geohashPrecision > Geohasher.MaxPrecision)
                throw new ArgumentOutOfRangeException(nameof(geohashPrecision), geohashPrecision,
                    $"Precision must be between 1 and {Geohasher.MaxPrecision}.");

            longitude = NormalizeLongitude(longitude);

            // --- Geographic bounding box of the circle ---
            double angularRadius = radiusMeters / EarthRadiusMeters; // radians
            double radiusDeg = angularRadius / DegToRad;

            double latMin = latitude - radiusDeg;
            double latMax = latitude + radiusDeg;

            // If the circle reaches a pole, it spans all longitudes.
            bool fullLngRange = latMax >= 90.0 || latMin <= -90.0;

            double lngMin = -180.0, lngMax = 180.0;
            if (!fullLngRange)
            {
                // Longitude half-width of a circle's bounding box: Δλ = asin(sin δ / cos φ)
                double ratio = Math.Sin(angularRadius) / Math.Cos(latitude * DegToRad);
                if (ratio >= 1.0)
                {
                    fullLngRange = true;
                }
                else
                {
                    double deltaLngDeg = Math.Asin(ratio) / DegToRad;
                    // Deliberately unnormalized (may exceed ±180): the grid index loop
                    // handles antimeridian wrapping; Encode normalizes at the end.
                    lngMin = longitude - deltaLngDeg;
                    lngMax = longitude + deltaLngDeg;
                }
            }

            latMin = Math.Max(latMin, -90.0);
            latMax = Math.Min(latMax, 90.0);

            // --- Grid setup (same scheme as PolygonHasher: cell i spans [i·step, (i+1)·step)) ---
            int totalBits = 5 * geohashPrecision;
            double latStep = 180.0 / (1L << (totalBits / 2));
            double lngStep = 360.0 / (1L << ((totalBits + 1) / 2));

            int latStart = (int)Math.Floor(latMin / latStep);
            int latEnd = (int)Math.Ceiling(latMax / latStep);
            int lngStart = (int)Math.Floor(lngMin / lngStep);
            int lngEnd = (int)Math.Ceiling(lngMax / lngStep);

            long candidates = (long)(latEnd - latStart) * (lngEnd - lngStart);
            if (candidates > maxCandidateCells)
                throw new ArgumentException(
                    $"Search would examine {candidates:N0} cells (limit {maxCandidateCells:N0}). " +
                    $"Reduce precision (currently {geohashPrecision}) or radius, " +
                    $"or raise {nameof(maxCandidateCells)}.");

            // --- Hot-loop precomputation ---
            // d <= r  <=>  haversineTerm <= sin²(r / 2R). Avoids asin/sqrt per cell.
            double s = Math.Sin(Math.Min(angularRadius, Math.PI) * 0.5);
            double threshold = s * s;

            double centerLatRad = latitude * DegToRad;
            double cosCenterLat = Math.Cos(centerLatRad);
            bool requireContains = criteria == GeohashInclusionCriteria.Contains;

            var results = new HashSet<string>(StringComparer.Ordinal);

            for (int latIdx = latStart; latIdx < latEnd; latIdx++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double cellMinLat = Math.Max(latIdx * latStep, -90.0);
                double cellMaxLat = Math.Min(cellMinLat + latStep, 90.0);

                for (int lngIdx = lngStart; lngIdx < lngEnd; lngIdx++)
                {
                    double cellMinLng = lngIdx * lngStep;
                    double cellMaxLng = cellMinLng + lngStep;

                    // Shift the query longitude into the cell's (possibly unnormalized)
                    // frame so plain clamping works across the antimeridian.
                    double qLng = longitude;
                    if (qLng < cellMinLng - 180.0) qLng += 360.0;
                    else if (qLng > cellMaxLng + 180.0) qLng -= 360.0;

                    bool include;
                    if (requireContains)
                    {
                        // Entire cell inside circle <=> farthest point inside circle.
                        // The farthest boundary point is always a corner...
                        include =
                            HaversineTerm(centerLatRad, cosCenterLat, cellMinLat, qLng - cellMinLng) <= threshold &&
                            HaversineTerm(centerLatRad, cosCenterLat, cellMinLat, qLng - cellMaxLng) <= threshold &&
                            HaversineTerm(centerLatRad, cosCenterLat, cellMaxLat, qLng - cellMinLng) <= threshold &&
                            HaversineTerm(centerLatRad, cosCenterLat, cellMaxLat, qLng - cellMaxLng) <= threshold;

                        // ...unless the cell contains the center's antipode (distance πR),
                        // which can exceed all four corner distances.
                        if (include && threshold < 1.0)
                        {
                            double aLng = qLng + 180.0;
                            if (aLng > cellMaxLng + 180.0) aLng -= 360.0;
                            if (-latitude >= cellMinLat && -latitude <= cellMaxLat &&
                                aLng >= cellMinLng && aLng <= cellMaxLng)
                            {
                                include = false;
                            }
                        }
                    }
                    else
                    {
                        // Cell touches circle <=> nearest point of cell inside circle.
                        include = NearestHaversineTerm(centerLatRad, cosCenterLat, latitude, qLng,
                            cellMinLat, cellMaxLat, cellMinLng, cellMaxLng) <= threshold;
                    }

                    if (include)
                    {
                        // Encode normalizes longitude back into [-180, 180).
                        results.Add(Hasher.Encode(
                            cellMinLat + latStep * 0.5,
                            cellMinLng + lngStep * 0.5,
                            geohashPrecision));
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Smallest haversine term between the query point and any point of the cell.
        /// The independently-clamped point is exact for the cell interior and its
        /// constant-latitude edges. For the meridian edges the great-circle optimum
        /// satisfies tan φ* = tan φ1 / cos Δλ, which moves poleward as |Δλ| grows —
        /// plain latitude clamping misses this (severely so near the poles, where the
        /// shortest path cuts across the pole instead of along the parallel).
        /// </summary>
        private static double NearestHaversineTerm(
            double centerLatRad, double cosCenterLat,
            double centerLatDeg, double qLngDeg,
            double cellMinLat, double cellMaxLat,
            double cellMinLng, double cellMaxLng)
        {
            // Candidate 1: clamped point (also handles "query inside cell" => 0).
            double cLat = Clamp(centerLatDeg, cellMinLat, cellMaxLat);
            double cLng = Clamp(qLngDeg, cellMinLng, cellMaxLng);
            double best = HaversineTerm(centerLatRad, cosCenterLat, cLat, qLngDeg - cLng);

            // Candidates 2 & 3: analytic optimum on each meridian edge.
            double sinCenterLat = Math.Sin(centerLatRad);

            best = Math.Min(best, MeridianEdgeTerm(cellMinLng));
            best = Math.Min(best, MeridianEdgeTerm(cellMaxLng));
            return best;

            double MeridianEdgeTerm(double edgeLng)
            {
                double dLngRad = (qLngDeg - edgeLng) * DegToRad;
                double optimalLatDeg = Math.Atan2(sinCenterLat, cosCenterLat * Math.Cos(dLngRad)) / DegToRad;
                double lat = Clamp(optimalLatDeg, cellMinLat, cellMaxLat);
                return HaversineTerm(centerLatRad, cosCenterLat, lat, qLngDeg - edgeLng);
            }
        }
        /// <summary>
        /// Returns the smallest precision whose cell size (at the given latitude) is at most
        /// half the radius, so a circle is covered by roughly 15–60 cells.
        /// </summary>
        public static int GetPrecisionForRadius(double radiusMeters, double latitude = 0)
        {
            if (radiusMeters < 0 || double.IsNaN(radiusMeters))
                throw new ArgumentOutOfRangeException(nameof(radiusMeters));

            for (int p = 1; p <= Geohasher.MaxPrecision; p++)
            {
                var (width, height) = GetCellSizeMeters(p, latitude);
                if (Math.Max(width, height) <= radiusMeters * 0.5)
                    return p;
            }
            return Geohasher.MaxPrecision;
        }

        /// <summary>
        /// Approximate physical size of a geohash cell at a given latitude.
        /// Width shrinks toward the poles by cos(latitude); height is constant.
        /// </summary>
        public static (double widthMeters, double heightMeters) GetCellSizeMeters(
            int precision, double latitude = 0)
        {
            if (precision < 1 || precision > Geohasher.MaxPrecision)
                throw new ArgumentOutOfRangeException(nameof(precision));

            int totalBits = 5 * precision;
            double latStep = 180.0 / (1L << (totalBits / 2));
            double lngStep = 360.0 / (1L << ((totalBits + 1) / 2));

            double height = latStep * MetersPerDegree;
            double width = lngStep * MetersPerDegree * Math.Abs(Math.Cos(latitude * DegToRad));
            return (width, height);
        }

        /// <summary>Great-circle (haversine) distance between two points in meters.</summary>
        public static double GetDistanceMeters(double lat1, double lng1, double lat2, double lng2)
        {
            double lat1Rad = lat1 * DegToRad;
            double a = HaversineTerm(lat1Rad, Math.Cos(lat1Rad), lat2, lng1 - lng2);
            return 2.0 * EarthRadiusMeters * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
        }

        /// <summary>Great-circle distance between the centers of two geohash cells in meters.</summary>
        public static double GetDistanceMeters(string geohashA, string geohashB)
        {
            var (latA, lngA) = Hasher.Decode(geohashA);
            var (latB, lngB) = Hasher.Decode(geohashB);
            return GetDistanceMeters(latA, lngA, latB, lngB);
        }

        // --- Internals ---

        /// <summary>
        /// Inner haversine term: sin²(Δφ/2) + cosφ1·cosφ2·sin²(Δλ/2).
        /// Monotonic in distance, so it can be compared against a precomputed
        /// threshold without asin/sqrt. Periodic in Δλ, so unnormalized
        /// longitude differences are safe.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double HaversineTerm(double lat1Rad, double cosLat1, double lat2Deg, double dLngDeg)
        {
            double lat2Rad = lat2Deg * DegToRad;
            double sinLat = Math.Sin((lat2Rad - lat1Rad) * 0.5);
            double sinLng = Math.Sin(dLngDeg * DegToRad * 0.5);
            return sinLat * sinLat + cosLat1 * Math.Cos(lat2Rad) * sinLng * sinLng;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Clamp(double value, double min, double max) =>
            value < min ? min : (value > max ? max : value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double NormalizeLongitude(double lng)
        {
            double result = (lng + 180.0) % 360.0;
            if (result < 0) result += 360.0;
            return result - 180.0;
        }
    }
}