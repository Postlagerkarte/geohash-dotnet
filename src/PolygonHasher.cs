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

        public async Task<List<string>> GetHashesAsync(Polygon polygon, int precision, Mode mode, IProgress<HashingProgress> progress = null)
        {
            var stopwatch = Stopwatch.StartNew();

            var envelope = polygon.Envelope;
            var centroid = polygon.Centroid;

            var queuedHashes = new ConcurrentQueue<string>();
            var processedHashes = new ConcurrentDictionary<string, bool>();


            queuedHashes.Enqueue(geohasher.Encode(polygon.Centroid.X, polygon.Centroid.Y, precision));

            do
            {
                var tasks = Enumerable.Range(1, queuedHashes.Count > 10 ? 10 : queuedHashes.Count).Select(_ =>
                {
                    return Task.Factory.StartNew(() =>
                    {
                        if (queuedHashes.TryDequeue(out string current_geohash))
                        {
                            if (!processedHashes.ContainsKey(current_geohash))
                            {
                                var current_polygon = GeohashToPolygon(current_geohash);

                                if (envelope.Intersects(current_polygon))
                                {
                                    if (CheckIfMatch(polygon, current_polygon, mode))
                                    {
                                        processedHashes.TryAdd(current_geohash, true);
                                    }
                                    else
                                    {
                                        processedHashes.TryAdd(current_geohash, false);
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
                    });
                });

                await Task.WhenAll(tasks.ToArray());

                progress?.Report(new HashingProgress() { QueueSize = queuedHashes.Count, HashesProcessed = processedHashes.Count, RunningSince = stopwatch.Elapsed });

            } while (queuedHashes.Count != 0);


            var res = processedHashes.Where(x => x.Value == true).Select(x => x.Key).ToList();

            return res;

        }

        private bool CheckIfMatch(Polygon polygon, Geometry current_polygon, Mode mode)
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
