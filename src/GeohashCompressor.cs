using System;
using System.Collections.Generic;
using System.Linq;

namespace Geohash
{
    /// <summary>
    /// Compresses sets of geohashes into the minimal set of nodes covering the same surface area.
    /// </summary>
    public class GeohashCompressor
    {
        /// <summary>
        /// Compresses the input geohashes.
        /// Logic:
        /// 1. Normalize (truncate to maxLevel).
        /// 2. Prune redundancies (if "a" and "ab" exist, "ab" is removed).
        /// 3. Merge siblings (if all 32 children of "a" exist, replace with "a").
        /// </summary>
        public List<string> Compress(IEnumerable<string> geohashes, int minLevel = 1, int maxLevel = 12)
        {
            if (geohashes == null) throw new ArgumentNullException(nameof(geohashes));

            // 1. Pre-process: Filter empty, Truncate length, Remove strict duplicates
            var inputSet = new HashSet<string>();
            foreach (var hash in geohashes)
            {
                if (string.IsNullOrEmpty(hash)) continue;

                // Truncate if exceeding limits
                string h = hash.Length > maxLevel ? hash.Substring(0, maxLevel) : hash;
                inputSet.Add(h);
            }

            // 2. Prune Redundancies (Top-Down)
            // e.g., Input: {"y0", "y01", "z2"} -> "y01" is inside "y0", so remove "y01".
            // We sort by length so we process parents ("y0") before children ("y01").
            var sortedCandidates = inputSet.OrderBy(x => x.Length).ToList();
            var prunedSet = new HashSet<string>();

            foreach (var hash in sortedCandidates)
            {
                // Check if we have already processed a parent of this hash
                bool isRedundant = false;
                for (int i = 1; i < hash.Length; i++)
                {
                    // Check prefixes: e.g. for "y01", check "y", "y0"
                    if (prunedSet.Contains(hash.Substring(0, i)))
                    {
                        isRedundant = true;
                        break;
                    }
                }

                // If no parent exists in the set, this is a significant geohash
                if (!isRedundant)
                {
                    prunedSet.Add(hash);
                }
            }

            // 3. Recursive Compression (Bottom-Up)
            // Find groups of 32 siblings and merge them into the parent.

            if (prunedSet.Count == 0) return new List<string>();

            int currentMaxDepth = prunedSet.Max(x => x.Length);

            // Iterate from deepest level up to (minLevel + 1)
            // We cannot compress a hash of length == minLevel (or shorter)
            for (int len = currentMaxDepth; len > minLevel; len--)
            {
                // Get all hashes at current depth
                var levelHashes = prunedSet.Where(x => x.Length == len).ToList();

                // Group by parent string (substring 0..len-1)
                // e.g. "a0", "a1"... group under "a"
                var groups = levelHashes.GroupBy(x => x.Substring(0, len - 1));

                foreach (var group in groups)
                {
                    // In Geohash Base32, a full grid has exactly 32 cells.
                    if (group.Count() == 32)
                    {
                        string parent = group.Key;

                        // Remove all 32 children from the result set
                        foreach (var child in group)
                        {
                            prunedSet.Remove(child);
                        }

                        // Add the parent to the result set
                        // The parent acts as a candidate for the next iteration (len - 1)
                        prunedSet.Add(parent);
                    }
                }
            }

            return prunedSet.OrderBy(x => x).ToList();
        }
    }
}