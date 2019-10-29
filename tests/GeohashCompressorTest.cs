using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Geohash.Tests
{
    [TestClass]
    public class GeohashCompressorTest
    {
        [TestMethod]
        public void SmallCompressionTest()
        {
            var compressor = new GeohashCompressor();

            var hasher = new Geohasher();

            var list = new List<string>();

            list.AddRange(hasher.GetSubhashes("ABC"));

            list.AddRange(hasher.GetSubhashes("ABF"));

            list.AddRange(hasher.GetSubhashes("AFF"));

            list.AddRange(new List<string>{ "KK", "F", "FKUVC", "FKUVX"});

            var compressed = compressor.Compress(list.ToArray());

            Assert.AreEqual(7, compressed.Count);
        }

        [TestMethod]
        public void LargeCompressionTest()
        {
            var compressor = new GeohashCompressor();

            var hasher = new Geohasher();

            var compressed = compressor.Compress(GetHashes().ToArray());

            Assert.AreEqual(152, compressed.Count);

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
