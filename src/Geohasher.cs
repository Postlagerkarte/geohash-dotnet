using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geohash
{
    /// <summary>
    /// Provides methods for encoding, decoding, and working with geohashes.
    /// </summary>
    public class Geohasher
    {
        private static readonly char[] base32Chars = "0123456789bcdefghjkmnpqrstuvwxyz".ToCharArray();
        private static readonly int[] bits = { 16, 8, 4, 2, 1 };

        /// <summary>
        /// Encodes the latitude and longitude into a geohash string by converting the coordinates
        /// into binary representations and interleaving them, then converting the combined binary
        /// sequence into a base32-encoded string. The length of the geohash determines the precision
        /// of the encoded location.
        /// </summary>
        /// <param name="latitude">The latitude coordinate.</param>
        /// <param name="longitude">The longitude coordinate.</param>
        /// <param name="precision">The length of the geohash. Must be between 1 and 12. Defaults to 6.</param>
        /// <returns>The created geohash for the given coordinates.</returns>
        public string Encode(double latitude, double longitude, int precision = 6)
        {
            // Validate input coordinates.
            Validate(latitude, longitude);

            // Validate precision value.
            if (precision < 1 || precision > 12)
            {
                throw new ArgumentException("Precision must be between 1 and 12");
            }

            // Initialize latitude and longitude intervals.
            double[] latInterval = { -90.0, 90.0 };
            double[] lonInterval = { -180.0, 180.0 };

            // Initialize a StringBuilder to store the geohash.
            var geohash = new StringBuilder();
            bool isEven = true;
            int bit = 0;
            int ch = 0;

            // Loop until the desired geohash length is reached.
            while (geohash.Length < precision)
            {
                double mid;

                // If it's an even iteration, adjust longitude interval and character value.
                if (isEven)
                {
                    mid = (lonInterval[0] + lonInterval[1]) / 2;

                    if (longitude >= mid)
                    {
                        ch |= bits[bit];
                        lonInterval[0] = mid;
                    }
                    else
                    {
                        lonInterval[1] = mid;
                    }
                }
                // If it's an odd iteration, adjust latitude interval and character value.
                else
                {
                    mid = (latInterval[0] + latInterval[1]) / 2;

                    if (latitude >= mid)
                    {
                        ch |= bits[bit];
                        latInterval[0] = mid;
                    }
                    else
                    {
                        latInterval[1] = mid;
                    }
                }

                // Toggle isEven flag.
                isEven = !isEven;

                // Increment bit index or reset and append character to geohash.
                if (bit < 4)
                {
                    bit++;
                }
                else
                {
                    geohash.Append(base32Chars[ch]);
                    bit = 0;
                    ch = 0;
                }
            }

            return geohash.ToString();
        }

        /// <summary>
        /// Returns the 32 subhashes for the given geohash string.
        /// </summary>
        /// <param name="geohash">Geohash for which to get the subhashes.</param>
        /// <returns>An array of subhashes.</returns>
        public string[] GetSubhashes(string geohash)
        {
            if (String.IsNullOrEmpty(geohash)) throw new ArgumentNullException(nameof(geohash));
            if (geohash.Length > 12) throw new ArgumentException("geohash length must be <= 12");

            return base32Chars.Select(x => $"{geohash}{x}").ToArray();
        }


        /// <summary>
        /// Decodes a geohash to the corresponding coordinates.
        /// </summary>
        /// <param name="geohash">Geohash for which to get the coordinates.</param>
        /// <returns>ValueTuple with latitude and longitude.</returns>
        public (double latitude, double longitude) Decode(string geohash)
        {
            // Validate the input geohash.
            ValidateGeohash(geohash);

            // Get the bounding box of the geohash.
            BoundingBox bbox = GetBoundingBox(geohash);

            // Calculate the center of the bounding box as the resulting coordinates.
            double latitude = (bbox.MinLat + bbox.MaxLat) / 2;
            double longitude = (bbox.MinLng + bbox.MaxLng) / 2;

            return (latitude, longitude);
        }


        /// <summary>
        /// Returns the neighbor for a given geohash and directions.
        /// </summary>
        /// <param name="geohash">geohash for which to find the neighbor</param>
        /// <param name="direction">direction of the neighbor</param>
        /// <returns>geohash</returns>
        public string GetNeighbor(string geohash, Direction direction)
        {
            if (String.IsNullOrEmpty(geohash)) throw new ArgumentNullException(nameof(geohash));
            if (geohash.Length > 12) throw new ArgumentException($"{nameof(geohash)} length > 12");
            var neighbors = CreateNeighbors(geohash);
            return neighbors[direction];
        }

        /// <summary>
        /// Returns all neighbors for a given geohash.
        /// </summary>
        /// <param name="geohash">geohash for which to find the neighbors</param>
        /// <returns>Dictionary with direction and geohash</returns>
        public Dictionary<Direction, string> GetNeighbors(string geohash)
        {
            if (String.IsNullOrEmpty(geohash)) throw new ArgumentNullException(nameof(geohash));
            if (geohash.Length > 12) throw new ArgumentException($"{nameof(geohash)} length > 12");
            return CreateNeighbors(geohash);
        }

        /// <summary>
        /// Returns the parent of the given geohash.
        /// </summary>
        /// <param name="geohash">geohash for which to get the parent.</param>
        /// <returns>parent geohash</returns>
        public string GetParent(string geohash)
        {
            ValidateGeohash(geohash);
            return geohash.Substring(0, geohash.Length - 1);
        }

        /// <summary>
        /// Calculates the bounding box for the given geohash.
        /// </summary>
        /// <param name="geohash">Geohash for which to get the bounding box.</param>
        /// <returns>A BoundingBox object representing the latitude and longitude intervals of the geohash.</returns>
        public BoundingBox GetBoundingBox(string geohash)
        {
            // Validate the input geohash.
            ValidateGeohash(geohash);

            // Initialize latitude and longitude intervals.
            double[] latInterval = { -90.0, 90.0 };
            double[] lonInterval = { -180.0, 180.0 };

            // Process each character in the geohash string.
            bool isEven = true;
            for (int i = 0; i < geohash.Length; i++)
            {
                int currentCharacter = Array.IndexOf(base32Chars, geohash[i]);

                // Process each bit in the character.
                for (int z = 0; z < bits.Length; z++)
                {
                    int mask = bits[z];

                    // Update the longitude interval if the current bit is even.
                    if (isEven)
                    {
                        if ((currentCharacter & mask) != 0)
                        {
                            lonInterval[0] = (lonInterval[0] + lonInterval[1]) / 2;
                        }
                        else
                        {
                            lonInterval[1] = (lonInterval[0] + lonInterval[1]) / 2;
                        }
                    }
                    // Update the latitude interval if the current bit is odd.
                    else
                    {
                        if ((currentCharacter & mask) != 0)
                        {
                            latInterval[0] = (latInterval[0] + latInterval[1]) / 2;
                        }
                        else
                        {
                            latInterval[1] = (latInterval[0] + latInterval[1]) / 2;
                        }
                    }
                    // Toggle the isEven flag for the next bit.
                    isEven = !isEven;
                }
            }

            // Return the resulting bounding box.
            return new BoundingBox
            {
                MinLat = latInterval[0],
                MaxLat = latInterval[1],
                MinLng = lonInterval[0],
                MaxLng = lonInterval[1]
            };
        }

        /// <summary>
        /// Validates the input geohash string.
        /// </summary>
        /// <param name="geohash">Geohash to validate.</param>
        private static void ValidateGeohash(string geohash)
        {
            if (String.IsNullOrEmpty(geohash)) throw new ArgumentNullException("geohash");
            if (geohash.Length > 12) throw new ArgumentException("geohash length > 12");

            foreach (var ch in geohash)
            {
                if (!base32Chars.Contains(ch)) throw new ArgumentException("Invalid character in geohash", nameof(geohash));
            }
        }

        /// <summary>
        /// Creates neighbors for a given geohash.
        /// </summary>
        /// <param name="geohash">geohash for which to create the neighbors</param>
        /// <returns>Dictionary with direction and geohash</returns>
        private Dictionary<Direction, string> CreateNeighbors(string geohash)
        {
            var result = new Dictionary<Direction, string>();
            result.Add(Direction.North, North(geohash));
            result.Add(Direction.NorthWest, West(result[Direction.North]));
            result.Add(Direction.NorthEast, East(result[Direction.North]));
            result.Add(Direction.East, East(geohash));
            result.Add(Direction.South, South(geohash));
            result.Add(Direction.SouthWest, West(result[Direction.South]));
            result.Add(Direction.SouthEast, East(result[Direction.South]));
            result.Add(Direction.West, West(geohash));
            return result;
        }

        private void Validate(double latitude, double longitude)
        {
            if (latitude < -90.0 || latitude > 90.0)
            {
                throw new ArgumentException("Latitude " + latitude + " is outside valid range of [-90,90]");
            }
            if (longitude < -180.0 || longitude > 180.0)
            {
                throw new ArgumentException("Longitude " + longitude + " is outside valid range of [-180,180]");
            }
        }

        // Returns the geohash for the location directly south of the given geohash
        private string South(string geoHash)
        {
            BoundingBox bbox = GetBoundingBox(geoHash);
            double latDiff = bbox.MaxLat - bbox.MinLat;
            double lat = bbox.MinLat - latDiff / 2;
            double lon = (bbox.MinLng + bbox.MaxLng) / 2;

            if (lat < -90)
            {
                lat = (-90 + (-90 - lat)) * -1;
            }

            return Encode(lat, lon, geoHash.Length);
        }

        // Returns the geohash for the location directly north of the given geohash
        private string North(string geoHash)
        {
            BoundingBox bbox = GetBoundingBox(geoHash);
            double latDiff = bbox.MaxLat - bbox.MinLat;
            double lat = bbox.MaxLat + latDiff / 2;

            if (lat > 90)
            {
                lat = (90 - (lat - 90)) * -1;
            }

            double lon = (bbox.MinLng + bbox.MaxLng) / 2;
            return Encode(lat, lon, geoHash.Length);
        }

        // Returns the geohash for the location directly west of the given geohash
        private string West(string geoHash)
        {
            BoundingBox bbox = GetBoundingBox(geoHash);
            double lonDiff = bbox.MaxLng - bbox.MinLng;
            double lat = (bbox.MinLat + bbox.MaxLat) / 2;
            double lon = bbox.MinLng - lonDiff / 2;
            if (lon < -180)
            {
                lon = 180 - (lon + 180);
            }
            if (lon > 180)
            {
                lon = 180;
            }

            return Encode(lat, lon, geoHash.Length);
        }

        // Returns the geohash for the location directly east of the given geohash
        private string East(string geoHash)
        {
            BoundingBox bbox = GetBoundingBox(geoHash);
            double lonDiff = bbox.MaxLng - bbox.MinLng;
            double lat = (bbox.MinLat + bbox.MaxLat) / 2;
            double lon = bbox.MaxLng + lonDiff / 2;

            if (lon > 180)
            {
                lon = -180 + (lon - 180);
            }
            if (lon < -180)
            {
                lon = -180;
            }

            return Encode(lat, lon, geoHash.Length);
        }
    }

}
