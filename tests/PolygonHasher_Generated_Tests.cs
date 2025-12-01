using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Geohash.Tests
{
    /// <summary>
    //
    //  In the pursuit of reliable code, remember: "Testing shows the presence, not the absence, of bugs"
    //
    /// </summary>
    public class PolygonHasher_Generated_Tests
    {
        private readonly ITestOutputHelper _output;
        private readonly PolygonHasher _sut; // System Under Test
        private readonly Geohasher _geohasher; // Helper

        public PolygonHasher_Generated_Tests(ITestOutputHelper output)
        {
            _output = output;
            _sut = new PolygonHasher();
            _geohasher = new Geohasher();
        }

        [Fact]
        public void Intersects_ConcavePolygon_ShouldNotIncludeHashesInEmptySpace()
        {
            // ARRANGE: Create a "C" shape.
            // The Bounding box of this covers the empty space in the middle.
            // A naive implementation might return geohashes in the empty space.
            /*
               (0,10) _______ (10,10)
                     | ___ |
                     | | | |
                     | |___| |
               (0,0) |_______| (10,0)
            */
            var coords = new[]
            {
                new Coordinate(0, 0),
                new Coordinate(10, 0),
                new Coordinate(10, 10),
                new Coordinate(0, 10),
                new Coordinate(0, 8),
                new Coordinate(8, 8),
                new Coordinate(8, 2),
                new Coordinate(0, 2),
                new Coordinate(0, 0)
            };
            var polygon = new Polygon(new LinearRing(coords));
            int precision = 4; // Roughly 40km error, shape is 10deg (~1000km), good fit.
            // ACT
            var result = _sut.GetHashes(polygon, precision, PolygonHasher.GeohashInclusionCriteria.Intersects);
            // ASSERT
            VerifyResultsAreStrictlyValid(polygon, result, precision);
        }

        [Fact]
        public void Contains_ConcavePolygon_ShouldNotIncludePartialHashesInEmptySpace()
        {
            // Same "C" shape as above
            var coords = new[]
            {
                new Coordinate(0, 0),
                new Coordinate(10, 0),
                new Coordinate(10, 10),
                new Coordinate(0, 10),
                new Coordinate(0, 8),
                new Coordinate(8, 8),
                new Coordinate(8, 2),
                new Coordinate(0, 2),
                new Coordinate(0, 0)
            };
            var polygon = new Polygon(new LinearRing(coords));
            int precision = 4;
            // ACT
            var result = _sut.GetHashes(polygon, precision, PolygonHasher.GeohashInclusionCriteria.Contains);
            // ASSERT
            VerifyResultsAreStrictlyValidForContains(polygon, result, precision);
        }

        [Fact]
        public void Intersects_PolygonWithHole_ShouldNotIncludeHashesInHole()
        {
            // ARRANGE: A Donut
            var shell = new LinearRing(new[] {
                new Coordinate(0, 0), new Coordinate(10, 0), new Coordinate(10, 10), new Coordinate(0, 10), new Coordinate(0, 0)
            });
            // Hole in the middle
            var hole = new LinearRing(new[] {
                new Coordinate(4, 4), new Coordinate(4, 6), new Coordinate(6, 6), new Coordinate(6, 4), new Coordinate(4, 4)
            });
            var polygon = new Polygon(shell, new[] { hole });
            int precision = 5;
            // ACT
            var result = _sut.GetHashes(polygon, precision, PolygonHasher.GeohashInclusionCriteria.Intersects);
            // ASSERT
            // We explicitly check a point known to be in the hole
            var centerOfHoleHash = _geohasher.Encode(5, 5, precision);
            // If the hash is fully inside the hole, it does NOT intersect the polygon
            Assert.DoesNotContain(centerOfHoleHash, result);
            VerifyResultsAreStrictlyValid(polygon, result, precision);
        }

        [Fact]
        public void Contains_PolygonWithHole_ShouldNotIncludeHashesInHoleOrPartial()
        {
            // Same Donut
            var shell = new LinearRing(new[] {
                new Coordinate(0, 0), new Coordinate(10, 0), new Coordinate(10, 10), new Coordinate(0, 10), new Coordinate(0, 0)
            });
            var hole = new LinearRing(new[] {
                new Coordinate(4, 4), new Coordinate(4, 6), new Coordinate(6, 6), new Coordinate(6, 4), new Coordinate(4, 4)
            });
            var polygon = new Polygon(shell, new[] { hole });
            int precision = 5;
            // ACT
            var result = _sut.GetHashes(polygon, precision, PolygonHasher.GeohashInclusionCriteria.Contains);
            // ASSERT
            var centerOfHoleHash = _geohasher.Encode(5, 5, precision);
            Assert.DoesNotContain(centerOfHoleHash, result);
            VerifyResultsAreStrictlyValidForContains(polygon, result, precision);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        public void Compare_With_Mathematical_Grid_Scan(int precision)
        {
            // ARRANGE: NYC Area irregular polygon
            var polygon = new Polygon(new LinearRing(new[]
            {
                new Coordinate(-74.05, 40.65),
                new Coordinate(-73.95, 40.75), // Jagged edge
                new Coordinate(-74.00, 40.85),
                new Coordinate(-73.85, 40.85),
                new Coordinate(-73.85, 40.65),
                new Coordinate(-74.05, 40.65)
            }));
            // ACT
            var actualHashes = _sut.GetHashes(polygon, precision, PolygonHasher.GeohashInclusionCriteria.Intersects);
            // ASSERT - Use Independent Grid Scan
            // This generates a grid of center points based on lat/lon math, ignoring geohash library logic
            var expectedHashes = CalculateExpectedHashesByGridScan(polygon, precision);
            var missing = expectedHashes.Except(actualHashes).ToList();
            var extras = actualHashes.Except(expectedHashes).ToList();
            // Filter out "Borderline" cases where precision issues might cause disagreement
            // (If the NTS Intersection logic and the Geohasher logic disagree by 0.0000001)
            RemoveBorderlineCases(missing, polygon, precision, "Missing");
            RemoveBorderlineCases(extras, polygon, precision, "Extra");
            if (missing.Any() || extras.Any())
            {
                _output.WriteLine($"Missing: {string.Join(",", missing.Take(10))}");
                _output.WriteLine($"Extras: {string.Join(",", extras.Take(10))}");
            }
            Assert.Empty(missing);
            Assert.Empty(extras);
        }

        [Fact]
        public void Contains_Mode_ShouldBeStrict()
        {
            // ARRANGE
            // A box that is exactly the size of a hash, shifted slightly so it covers
            // parts of 4 hashes, but contains NONE of them fully.
            var centerLat = 40.0;
            var centerLon = -74.0;
            var precision = 6;
            // Get size of a hash at this precision
            var bbox = _geohasher.GetBoundingBox(_geohasher.Encode(centerLat, centerLon, precision));
            double width = bbox.MaxLng - bbox.MinLng;
            double height = bbox.MaxLat - bbox.MinLat;
            // Create a polygon smaller than a geohash
            var smallPoly = CreateRectangle(centerLat, centerLon, width * 0.5, height * 0.5);
            // ACT
            var result = _sut.GetHashes(smallPoly, precision, PolygonHasher.GeohashInclusionCriteria.Contains);
            // ASSERT
            // The polygon is smaller than the geohash cell, so it cannot CONTAIN a geohash cell.
            Assert.Empty(result);
        }

        [Fact]
        public void Intersects_PolygonNearNorthPole_ShouldReturnCorrectHashes()
        {
            // ARRANGE: Small rectangle near North Pole to test latitude distortion and bounding
            var centerLat = 89.99;
            var centerLon = 0.0;
            var precision = 5; // Balanced for cell size near pole (~5km lat error)
            var centerHash = _geohasher.Encode(centerLat, centerLon, precision); // Expected 'upbpb' based on encoding
            var bbox = _geohasher.GetBoundingBox(centerHash);
            double width = (bbox.MaxLng - bbox.MinLng) * 0.8; // Slightly smaller to fit within cells
            double height = (bbox.MaxLat - bbox.MinLat) * 0.8;
            var poly = CreateRectangle(centerLat, centerLon, width, height);
            // ACT
            var result = _sut.GetHashes(poly, precision, PolygonHasher.GeohashInclusionCriteria.Intersects);
            // ASSERT
            var expected = CalculateExpectedHashesByGridScan(poly, precision);
            var missing = expected.Except(result).ToList();
            var extras = result.Except(expected).ToList();
            RemoveBorderlineCases(missing, poly, precision, "Missing");
            RemoveBorderlineCases(extras, poly, precision, "Extra");
            Assert.Empty(missing);
            Assert.Empty(extras);
            VerifyResultsAreStrictlyValid(poly, result, precision);
        }

        [Fact]
        public void Contains_PolygonNearSouthPole_ShouldBeStrict()
        {
            // ARRANGE: Small polygon near South Pole, testing containment at high negative latitude
            var centerLat = -89.99;
            var centerLon = 0.0;
            var precision = 5;
            var centerHash = _geohasher.Encode(centerLat, centerLon, precision); // Expected 'h0000'
            var bbox = _geohasher.GetBoundingBox(centerHash);
            double width = (bbox.MaxLng - bbox.MinLng) * 0.4; // Very small to test strict containment
            double height = (bbox.MaxLat - bbox.MinLat) * 0.4;
            var poly = CreateRectangle(centerLat, centerLon, width, height);
            // ACT
            var result = _sut.GetHashes(poly, precision, PolygonHasher.GeohashInclusionCriteria.Contains);
            // ASSERT
            Assert.Empty(result); // Small size should not fully contain any cell near pole
                                  // Note: If implementation mishandles pole bounding, this may incorrectly return hashes
        }

        [Fact]
        public void Intersects_PolygonNearAntimeridian_ShouldHandleBoundaryWithoutWrapping()
        {
            // ARRANGE: Polygon near but not crossing antimeridian (positive side)
            var poly = new Polygon(new LinearRing(new[]
            {
        new Coordinate(179.5, 0),
        new Coordinate(179.9, 0),
        new Coordinate(179.9, 1),
        new Coordinate(179.5, 1),
        new Coordinate(179.5, 0)
    }));
            var precision = 6; // Fine-grained for boundary
                               // ACT
            var result = _sut.GetHashes(poly, precision, PolygonHasher.GeohashInclusionCriteria.Intersects);
            // ASSERT
            var expected = CalculateExpectedHashesByGridScan(poly, precision);
            var missing = expected.Except(result).ToList();
            var extras = result.Except(expected).ToList();
            RemoveBorderlineCases(missing, poly, precision, "Missing");
            RemoveBorderlineCases(extras, poly, precision, "Extra");
            Assert.Empty(missing);
            Assert.Empty(extras);
            VerifyResultsAreStrictlyValid(poly, result, precision);
            // Additional check: Ensure no hashes from negative side (e.g., near -180)
            Assert.All(result, hash => Assert.True(_geohasher.GetBoundingBox(hash).MinLng > 0)); // Positive longitude only
        }

        [Fact]
        public void Intersects_PolygonCrossingAntimeridian_ShouldDetectPlanarLimitation()
        {
            // ARRANGE: Polygon intending to cross antimeridian (small strip), but planar may interpret as large
            // Note: This tests for over-inclusion; if too many hashes, indicates no wrap-around handling
            var poly = new Polygon(new LinearRing(new[]
            {
        new Coordinate(179.9, 0),
        new Coordinate(-179.9, 0), // Jump across; planar treats as long way
        new Coordinate(-179.9, 1),
        new Coordinate(179.9, 1),
        new Coordinate(179.9, 0)
    }));
            var precision = 3; // Low to avoid timeout; full scan ~32^3 = 32k, manageable
                               // ACT
            var result = _sut.GetHashes(poly, precision, PolygonHasher.GeohashInclusionCriteria.Intersects);
            // ASSERT
            // If handling wrap (ideal), few hashes; but planar will return many (almost global band)
            // Expect <10 for correct wrap; fail if >100 indicating bug
            Assert.True(result.Count < 50, $"Too many hashes ({result.Count}); likely planar over-inclusion without antimeridian handling");
            VerifyResultsAreStrictlyValid(poly, result, precision);
        }

        [Fact]
        public void Intersects_LargePolygonWithMultipleHoles_ShouldHandleComplexity()
        {
            // ARRANGE: Large polygon spanning significant area with multiple holes
            var shell = new LinearRing(new[] {
        new Coordinate(-80, 30), new Coordinate(-70, 30), new Coordinate(-70, 40), new Coordinate(-80, 40), new Coordinate(-80, 30)
            });
                    var hole1 = new LinearRing(new[] {
                new Coordinate(-78, 32), new Coordinate(-77, 32), new Coordinate(-77, 33), new Coordinate(-78, 33), new Coordinate(-78, 32)
            });
                    var hole2 = new LinearRing(new[] {
                new Coordinate(-75, 35), new Coordinate(-74, 35), new Coordinate(-74, 36), new Coordinate(-75, 36), new Coordinate(-75, 35)
            });
            var polygon = new Polygon(shell, new[] { hole1, hole2 });
            int precision = 2; // Low for large area to keep test fast
                               // ACT
            var result = _sut.GetHashes(polygon, precision, PolygonHasher.GeohashInclusionCriteria.Intersects);
            // ASSERT
            // Explicitly check holes are excluded
            var hole1CenterHash = _geohasher.Encode(-77.5, 32.5, precision);
            var hole2CenterHash = _geohasher.Encode(-74.5, 35.5, precision);
            Assert.DoesNotContain(hole1CenterHash, result);
            Assert.DoesNotContain(hole2CenterHash, result);
            VerifyResultsAreStrictlyValid(polygon, result, precision);
        }

        [Fact]
        public void Intersects_SmallPolygon_ShouldIncludeOverlappingHashes()
        {
            // ARRANGE: Small polygon half the width and height of a geohash cell, centered at the cell's geometric center
            var initialLat = 40.0;
            var initialLon = -74.0;
            var precision = 6;
            var centerHash = _geohasher.Encode(initialLat, initialLon, precision);
            var bbox = _geohasher.GetBoundingBox(centerHash);
            double cellWidth = bbox.MaxLng - bbox.MinLng;
            double cellHeight = bbox.MaxLat - bbox.MinLat;
            double polyWidth = cellWidth * 0.5;
            double polyHeight = cellHeight * 0.5;
            double actualCenterLat = (bbox.MinLat + bbox.MaxLat) / 2;
            double actualCenterLon = (bbox.MinLng + bbox.MaxLng) / 2;
            var smallPoly = CreateRectangle(actualCenterLat, actualCenterLon, polyWidth, polyHeight);
            // ACT
            var result = _sut.GetHashes(smallPoly, precision, PolygonHasher.GeohashInclusionCriteria.Intersects);
            // ASSERT
            Assert.Contains(centerHash, result);
            Assert.Single(result); // Centered and small, intersects only one
            VerifyResultsAreStrictlyValid(smallPoly, result, precision);
        }

        [Fact]
        public void Intersects_EmptyPolygon_ReturnsEmpty()
        {
            var empty = Polygon.Empty;
            var result = _sut.GetHashes(empty, 5, PolygonHasher.GeohashInclusionCriteria.Intersects);
            Assert.Empty(result);
        }

        [Fact]
        public void Intersects_PolygonOutsideValidRange_HandlesGracefully()
        {
            // Polygon far from any geohash grid
            var polygon = CreateRectangle(0, 0, 0.00001, 0.00001);
            var result = _sut.GetHashes(polygon, 9, PolygonHasher.GeohashInclusionCriteria.Intersects);
            // Should return at least the cell containing the polygon
            Assert.NotEmpty(result);
        }


        [Fact]
        public void Intersects_SimpleRectangle_ShouldReturnExpectedHashes()
        {
            // ARRANGE: Simple 10x10 rectangle
            var polygon = CreateRectangle(0, 0, 10, 10);
            int precision = 4;
            // ACT
            var result = _sut.GetHashes(polygon, precision, PolygonHasher.GeohashInclusionCriteria.Intersects);
            // ASSERT
            var expected = CalculateExpectedHashesByGridScan(polygon, precision);
            Assert.Equal(expected.Count, result.Count); // Basic count check
            VerifyResultsAreStrictlyValid(polygon, result, precision);
        }

        [Fact]
        public void Contains_SimpleRectangle_ShouldReturnFullyContainedHashes()
        {
            // ARRANGE: Simple 10x10 rectangle
            var polygon = CreateRectangle(0, 0, 10, 10);
            int precision = 4;
            // ACT
            var result = _sut.GetHashes(polygon, precision, PolygonHasher.GeohashInclusionCriteria.Contains);
            // ASSERT
            VerifyResultsAreStrictlyValidForContains(polygon, result, precision);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(13)]
        public void InvalidPrecision_ThrowsArgumentOutOfRangeException(int precision)
        {
            // ARRANGE: Simple polygon
            var polygon = new Polygon(new LinearRing(new[]
            {
                new Coordinate(0, 0), new Coordinate(1, 0), new Coordinate(1, 1), new Coordinate(0, 1), new Coordinate(0, 0)
            }));
            // ACT & ASSERT
            Assert.Throws<ArgumentOutOfRangeException>(() => _sut.GetHashes(polygon, precision, PolygonHasher.GeohashInclusionCriteria.Intersects));
        }

        [Fact]
        public void NullPolygon_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _sut.GetHashes(null!, 5, PolygonHasher.GeohashInclusionCriteria.Intersects));
        }

        // ---------------------------------------------------------
        // Helpers & Oracles
        // ---------------------------------------------------------
        /// <summary>
        /// Validates that every returned hash actually touches the polygon.
        /// </summary>
        private void VerifyResultsAreStrictlyValid(Polygon polygon, IEnumerable<string> hashes, int precision)
        {
            foreach (var hash in hashes)
            {
                var poly = GeohashToPolygon(hash);
                Assert.True(polygon.Intersects(poly), $"Result {hash} does not intersect the polygon (False Positive)");
            }
        }

        /// <summary>
        /// Validates that every returned hash is fully contained in the polygon.
        /// </summary>
        private void VerifyResultsAreStrictlyValidForContains(Polygon polygon, IEnumerable<string> hashes, int precision)
        {
            foreach (var hash in hashes)
            {
                var poly = GeohashToPolygon(hash);
                Assert.True(polygon.Contains(poly), $"Result {hash} is not fully contained in the polygon (False Positive)");
            }
        }

        /// <summary>
        /// An independent Oracle that scans the envelope using pure math.
        /// It is slower but highly reliable.
        /// </summary>
        private HashSet<string> CalculateExpectedHashesByGridScan(Polygon polygon, int precision)
        {
            var expected = new HashSet<string>();
            var env = polygon.EnvelopeInternal;
            var sampleHash = _geohasher.Encode(env.Centre.Y, env.Centre.X, precision);
            var sampleBox = _geohasher.GetBoundingBox(sampleHash);
            double latStep = (sampleBox.MaxLat - sampleBox.MinLat);
            double lonStep = (sampleBox.MaxLng - sampleBox.MinLng);
            double scanLatStep = latStep / 3.0;
            double scanLonStep = lonStep / 3.0;
            for (double lat = env.MinY - latStep; lat <= env.MaxY + latStep; lat += scanLatStep)
            {
                for (double lon = env.MinX - lonStep; lon <= env.MaxX + lonStep; lon += scanLonStep)
                {
                    // Clamp lat to valid range and normalize lon
                    double clampedLat = Math.Max(-90.0, Math.Min(90.0, lat));
                    double normalizedLon = ((lon + 180.0) % 360.0) - 180.0;
                    var hash = _geohasher.Encode(clampedLat, normalizedLon, precision);
                    if (expected.Contains(hash)) continue;
                    var hashPoly = GeohashToPolygon(hash);
                    if (polygon.Intersects(hashPoly))
                    {
                        expected.Add(hash);
                    }
                }
            }
            return expected;
        }

        private Polygon GeohashToPolygon(string hash)
        {
            var bbox = _geohasher.GetBoundingBox(hash);
            return new Polygon(new LinearRing(new[]
            {
                new Coordinate(bbox.MinLng, bbox.MinLat),
                new Coordinate(bbox.MinLng, bbox.MaxLat),
                new Coordinate(bbox.MaxLng, bbox.MaxLat),
                new Coordinate(bbox.MaxLng, bbox.MinLat),
                new Coordinate(bbox.MinLng, bbox.MinLat)
            }));
        }

        private Polygon CreateRectangle(double lat, double lon, double width, double height)
        {
            double halfW = width / 2;
            double halfH = height / 2;
            return new Polygon(new LinearRing(new[]
            {
                new Coordinate(lon - halfW, lat - halfH),
                new Coordinate(lon - halfW, lat + halfH),
                new Coordinate(lon + halfW, lat + halfH),
                new Coordinate(lon + halfW, lat - halfH),
                new Coordinate(lon - halfW, lat - halfH)
            }));
        }

        /// <summary>
        /// Filters out false positives/negatives caused by floating point precision
        /// on the exact edge of a line.
        /// </summary>
        private void RemoveBorderlineCases(List<string> failures, Polygon p, int precision, string type)
        {
            for (int i = failures.Count - 1; i >= 0; i--)
            {
                var hash = failures[i];
                var hashPoly = GeohashToPolygon(hash);
                // Calculate intersection area
                var intersection = p.Intersection(hashPoly);
                // If the intersection is extremely small (just a touching line or point),
                // we consider it ambiguous and acceptable for the test to pass.
                // NTS 'Intersects' is true for touching borders, but Geohash logic might exclude top/right borders.
                if (intersection.Area < 1e-9)
                {
                    failures.RemoveAt(i);
                }
            }
        }
    }
}