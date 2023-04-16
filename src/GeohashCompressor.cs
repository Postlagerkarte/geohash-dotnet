using System.Collections.Generic;
using System.Linq;

namespace Geohash
{
    /// <summary>
    /// Provides functionality to compress an array of geohashes by finding the smallest possible set of geohashes that still cover the same area.
    /// </summary>
    public class GeohashCompressor
    {
        private Geohasher geohasher = new Geohasher();


        /// <summary>
        /// Compresses an array of geohashes by finding the smallest possible set of geohashes that still cover the same area.
        /// </summary>
        /// <param name="hashes">An array of geohashes to compress.</param>
        /// <param name="minlevel">The minimum allowed length of a geohash (default value is 1).</param>
        /// <param name="maxlevel">The maximum allowed length of a geohash (default value is 12).</param>
        /// <returns>A list of compressed geohashes.</returns>
        public List<string> Compress(string[] hashes, int minlevel = 1, int maxlevel = 12)
        {
            // Initialize data structures used in the algorithm
            HashSet<string> geohashes = new HashSet<string>(hashes);
            HashSet<string> deletegh = new HashSet<string>();
            HashSet<string> final_geohashes = new HashSet<string>();
            bool keepWorking = true;
            int final_geohashes_size = 0;

            // Continue the compression algorithm until no further compression is possible
            while (keepWorking)
            {
                // Clear temporary data structures for each iteration
                deletegh.Clear();
                final_geohashes.Clear();

                // Iterate through each geohash in the input set
                foreach (var geohash in geohashes)
                {
                    var geohash_length = geohash.Length;

                    // Check if the geohash length is within the allowed range
                    if (geohash_length >= minlevel)
                    {
                        var part = geohash;

                        // Remove the last character from the geohash if it is longer than 1 character
                        if (geohash.Length > 1)
                        {
                            part = geohash.Substring(0, geohash.Length - 1);
                        }

                        // Ensure the geohash is not already marked for deletion
                        if (!deletegh.Contains(part) && !deletegh.Contains(geohash))
                        {
                            // Generate all possible subhashes for the current geohash
                            var combinations = new HashSet<string>(geohasher.GetSubhashes(part));

                            // Check if all subhashes are part of the input geohash set
                            if (combinations.IsSubsetOf(geohashes))
                            {
                                final_geohashes.Add(part);
                                deletegh.Add(part);
                            }
                            else
                            {
                                // Mark the current geohash for deletion and add it to the final set
                                deletegh.Add(geohash);
                                if (geohash.Length >= maxlevel)
                                {
                                    final_geohashes.Add(geohash.Substring(0, maxlevel));
                                }
                                else
                                {
                                    final_geohashes.Add(geohash);
                                }
                            }
                        }
                    }
                }

                // Terminate the loop if the final set of geohashes has not changed in size
                if (final_geohashes_size == final_geohashes.Count)
                {
                    keepWorking = false;
                }
                else
                {
                    final_geohashes_size = final_geohashes.Count();
                    geohashes = new HashSet<string>(final_geohashes);
                }
            }

            // Return the final compressed set of geohashes
            return geohashes.ToList();
        }
    }

}



