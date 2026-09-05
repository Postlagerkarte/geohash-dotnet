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



        public HashSet<string> GetHashes(
    double latitude,
    double longitude,
    double radiusMeters,
    int geohashPrecision,
    GeohashInclusionCriteria criteria = GeohashInclusionCriteria.Intersects,
    long maxCandidateCells = 10_000_000,
    CancellationToken cancellationToken = default)
        {
            ValidateLatitude(latitude);
            ValidateRadius(radiusMeters);

            if (!GeographicMath.IsFinite(longitude))
                throw new ArgumentOutOfRangeException(
                    nameof(longitude), longitude, "Longitude must be finite.");

            if (geohashPrecision < 1 ||
                geohashPrecision > Geohasher.MaxPrecision)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(geohashPrecision), geohashPrecision,
                    $"Precision must be between 1 and {Geohasher.MaxPrecision}.");
            }

            if (criteria != GeohashInclusionCriteria.Contains &&
                criteria != GeohashInclusionCriteria.Intersects)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(criteria), criteria, "Unknown inclusion criterion.");
            }

            if (maxCandidateCells <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(maxCandidateCells), maxCandidateCells,
                    "The candidate-cell limit must be positive.");

            cancellationToken.ThrowIfCancellationRequested();

            longitude = GeographicMath.NormalizeLongitude(longitude);

            var results = new HashSet<string>(StringComparer.Ordinal);
            bool requireContains = criteria == GeohashInclusionCriteria.Contains;

            // Every supported geohash cell has nonzero area.
            if (requireContains && radiusMeters == 0.0)
                return results;

            double angularRadius = radiusMeters / EarthRadiusMeters;
            double radiusDeg = angularRadius / DegToRad;

            double latMin = latitude - radiusDeg;
            double latMax = latitude + radiusDeg;

            bool fullLngRange = latMax >= 90.0 || latMin <= -90.0;

            double lngMin = -180.0;
            double lngMax = 180.0;

            if (!fullLngRange)
            {
                double ratio =
                    Math.Sin(angularRadius) / CosLatitude(latitude);

                if (ratio >= 1.0)
                {
                    fullLngRange = true;
                }
                else
                {
                    double deltaLngDeg =
                        Math.Asin(Clamp(ratio, -1.0, 1.0)) / DegToRad;

                    // Leave these unnormalized: the grid may cross the date line.
                    lngMin = longitude - deltaLngDeg;
                    lngMax = longitude + deltaLngDeg;
                }
            }

            latMin = Math.Max(latMin, -90.0);
            latMax = Math.Min(latMax, 90.0);

            int totalBits = 5 * geohashPrecision;
            int latBits = totalBits / 2;
            int lngBits = (totalBits + 1) / 2;

            int latCellCount = 1 << latBits;
            int lngCellCount = 1 << lngBits;

            double latStep = 180.0 / latCellCount;
            double lngStep = 360.0 / lngCellCount;

            int worldLatStart = -(latCellCount / 2);
            int worldLatEnd = latCellCount / 2;
            int worldLngStart = -(lngCellCount / 2);
            int worldLngEnd = lngCellCount / 2;

            // Intersects includes touching. Pad the bounding grid so exact
            // boundaries and zero-radius points do not disappear.
            // The distance predicate rejects unnecessary candidates.
            int padding = requireContains ? 0 : 1;

            int latStart = Math.Max(
                worldLatStart,
                (int)Math.Floor(latMin / latStep) - padding);

            int latEnd = Math.Min(
                worldLatEnd,
                (int)Math.Ceiling(latMax / latStep) + padding);

            int lngStart;
            int lngEnd;

            if (fullLngRange)
            {
                // Enumerate one complete revolution, without duplicate padding.
                lngStart = worldLngStart;
                lngEnd = worldLngEnd;
            }
            else
            {
                lngStart = (int)Math.Floor(lngMin / lngStep) - padding;
                lngEnd = (int)Math.Ceiling(lngMax / lngStep) + padding;
            }

            long candidates =
                ((long)latEnd - latStart) * ((long)lngEnd - lngStart);

            if (candidates > maxCandidateCells)
            {
                throw new ArgumentException(
                    $"Search would examine {candidates:N0} cells " +
                    $"(limit {maxCandidateCells:N0}). " +
                    $"Reduce precision (currently {geohashPrecision}) or radius, " +
                    $"or raise {nameof(maxCandidateCells)}.",
                    nameof(maxCandidateCells));
            }

            double s = Math.Sin(Math.Min(angularRadius, Math.PI) * 0.5);
            double threshold = s * s;

            double centerLatRad = latitude * DegToRad;
            double cosCenterLat = CosLatitude(latitude);

            for (int latIdx = latStart; latIdx < latEnd; latIdx++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double cellMinLat = latIdx * latStep;
                double cellMaxLat = cellMinLat + latStep;

                for (int lngIdx = lngStart; lngIdx < lngEnd; lngIdx++)
                {
                    // A single row can contain many millions of candidates.
                    if ((lngIdx & 1023) == 0)
                        cancellationToken.ThrowIfCancellationRequested();

                    double cellMinLng = lngIdx * lngStep;
                    double cellMaxLng = cellMinLng + lngStep;

                    double qLng = ShiftLongitudeToCell(
                        longitude, cellMinLng, cellMaxLng);

                    double term = requireContains
                        ? FarthestHaversineTerm(
                            centerLatRad, cosCenterLat, latitude, qLng,
                            cellMinLat, cellMaxLat, cellMinLng, cellMaxLng)
                        : NearestHaversineTerm(
                            centerLatRad, cosCenterLat, latitude, qLng,
                            cellMinLat, cellMaxLat, cellMinLng, cellMaxLng);

                    if (term <= threshold)
                    {
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
      double centerLatRad,
      double cosCenterLat,
      double centerLatDeg,
      double qLngDeg,
      double cellMinLat,
      double cellMaxLat,
      double cellMinLng,
      double cellMaxLng)
        {
            // Handles the cell interior and nearest points on latitude edges.
            double cLat = Clamp(centerLatDeg, cellMinLat, cellMaxLat);
            double cLng = Clamp(qLngDeg, cellMinLng, cellMaxLng);

            double best = HaversineTerm(
                centerLatRad, cosCenterLat, cLat, qLngDeg - cLng);

            double sinCenterLat = Math.Sin(centerLatRad);

            best = Math.Min(best, MeridianHaversineExtreme(
                centerLatRad, sinCenterLat, cosCenterLat,
                qLngDeg, cellMinLng, cellMinLat, cellMaxLat,
                maximum: false));

            best = Math.Min(best, MeridianHaversineExtreme(
                centerLatRad, sinCenterLat, cosCenterLat,
                qLngDeg, cellMaxLng, cellMinLat, cellMaxLat,
                maximum: false));

            return best;
        }

        private static double FarthestHaversineTerm(
            double centerLatRad,
            double cosCenterLat,
            double centerLatDeg,
            double qLngDeg,
            double cellMinLat,
            double cellMaxLat,
            double cellMinLng,
            double cellMaxLng)
        {
            double sinCenterLat = Math.Sin(centerLatRad);

            double best = Math.Max(
                MeridianHaversineExtreme(
                    centerLatRad, sinCenterLat, cosCenterLat,
                    qLngDeg, cellMinLng, cellMinLat, cellMaxLat,
                    maximum: true),
                MeridianHaversineExtreme(
                    centerLatRad, sinCenterLat, cosCenterLat,
                    qLngDeg, cellMaxLng, cellMinLat, cellMaxLat,
                    maximum: true));

            // A latitude-edge maximum may occur at the antipodal longitude,
            // rather than at either corner.
            double antipodeLng = ShiftLongitudeToCell(
                qLngDeg + 180.0, cellMinLng, cellMaxLng);

            if (antipodeLng >= cellMinLng && antipodeLng <= cellMaxLng)
            {
                // If the complete antipode lies in the cell, the maximum
                // distance is exactly pi * EarthRadiusMeters.
                if (-centerLatDeg >= cellMinLat &&
                    -centerLatDeg <= cellMaxLat)
                {
                    return 1.0;
                }

                best = Math.Max(best, MeridianHaversineExtreme(
                    centerLatRad, sinCenterLat, cosCenterLat,
                    qLngDeg, antipodeLng, cellMinLat, cellMaxLat,
                    maximum: true));
            }

            return best;
        }

        private static double MeridianHaversineExtreme(
            double centerLatRad,
            double sinCenterLat,
            double cosCenterLat,
            double qLngDeg,
            double edgeLngDeg,
            double cellMinLat,
            double cellMaxLat,
            bool maximum)
        {
            double deltaLngDeg = qLngDeg - edgeLngDeg;

            double atMinLat = HaversineTerm(
                centerLatRad, cosCenterLat, cellMinLat, deltaLngDeg);

            double atMaxLat = HaversineTerm(
                centerLatRad, cosCenterLat, cellMaxLat, deltaLngDeg);

            // Endpoints are mandatory: clamping a stationary point does not
            // necessarily select the correct endpoint on the far hemisphere.
            double best = maximum
                ? Math.Max(atMinLat, atMaxLat)
                : Math.Min(atMinLat, atMaxLat);

            // The spherical dot product along this meridian is:
            // A * sin(latitude) + B * cos(latitude).
            double a = sinCenterLat;
            double b = cosCenterLat * Math.Cos(deltaLngDeg * DegToRad);

            // Minimum distance maximizes the dot product.
            // Maximum distance minimizes it.
            double stationaryLatDeg = maximum
                ? Math.Atan2(-a, -b) / DegToRad
                : Math.Atan2(a, b) / DegToRad;

            if (stationaryLatDeg >= cellMinLat &&
                stationaryLatDeg <= cellMaxLat)
            {
                double stationaryTerm = HaversineTerm(
                    centerLatRad, cosCenterLat,
                    stationaryLatDeg, deltaLngDeg);

                best = maximum
                    ? Math.Max(best, stationaryTerm)
                    : Math.Min(best, stationaryTerm);
            }

            return best;
        }

        private static double ShiftLongitudeToCell(
            double longitude,
            double cellMinLng,
            double cellMaxLng)
        {
            double cellCenterLng =
                cellMinLng + (cellMaxLng - cellMinLng) * 0.5;

            double revolutions =
                Math.Round((cellCenterLng - longitude) / 360.0);

            return longitude + revolutions * 360.0;
        }
        /// <summary>
        /// Returns the smallest precision whose cell size (at the given latitude) is at most
        /// half the radius, so a circle is covered by roughly 15–60 cells.
        /// </summary>
        public static int GetPrecisionForRadius(double radiusMeters, double latitude = 0)
        {
            ValidateRadius(radiusMeters);
            ValidateLatitude(latitude);

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
            ValidateLatitude(latitude);

            int totalBits = 5 * precision;
            double latStep = 180.0 / (1L << (totalBits / 2));
            double lngStep = 360.0 / (1L << ((totalBits + 1) / 2));

            double height = latStep * MetersPerDegree;
            double width = lngStep * MetersPerDegree * CosLatitude(latitude);
            return (width, height);
        }

        /// <summary>
        /// Selects precision from the radius and latitude, then generates coverage.
        /// The candidate-cell safety limit still applies.
        /// </summary>
        public HashSet<string> GetHashes(
            double latitude,
            double longitude,
            double radiusMeters,
            GeohashInclusionCriteria criteria = GeohashInclusionCriteria.Intersects,
            CancellationToken cancellationToken = default)
        {
            int precision = GetPrecisionForRadius(radiusMeters, latitude);

            return GetHashes(
                latitude: latitude,
                longitude: longitude,
                radiusMeters: radiusMeters,
                geohashPrecision: precision,
                criteria: criteria,
                cancellationToken: cancellationToken);
        }

        /// <summary>Great-circle (haversine) distance between two points in meters.</summary>
        public static double GetDistanceMeters(
            double lat1,
            double lng1,
            double lat2,
            double lng2)
        {
            if (!GeographicMath.IsFinite(lat1) || lat1 < -90.0 || lat1 > 90.0)
                throw new ArgumentOutOfRangeException(
                    nameof(lat1), lat1, "Latitude must be finite and between -90 and 90.");

            if (!GeographicMath.IsFinite(lat2) || lat2 < -90.0 || lat2 > 90.0)
                throw new ArgumentOutOfRangeException(
                    nameof(lat2), lat2, "Latitude must be finite and between -90 and 90.");

            if (!GeographicMath.IsFinite(lng1))
                throw new ArgumentOutOfRangeException(
                    nameof(lng1), lng1, "Longitude must be finite.");

            if (!GeographicMath.IsFinite(lng2))
                throw new ArgumentOutOfRangeException(
                    nameof(lng2), lng2, "Longitude must be finite.");

            // Normalize separately before subtracting to avoid overflow for
            // large finite longitudes of opposite signs.
            double normalizedLng1 = GeographicMath.NormalizeLongitude(lng1);
            double normalizedLng2 = GeographicMath.NormalizeLongitude(lng2);

            double deltaLng = GeographicMath.NormalizeLongitude(
                normalizedLng1 - normalizedLng2);

            double a = HaversineTerm(
                lat1 * DegToRad,
                CosLatitude(lat1),
                lat2,
                deltaLng);

            return 2.0 * EarthRadiusMeters * Math.Asin(Math.Sqrt(a));
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
        private static double HaversineTerm(
            double lat1Rad,
            double cosLat1,
            double lat2Deg,
            double dLngDeg)
        {
            double lat2Rad = lat2Deg * DegToRad;

            double sinLat = Math.Sin((lat2Rad - lat1Rad) * 0.5);
            double sinLng = Math.Sin(dLngDeg * DegToRad * 0.5);

            double term =
                sinLat * sinLat +
                cosLat1 * CosLatitude(lat2Deg) * sinLng * sinLng;

            return Clamp(term, 0.0, 1.0);
        }

        private static void ValidateLatitude(double latitude)
        {
            if (!GeographicMath.IsFinite(latitude) ||
                latitude < -90.0 ||
                latitude > 90.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(latitude), latitude,
                    "Latitude must be finite and between -90 and 90.");
            }
        }

        private static void ValidateRadius(double radiusMeters)
        {
            if (!GeographicMath.IsFinite(radiusMeters) || radiusMeters < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(radiusMeters), radiusMeters,
                    "Radius must be finite and non-negative.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double CosLatitude(double latitude)
        {
            // Math.Cos(pi / 2) is not exactly zero in floating-point arithmetic.
            if (latitude == -90.0 || latitude == 90.0)
                return 0.0;

            return Math.Cos(latitude * DegToRad);
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