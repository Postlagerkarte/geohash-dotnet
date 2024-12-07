using System.Collections.Generic;
using System.Linq;

namespace Geohash
{
    /// <summary>
    /// Compresses an array of geohashes to the smallest possible set covering the same area.
    /// </summary>
    public class GeohashCompressor
    {
        private readonly Geohasher _geohasher = new Geohasher();

        /// <summary>
        /// Compresses geohashes to the smallest possible set covering the same area.
        /// </summary>
        /// <param name="hashes">Input geohashes to compress.</param>
        /// <param name="minLevel">Minimum allowed geohash length (default: 1).</param>
        /// <param name="maxLevel">Maximum allowed geohash length (default: 12).</param>
        /// <returns>Compressed list of geohashes.</returns>
        public List<string> Compress(string[] hashes, int minLevel = 1, int maxLevel = 12)
        {
            var geohashSet = new HashSet<string>(hashes);
            var compressedGeohashes = new HashSet<string>();

            while (true)
            {
                var newCompressedGeohashes = GetCompressedGeohashes(geohashSet, minLevel, maxLevel);
                if (compressedGeohashes.SetEquals(newCompressedGeohashes))
                {
                    break;
                }
                compressedGeohashes = newCompressedGeohashes;
                geohashSet = new HashSet<string>(compressedGeohashes); // Update geohashSet to reflect compressed state
            }

            return compressedGeohashes.ToList();
        }

        private HashSet<string> GetCompressedGeohashes(HashSet<string> geohashSet, int minLevel, int maxLevel)
        {
            var compressedGeohashes = new HashSet<string>();

            foreach (var geohash in geohashSet)
            {
                if (geohash.Length < minLevel)
                {
                    compressedGeohashes.Add(geohash); // Add short geohashes as is
                    continue;
                }

                var parentGeohash = GetParentGeohash(geohash);
                if (parentGeohash != null && AreAllSubhashesPresent(geohashSet, parentGeohash))
                {
                    compressedGeohashes.Add(parentGeohash);
                }
                else
                {
                    compressedGeohashes.Add(LimitGeohashLength(geohash, maxLevel));
                }
            }

            return compressedGeohashes;
        }

        private string GetParentGeohash(string geohash) => geohash.Length > 1 ? geohash.Substring(0, geohash.Length - 1) : null;

        private bool AreAllSubhashesPresent(HashSet<string> geohashSet, string parentGeohash)
            => _geohasher.GetSubhashes(parentGeohash).All(geohashSet.Contains);

        private string LimitGeohashLength(string geohash, int maxLength) => geohash.Length > maxLength ? geohash.Substring(0, maxLength) : geohash;
    }
}
