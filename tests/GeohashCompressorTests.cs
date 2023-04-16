using System.Collections.Generic;
using Xunit;

namespace Geohash.Tests
{
    public class GeohashCompressorTests
    {
        [Fact]
        public void TestCompressWithNoCompressionPossible()
        {
            // Arrange
            var geohashCompressor = new GeohashCompressor();
            var input = new[] { "abcd", "abce", "abcf", "wxyz" };
            var expected = new List<string> { "abcd", "abce", "abcf", "wxyz" };

            // Act
            var result = geohashCompressor.Compress(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TestCompressWithEmptyInput()
        {
            // Arrange
            var geohashCompressor = new GeohashCompressor();
            var input = new string[0];
            var expected = new List<string>();

            // Act
            var result = geohashCompressor.Compress(input);

            // Assert
            Assert.Equal(expected, result);
        }


        [Fact]
        public void TestCompressionWithLargeAmountOfHashes()
        {
            var compressor = new GeohashCompressor();

            var compressed = compressor.Compress(GetHashes().ToArray());

            Assert.Equal(152, compressed.Count);

        }

        [Fact]
        public void TestCompressWith32Geohashes()
        {
            // Arrange
            var geohashCompressor = new GeohashCompressor();
            var input = new[] {
                "tdnu20", "tdnu21", "tdnu22", "tdnu23", "tdnu24", "tdnu25", "tdnu26", "tdnu27", "tdnu28", "tdnu29",
                "tdnu2b", "tdnu2c", "tdnu2d", "tdnu2e", "tdnu2f", "tdnu2g", "tdnu2h", "tdnu2j", "tdnu2k", "tdnu2m",
                "tdnu2n", "tdnu2p", "tdnu2q", "tdnu2r", "tdnu2s", "tdnu2t", "tdnu2u", "tdnu2v", "tdnu2w", "tdnu2x",
                "tdnu2y", "tdnu2z"
            };
            var expected = new List<string> { "tdnu2" };

            // Act
            var result = geohashCompressor.Compress(input);

            // Assert
            Assert.Equal(expected, result);
        }

        private List<string> GetHashes()
        {
            return new List<string>()
                    {
                        "u2uk",
                        "u2fm",
                        "u2c7",
                        "u2gw",
                        "u2dx",
                        "u2gv",
                        "u2gu",
                        "u2um",
                        "u2u4",
                        "u2f7",
                        "u349",
                        "u2gh",
                        "u350",
                        "u2dq",
                        "u2dh",
                        "u2cu",
                        "u2f4",
                        "u34b",
                        "u2cd",
                        "u2u7",
                        "u2sx",
                        "u2cs",
                        "u2ff",
                        "u2c3",
                        "u2sq",
                        "u2fh",
                        "u2sw",
                        "u2c6",
                        "u2en",
                        "u31b",
                        "u2sz",
                        "u2f0",
                        "u2cg",
                        "u2fv",
                        "u2bv",
                        "u2g5",
                        "u2sh",
                        "u2bu",
                        "u2fr",
                        "u2ch",
                        "u2cm",
                        "u2gn",
                        "u2gm",
                        "u2bt",
                        "u2cx",
                        "u2c2",
                        "u2g8",
                        "u2ub",
                        "u2dr",
                        "u2u1",
                        "u2cv",
                        "u2gy",
                        "u2fe",
                        "u2cb",
                        "u29x",
                        "u2ge",
                        "u2u3",
                        "u2fz",
                        "u2gf",
                        "u2u6",
                        "u2gk",
                        "u2gd",
                        "u2dj",
                        "u2fy",
                        "u2g4",
                        "u2sj",
                        "u2v4",
                        "u2fu",
                        "u2ft",
                        "u2fw",
                        "u352",
                        "u2un",
                        "u343",
                        "u2g7",
                        "u2gr",
                        "u2uj",
                        "u2ue",
                        "u2g2",
                        "u2dk",
                        "u2gq",
                        "u2ud",
                        "u2er",
                        "u2u9",
                        "u2ct",
                        "u2ez",
                        "u2dy",
                        "u2sr",
                        "u348",
                        "u2f3",
                        "u2ey",
                        "u2g6",
                        "u2cf",
                        "u2dn",
                        "u2fd",
                        "u2ep",
                        "u2gg",
                        "u2f9",
                        "u2gb",
                        "u2ug",
                        "u2gp",
                        "u2em",
                        "u2sn",
                        "u29w",
                        "u342",
                        "u2dp",
                        "u2ce",
                        "u2ew",
                        "u34c",
                        "u2gj",
                        "u2ev",
                        "u29y",
                        "u2fg",
                        "u2cw",
                        "u2g1",
                        "u2bg",
                        "u2cz",
                        "u2v5",
                        "u2c9",
                        "u2v3",
                        "u2f8",
                        "u2fk",
                        "u2gs",
                        "u2fq",
                        "u2f2",
                        "u318",
                        "u2bs",
                        "u2uc",
                        "u2c4",
                        "u2f1",
                        "u2cc",
                        "u340",
                        "u2v0",
                        "u341",
                        "u29z",
                        "u2dm",
                        "u2dt",
                        "u2f5",
                        "u2ck",
                        "u2g9",
                        "u2sm",
                        "u2cq",
                        "u2dw",
                        "u2fb",
                        "u351",
                        "u2c8",
                        "u2u2",
                        "u29u",
                        "u2cn",
                        "u2u5",
                        "u2sp",
                        "u2fn",
                        "u2v6",
                        "u2eq",
                        "u2gc",
                        "u2uf",
                        "u2uh",
                        "u2cr",
                        "u2fp",
                        "u2v1",
                        "u2c5",
                        "u2u0",
                        "u2fs",
                        "u2c1",
                        "u2fc",
                        "u29v",
                        "u346",
                        "u2gx",
                        "u2u8",
                        "u2g0",
                        "u2us",
                        "u2cy",
                        "u2fx",
                        "u2dz",
                        "u2by",
                        "u2uu",
                        "u2fj",
                        "u34d",
                        "u2g3",
                        "u2cj",
                        "u2f6",
                        "u2gt",
                        "u2et",
                        "u2ex"
                    };
        }
    }
}
