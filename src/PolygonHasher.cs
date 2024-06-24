using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
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
        /// <param name="progress">Allows to report progress</param>
        /// <returns>A HashSet containing the geohashes that meet the specified inclusion criteria with the input polygon.</returns>

        public HashSet<string> GetHashes(Polygon polygon, int geohashPrecision, GeohashInclusionCriteria geohashInclusionCriteria = GeohashInclusionCriteria.Contains, IProgress<double> progress = null)
        {
            var envelope = polygon.EnvelopeInternal;
            double latStep = 180.0 / Math.Pow(2, (5 * geohashPrecision - geohashPrecision % 2) / 2);
            double lngStep = 360.0 / Math.Pow(2, (5 * geohashPrecision + geohashPrecision % 2) / 2);
            envelope.ExpandBy(latStep / 2, lngStep / 2);

            HashSet<string> geohashes = new HashSet<string>();
            bool checkContains = geohashInclusionCriteria == GeohashInclusionCriteria.Contains;
            bool checkIntersects = geohashInclusionCriteria == GeohashInclusionCriteria.Intersects;

            int totalSteps = (int)Math.Ceiling((envelope.MaxY - envelope.MinY) / latStep);
            int stepsCompleted = 0;

            // Calculate the size of the work batch for progress reporting
            int progressBatchSize = Math.Max(1, totalSteps / 100);

            Parallel.For((int)(envelope.MinY / latStep), (int)(envelope.MaxY / latStep) + 1, () => new HashSet<string>(),
            (latIdx, state, localGeohashes) =>
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


                    if ((checkContains && polygon.Contains(geohashPoly)) ||
                        (checkIntersects && polygon.Intersects(geohashPoly)))
                    {
                        localGeohashes.Add(curGeohash);
                    }
                }

                if ((latIdx + 1) % progressBatchSize == 0)
                {
                    progress?.Report(Math.Min(1.0, (double)latIdx / totalSteps));
                }

                return localGeohashes;
            },
            (localGeohashes) =>
            {
                lock (geohashes)
                {
                    foreach (var geohash in localGeohashes)
                    {
                        geohashes.Add(geohash);
                    }
                }
            });

            // Final progress update to ensure we reach 100% when done
            progress?.Report(1.0);

            return geohashes;
        }

    }
}