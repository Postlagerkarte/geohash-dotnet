using NetTopologySuite.Geometries;
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
            // Get the bounding box of the input polygon
            var envelope = polygon.EnvelopeInternal;
            double minx = envelope.MinX;
            double miny = envelope.MinY;
            double maxx = envelope.MaxX;
            double maxy = envelope.MaxY;

            // Set latitude and longitude step sizes based on the bounding box
            double latStep = (maxy - miny) / 100;
            double lonStep = (maxx - minx) / 100;

            // Initialize a HashSet to store the geohashes inside the bounding box
            var geohashes = new HashSet<string>();

            // Generate geohashes inside the bounding box
            double lat = miny;
            while (lat <= maxy)
            {
                double lon = minx;
                while (lon <= maxx)
                {
                    // Encode the current latitude and longitude to a geohash
                    string curGeohash = _geohasher.Encode(lat, lon, geohashPrecision);

                    // Get the bounding box of the current geohash
                    var bbox = _geohasher.GetBoundingBox(curGeohash);

                    // Create a polygon from the bounding box coordinates
                    var geohashPoly = new Polygon(new LinearRing(new Coordinate[] {
                        new Coordinate(bbox.MinLng, bbox.MinLat),
                        new Coordinate(bbox.MinLng, bbox.MaxLat),
                        new Coordinate(bbox.MaxLng, bbox.MaxLat),
                        new Coordinate(bbox.MaxLng, bbox.MinLat),
                        new Coordinate(bbox.MinLng, bbox.MinLat) // Close the ring
                    }));

                    // Check if the input polygon intersects the geohash polygon
                    if (geohashInclusionCriteria == GeohashInclusionCriteria.Contains && polygon.Contains(geohashPoly) ||
                       geohashInclusionCriteria == GeohashInclusionCriteria.Intersects && polygon.Intersects(geohashPoly))
                    {
                        // If the polygons intersect, add the geohash to the HashSet
                        geohashes.Add(curGeohash);
                    }

                    // Increment the longitude by the step size
                    lon += lonStep;
                }

                // Increment the latitude by the step size
                lat += latStep;
            }

            // Return the geohashes that intersect the input polygon
            return geohashes;
        }
    }

}