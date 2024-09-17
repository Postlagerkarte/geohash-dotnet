using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Geohash
{
    /// <summary>
    /// Provides functionality to generate geohashes within a polygon based on the specified precision and inclusion criteria.
    /// </summary>
    public class PolygonHasher
    {
        private const int MinGeohashPrecision = 1;
        private const int MaxGeohashPrecision = 12;
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
            if (geohashPrecision < MinGeohashPrecision || geohashPrecision > MaxGeohashPrecision)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(geohashPrecision),
                    $"Geohash precision must be between {MinGeohashPrecision} and {MaxGeohashPrecision}."
                );
            }

            var envelope = polygon.EnvelopeInternal;

            // Calculate the number of bits used for latitude and longitude
            int totalBits = 5 * geohashPrecision;
            int numLonBits = (totalBits + 1) / 2; // Integer division
            int numLatBits = totalBits / 2;       // Integer division

            // Calculate the step sizes for latitude and longitude
            double latStep = 180.0 / Math.Pow(2, numLatBits);
            double lngStep = 360.0 / Math.Pow(2, numLonBits);

            // Expand the envelope to cover edge geohashes
            envelope.ExpandBy(lngStep / 2, latStep / 2);

            // Clamp the envelope to valid latitude and longitude ranges
            envelope = new Envelope(
                Math.Max(envelope.MinX, -180.0),
                Math.Min(envelope.MaxX, 180.0),
                Math.Max(envelope.MinY, -90.0),
                Math.Min(envelope.MaxY, 90.0)
            );


            HashSet<string> geohashes = new HashSet<string>();
            bool checkContains = geohashInclusionCriteria == GeohashInclusionCriteria.Contains;
            bool checkIntersects = geohashInclusionCriteria == GeohashInclusionCriteria.Intersects;

            // Accurate loop index calculation using Math.Floor and Math.Ceiling
            int startLatIdx = (int)Math.Floor(envelope.MinY / latStep);
            int endLatIdx = (int)Math.Ceiling(envelope.MaxY / latStep);
            int startLngIdx = (int)Math.Floor(envelope.MinX / lngStep);
            int endLngIdx = (int)Math.Ceiling(envelope.MaxX / lngStep);

            // Initialize progress reporting variables
            int totalSteps = endLatIdx - startLatIdx;
            totalSteps = Math.Max(totalSteps, 1);

            int reportInterval = Math.Max(1, totalSteps / 100); // Report every 1%
            long currentStep = 0;

            // Use ConcurrentBag for thread-safe collection without explicit locking
            var concurrentGeohashes = new ConcurrentBag<string>();

            // Use a thread-safe counter for progress
            object progressLock = new object();

            Parallel.For(startLatIdx, endLatIdx, latIdx =>
            {
                double lat = latIdx * latStep;

                for (int lngIdx = startLngIdx; lngIdx <= endLngIdx; lngIdx++)
                {
                    double lng = lngIdx * lngStep;

                    // Generate a geohash for the latitude-longitude pair.
                    string curGeohash = _geohasher.Encode(lat, lng, geohashPrecision);

                    // Get bounding box for geohash and convert to polygon.
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

                    if ((checkContains && polygon.Contains(geohashPoly)) ||
                        (checkIntersects && polygon.Intersects(geohashPoly)))
                    {
                        concurrentGeohashes.Add(curGeohash);
                    }
                }


                // Update progress
                if (progress != null)
                {
                    long step = Interlocked.Increment(ref currentStep);

                    // Report progress at defined intervals or on the last step
                    if (step % reportInterval == 0 || step == totalSteps)
                    {
                        double progressValue = (double)step / totalSteps;
                        progress.Report(Math.Min(progressValue, 1.0));
                    }
                }

            });

            // Transfer geohashes from ConcurrentBag to HashSet
            foreach (var gh in concurrentGeohashes)
            {
                geohashes.Add(gh);
            }

            // Final progress update to ensure we reach 100% when done
            progress?.Report(1.0);

            return geohashes;
        }

    }
}