using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using NetTopologySuite.Geometries.Prepared;

namespace Geohash
{
    internal class PolygonHasher
    {


        private Geohasher geohasher = new Geohasher();

        private Geometry GeohashToPolygon(string geohash)
        {
            var corners = geohasher.GetBoundingBox(geohash);

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

        public List<string> GetHashes(string startingHash, IPreparedGeometry polygon, int precision, Mode mode, IProgress<HashingProgress> progress = null)
        {

            var startTime = Stopwatch.StartNew();

            var queuedHashes = new Queue<string>();
            var processedHashes = new Dictionary<string, bool>();

            //starting hash
            queuedHashes.Enqueue(startingHash);

            while (queuedHashes.Count > 0)
            {
                var current_geohash = queuedHashes.Dequeue();

                progress?.Report(new HashingProgress() { QueueSize = queuedHashes.Count, HashesProcessed = processedHashes.Count, RunningSince = startTime.Elapsed });

                if (!processedHashes.ContainsKey(current_geohash))
                {
                    var current_polygon = GeohashToPolygon(current_geohash);

               
                    if (CheckIfMatch2(polygon, current_polygon, mode))
                    {
                        processedHashes.Add(current_geohash, true);

                        foreach (var neighborHash in geohasher.GetNeighbors(current_geohash).Values)
                        {

                            if (!processedHashes.ContainsKey(neighborHash))
                            {
                                queuedHashes.Enqueue(neighborHash);
                            }
                        }
                    }
                    else
                    {
                        processedHashes.Add(current_geohash, false);
                    }

         
                    
                }
            }


            return processedHashes.Where(x => x.Value == true).Select(x => x.Key).ToList();
        }


        private bool CheckIfMatch(IPreparedGeometry polygon, Geometry current_polygon, Mode mode)
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
