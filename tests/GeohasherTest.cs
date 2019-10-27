using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetTopologySuite.Geometries;
using System;
using System.Diagnostics;
using System.IO;

namespace Geohash.Tests
{
    [TestClass]
    public class GeohashBaseTests
    {
        [TestMethod]
        public void Should_Encode_WithDefaultPrecison()
        {
            var hasher = new Geohasher();

            var hash = hasher.Encode(52.5174, 13.409);

            Assert.AreEqual("u33dc0", hash);
        }

        [TestMethod]
        public void Should_Encode_WithGivenPrecision_11()
        {
            var hasher = new Geohasher();

            var hash = hasher.Encode(52.517395, 13.408813, 11);

            Assert.AreEqual("u33dc07zzzz", hash);
        }

        [TestMethod]
        public void Should_Decode_Precision6()
        {
            var hasher = new Geohasher();

            var hash = hasher.Decode("u33dc0");

            Assert.AreEqual(52.5174, Math.Round(hash.Item1, 4));
            Assert.AreEqual(13.409, Math.Round(hash.Item2, 3));
        }

        [TestMethod]
        public void Should_Decode_Precision12()
        {
            var hasher = new Geohasher();

            var hash = hasher.Decode("u33dc07zzzzx");

            Assert.AreEqual(52.51739494, Math.Round(hash.Item1, 8));
            Assert.AreEqual(13.40881297, Math.Round(hash.Item2, 8));
        }

        [TestMethod]
        public void Should_Give_Subhashes()
        {
            var hasher = new Geohasher();

            var subhashes = hasher.GetSubhashes("u33dc0");

            Assert.AreEqual(32, subhashes.Length);
        }

        [TestMethod]
        public void Should_Give_Subhashes_1()
        {
            var hasher = new Geohasher();

            var subhashes = hasher.GetSubhashes("u");

            Assert.AreEqual(32, subhashes.Length);
        }

        [TestMethod]
        public void Should_Give_Neighbors()
        {
            var hasher = new Geohasher();

            var subhashes = hasher.GetNeighbors("u33dc0");

            Assert.AreEqual("u33dc1", subhashes[Direction.North]);
            Assert.AreEqual("u33dc3", subhashes[Direction.NorthEast]);
            Assert.AreEqual("u33dc2", subhashes[Direction.East]);
            Assert.AreEqual("u33d9r", subhashes[Direction.SouthEast]);
            Assert.AreEqual("u33d9p", subhashes[Direction.South]);
            Assert.AreEqual("u33d8z", subhashes[Direction.SouthWest]);
            Assert.AreEqual("u33dbb", subhashes[Direction.West]);
            Assert.AreEqual("u33dbc", subhashes[Direction.NorthWest]);
        }

        [TestMethod]
        public void Should_Give_Neighbors_EdgeNorth()
        {
            var hasher = new Geohasher();

            var subhashes = hasher.GetNeighbors("u");

            Assert.AreEqual("h", subhashes[Direction.North]);
            Assert.AreEqual("5", subhashes[Direction.NorthWest]);
            Assert.AreEqual("j", subhashes[Direction.NorthEast]);
            Assert.AreEqual("v", subhashes[Direction.East]);
            Assert.AreEqual("s", subhashes[Direction.South]);
            Assert.AreEqual("e", subhashes[Direction.SouthWest]);
            Assert.AreEqual("t", subhashes[Direction.SouthEast]);
            Assert.AreEqual("g", subhashes[Direction.West]);
        }

        [TestMethod]
        public void Should_Give_Neighbors_EdgeWest()
        {
            var hasher = new Geohasher();

            var subhashes = hasher.GetNeighbors("9");

            Assert.AreEqual("c", subhashes[Direction.North]);
            Assert.AreEqual("b", subhashes[Direction.NorthWest]);
            Assert.AreEqual("f", subhashes[Direction.NorthEast]);
            Assert.AreEqual("d", subhashes[Direction.East]);
            Assert.AreEqual("3", subhashes[Direction.South]);
            Assert.AreEqual("2", subhashes[Direction.SouthWest]);
            Assert.AreEqual("6", subhashes[Direction.SouthEast]);
            Assert.AreEqual("8", subhashes[Direction.West]);
        }

        [TestMethod]
        public void Should_Give_Neighbors_EdgeSouth()
        {
            var hasher = new Geohasher();

            var subhashes = hasher.GetNeighbors("h");

            Assert.AreEqual("k", subhashes[Direction.North]);
            Assert.AreEqual("7", subhashes[Direction.NorthWest]);
            Assert.AreEqual("m", subhashes[Direction.NorthEast]);
            Assert.AreEqual("j", subhashes[Direction.East]);
            Assert.AreEqual("u", subhashes[Direction.South]);
            Assert.AreEqual("g", subhashes[Direction.SouthWest]);
            Assert.AreEqual("v", subhashes[Direction.SouthEast]);
            Assert.AreEqual("5", subhashes[Direction.West]);
        }

        [TestMethod]
        public void Should_Give_Neighbor()
        {
            var hasher = new Geohasher();

            Assert.AreEqual("u33dc1", hasher.GetNeighbor("u33dc0", Direction.North));
            Assert.AreEqual("u33dc3", hasher.GetNeighbor("u33dc0", Direction.NorthEast));
            Assert.AreEqual("u33dc2", hasher.GetNeighbor("u33dc0", Direction.East));
            Assert.AreEqual("u33d9r", hasher.GetNeighbor("u33dc0", Direction.SouthEast));
            Assert.AreEqual("u33d9p", hasher.GetNeighbor("u33dc0", Direction.South));
            Assert.AreEqual("u33d8z", hasher.GetNeighbor("u33dc0", Direction.SouthWest));
            Assert.AreEqual("u33dbb", hasher.GetNeighbor("u33dc0", Direction.West));
            Assert.AreEqual("u33dbc", hasher.GetNeighbor("u33dc0", Direction.NorthWest));
        }

        [TestMethod]
        public void Should_Give_Parent()
        {
            var hasher = new Geohasher();
            Assert.AreEqual("u33db", hasher.GetParent("u33dbc"));
        }

        [TestMethod]
        public void Should_Throw_Given_Incorrect_Lat()
        {
            var hasher = new Geohasher();

            Assert.ThrowsException<ArgumentException>(() => hasher.Encode(152.517395, 13.408813, 12));
        }

        [TestMethod]
        public void Should_Throw_Given_Incorrect_Lng()
        {
            var hasher = new Geohasher();

            Assert.ThrowsException<ArgumentException>(() => hasher.Encode(52.517395, 183.408813, 12));
        }


        [TestMethod]
        public async System.Threading.Tasks.Task Should_Get_Hashes_For_PolygonAsync()
        {
            var hasher = new Geohasher();

            var geometryFactory = new GeometryFactory();

            var p1 = new Coordinate() { X = 9.612350463867186, Y = 52.31141727938367 };
            var p2 = new Coordinate() { X = 9.914474487304686, Y = 52.31141727938367 };
            var p3 = new Coordinate() { X = 9.914474487304686, Y = 52.42252295423907 };
            var p4 = new Coordinate() { X = 9.612350463867186, Y = 52.42252295423907 };

            var polygon = geometryFactory.CreatePolygon(new[] { p1, p2, p3, p4, p1 });

            var result = await hasher.GetHashesAsync(polygon, 6);

            Assert.AreEqual(486, result.Count);
        }

      
        [TestMethod]
        [TestCategory("LongRunning")]
        public async System.Threading.Tasks.Task Should_Get_Hashes_For_PolygonAsync_LongRunning()
        {
            var hasher = new Geohasher();

            var geometryFactory = new GeometryFactory();

            var progess = new Progress<HashingProgress>();

            progess.ProgressChanged += (e, hp) =>
            {
                Debug.WriteLine($"Processed: {hp.HashesProcessed}, Queued: {hp.QueueSize}, Running Since: {hp.RunningSince}");
            };

            Polygon polygon = GetTestPolygon(geometryFactory);

            var result = await hasher.GetHashesAsync(polygon, 6, progress: progess);

            Assert.AreEqual(149180, result.Count);

        }

        [TestMethod]
        public async System.Threading.Tasks.Task Should_Get_Hashes_For_PolygonAsync_ContaisMode()
        {
            var hasher = new Geohasher();

            var geometryFactory = new GeometryFactory();

            var progess = new Progress<HashingProgress>();

            progess.ProgressChanged += (e, hp) =>
            {
                Debug.WriteLine($"Processed: {hp.HashesProcessed}, Queued: {hp.QueueSize}, Running Since: {hp.RunningSince}");
            };

            Polygon polygon = GetTestPolygon(geometryFactory);

            var result = await hasher.GetHashesAsync(polygon, 4, mode: Mode.Contains, progress: progess);

            Assert.AreEqual(112, result.Count);

        }

        [TestMethod]
        public async System.Threading.Tasks.Task Should_Get_Hashes_For_PolygonAsync_IntersectMode()
        {
            var hasher = new Geohasher();

            var geometryFactory = new GeometryFactory();

            var progess = new Progress<HashingProgress>();

            progess.ProgressChanged += (e, hp) =>
            {
                Debug.WriteLine($"Processed: {hp.HashesProcessed}, Queued: {hp.QueueSize}, Running Since: {hp.RunningSince}");
            };

            Polygon polygon = GetTestPolygon(geometryFactory);

            var result = await hasher.GetHashesAsync(polygon, 4, mode: Mode.Intersect, progress: progess);

            Assert.AreEqual(183, result.Count);

        }

        private static Polygon GetTestPolygon(GeometryFactory geometryFactory)
        {
            var p1 = new Coordinate() { Y = 14.87548828125, X = 51.05520733858494 };
            var p2 = new Coordinate() { Y = 12.1728515625, X = 50.17689812200107 };
            var p3 = new Coordinate() { Y = 14.26025390625, X = 48.531157010976706 };
            var p4 = new Coordinate() { Y = 15.073242187499998, X = 49.05227025601607 };

            var p5 = new Coordinate() { Y = 17.02880859375, X = 48.67645370777654 };
            var p6 = new Coordinate() { Y = 18.852539062499996, X = 49.5822260446217 };
            var p7 = new Coordinate() { Y = 14.87548828125, X = 51.05520733858494 };


            var polygon = geometryFactory.CreatePolygon(new[] { p1, p2, p3, p4, p5, p6, p7, p1 });

            return polygon;
        }
    }

   
}


    
