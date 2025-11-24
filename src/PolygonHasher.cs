using NetTopologySuite.Geometries;
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
        private Geohasher _geohasher = new Geohasher();

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

        /// <param name="polygon">Must be valid; antimeridian-crossing polygons are automatically split.</param>
        /// <param name="geohashPrecision">1-12; higher precision = smaller cells = exponentially more results.</param>
        /// <param name="progress">Reports 0.0 to 1.0; updates throttled to 1% increments to avoid callback overhead.</param>
        public HashSet<string> GetHashes(Polygon polygon, int geohashPrecision,
            GeohashInclusionCriteria geohashInclusionCriteria = GeohashInclusionCriteria.Contains,
            IProgress<double> progress = null)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            var originalEnv = polygon.EnvelopeInternal;

            // World-spanning polygons don't need antimeridian handling
            List<Polygon> polys;
            if (originalEnv.Width >= 360.0 && originalEnv.Height >= 180.0)
            {
                polys = new List<Polygon> { polygon };
            }
            else
            {
                polys = SplitAntimeridian(polygon);
            }

            var concurrentGeohashes = new ConcurrentBag<string>();
            bool checkContains = geohashInclusionCriteria == GeohashInclusionCriteria.Contains;
            bool checkIntersects = geohashInclusionCriteria == GeohashInclusionCriteria.Intersects;

            long totalSteps = 0;
            var polyInfos = new List<(Polygon poly, Envelope env, double latStep, double lngStep,
                int startLatIdx, int endLatIdx, int startLngIdx, int endLngIdx)>();

            foreach (var poly in polys)
            {
                var envelope = poly.EnvelopeInternal;

                // Geohash precision determines cell dimensions:
                // - Total bits = 5 * precision (each character encodes 5 bits)
                // - Longitude gets the extra bit on odd totals (interleaved encoding)
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
                polyInfos.Add((poly, envelope, latStep, lngStep, startLatIdx, endLatIdx, startLngIdx, endLngIdx));
            }

            if (totalSteps == 0)
            {
                progress?.Report(1.0);
                return new HashSet<string>();
            }

            long completedSteps = 0;
            int lastReportedPercent = -1;

            foreach (var info in polyInfos)
            {
                // Parallelize by latitude rows; each row processes all longitude cells
                Parallel.For(info.startLatIdx, info.endLatIdx, latIdx =>
                {
                    double lat = latIdx * info.latStep;

                    for (int lngIdx = info.startLngIdx; lngIdx <= info.endLngIdx; lngIdx++)
                    {
                        double lng = lngIdx * info.lngStep;
                        string curGeohash = _geohasher.Encode(lat, lng, geohashPrecision);

                        // Build geohash cell as polygon for spatial comparison
                        var bbox = _geohasher.GetBoundingBox(curGeohash);
                        var coords = new Coordinate[]
                        {
                            new Coordinate(bbox.MinLng, bbox.MinLat),
                            new Coordinate(bbox.MinLng, bbox.MaxLat),
                            new Coordinate(bbox.MaxLng, bbox.MaxLat),
                            new Coordinate(bbox.MaxLng, bbox.MinLat),
                            new Coordinate(bbox.MinLng, bbox.MinLat)
                        };
                        var geohashPoly = new Polygon(new LinearRing(coords));

                        if ((checkContains && info.poly.Contains(geohashPoly)) ||
                            (checkIntersects && info.poly.Intersects(geohashPoly)))
                        {
                            concurrentGeohashes.Add(curGeohash);
                        }
                    }

                    if (progress != null)
                    {
                        long completed = Interlocked.Increment(ref completedSteps);
                        int percent = (int)(completed * 100 / totalSteps);
                        int lastPercent = Volatile.Read(ref lastReportedPercent);

                        // CAS ensures only one thread reports each percentage milestone
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

            progress?.Report(1.0);
            return new HashSet<string>(concurrentGeohashes);
        }

        /// <summary>
        /// Detects antimeridian crossing by checking for large longitude jumps between consecutive points.
        /// </summary>
        private bool CheckCrossing(double lon1, double lon2, double dlonThreshold = 180.0)
        {
            return Math.Abs(lon2 - lon1) > dlonThreshold;
        }

        /// <summary>
        /// Splits polygons crossing the antimeridian (±180°) into separate valid polygons.
        /// 
        /// Algorithm:
        /// 1. Detect crossings by finding >180° longitude jumps between consecutive vertices
        /// 2. "Unwrap" coordinates by shifting crossed vertices ±360° to create a continuous ring
        /// 3. If unwrapped polygon extends beyond ±180°, split along the exceeded meridian
        /// 4. Translate split pieces back into valid [-180, 180] longitude range
        /// </summary>
        private List<Polygon> SplitAntimeridian(Polygon original)
        {
            // Deep copy all rings (exterior shell + holes) for coordinate manipulation
            var coordsShift = new List<Coordinate[]>();
            coordsShift.Add(original.ExteriorRing.Coordinates.Select(c => new Coordinate(c)).ToArray());
            foreach (var hole in original.InteriorRings)
            {
                coordsShift.Add(hole.Coordinates.Select(c => new Coordinate(c)).ToArray());
            }

            double? shellMinX = null, shellMaxX = null;
            var splitMeridians = new HashSet<double>();

            for (int ringIndex = 0; ringIndex < coordsShift.Count; ringIndex++)
            {
                var ring = coordsShift[ringIndex];
                if (ring.Length < 1) continue;

                double ringMinX = ring[0].X;
                double ringMaxX = ring[0].X;
                int crossings = 0;

                // Unwrap: when we detect a crossing, shift the coordinate to maintain continuity
                for (int coordIndex = 1; coordIndex < ring.Length; coordIndex++)
                {
                    double lon = ring[coordIndex].X;
                    double lonPrev = ring[coordIndex - 1].X;

                    if (CheckCrossing(lon, lonPrev))
                    {
                        double direction = Math.Sign(lon - lonPrev);
                        ring[coordIndex].X = lon - direction * 360.0;
                        crossings++;
                    }

                    double xShift = ring[coordIndex].X;
                    if (xShift < ringMinX) ringMinX = xShift;
                    if (xShift > ringMaxX) ringMaxX = xShift;
                }

                if (ringIndex == 0)
                {
                    // Shell defines the reference frame for holes
                    shellMinX = ringMinX;
                    shellMaxX = ringMaxX;
                }
                else
                {
                    // Holes may end up on the "wrong side" after unwrapping; re-align with shell
                    if (ringMinX < shellMinX)
                    {
                        for (int j = 0; j < ring.Length; j++)
                            ring[j].X += 360;
                        ringMinX += 360;
                        ringMaxX += 360;
                    }
                    else if (ringMaxX > shellMaxX)
                    {
                        for (int j = 0; j < ring.Length; j++)
                            ring[j].X -= 360;
                        ringMinX -= 360;
                        ringMaxX -= 360;
                    }
                }

                if (crossings > 0)
                {
                    // Track which meridian(s) the unwrapped polygon exceeds
                    if (ringMinX < -180) splitMeridians.Add(-180);
                    if (ringMaxX > 180) splitMeridians.Add(180);
                }
            }

            // Polygons crossing both meridians would require multi-way splitting
            if (splitMeridians.Count > 1)
            {
                throw new NotImplementedException("Splitting across multiple meridians not supported.");
            }

            var geometryFactory = new GeometryFactory();
            var shellRing = new LinearRing(coordsShift[0]);
            var holeRings = coordsShift.Skip(1).Select(r => new LinearRing(r)).ToArray();
            var shiftedPoly = geometryFactory.CreatePolygon(shellRing, holeRings);

            var resultPolys = new List<Polygon>();

            if (splitMeridians.Count == 1)
            {
                double splitLon = splitMeridians.First();

                // Use oversized half-planes to ensure complete intersection coverage
                // (±1000° handles any amount of coordinate unwrapping)
                var leftHalf = geometryFactory.CreatePolygon(new Coordinate[]
                {
                    new Coordinate(-1000, -90),
                    new Coordinate(-1000, 90),
                    new Coordinate(splitLon, 90),
                    new Coordinate(splitLon, -90),
                    new Coordinate(-1000, -90)
                });

                var rightHalf = geometryFactory.CreatePolygon(new Coordinate[]
                {
                    new Coordinate(splitLon, -90),
                    new Coordinate(splitLon, 90),
                    new Coordinate(1000, 90),
                    new Coordinate(1000, -90),
                    new Coordinate(splitLon, -90)
                });

                var leftPart = shiftedPoly.Intersection(leftHalf) as Polygon;
                var rightPart = shiftedPoly.Intersection(rightHalf) as Polygon;

                if (leftPart != null) resultPolys.Add(TranslatePolygon(leftPart));
                if (rightPart != null) resultPolys.Add(TranslatePolygon(rightPart));
            }
            else
            {
                resultPolys.Add(TranslatePolygon(shiftedPoly));
            }

            return resultPolys;
        }

        /// <summary>
        /// Shifts polygon coordinates by ±360° to bring them into valid [-180, 180] range.
        /// </summary>
        private Polygon TranslatePolygon(Polygon poly)
        {
            var env = poly.EnvelopeInternal;
            double shift = 0;

            if (env.MinX < -180) shift = 360;
            else if (env.MaxX > 180) shift = -360;

            if (shift == 0) return poly;

            var translatedShell = poly.ExteriorRing.Coordinates
                .Select(c => new Coordinate(c.X + shift, c.Y)).ToArray();
            var translatedHoles = poly.InteriorRings
                .Select(h => new LinearRing(h.Coordinates
                    .Select(c => new Coordinate(c.X + shift, c.Y)).ToArray()))
                .ToArray();

            return new Polygon(new LinearRing(translatedShell), translatedHoles);
        }
    }
}