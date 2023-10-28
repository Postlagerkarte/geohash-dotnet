using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Geohash
{
    /// <summary>
    /// Provides functionality to generate geohashes within a polygon based on the specified precision and inclusion criteria.
    /// </summary>
    public class PolygonHasher
    {
        // Create a Geohasher instance for encoding and decoding geohashes
        private Geohasher _geohasher = new Geohasher();

        /// <summary>
        /// Specifies the criteria used to determine if a geohash should be included in the result set.
        /// </summary>
        public enum GeohashInclusionCriteria
        {
            /// <summary>
            /// Include geohashes that are entirely contained within the input polygon.
            /// </summary>
            Contains,

            /// <summary>
            /// Include geohashes that intersect the input polygon, even partially.
            /// </summary>
            Intersects
        }

        /// <summary>
        /// Get the geohashes of the specified precision that meet the specified inclusion criteria with the input polygon.
        /// </summary>
        /// <param name="polygon">The input polygon.</param>
        /// <param name="geohashPrecision">The desired geohash precision.</param>
        /// <param name="geohashInclusionCriteria">The criteria for including geohashes in the result set.</param>
        /// <returns>A HashSet containing the geohashes that meet the specified inclusion criteria with the input polygon.</returns>
        public HashSet<string> GetHashes(Polygon polygon, int geohashPrecision, GeohashInclusionCriteria geohashInclusionCriteria = GeohashInclusionCriteria.Intersects)
        {
            var envelope = polygon.EnvelopeInternal;

            double latRange = envelope.MaxY - envelope.MinY;
            double lngRange = envelope.MaxX - envelope.MinX;

            double baseLatStep = 180 / Math.Pow(2, (5 * geohashPrecision - geohashPrecision % 2) / 2);
            double baseLngStep = 180 / Math.Pow(2, (5 * geohashPrecision + geohashPrecision % 2) / 2 - 1);

            double latStep = Math.Min(baseLatStep, latRange / 10); // Here, "10" determines the granularity. You can adjust this value.
            double lngStep = Math.Min(baseLngStep, lngRange / 10);

            var geohashes = new ConcurrentBag<string>();

            Parallel.ForEach(Enumerable.Range(0, (int)((envelope.MaxY - envelope.MinY) / latStep)), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, latIndex =>
            {
                double lat = envelope.MinY + latIndex * latStep;
                for (double lng = envelope.MinX; lng <= envelope.MaxX; lng += lngStep)
                {
                    string curGeohash = _geohasher.Encode(lat, lng, geohashPrecision);
                    var bbox = _geohasher.GetBoundingBox(curGeohash);
                    var geohashPoly = new Polygon(new LinearRing(new Coordinate[] {
                        new Coordinate(bbox.MinLng, bbox.MinLat),
                        new Coordinate(bbox.MinLng, bbox.MaxLat),
                        new Coordinate(bbox.MaxLng, bbox.MaxLat),
                        new Coordinate(bbox.MaxLng, bbox.MinLat),
                        new Coordinate(bbox.MinLng, bbox.MinLat)
                    }));

                    if (polygon.EnvelopeInternal.Intersects(geohashPoly.EnvelopeInternal) &&
                        ((geohashInclusionCriteria == GeohashInclusionCriteria.Contains && polygon.Contains(geohashPoly)) ||
                         (geohashInclusionCriteria == GeohashInclusionCriteria.Intersects && polygon.Intersects(geohashPoly))))
                    {
                        geohashes.Add(curGeohash);
                    }
                }
            });

            return new HashSet<string>(geohashes);
        }
    }

}