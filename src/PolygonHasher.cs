using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Polygonize;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Geohash
{
    /// <summary>
    /// Generates geohashes covering a polygon. Handles antimeridian-crossing polygons
    /// by splitting them into valid longitude ranges before processing.
    /// </summary>
    public class PolygonHasher
    {
        private const int MinGeohashPrecision = 1;
        private const int MaxGeohashPrecision = 12;
        private static readonly Geohasher _geohasher = new Geohasher();

        public enum GeohashInclusionCriteria
        {
            /// <summary>
            /// Geohash cell must be entirely within the polygon.
            /// </summary>
            Contains,

            /// <summary>
            /// Geohash cell may partially overlap the polygon boundary.
            /// </summary>
            Intersects
        }

        /// <summary>
        /// Grid information for processing a polygon.
        /// </summary>
        private readonly struct PolygonGridInfo
        {
            public Polygon Polygon { get; init; }
            public double LatStep { get; init; }
            public double LngStep { get; init; }
            public int StartLatIdx { get; init; }
            public int EndLatIdx { get; init; }
            public int StartLngIdx { get; init; }
            public int EndLngIdx { get; init; }
        }

        /// <summary>
        /// Generates geohashes covering the specified polygon.
        /// </summary>
        /// <param name="polygon">Must be valid; antimeridian-crossing polygons are automatically split.</param>
        /// <param name="geohashPrecision">1-12; higher precision = smaller cells = exponentially more results.</param>
        /// <param name="geohashInclusionCriteria">Whether cells must be fully contained or just intersecting.</param>
        /// <param name="progress">Reports 0.0 to 1.0; updates throttled to 1% increments.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Set of geohash strings covering the polygon.</returns>
        public HashSet<string> GetHashes(
            Polygon polygon,
            int geohashPrecision,
            GeohashInclusionCriteria geohashInclusionCriteria = GeohashInclusionCriteria.Contains,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            // Validate inputs
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            if (geohashPrecision < MinGeohashPrecision || geohashPrecision > MaxGeohashPrecision)
                throw new ArgumentOutOfRangeException(nameof(geohashPrecision),
                    $"Precision must be between {MinGeohashPrecision} and {MaxGeohashPrecision}");

            if (polygon.IsEmpty)
            {
                progress?.Report(1.0);
                return new HashSet<string>();
            }

            if (!polygon.IsValid)
                throw new ArgumentException("Polygon must be valid", nameof(polygon));

            // Handle antimeridian crossing
            List<Polygon> polys = HandleAntimeridian(polygon);

            // Filter out any invalid polygons
            polys = polys.Where(p => p != null && !p.IsEmpty && p.IsValid).ToList();

            if (polys.Count == 0)
            {
                progress?.Report(1.0);
                return new HashSet<string>();
            }

            var concurrentGeohashes = new ConcurrentBag<string>();
            bool checkContains = geohashInclusionCriteria == GeohashInclusionCriteria.Contains;
            bool checkIntersects = geohashInclusionCriteria == GeohashInclusionCriteria.Intersects;

            // Calculate grid parameters for each polygon
            long totalSteps = 0;
            var polyInfos = new List<PolygonGridInfo>();

            foreach (var poly in polys)
            {
                var envelope = poly.EnvelopeInternal;

                // Geohash precision determines cell dimensions
                int totalBits = 5 * geohashPrecision;
                int numLonBits = (totalBits + 1) / 2;
                int numLatBits = totalBits / 2;
                double latStep = 180.0 / Math.Pow(2, numLatBits);
                double lngStep = 360.0 / Math.Pow(2, numLonBits);

                // Expand envelope by half a cell to catch edge-touching geohashes
                envelope.ExpandBy(lngStep / 2, latStep / 2);

                envelope = new Envelope(
                    Math.Max(envelope.MinX, -180.0),
                    Math.Min(envelope.MaxX, 180.0),
                    Math.Max(envelope.MinY, -90.0),
                    Math.Min(envelope.MaxY, 90.0)
                );

                // Convert envelope bounds to grid indices for iteration
                int startLatIdx = (int)Math.Floor(envelope.MinY / latStep);
                int endLatIdx = (int)Math.Ceiling(envelope.MaxY / latStep);
                int startLngIdx = (int)Math.Floor(envelope.MinX / lngStep);
                int endLngIdx = (int)Math.Ceiling(envelope.MaxX / lngStep);

                totalSteps += Math.Max(endLatIdx - startLatIdx, 0);
                polyInfos.Add(new PolygonGridInfo
                {
                    Polygon = poly,
                    LatStep = latStep,
                    LngStep = lngStep,
                    StartLatIdx = startLatIdx,
                    EndLatIdx = endLatIdx,
                    StartLngIdx = startLngIdx,
                    EndLngIdx = endLngIdx
                });
            }

            if (totalSteps == 0)
            {
                progress?.Report(1.0);
                return new HashSet<string>();
            }

            long completedSteps = 0;
            int lastReportedPercent = -1;

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken
            };

            try
            {
                foreach (var info in polyInfos)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Parallel.For(info.StartLatIdx, info.EndLatIdx, parallelOptions, latIdx =>
                    {
                        var factory = new GeometryFactory();
                        double lat = latIdx * info.LatStep;

                        for (int lngIdx = info.StartLngIdx; lngIdx < info.EndLngIdx; lngIdx++)
                        {
                            double lng = lngIdx * info.LngStep;
                            string curGeohash = _geohasher.Encode(lat, lng, geohashPrecision);

                            var bbox = _geohasher.GetBoundingBox(curGeohash);
                            var geohashPoly = CreateBoundingBoxPolygon(factory, bbox);

                            if ((checkContains && info.Polygon.Contains(geohashPoly)) ||
                                (checkIntersects && info.Polygon.Intersects(geohashPoly)))
                            {
                                concurrentGeohashes.Add(curGeohash);
                            }
                        }

                        if (progress != null)
                        {
                            long completed = Interlocked.Increment(ref completedSteps);
                            int percent = (int)(completed * 100 / totalSteps);
                            int lastPercent = Volatile.Read(ref lastReportedPercent);

                            if (percent > lastPercent)
                            {
                                if (Interlocked.CompareExchange(ref lastReportedPercent, percent, lastPercent) == lastPercent)
                                {
                                    progress.Report(percent / 100.0);
                                }
                            }
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            progress?.Report(1.0);
            return new HashSet<string>(concurrentGeohashes);
        }

        /// <summary>
        /// Creates a polygon from a bounding box.
        /// </summary>
        private static Polygon CreateBoundingBoxPolygon(GeometryFactory factory, BoundingBox bbox)
        {
            var coords = new Coordinate[]
            {
                new Coordinate(bbox.MinLng, bbox.MinLat),
                new Coordinate(bbox.MinLng, bbox.MaxLat),
                new Coordinate(bbox.MaxLng, bbox.MaxLat),
                new Coordinate(bbox.MaxLng, bbox.MinLat),
                new Coordinate(bbox.MinLng, bbox.MinLat)
            };
            return factory.CreatePolygon(coords);
        }

        /// <summary>
        /// Handles antimeridian crossing by detecting and splitting if necessary.
        /// </summary>
        private List<Polygon> HandleAntimeridian(Polygon original)
        {
            if (original == null || original.IsEmpty)
                return new List<Polygon>();

            var envelope = original.EnvelopeInternal;

            // Check 1: If envelope is within valid bounds and no coordinate jumps, no split needed
            bool envelopeWithinBounds = envelope.MinX >= -180 && envelope.MaxX <= 180;
            bool hasLargeJump = HasAntimeridianJump(original);

            if (envelopeWithinBounds && !hasLargeJump)
            {
                // Normal polygon, no antimeridian issues
                return new List<Polygon> { original };
            }

            // Check 2: World-spanning polygons don't need splitting
            if (envelope.Width >= 360.0)
            {
                return new List<Polygon> { original };
            }

            // Polygon crosses antimeridian - needs splitting
            return SplitAntimeridian(original);
        }

        /// <summary>
        /// Detects if polygon has coordinate jumps indicating antimeridian crossing.
        /// A jump > 180° between consecutive vertices indicates crossing.
        /// </summary>
        private bool HasAntimeridianJump(Polygon polygon)
        {
            const double jumpThreshold = 180.0;

            // Check exterior ring
            var coords = polygon.ExteriorRing.Coordinates;
            for (int i = 1; i < coords.Length; i++)
            {
                double jump = Math.Abs(coords[i].X - coords[i - 1].X);
                if (jump > jumpThreshold)
                    return true;
            }

            // Check interior rings
            foreach (var hole in polygon.InteriorRings)
            {
                coords = hole.Coordinates;
                for (int i = 1; i < coords.Length; i++)
                {
                    double jump = Math.Abs(coords[i].X - coords[i - 1].X);
                    if (jump > jumpThreshold)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Splits a polygon that crosses the antimeridian into separate valid polygons.
        /// Uses coordinate unwrapping and half-plane intersection.
        /// </summary>
        private List<Polygon> SplitAntimeridian(Polygon original)
        {
            var factory = original.Factory ?? new GeometryFactory();

            try
            {
                // Step 1: Unwrap coordinates to make polygon continuous
                var unwrappedShell = UnwrapRing(original.ExteriorRing.Coordinates);
                var unwrappedHoles = original.InteriorRings
                    .Select(h => UnwrapRing(h.Coordinates))
                    .ToArray();

                // Align holes to shell
                double shellMinX = unwrappedShell.Min(c => c.X);
                double shellMaxX = unwrappedShell.Max(c => c.X);

                for (int i = 0; i < unwrappedHoles.Length; i++)
                {
                    unwrappedHoles[i] = AlignHoleToShell(unwrappedHoles[i], shellMinX, shellMaxX);
                }

                // Create unwrapped polygon
                var unwrappedPoly = factory.CreatePolygon(
                    factory.CreateLinearRing(unwrappedShell),
                    unwrappedHoles.Select(h => factory.CreateLinearRing(h)).ToArray()
                );

                // Step 2: Determine split meridian(s)
                double minX = unwrappedShell.Min(c => c.X);
                double maxX = unwrappedShell.Max(c => c.X);

                var results = new List<Polygon>();

                if (minX < -180 || maxX > 180)
                {
                    // Need to split
                    double splitLon = maxX > 180 ? 180 : -180;

                    var leftHalf = factory.CreatePolygon(new Coordinate[]
                    {
                        new Coordinate(-1000, -90),
                        new Coordinate(-1000, 90),
                        new Coordinate(splitLon, 90),
                        new Coordinate(splitLon, -90),
                        new Coordinate(-1000, -90)
                    });

                    var rightHalf = factory.CreatePolygon(new Coordinate[]
                    {
                        new Coordinate(splitLon, -90),
                        new Coordinate(splitLon, 90),
                        new Coordinate(1000, 90),
                        new Coordinate(1000, -90),
                        new Coordinate(splitLon, -90)
                    });

                    var leftPart = SafeIntersection(unwrappedPoly, leftHalf);
                    var rightPart = SafeIntersection(unwrappedPoly, rightHalf);

                    foreach (var poly in ExtractPolygons(leftPart))
                        results.Add(NormalizePolygon(poly, factory));

                    foreach (var poly in ExtractPolygons(rightPart))
                        results.Add(NormalizePolygon(poly, factory));
                }
                else
                {
                    results.Add(NormalizePolygon(unwrappedPoly, factory));
                }

                return results.Where(p => p != null && !p.IsEmpty && p.IsValid).ToList();
            }
            catch
            {
                // Fallback: return original if splitting fails
                return new List<Polygon> { original };
            }
        }

        /// <summary>
        /// Unwraps ring coordinates to make them continuous across the antimeridian.
        /// </summary>
        private Coordinate[] UnwrapRing(Coordinate[] coords)
        {
            if (coords.Length == 0) return coords;

            var result = new Coordinate[coords.Length];
            result[0] = new Coordinate(coords[0]);
            double offset = 0;

            for (int i = 1; i < coords.Length; i++)
            {
                double diff = coords[i].X - coords[i - 1].X;

                if (diff > 180) offset -= 360;
                else if (diff < -180) offset += 360;

                result[i] = new Coordinate(coords[i].X + offset, coords[i].Y);
            }

            return result;
        }

        /// <summary>
        /// Aligns hole coordinates to be within shell bounds.
        /// </summary>
        private Coordinate[] AlignHoleToShell(Coordinate[] holeCoords, double shellMinX, double shellMaxX)
        {
            double holeMinX = holeCoords.Min(c => c.X);
            double holeMaxX = holeCoords.Max(c => c.X);

            double shift = 0;
            if (holeMinX < shellMinX - 180)
                shift = 360;
            else if (holeMaxX > shellMaxX + 180)
                shift = -360;

            if (shift == 0)
                return holeCoords;

            return holeCoords.Select(c => new Coordinate(c.X + shift, c.Y)).ToArray();
        }

        /// <summary>
        /// Performs intersection with error handling.
        /// </summary>
        private Geometry SafeIntersection(Geometry a, Geometry b)
        {
            try
            {
                if (a == null || b == null || a.IsEmpty || b.IsEmpty)
                    return null;

                var cleanA = a.IsValid ? a : a.Buffer(0);
                var cleanB = b.IsValid ? b : b.Buffer(0);

                return cleanA.Intersection(cleanB);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Normalizes polygon coordinates to [-180, 180] range.
        /// </summary>
        private Polygon NormalizePolygon(Polygon poly, GeometryFactory factory)
        {
            if (poly == null || poly.IsEmpty)
                return null;

            try
            {
                var env = poly.EnvelopeInternal;
                double shift = 0;

                if (env.MinX < -180) shift = 360;
                else if (env.MaxX > 180) shift = -360;

                if (shift == 0) return poly;

                var shellCoords = poly.ExteriorRing.Coordinates
                    .Select(c => new Coordinate(c.X + shift, c.Y))
                    .ToArray();

                var holes = poly.InteriorRings
                    .Select(h => factory.CreateLinearRing(
                        h.Coordinates.Select(c => new Coordinate(c.X + shift, c.Y)).ToArray()))
                    .ToArray();

                return factory.CreatePolygon(factory.CreateLinearRing(shellCoords), holes);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts all polygons from a geometry.
        /// </summary>
        private IEnumerable<Polygon> ExtractPolygons(Geometry geometry)
        {
            if (geometry == null || geometry.IsEmpty)
                yield break;

            switch (geometry)
            {
                case Polygon poly:
                    yield return poly;
                    break;

                case MultiPolygon multi:
                    for (int i = 0; i < multi.NumGeometries; i++)
                    {
                        if (multi.GetGeometryN(i) is Polygon p)
                            yield return p;
                    }
                    break;

                case GeometryCollection collection:
                    for (int i = 0; i < collection.NumGeometries; i++)
                    {
                        foreach (var p in ExtractPolygons(collection.GetGeometryN(i)))
                            yield return p;
                    }
                    break;
            }
        }
    }
}