using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geohash
{
    public class GeohashCompressor
    {
        private Geohasher geohasher = new Geohasher();

        public List<string> Compress(string[] hashes, int minlevel = 1, int maxlevel = 12)
        {
            HashSet<string> geohashes = new HashSet<string>(hashes);
            HashSet<string> deletegh = new HashSet<string>();
            HashSet<string> final_geohashes = new HashSet<string>();
            bool keepWorking = true;
            int final_geohashes_size = 0;

            while (keepWorking)
            {
                deletegh.Clear();
                final_geohashes.Clear();

                foreach (var geohash in geohashes)
                {
                    var geohash_length = geohash.Length;

                    if (geohash_length >= minlevel)
                    {
                        var part = geohash;

                        if(geohash.Length > 1)
                        {
                            part = geohash.Substring(0, geohash.Length - 1);
                        }
                   
                        if (!deletegh.Contains(part) && !deletegh.Contains(geohash))
                        {
                            var combinations = new HashSet<string>(geohasher.GetSubhashes(part));

                            if (combinations.IsSubsetOf(geohashes))
                            {
                                final_geohashes.Add(part);
                                deletegh.Add(part);

                            }
                            else
                            {

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

            return geohashes.ToList();
        }
    }
}



