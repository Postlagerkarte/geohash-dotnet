using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace Geohash.Tests
{
    public class PolygonHasherTests
    {
        private readonly PolygonHasher _polygonHasher = new PolygonHasher();

        [Fact]
         public async System.Threading.Tasks.Task Should_Get_Hashes_For_PolygonAsync_IntersectMode()
        {

            var geometryFactory = new GeometryFactory();
     
            Polygon polygon = GetLargePolygon(geometryFactory);

            var result =  _polygonHasher.GetHashes(polygon, 4);

            Assert.Equal(183, result.Count);

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

    }
}
