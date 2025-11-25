using System;
using Xunit;

namespace Geohash.Tests
{

    public class DecodingTests
    {
        private Geohasher _geohasher;

        public DecodingTests()
        {
            _geohasher = new Geohasher();
        }

        [Fact]
        public void West_Neighbor_Should_Wrap_Antimeridian_Correctly()
        {
            // 1. Setup
            var hasher = new Geohasher();

            // This geohash is physically located at Lat [0, 45], Lng [-180, -135]
            // Its left edge IS the International Date Line (-180).
            string startHash = "8";

            // We ask for the neighbor to the WEST. 
            // We expect to cross -180 and land in +179 territory (e.g., geohash "r..." or "p...")
            string westNeighbor = hasher.GetNeighbor(startHash, Direction.West);

            // Decode to verify coordinates
            var (lat, lng) = hasher.Decode(westNeighbor);

            // 3. Assert
            // The neighbor to the west of -180 should have a POSITIVE longitude (approx +157 for this precision).
            Assert.True(lng > 0, $"Expected positive Longitude (Eastern Hemisphere) when moving West of -180. Got {lng} (Hash: {westNeighbor})");
        }

        [Fact]
        public void Encode_Longitude180And_Negative180_ShouldBeEquivalentOrAdjacent()
        {
            var hash180 = _geohasher.Encode(0, 180, 6);
            var hashNeg180 = _geohasher.Encode(0, -180, 6);

            // These should be the same cell (both are the antimeridian)
            // or immediately adjacent cells
            var (lat1, lon1) = _geohasher.Decode(hash180);
            var (lat2, lon2) = _geohasher.Decode(hashNeg180);

            // The center longitudes should be very close (within one cell width)
            double lonDiff = Math.Abs(lon1 - lon2);
            if (lonDiff > 180) lonDiff = 360 - lonDiff;  // Handle wraparound

            Assert.True(lonDiff < 1.0,
                $"Hashes for lon=180 and lon=-180 should be close, but centers are {lon1} and {lon2}");
        }

        [Fact]
        public void GetParent_SingleCharGeohashGetParent_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() => _geohasher.GetParent("s"));
        }

        [Fact]
        public void NorthThenSouth_ShouldReturnToSameCell()
        {
            // Use a cell near (but not at) the pole where north goes over the pole
            string original = _geohasher.Encode(89.5, 45, 3);
            string north = _geohasher.GetNeighbor(original, Direction.North);
            string backSouth = _geohasher.GetNeighbor(north, Direction.South);

            // Going north then south should bring us back to the original cell (or very close)
            // Due to the bounce bug, we end up in a completely different location
            var (origLat, origLon) = _geohasher.Decode(original);
            var (backLat, backLon) = _geohasher.Decode(backSouth);

            // The latitudes should be within a few degrees of each other
            Assert.True(Math.Abs(origLat - backLat) < 10,
                $"Expected to return near original latitude {origLat}, but got {backLat}");
        }

        [Fact]
        public void South_NearSouthPole_ShouldBounceBackToSouthernHemisphere()
        {
            string nearPole = _geohasher.Encode(-89.9, 0, 4);
            string southNeighbor = _geohasher.GetNeighbor(nearPole, Direction.South);

            var (lat, lon) = _geohasher.Decode(southNeighbor);

            // After bouncing off the pole, latitude should still be negative (southern hemisphere)
            // The bug makes it positive
            Assert.True(lat < 0, $"South neighbor of near-pole hash should be in southern hemisphere, but got lat={lat}");
        }

        [Fact]
        public void North_NearNorthPole_ShouldBounceBackToNorthernHemisphere()
        {
            // Create a geohash very close to the north pole
            // "zzzzzz" is at approximately (84.7, 179.6)
            // Its north neighbor should still be in the northern hemisphere

            string nearPole = _geohasher.Encode(89.9, 0, 4);
            string northNeighbor = _geohasher.GetNeighbor(nearPole, Direction.North);

            var (lat, lon) = _geohasher.Decode(northNeighbor);

            // After bouncing off the pole, latitude should still be positive (northern hemisphere)
            // The bug makes it negative
            Assert.True(lat > 0, $"North neighbor of near-pole hash should be in northern hemisphere, but got lat={lat}");
        }

        [Theory]
        [InlineData(-360, 0)]      // -360 should normalize to 0
        [InlineData(-540, -180)]   // -540 should normalize to -180 (or 180)
        [InlineData(-270, 90)]     // -270 should normalize to 90
        public void NormalizeLongitude_NegativeValues_ShouldWrapCorrectly(double input, double expected)
        {
            // We can't test private method directly, but we can test via Encode
            // Encoding at longitude -360 should be same as longitude 0

            var hash1 = _geohasher.Encode(0, input, 6);
            var hash2 = _geohasher.Encode(0, expected, 6);

            Assert.Equal(hash2, hash1);
        }

        [Theory]
        [InlineData("ezs434y", 42.59880066, -5.57212830)] // León, Spain
        [InlineData("9q8yyk8", 37.77442932, -122.41996765)] // San Francisco, USA
        [InlineData("u4png7x", 57.45643616, 9.99687195)] // Hjørring, Denmark
        public void Decode_Should_Return_CorrectCoordinates(string geohash, double expectedLat, double expectedLon)
        {
            (double lat, double lon) = _geohasher.Decode(geohash);

            Assert.Equal(expectedLat, lat, 5);
            Assert.Equal(expectedLon, lon, 5);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Decode_NullOrEmptyGeohash_ThrowsArgumentNullException(string geohash)
        {
            Assert.Throws<System.ArgumentException>(() => _geohasher.Decode(geohash));
        }

        [Theory]
        [InlineData("1234567890123")]
        public void Decode_TooLongGeohash_ThrowsArgumentException(string geohash)
        {
            Assert.Throws<System.ArgumentException>(() => _geohasher.Decode(geohash));
        }

        [Theory]
        [InlineData("invalid1")]
        [InlineData("a?b%c^d")]
        public void Decode_InvalidCharactersInGeohash_ThrowsArgumentException(string geohash)
        {
            Assert.Throws<System.ArgumentException>(() => _geohasher.Decode(geohash));
        }

        [Theory]
        [InlineData(0, 0, 6, "s00000")]
        [InlineData(40.7128, -74.0060, 6, "dr5reg")]
        [InlineData(-33.8688, 151.2093, 6, "r3gx2f")]
        [InlineData(35.6895, 139.6917, 6, "xn774c")]
        [InlineData(-22.9083, -43.1964, 6, "75cm9j")]
        [InlineData(-33.9249, 18.4241, 6, "k3vp52")]
        [InlineData(89.99999999, 0, 6, "upbpbp")]
        [InlineData(0, 179.99999999, 6, "xbpbpb")]
        [InlineData(40.390943, -75.937500, 12, "dr4jb0bn2180")]
        public void Encode_ValidCoordinates_ReturnsExpectedGeohash(double latitude, double longitude, int precision, string expectedGeohash)
        {
            string actualGeohash = _geohasher.Encode(latitude, longitude, precision);
            Assert.Equal(expectedGeohash, actualGeohash);
        }

        [Fact]
        public void Encode_Should_Throw_Exception_For_Invalid_Precision()
        {
            Assert.Throws<ArgumentException>(() => _geohasher.Encode(0, 0, 13));
        }

        [Theory]
        [InlineData("s000", 32)]
        [InlineData("dr5", 32)]
        [InlineData("r3gx", 32)]
        [InlineData("upb", 32)]
        public void GetSubhashes_Should_Return_32_Subhashes(string geohash, int expectedCount)
        {
            var subhashes = _geohasher.GetSubhashes(geohash);
            Assert.Equal(expectedCount, subhashes.Length);
        }

        [Fact]
        public void GetSubhashes_Should_Throw_ArgumentNullException_When_Null_Or_Empty()
        {
            Assert.Throws<ArgumentException>(() => _geohasher.GetSubhashes(null));
            Assert.Throws<ArgumentException>(() => _geohasher.GetSubhashes(""));
        }

        [Fact]
        public void GetSubhashes_Should_Throw_ArgumentException_When_Length_Exceeds_12()
        {
            Assert.Throws<ArgumentException>(() => _geohasher.GetSubhashes("abcdefghijklm"));
        }

        [Fact]
        public void GetSubhashes_Should_Generate_Valid_Subhashes()
        {
            string geohash = "s000";
            string[] expectedSubhashes = {
                "s0000", "s0001", "s0002", "s0003", "s0004", "s0005", "s0006", "s0007",
                "s0008", "s0009", "s000b", "s000c", "s000d", "s000e", "s000f", "s000g",
                "s000h", "s000j", "s000k", "s000m", "s000n", "s000p", "s000q", "s000r",
                "s000s", "s000t", "s000u", "s000v", "s000w", "s000x", "s000y", "s000z"
            };

            var subhashes = _geohasher.GetSubhashes(geohash);

            for (int i = 0; i < expectedSubhashes.Length; i++)
            {
                Assert.True(expectedSubhashes[i] == subhashes[i], $"Mismatch at index {i}: Expected {expectedSubhashes[i]}, but got {subhashes[i]}.");
            }
        }

    }
}
