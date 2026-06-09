using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using System;
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
        private static readonly Geohasher Hasher = new Geohasher();

        /// <summary>Criteria deciding whether a geohash cell belongs to the result.</summary>
        public enum GeohashInclusionCriteria
        {
            /// <summary>Geohash cell must be entirely within the polygon.</summary>
            Contains,

            /// <summary>Geohash cell may partially overlap the polygon boundary.</summary>
            Intersects
        }

        private readonly struct PolygonGridInfo
        {
            public Polygon Polygon { get; init; }
            public int StartLatIdx { get; init; }
            public int EndLatIdx { get; init; }
            public int StartLngIdx { get; init; }
            public int EndLngIdx { get; init; }
        }

        /// <summary>
        /// Generates geohashes covering the specified polygon.
        /// </summary>
        /// <param name="polygon">Must be valid; antimeridian-crossing polygons are automatically split.</param>
        /// <param name="geohashPrecision">1–12; each level multiplies the cell count by 32.</param>
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
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            if (geohashPrecision < 1 || geohashPrecision > Geohasher.MaxPrecision)
                throw new ArgumentOutOfRangeException(nameof(geohashPrecision),
                    $"Precision must be between 1 and {Geohasher.MaxPrecision}.");

            var results = new HashSet<string>(StringComparer.Ordinal);

            if (polygon.IsEmpty)
            {
                progress?.Report(1.0);
                return results;
            }

            if (!polygon.IsValid)
                throw new ArgumentException("Polygon must be valid.", nameof(polygon));

            var polys = HandleAntimeridian(polygon)
                .Where(p => p != null && !p.IsEmpty && p.IsValid)
                .ToList();

            if (polys.Count == 0)
            {
                progress?.Report(1.0);
                return results;
            }

            // Cell dimensions: a hash of precision p has 5p bits, split lon-first.
            int totalBits = 5 * geohashPrecision;
            double latStep = 180.0 / (1L << (totalBits / 2));
            double lngStep = 360.0 / (1L << ((totalBits + 1) / 2));

            long totalSteps = 0;
            var polyInfos = new List<PolygonGridInfo>(polys.Count);

            foreach (var poly in polys)
            {
                var envelope = poly.EnvelopeInternal.Copy();
                envelope.ExpandBy(lngStep / 2, latStep / 2); // catch edge-touching cells
                envelope = new Envelope(
                    Math.Max(envelope.MinX, -180.0), Math.Min(envelope.MaxX, 180.0),
                    Math.Max(envelope.MinY, -90.0), Math.Min(envelope.MaxY, 90.0));

                // The geohash grid is aligned at 0°, so cell i spans [i*step, (i+1)*step).
                var info = new PolygonGridInfo
                {
                    Polygon = poly,
                    StartLatIdx = (int)Math.Floor(envelope.MinY / latStep),
                    EndLatIdx = (int)Math.Ceiling(envelope.MaxY / latStep),
                    StartLngIdx = (int)Math.Floor(envelope.MinX / lngStep),
                    EndLngIdx = (int)Math.Ceiling(envelope.MaxX / lngStep),
                };

                totalSteps += Math.Max(info.EndLatIdx - info.StartLatIdx, 0);
                polyInfos.Add(info);
            }

            if (totalSteps == 0)
            {
                progress?.Report(1.0);
                return results;
            }

            long completedSteps = 0;
            int lastReportedPercent = -1;
            var sync = new object();
            var parallelOptions = new ParallelOptions { CancellationToken = cancellationToken };
            bool checkContains = geohashInclusionCriteria == GeohashInclusionCriteria.Contains;

            foreach (var info in polyInfos)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Prepared geometries cache an edge index, making repeated spatial
                // predicates dramatically faster. Thread-safe for reads in NTS 2.x.
                var prepared = PreparedGeometryFactory.Prepare(info.Polygon);
                var envelope = info.Polygon.EnvelopeInternal;
                var factory = info.Polygon.Factory ?? GeometryFactory.Default;

                Parallel.For(
                    info.StartLatIdx, info.EndLatIdx, parallelOptions,
                    static () => new List<string>(),
                    (latIdx, _, local) =>
                    {
                        double cellMinLat = latIdx * latStep;
                        double cellMaxLat = cellMinLat + latStep;

                        for (int lngIdx = info.StartLngIdx; lngIdx < info.EndLngIdx; lngIdx++)
                        {
                            double cellMinLng = lngIdx * lngStep;
                            double cellMaxLng = cellMinLng + lngStep;

                            // Cheap envelope rejection before any geometry allocation.
                            if (cellMaxLng < envelope.MinX || cellMinLng > envelope.MaxX ||
                                cellMaxLat < envelope.MinY || cellMinLat > envelope.MaxY)
                                continue;

                            var cell = CreateCellPolygon(factory, cellMinLng, cellMinLat, cellMaxLng, cellMaxLat);

                            bool match = checkContains
                                ? prepared.Contains(cell)
                                : prepared.Intersects(cell);

                            if (match)
                            {
                                // Encode the cell center; only pay for Encode on accepted cells.
                                local.Add(Hasher.Encode(
                                    cellMinLat + latStep * 0.5,
                                    cellMinLng + lngStep * 0.5,
                                    geohashPrecision));
                            }
                        }

                        ReportProgress(progress, ref completedSteps, ref lastReportedPercent, totalSteps);
                        return local;
                    },
                    local =>
                    {
                        if (local.Count == 0) return;
                        lock (sync) results.UnionWith(local);
                    });
            }

            progress?.Report(1.0);
            return results;
        }

        private static void ReportProgress(IProgress<double> progress,
            ref long completedSteps, ref int lastReportedPercent, long totalSteps)
        {
            if (progress == null) return;

            long completed = Interlocked.Increment(ref completedSteps);
            int percent = (int)(completed * 100 / totalSteps);
            int lastPercent = Volatile.Read(ref lastReportedPercent);

            if (percent > lastPercent &&
                Interlocked.CompareExchange(ref lastReportedPercent, percent, lastPercent) == lastPercent)
            {
                progress.Report(percent / 100.0);
            }
        }

        private static Polygon CreateCellPolygon(GeometryFactory factory,
            double minLng, double minLat, double maxLng, double maxLat)
        {
            return factory.CreatePolygon(new[]
            {
                new Coordinate(minLng, minLat),
                new Coordinate(minLng, maxLat),
                new Coordinate(maxLng, maxLat),
                new Coordinate(maxLng, minLat),
                new Coordinate(minLng, minLat)
            });
        }

        // ---------- Antimeridian handling (logic unchanged, helpers made static) ----------

        private static List<Polygon> HandleAntimeridian(Polygon original)
        {
            if (original == null || original.IsEmpty)
                return new List<Polygon>();

            var envelope = original.EnvelopeInternal;
            bool envelopeWithinBounds = envelope.MinX >= -180 && envelope.MaxX <= 180;

            if (envelopeWithinBounds && !HasAntimeridianJump(original))
                return new List<Polygon> { original };

            if (envelope.Width >= 360.0)
                return new List<Polygon> { original };

            return SplitAntimeridian(original);
        }

        private static bool HasAntimeridianJump(Polygon polygon)
        {
            const double jumpThreshold = 180.0;

            if (RingHasJump(polygon.ExteriorRing, jumpThreshold)) return true;
            foreach (var hole in polygon.InteriorRings)
                if (RingHasJump(hole, jumpThreshold)) return true;
            return false;

            static bool RingHasJump(LineString ring, double threshold)
            {
                var coords = ring.Coordinates;
                for (int i = 1; i < coords.Length; i++)
                    if (Math.Abs(coords[i].X - coords[i - 1].X) > threshold)
                        return true;
                return false;
            }
        }

        private static List<Polygon> SplitAntimeridian(Polygon original)
        {
            var factory = original.Factory ?? GeometryFactory.Default;

            try
            {
                var unwrappedShell = UnwrapRing(original.ExteriorRing.Coordinates);
                var unwrappedHoles = original.InteriorRings
                    .Select(h => UnwrapRing(h.Coordinates))
                    .ToArray();

                double shellMinX = unwrappedShell.Min(c => c.X);
                double shellMaxX = unwrappedShell.Max(c => c.X);

                for (int i = 0; i < unwrappedHoles.Length; i++)
                    unwrappedHoles[i] = AlignHoleToShell(unwrappedHoles[i], shellMinX, shellMaxX);

                var unwrappedPoly = factory.CreatePolygon(
                    factory.CreateLinearRing(unwrappedShell),
                    unwrappedHoles.Select(factory.CreateLinearRing).ToArray());

                var results = new List<Polygon>();

                if (shellMinX < -180 || shellMaxX > 180)
                {
                    double splitLon = shellMaxX > 180 ? 180 : -180;

                    var leftHalf = CreateHalfPlane(factory, -1000, splitLon);
                    var rightHalf = CreateHalfPlane(factory, splitLon, 1000);

                    foreach (var poly in ExtractPolygons(SafeIntersection(unwrappedPoly, leftHalf)))
                        results.Add(NormalizePolygon(poly, factory));
                    foreach (var poly in ExtractPolygons(SafeIntersection(unwrappedPoly, rightHalf)))
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
                // Fallback: return original if splitting fails.
                return new List<Polygon> { original };
            }
        }

        private static Polygon CreateHalfPlane(GeometryFactory factory, double minX, double maxX)
        {
            return factory.CreatePolygon(new[]
            {
                new Coordinate(minX, -90),
                new Coordinate(minX, 90),
                new Coordinate(maxX, 90),
                new Coordinate(maxX, -90),
                new Coordinate(minX, -90)
            });
        }

        private static Coordinate[] UnwrapRing(Coordinate[] coords)
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

        private static Coordinate[] AlignHoleToShell(Coordinate[] holeCoords, double shellMinX, double shellMaxX)
        {
            double holeMinX = holeCoords.Min(c => c.X);
            double holeMaxX = holeCoords.Max(c => c.X);

            double shift = 0;
            if (holeMinX < shellMinX - 180) shift = 360;
            else if (holeMaxX > shellMaxX + 180) shift = -360;

            return shift == 0
                ? holeCoords
                : holeCoords.Select(c => new Coordinate(c.X + shift, c.Y)).ToArray();
        }

        private static Geometry SafeIntersection(Geometry a, Geometry b)
        {
            try
            {
                if (a == null || b == null || a.IsEmpty || b.IsEmpty) return null;
                var cleanA = a.IsValid ? a : a.Buffer(0);
                var cleanB = b.IsValid ? b : b.Buffer(0);
                return cleanA.Intersection(cleanB);
            }
            catch
            {
                return null;
            }
        }

        private static Polygon NormalizePolygon(Polygon poly, GeometryFactory factory)
        {
            if (poly == null || poly.IsEmpty) return null;

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

        private static IEnumerable<Polygon> ExtractPolygons(Geometry geometry)
        {
            if (geometry == null || geometry.IsEmpty)
                yield break;

            switch (geometry)
            {
                case Polygon poly:
                    yield return poly;
                    break;

                case GeometryCollection collection: // also covers MultiPolygon
                    for (int i = 0; i < collection.NumGeometries; i++)
                        foreach (var p in ExtractPolygons(collection.GetGeometryN(i)))
                            yield return p;
                    break;
            }
        }
    }
}