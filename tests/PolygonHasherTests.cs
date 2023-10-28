using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Geohash.Tests
{
    public class PolygonHasherTests
    {
        private readonly PolygonHasher _polygonHasher = new PolygonHasher();

        [Fact]
        public void TestLargePolygonCoverageGaps()
        {
            // Create a large rectangle
            Polygon largeRectangle = new Polygon(new LinearRing(new Coordinate[] {
                new Coordinate(-45, -45),
                new Coordinate(-45, 45),
                new Coordinate(45, 45),
                new Coordinate(45, -45),
                new Coordinate(-45, -45)
            }));

            // Use the PolygonHasher to get geohashes 
            PolygonHasher hasher = new PolygonHasher();
            HashSet<string> originalGeohashes = hasher.GetHashes(largeRectangle, 5);

            // Now let's check a smaller rectangle at the top-right corner of the large rectangle
            Polygon smallRectangle = new Polygon(new LinearRing(new Coordinate[] {
                new Coordinate(44, 44),
                new Coordinate(44, 45),
                new Coordinate(45, 45),
                new Coordinate(45, 44),
                new Coordinate(44, 44)
            }));

            PolygonHasher smallRectangleHasher = new PolygonHasher();
            HashSet<string> smallRectangleGeohashes = smallRectangleHasher.GetHashes(smallRectangle, 5);

            // Check that the geohashes from the smaller rectangle are all present in the original geohashes
            Assert.All(smallRectangleGeohashes, gh => Assert.Contains(gh, originalGeohashes));
        }


        [Fact]
        public async System.Threading.Tasks.Task Should_Get_Hashes_For_PolygonAsync_IntersectMode()
        {

            var geometryFactory = new GeometryFactory();

            Polygon polygon = GetLargePolygon(geometryFactory);

            var hashed = _polygonHasher.GetHashes(polygon, 4, PolygonHasher.GeohashInclusionCriteria.Contains);

            List<string> expected = new List<string>
            {
                "u2fr", "u2ge", "u2ct", "u2ew", "u2ck", "u2dn", "u2f0", "u2fb",
                "u2gn", "u2cc", "u2u3", "u2f5", "u2cj", "u2cv", "u2cf", "u2gt",
                "u2fc", "u2fw", "u2fp", "u2c6", "u2u5", "u2cw", "u2cu", "u2ch",
                "u2fv", "u2cs", "u2gc", "u2f7", "u2f2", "u2u4", "u2gp", "u2fe",
                "u2u8", "u342", "u2fs", "u2sn", "u2fu", "u2gj", "u2sr", "u2ff",
                "u2u6", "u2gf", "u2gu", "u2fh", "u2fx", "u2g2", "u2u1", "u2gd",
                "u2u9", "u2gv", "u2fn", "u2dp", "u2gg", "u2g8", "u2gk", "u29z",
                "u2f9", "u2ft", "u2gs", "u2fd", "u2g4", "u2fz", "u2fq", "u2cg",
                "u2ez", "u2g1", "u2fy", "u348", "u34b", "u2u7", "u2ue", "u2f3",
                "u2g7", "u2gm", "u2g9", "u2ud", "u2dr", "u2er", "u2c9", "u2cd",
                "u2cy", "u2fk", "u2f4", "u2ep", "u2dj", "u2f6", "u2uh", "u2uf",
                "u2gh", "u2ex", "u2u2", "u2uc", "u2f8", "u2cb", "u2u0", "u2ce",
                "u2gb", "u2g5", "u2f1", "u2gq", "u2fg", "u2c7", "u2cm", "u2g0",
                "u2sp", "u2cz", "u2fj", "u2dq", "u2g3", "u2ey", "u2fm", "u2g6"
            };


            // Sort both collections
            var sortedHashed = hashed.OrderBy(x => x).ToList();
            var sortedExpected = expected.OrderBy(x => x).ToList();

            // Compare the two collections with Xunit
            Assert.Equal(sortedExpected, sortedHashed);
        }

        [Fact]
        public void GetHashes_ReturnsExpectedGeohashes()
        {
            // Arrange
            var geometryFactory = new GeometryFactory();
            Polygon polygon = GetSmallPolygon(geometryFactory);
            var expectedGeohashes = new HashSet<string>
            {
                "u09t", "u09w"
            };

            // Act
            var result = _polygonHasher.GetHashes(polygon, 4);

            // Assert
            Assert.Equal(expectedGeohashes, result);
        }

        private static Polygon GetLargePolygon(GeometryFactory geometryFactory)
        {
            var p1 = new Coordinate() { X = 14.87548828125, Y = 51.05520733858494 };
            var p2 = new Coordinate() { X = 12.1728515625, Y = 50.17689812200107 };
            var p3 = new Coordinate() { X = 14.26025390625, Y = 48.531157010976706 };
            var p4 = new Coordinate() { X = 15.073242187499998, Y = 49.05227025601607 };

            var p5 = new Coordinate() { X = 17.02880859375, Y = 48.67645370777654 };
            var p6 = new Coordinate() { X = 18.852539062499996, Y = 49.5822260446217 };
            var p7 = new Coordinate() { X = 14.87548828125, Y = 51.05520733858494 };


            var polygon = geometryFactory.CreatePolygon(new[] { p1, p2, p3, p4, p5, p6, p7, p1 });

            return polygon;
        }

        private static Polygon GetSmallPolygon(GeometryFactory geometryFactory)
        {
            var p1 = new Coordinate(2.2, 48.9);
            var p2 = new Coordinate(2.3, 48.9);
            var p3 = new Coordinate(2.3, 48.8);
            var p4 = new Coordinate(2.2, 48.8);

            var polygon = geometryFactory.CreatePolygon(new[] { p1, p2, p3, p4, p1 });

            return polygon;
        }

        [Fact]
        public void TestLargePolygon()
        {
            Coordinate[] coordinates = new[]
            {
                new Coordinate(-99.1795917, 19.432134),
                new Coordinate(-99.1656847, 19.429034),
                new Coordinate(-99.1776492, 19.414236),
                new Coordinate(-99.1795917, 19.432134)  // Closing the polygon with the starting point
            };

            Polygon polygon = new Polygon(new LinearRing(coordinates));

            var hashed = _polygonHasher.GetHashes(polygon, 7, PolygonHasher.GeohashInclusionCriteria.Contains);

            var expected = new List<string>()
            {
                "9g3qx26", "9g3qx2b", "9g3qx0u", "9g3qrpw", "9g3qx2d", "9g3qx1p", "9g3qx2c", "9g3qx2g", "9g3qx0p", "9g3qx0w",
                "9g3qrpt", "9g3qx0z", "9g3qx22", "9g3qrpn", "9g3qx23", "9g3qx0v", "9g3qrr8", "9g3qx0t", "9g3qx21", "9g3qx20",
                "9g3qrpj", "9g3qx1h", "9g3qx28", "9g3qx29", "9g3qx1j", "9g3qrpm", "9g3qrpx", "9g3qx0n", "9g3qrpy", "9g3qx0m",
                "9g3qx0q", "9g3qrpr", "9g3qrrb", "9g3qx2f", "9g3qrpq", "9g3qx0y", "9g3qx0x", "9g3qrpv", "9g3qx0j", "9g3qx2e",
                "9g3qx1n", "9g3qrnv", "9g3qx0r", "9g3qrpz"
            };

            // Sort both collections
            var sortedHashed = hashed.OrderBy(x => x).ToList();
            var sortedExpected = expected.OrderBy(x => x).ToList();

            // Compare the two collections with Xunit
            Assert.Equal(sortedExpected, sortedHashed);
        }

    }
}
