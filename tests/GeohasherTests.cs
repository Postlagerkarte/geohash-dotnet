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
            Assert.Throws<System.ArgumentNullException>(() => _geohasher.Decode(geohash));
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
            Assert.Throws<ArgumentNullException>(() => _geohasher.GetSubhashes(null));
            Assert.Throws<ArgumentNullException>(() => _geohasher.GetSubhashes(""));
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
