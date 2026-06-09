using System;
using System.Collections.Generic;

namespace Geohash
{
    /// <summary>
    /// Compresses a set of geohashes into the minimal set of cells covering the same area.
    /// </summary>
    public class GeohashCompressor
    {
        /// <summary>
        /// Compresses the input geohashes:
        /// 1. Truncates hashes longer than <paramref name="maxLevel"/> and removes duplicates.
        /// 2. Prunes redundancies (if "a" and "ab" both exist, "ab" is removed).
        /// 3. Merges complete sibling groups bottom-up (all 32 children of "a" → "a"),
        ///    cascading until no merge is possible or <paramref name="minLevel"/> is reached.
        /// </summary>
        /// <returns>The compressed set, sorted ordinally.</returns>
        public List<string> Compress(IEnumerable<string> geohashes, int minLevel = 1, int maxLevel = 12)
        {
            if (geohashes == null) throw new ArgumentNullException(nameof(geohashes));
            if (minLevel < 1 || minLevel > Geohasher.MaxPrecision)
                throw new ArgumentOutOfRangeException(nameof(minLevel));
            if (maxLevel < minLevel || maxLevel > Geohasher.MaxPrecision)
                throw new ArgumentOutOfRangeException(nameof(maxLevel));

            // 1. Normalize: truncate + dedupe + validate.
            var inputSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var hash in geohashes)
            {
                if (string.IsNullOrEmpty(hash)) continue;
                string h = hash.Length > maxLevel ? hash.Substring(0, maxLevel) : hash;
                Geohasher.ValidateGeohash(h); // garbage input would corrupt the 32-sibling merge
                inputSet.Add(h);
            }

            if (inputSet.Count == 0) return new List<string>();

            // 2. Prune redundancies in a single linear pass over the ordinally-sorted list.
            //    Ordinal sort guarantees that if any kept element is a prefix of the current
            //    element, it is exactly the most recently kept element.
            var sorted = new List<string>(inputSet);
            sorted.Sort(StringComparer.Ordinal);

            int maxDepth = 0;
            var byLength = new List<string>[maxLevel + 1];
            string lastKept = null;

            foreach (var h in sorted)
            {
                if (lastKept != null &&
                    h.Length > lastKept.Length &&
                    string.CompareOrdinal(h, 0, lastKept, 0, lastKept.Length) == 0)
                {
                    continue; // covered by an ancestor already in the result
                }

                lastKept = h;
                (byLength[h.Length] ??= new List<string>()).Add(h);
                if (h.Length > maxDepth) maxDepth = h.Length;
            }

            // 3. Bottom-up merge: a parent with all 32 children present replaces them.
            //    Merged parents land in level (len - 1) and cascade automatically.
            for (int len = maxDepth; len > minLevel; len--)
            {
                var level = byLength[len];
                if (level == null || level.Count < 32) continue;

                var childCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var h in level)
                {
                    string parent = h.Substring(0, len - 1);
                    childCounts.TryGetValue(parent, out int count);
                    childCounts[parent] = count + 1;
                }

                HashSet<string> fullParents = null;
                foreach (var kvp in childCounts)
                {
                    // Inputs are deduplicated & validated, so 32 children == complete cell.
                    if (kvp.Value == 32)
                        (fullParents ??= new HashSet<string>(StringComparer.Ordinal)).Add(kvp.Key);
                }

                if (fullParents == null) continue;

                level.RemoveAll(h => fullParents.Contains(h.Substring(0, len - 1)));
                (byLength[len - 1] ??= new List<string>()).AddRange(fullParents);
            }

            // Flatten + sort.
            var result = new List<string>();
            foreach (var level in byLength)
                if (level != null) result.AddRange(level);

            result.Sort(StringComparer.Ordinal);
            return result;
        }
    }
}