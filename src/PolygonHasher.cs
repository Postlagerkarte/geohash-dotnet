using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Geohash
{
    internal class PolygonHasher
    {


        private Geohasher geohasher = new Geohasher();

        private IGeometry GeohashToPolygon(string geohash)
        {



            var corners = geohasher.DecodeAsBox(geohash);

            var coordinates = new Coordinate[]
            {
                new Coordinate(corners[0], corners[2]),
                new Coordinate(corners[1], corners[2]),
                new Coordinate(corners[1], corners[3]),
                new Coordinate(corners[0], corners[3]),
                new Coordinate(corners[0], corners[2]),
            };

            var geometryFactory = new GeometryFactory();

            return geometryFactory.CreatePolygon(coordinates);

        }

        public List<string> GetHashes(IPolygon polygon, int precision, Mode mode, IProgress<HashingProgress> progress = null)
        {
            var startTime = DateTime.Now;

            var envelope = polygon.Envelope;
            var centroid = polygon.Centroid;

            var queuedHashes = new Queue<string>();
            var processedHashes = new Dictionary<string, bool>();

            queuedHashes.Enqueue(geohasher.Encode(polygon.Centroid.X, polygon.Centroid.Y, precision));

            while (queuedHashes.Count > 0)
            {
                var current_geohash = queuedHashes.Dequeue();

                progress?.Report(new HashingProgress() { QueueSize = queuedHashes.Count, HashesProcessed = processedHashes.Count, RunningSince = startTime });

                if (!processedHashes.ContainsKey(current_geohash))
                {
                    var current_polygon = GeohashToPolygon(current_geohash);

                    if (envelope.Intersects(current_polygon))
                    {
                        if (CheckIfMatch(polygon, current_polygon, mode))
                        {
                            processedHashes.Add(current_geohash, true);
                        }
                        else
                        {
                            processedHashes.Add(current_geohash, false);
                        }

                        foreach (var neighborHash in geohasher.GetNeighbors(current_geohash).Values)
                        {
                            if (!processedHashes.ContainsKey(neighborHash))
                            {
                                queuedHashes.Enqueue(neighborHash);
                            }
                        }
                    }
                }
            }


            return processedHashes.Where(x => x.Value == true).Select(x => x.Key).ToList();
        }

        private bool CheckIfMatch(IPolygon polygon, IGeometry current_polygon, Mode mode)
        {
            if (mode == Mode.Intersect)
            {
                return polygon.Intersects(current_polygon);
            }

            if (mode == Mode.Contains)
            {
                return polygon.Contains(current_polygon);
            }

            throw new InvalidEnumArgumentException("unkown mode");
        }
    }
}
