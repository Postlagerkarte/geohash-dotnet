using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NetTopologySuite.Geometries;
using Xunit;
using Geohash;
using System.Collections.Concurrent;

namespace Geohash.Tests
{
    public class PolygonHasherTests2
    {
        private readonly GeometryFactory _factory = new GeometryFactory();
        private readonly PolygonHasher _sut = new PolygonHasher();
        private readonly Geohasher _geohasher = new Geohasher();

        private class SynchronousProgress<T> : IProgress<T>
        {
            private readonly Action<T> _handler;

            public SynchronousProgress(Action<T> handler)
            {
                _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            }

            public void Report(T value)
            {
                _handler(value);
            }
        }

        // Helper to create a polygon covering the whole world
        private Polygon CreateWorldPolygon()
        {
            var coords = new Coordinate[]
            {
                new Coordinate(-180, -90),
                new Coordinate(-180, 90),
                new Coordinate(180, 90),
                new Coordinate(180, -90),
                new Coordinate(-180, -90)
            };
            return _factory.CreatePolygon(coords);
        }

        // Helper to create a polygon from a geohash bounding box
        private Polygon CreateBoundingBoxPolygon(BoundingBox bbox, GeometryFactory factory)
        {
            var coords = new Coordinate[]
            {
                new Coordinate(bbox.MinLng, bbox.MinLat),
                new Coordinate(bbox.MinLng, bbox.MaxLat),
                new Coordinate(bbox.MaxLng, bbox.MaxLat),
                new Coordinate(bbox.MaxLng, bbox.MinLat),
                new Coordinate(bbox.MinLng, bbox.MinLat)
            };
            return factory.CreatePolygon(coords);
        }

        [Fact]
        public void GetHashes_PolygonNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _sut.GetHashes(null, 1));
        }

        [Fact]
        public void GetHashes_PrecisionOutOfRange_ThrowsArgumentOutOfRangeException()
        {
            var poly = CreateWorldPolygon();
            Assert.Throws<ArgumentOutOfRangeException>(() => _sut.GetHashes(poly, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _sut.GetHashes(poly, 13));
        }

        [Fact]
        public void GetHashes_EmptyPolygon_ReturnsEmpty()
        {
            var empty = _factory.CreatePolygon();
            var result = _sut.GetHashes(empty, 1);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void GetHashes_InvalidPolygon_ThrowsArgumentException()
        {
            // Self‑intersecting polygon (bow‑tie)
            var coords = new Coordinate[]
            {
                new Coordinate(0, 0),
                new Coordinate(10, 10),
                new Coordinate(0, 10),
                new Coordinate(10, 0),
                new Coordinate(0, 0)
            };
            var invalidPoly = _factory.CreatePolygon(coords);

            Assert.Throws<ArgumentException>(() => _sut.GetHashes(invalidPoly, 1));
        }

        [Fact]
        public void GetHashes_WorldPolygon_Contains_ReturnsAllGeohashesForPrecision()
        {
            var world = CreateWorldPolygon();
            int precision = 1;

            var result = _sut.GetHashes(world, precision, PolygonHasher.GeohashInclusionCriteria.Contains);

            int expectedCount = 32; // 4 lat rows × 8 lon columns
            Assert.Equal(expectedCount, result.Count);
        }

        [Fact]
        public void GetHashes_SmallPolygon_ReturnsExpectedIntersectionBehavior()
        {
            double lat = 48.8588443;  // Eiffel Tower
            double lng = 2.2943506;
            int precision = 3;
            string expectedGeohash = _geohasher.Encode(lat, lng, precision);

            double offset = 0.0001;
            var coords = new Coordinate[]
            {
                new Coordinate(lng - offset, lat - offset),
                new Coordinate(lng - offset, lat + offset),
                new Coordinate(lng + offset, lat + offset),
                new Coordinate(lng + offset, lat - offset),
                new Coordinate(lng - offset, lat - offset)
            };
            var smallPoly = _factory.CreatePolygon(coords);

            var containsResult = _sut.GetHashes(smallPoly, precision,
                PolygonHasher.GeohashInclusionCriteria.Contains);
            var intersectsResult = _sut.GetHashes(smallPoly, precision,
                PolygonHasher.GeohashInclusionCriteria.Intersects);

            // Small polygon inside a cell: Contains returns nothing, Intersects returns the cell
            Assert.Empty(containsResult);
            Assert.Single(intersectsResult);
            Assert.Contains(expectedGeohash, intersectsResult);
        }

        [Fact]
        public void GetHashes_PolygonExactlyOneCell_ContainsReturnsThatCell()
        {
            string targetGeohash = "u09";
            var bbox = _geohasher.GetBoundingBox(targetGeohash);
            var poly = CreateBoundingBoxPolygon(bbox, _factory);
            int precision = targetGeohash.Length;

            var result = _sut.GetHashes(poly, precision, PolygonHasher.GeohashInclusionCriteria.Contains);

            Assert.Single(result);
            Assert.Contains(targetGeohash, result);
        }

        [Fact]
        public void GetHashes_AntimeridianCrossing_ReturnsGeohashesOnBothSides()
        {
            // Polygon spanning the IDL
            var coords = new Coordinate[]
            {
                new Coordinate(170, 10),
                new Coordinate(170, -10),
                new Coordinate(-170, -10),
                new Coordinate(-170, 10),
                new Coordinate(170, 10)
            };
            var poly = _factory.CreatePolygon(coords);
            int precision = 1;

            var result = _sut.GetHashes(poly, precision, PolygonHasher.GeohashInclusionCriteria.Intersects);

            Assert.NotEmpty(result);

            bool hasEast = false, hasWest = false;
            foreach (var hash in result)
            {
                var bbox = _geohasher.GetBoundingBox(hash);
                if (bbox.MaxLng > 0) hasEast = true;
                if (bbox.MinLng < 0) hasWest = true;
                if (hasEast && hasWest) break;
            }

            Assert.True(hasEast && hasWest, "Should have geohashes on both sides of the antimeridian");
        }

        [Fact]
        public void GetHashes_NonAntimeridianPolygon_ReturnsExpectedGeohashes()
        {
            var coords = new Coordinate[]
            {
                new Coordinate(2, 48),
                new Coordinate(2, 49),
                new Coordinate(3, 49),
                new Coordinate(3, 48),
                new Coordinate(2, 48)
            };
            var poly = _factory.CreatePolygon(coords);
            int precision = 3;
            var centerHash = _geohasher.Encode(48.5, 2.5, precision);

            var result = _sut.GetHashes(poly, precision, PolygonHasher.GeohashInclusionCriteria.Intersects);

            Assert.Contains(centerHash, result);
        }

        [Fact]
        public void GetHashes_ProgressReporting_ReportsIncreasingPercentagesAndCompletes()
        {
            var world = CreateWorldPolygon();
            int precision = 1;

            var reportedValues = new ConcurrentBag<double>();
            var completionEvent = new ManualResetEventSlim(false);

            // Use synchronous progress reporter
            var progress = new SynchronousProgress<double>(value =>
            {
                reportedValues.Add(value);
                if (value >= 1.0)
                {
                    completionEvent.Set();
                }
            });

            _sut.GetHashes(world, precision, progress: progress);

            // Wait for completion signal
            bool completed = completionEvent.Wait(TimeSpan.FromSeconds(5));
            Assert.True(completed, "Progress did not reach 100%");

            // Convert to sorted list for assertion
            var sortedValues = reportedValues.OrderBy(v => v).ToList();

            Assert.NotEmpty(sortedValues);
            Assert.True(sortedValues[0] >= 0.0);

            // Check that values are non-decreasing (already sorted, so just verify)
            for (int i = 1; i < sortedValues.Count; i++)
            {
                Assert.True(sortedValues[i] >= sortedValues[i - 1],
                    $"Progress went backward at index {i}");
            }

            // Ensure it finished at 1.0
            Assert.Equal(1.0, sortedValues.Last(), 9);
        }

        [Fact]
        public void GetHashes_WithNullProgress_DoesNotThrow()
        {
            var world = CreateWorldPolygon();
            var exception = Record.Exception(() => _sut.GetHashes(world, 1, progress: null));
            Assert.Null(exception);
        }

        [Fact]
        public void GetHashes_CancellationTokenAlreadyCanceled_ThrowsOperationCanceled()
        {
            var world = CreateWorldPolygon();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                _sut.GetHashes(world, 1, cancellationToken: cts.Token));
        }
    }
}