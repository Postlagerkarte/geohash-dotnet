using System;
using System.Linq;
using Geohash;
using NetTopologySuite.Geometries;
using Xunit;

using Inclusion = Geohash.GeohashInclusionCriteria;
using PolygonInclusion = Geohash.PolygonHasher.GeohashInclusionCriteria;

namespace Geohash.Tests
{
    public sealed class EncodingRegressionTests
    {
        private readonly Geohasher _hasher = new Geohasher();

        [Theory]
        // Immediately west of the prime meridian.
        [InlineData(-1e-15, "e")]

        // Immediately west of the 45-degree cell boundary.
        [InlineData(44.99999999999999, "s")]

        // Immediately west of the antimeridian.
        [InlineData(179.99999999999997, "x")]
        public void Encode_PreservesLongitudeOnItsOriginalSideOfBoundary(
            double longitude,
            string expected)
        {
            string actual = _hasher.Encode(
                latitude: 0,
                longitude: longitude,
                precision: 1);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Encode_LargeFiniteLongitude_WrapsCorrectly()
        {
            // For this represented double:
            // 1e20 % 360 == 280, which normalizes to -80 degrees.
            string actual = _hasher.Encode(
                latitude: 0,
                longitude: 1e20,
                precision: 1);

            Assert.Equal("d", actual);
        }
    }

    public sealed class CompressionRegressionTests
    {
        private readonly GeohashCompressor _compressor =
            new GeohashCompressor();

        [Fact]
        public void Compress_CaseVariantsMustNotCountAsDifferentChildren()
        {
            // Sixteen distinct child cells, each supplied twice with
            // different casing. This is NOT a complete group of 32 children.
            const string childCharacters = "bcdefghjkmnpqrst";

            string[] input = childCharacters
                .SelectMany(c => new[]
                {
                    "0" + c,
                    "0" + char.ToUpperInvariant(c)
                })
                .ToArray();

            Assert.Equal(32, input.Length);
            Assert.Equal(
                16,
                input.Select(h => h.ToLowerInvariant()).Distinct().Count());

            var result = _compressor.Compress(input);

            // Expand any precision-1 output back to precision 2 so we
            // compare covered cells, not the output's formatting/casing.
            var hasher = new Geohasher();

            string[] actualCells = result
                .Select(h => h.ToLowerInvariant())
                .SelectMany(h => h.Length == 1
                    ? hasher.GetSubhashes(h)
                    : new[] { h })
                .Distinct(StringComparer.Ordinal)
                .OrderBy(h => h, StringComparer.Ordinal)
                .ToArray();

            string[] expectedCells = input
                .Select(h => h.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(h => h, StringComparer.Ordinal)
                .ToArray();

            // Current code merges to "0", expanding coverage to 32 cells.
            Assert.Equal(expectedCells, actualCells);
        }

        [Fact]
        public void Compress_CaseEquivalentHashes_AreDeduplicated()
        {
            var result = _compressor.Compress(new[] { "U", "u" });

            Assert.Single(result);
            Assert.Equal("u", result[0].ToLowerInvariant());
        }

        [Fact]
        public void Compress_AncestorPruning_IsCaseInsensitive()
        {
            var result = _compressor.Compress(new[] { "U", "u0" });

            // "U" and "u" represent the same parent cell.
            Assert.Single(result);
            Assert.Equal("u", result[0].ToLowerInvariant());
        }
    }

    public sealed class RadiusCoverageRegressionTests
    {
        private readonly RadiusHasher _radiusHasher = new RadiusHasher();
        private readonly Geohasher _geohasher = new Geohasher();

        private static double RadiusForDegrees(double angularDegrees)
        {
            return RadiusHasher.EarthRadiusMeters
                * (angularDegrees * Math.PI / 180.0);
        }

        [Fact]
        public void Intersects_IncludesCellWithNearerPointTowardSouthPole()
        {
            double radius = RadiusForDegrees(100);

            // "p": latitude [-90, -45], longitude [135, 180].
            BoundingBox cell = _geohasher.GetBoundingBox("p");

            // This is an interior witness, not merely a shared pole.
            const double witnessLatitude = -80;
            const double witnessLongitude = 150;

            Assert.True(cell.Contains(
                witnessLatitude,
                witnessLongitude));

            double witnessDistance = RadiusHasher.GetDistanceMeters(
                0, 0,
                witnessLatitude, witnessLongitude);

            // Approximately 98.65 angular degrees, inside the 100-degree cap.
            Assert.True(
                witnessDistance < radius,
                "The witness point must be inside the query circle.");

            var result = _radiusHasher.GetHashes(
                latitude: 0,
                longitude: 0,
                radiusMeters: radius,
                geohashPrecision: 1,
                criteria: Inclusion.Intersects);

            // Current code incorrectly excludes this cell.
            Assert.Contains("p", result);
        }

        [Fact]
        public void Contains_RejectsCellWithInteriorEdgePointOutsideCircle()
        {
            const double centerLatitude = 20;
            const double centerLongitude = 0;
            double radius = RadiusForDegrees(137);

            // "q": latitude [-45, 0], longitude [90, 135].
            BoundingBox cell = _geohasher.GetBoundingBox("q");

            // Establish why checking only the corners is insufficient:
            // all four corners are inside the query circle.
            var corners = new[]
            {
                (Latitude: cell.MinLat, Longitude: cell.MinLng),
                (Latitude: cell.MinLat, Longitude: cell.MaxLng),
                (Latitude: cell.MaxLat, Longitude: cell.MinLng),
                (Latitude: cell.MaxLat, Longitude: cell.MaxLng)
            };

            foreach (var corner in corners)
            {
                double distance = RadiusHasher.GetDistanceMeters(
                    centerLatitude, centerLongitude,
                    corner.Latitude, corner.Longitude);

                Assert.True(
                    distance < radius,
                    "All corners must be inside for this reproduction.");
            }

            // A point inside the eastern edge is farther away than
            // any corner: approximately 138.4 angular degrees.
            const double witnessLatitude = -27.24;
            const double witnessLongitude = 135;

            Assert.True(cell.Contains(
                witnessLatitude,
                witnessLongitude));

            double witnessDistance = RadiusHasher.GetDistanceMeters(
                centerLatitude, centerLongitude,
                witnessLatitude, witnessLongitude);

            Assert.True(
                witnessDistance > radius,
                "The edge witness must be outside the query circle.");

            var result = _radiusHasher.GetHashes(
                latitude: centerLatitude,
                longitude: centerLongitude,
                radiusMeters: radius,
                geohashPrecision: 1,
                criteria: Inclusion.Contains);

            // Current code incorrectly includes this cell.
            Assert.DoesNotContain("q", result);
        }

        [Theory]
        [InlineData(0.0, 0.0)]      // Equator and prime meridian.
        [InlineData(0.0, 12.34)]    // Equator only.
        [InlineData(12.34, 0.0)]    // Prime meridian only.
        [InlineData(90.0, 12.34)]   // North pole.
        [InlineData(-90.0, 12.34)]  // South pole.
        public void ZeroRadius_IntersectsAtLeastTheEncodedCell(
            double latitude,
            double longitude)
        {
            const int precision = 6;

            string containingCell = _geohasher.Encode(
                latitude, longitude, precision);

            var result = _radiusHasher.GetHashes(
                latitude: latitude,
                longitude: longitude,
                radiusMeters: 0,
                geohashPrecision: precision,
                criteria: Inclusion.Intersects);

            // This does not require choosing between one owning cell
            // and all boundary-touching cells. Either interpretation
            // must include the cell selected by Encode.
            Assert.Contains(containingCell, result);
        }
    }

    // These tests adopt recommended validation behavior:
    // - reject non-finite coordinates;
    // - reject polygon latitude outside [-90, 90];
    // - reject undefined enum values.
    //
    // Keep these separate because they establish stricter public contracts.
    public sealed class ProposedInputValidationTests
    {
        [Theory]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void Encode_RejectsInfiniteLongitude(double longitude)
        {
            var hasher = new Geohasher();

            // Allows ArgumentException or an argument-specific subclass.
            Assert.ThrowsAny<ArgumentException>(() =>
            {
                hasher.Encode(0, longitude, 1);
            });
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(8)]
        [InlineData(99)]
        public void GetNeighbor_RejectsUndefinedDirection(int direction)
        {
            var hasher = new Geohasher();

            // Current code throws IndexOutOfRangeException instead.
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                hasher.GetNeighbor("u", (Direction)direction);
            });
        }

        [Fact]
        public void PolygonCoverage_RejectsLatitudeOutsideGeographicDomain()
        {
            var factory = new GeometryFactory();

            // Valid as a planar NTS polygon, but invalid geographically.
            Polygon polygon = factory.CreatePolygon(new[]
            {
                new Coordinate(0, 100),
                new Coordinate(1, 100),
                new Coordinate(1, 101),
                new Coordinate(0, 101),
                new Coordinate(0, 100)
            });

            Assert.True(polygon.IsValid);

            var hasher = new PolygonHasher();

            // Current code generates unrelated polar-row hashes.
            Assert.ThrowsAny<ArgumentException>(() =>
            {
                hasher.GetHashes(
                    polygon,
                    geohashPrecision: 2,
                    geohashInclusionCriteria: PolygonInclusion.Intersects);
            });
        }
    }
}