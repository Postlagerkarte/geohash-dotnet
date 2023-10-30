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
            // Determine the envelope of the polygon.
            var envelope = polygon.EnvelopeInternal;

            // Calculate step size based on geohash precision.
            double latStep = 180.0 / Math.Pow(2, (5 * geohashPrecision - geohashPrecision % 2) / 2);
            double lngStep = 180.0 / Math.Pow(2, (5 * geohashPrecision + geohashPrecision % 2) / 2 - 1);

            // Expand envelope by half the step size to ensure boundary geohashes are considered.
            envelope.ExpandBy(latStep / 2, lngStep / 2);

            var geohashes = new ConcurrentBag<string>();

            bool checkContains = geohashInclusionCriteria == GeohashInclusionCriteria.Contains;
            bool checkIntersects = geohashInclusionCriteria == GeohashInclusionCriteria.Intersects;

            // Parallelize the outer latitude loop.
            Parallel.For((int)(envelope.MinY / latStep), (int)(envelope.MaxY / latStep) + 1, (latIdx) =>
            {
                double lat = latIdx * latStep;
                Coordinate[] coords = new Coordinate[5];

                for (double lng = envelope.MinX; lng <= envelope.MaxX; lng += lngStep)
                {
                    // Generate a geohash for the latitude-longitude pair.
                    string curGeohash = _geohasher.Encode(lat, lng, geohashPrecision);

                    // Get bounding box for geohash and convert to polygon.
                    var bbox = _geohasher.GetBoundingBox(curGeohash);
                    coords[0] = new Coordinate(bbox.MinLng, bbox.MinLat);
                    coords[1] = new Coordinate(bbox.MinLng, bbox.MaxLat);
                    coords[2] = new Coordinate(bbox.MaxLng, bbox.MaxLat);
                    coords[3] = new Coordinate(bbox.MaxLng, bbox.MinLat);
                    coords[4] = coords[0];

                    var geohashPoly = new Polygon(new LinearRing(coords));

                    // Check inclusion criteria.
                    if ((checkContains && polygon.Contains(geohashPoly)) ||
                        (checkIntersects && polygon.Intersects(geohashPoly)))
                    {
                        geohashes.Add(curGeohash);
                    }
                }
            });

            return geohashes.ToHashSet();
        }











    }
}