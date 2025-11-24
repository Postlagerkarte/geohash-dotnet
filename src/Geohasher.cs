using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geohash
{
    /// <summary>
    /// Encodes/decodes geohashes - a hierarchical spatial index that maps lat/lng to short strings.
    /// 
    /// Key concepts:
    /// - Each character adds ~5 bits of precision, subdividing the cell into a 4x8 or 8x4 grid (32 cells)
    /// - Precision 1 = ~5000km cells, Precision 6 = ~1km cells, Precision 12 = ~3cm cells
    /// - Geohashes with a common prefix are spatially close (but the reverse isn't always true due to edge effects)
    /// - Uses base32 encoding (0-9, b-z excluding a, i, l, o to avoid confusion)
    /// </summary>
    public class Geohasher
    {
        // Geohash base32 alphabet - excludes 'a', 'i', 'l', 'o' to prevent ambiguity
        private static readonly char[] base32Chars = "0123456789bcdefghjkmnpqrstuvwxyz".ToCharArray();

        // Bit masks for extracting each of the 5 bits from a base32 character (MSB to LSB)
        private static readonly int[] bits = { 16, 8, 4, 2, 1 };

        /// <summary>
        /// Converts lat/lng to a geohash string.
        /// 
        /// The algorithm performs binary search on both dimensions simultaneously:
        /// longitude bits are placed at even positions (0, 2, 4...), latitude at odd positions.
        /// Every 5 bits are grouped into one base32 character.
        /// </summary>
        /// <param name="precision">Each level adds ~2.5 bits of lat precision and ~2.5 bits of lng precision.</param>
        public string Encode(double latitude, double longitude, int precision = 6)
        {
            longitude = NormalizeLongitude(longitude);
            Validate(latitude, longitude);

            if (precision < 1 || precision > 12)
            {
                throw new ArgumentException("Precision must be between 1 and 12");
            }

            double[] latInterval = { -90.0, 90.0 };
            double[] lonInterval = { -180.0, 180.0 };

            var geohash = new StringBuilder();
            bool isEven = true;  // Start with longitude (even bit positions)
            int bit = 0;         // Current bit position within the 5-bit character (0-4)
            int ch = 0;          // Accumulated bits for current character

            while (geohash.Length < precision)
            {
                double mid;

                if (isEven)
                {
                    // Binary search on longitude
                    mid = (lonInterval[0] + lonInterval[1]) / 2;
                    if (longitude >= mid)
                    {
                        ch |= bits[bit];  // Set bit to 1 = upper half
                        lonInterval[0] = mid;
                    }
                    else
                    {
                        lonInterval[1] = mid;  // Bit stays 0 = lower half
                    }
                }
                else
                {
                    // Binary search on latitude
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

                isEven = !isEven;

                if (bit < 4)
                {
                    bit++;
                }
                else
                {
                    // Completed 5 bits - emit character and reset
                    geohash.Append(base32Chars[ch]);
                    bit = 0;
                    ch = 0;
                }
            }

            return geohash.ToString();
        }

        /// <summary>
        /// Returns all 32 child cells (one precision level deeper).
        /// Each child covers 1/32 of the parent's area.
        /// </summary>
        public string[] GetSubhashes(string geohash)
        {
            if (String.IsNullOrEmpty(geohash)) throw new ArgumentNullException(nameof(geohash));
            if (geohash.Length > 12) throw new ArgumentException("geohash length must be <= 12");

            return base32Chars.Select(x => $"{geohash}{x}").ToArray();
        }

        /// <summary>
        /// Returns the center point of the geohash cell.
        /// Note: This is the geometric center, not the original encoded point.
        /// </summary>
        public (double latitude, double longitude) Decode(string geohash)
        {
            ValidateGeohash(geohash);
            BoundingBox bbox = GetBoundingBox(geohash);
            double latitude = (bbox.MinLat + bbox.MaxLat) / 2;
            double longitude = (bbox.MinLng + bbox.MaxLng) / 2;
            return (latitude, longitude);
        }

        public string GetNeighbor(string geohash, Direction direction)
        {
            if (String.IsNullOrEmpty(geohash)) throw new ArgumentNullException(nameof(geohash));
            if (geohash.Length > 12) throw new ArgumentException($"{nameof(geohash)} length > 12");
            var neighbors = CreateNeighbors(geohash);
            return neighbors[direction];
        }

        /// <summary>
        /// Returns all 8 adjacent cells at the same precision level.
        /// Handles antimeridian and pole wrapping automatically.
        /// </summary>
        public Dictionary<Direction, string> GetNeighbors(string geohash)
        {
            if (String.IsNullOrEmpty(geohash)) throw new ArgumentNullException(nameof(geohash));
            if (geohash.Length > 12) throw new ArgumentException($"{nameof(geohash)} length > 12");
            return CreateNeighbors(geohash);
        }

        /// <summary>
        /// Returns the containing cell one level up (32x larger area).
        /// </summary>
        public string GetParent(string geohash)
        {
            ValidateGeohash(geohash);
            return geohash.Substring(0, geohash.Length - 1);
        }

        /// <summary>
        /// Reconstructs the cell boundaries by reversing the encoding process.
        /// The bounding box represents the exact area covered by this geohash.
        /// </summary>
        public BoundingBox GetBoundingBox(string geohash)
        {
            ValidateGeohash(geohash);

            double[] latInterval = { -90.0, 90.0 };
            double[] lonInterval = { -180.0, 180.0 };

            bool isEven = true;
            for (int i = 0; i < geohash.Length; i++)
            {
                int currentCharacter = Array.IndexOf(base32Chars, geohash[i]);

                // Reverse the encoding: each bit narrows either lat or lng interval
                for (int z = 0; z < bits.Length; z++)
                {
                    int mask = bits[z];

                    if (isEven)
                    {
                        // Bit=1 means we took the upper half of longitude range
                        if ((currentCharacter & mask) != 0)
                            lonInterval[0] = (lonInterval[0] + lonInterval[1]) / 2;
                        else
                            lonInterval[1] = (lonInterval[0] + lonInterval[1]) / 2;
                    }
                    else
                    {
                        if ((currentCharacter & mask) != 0)
                            latInterval[0] = (latInterval[0] + latInterval[1]) / 2;
                        else
                            latInterval[1] = (latInterval[0] + latInterval[1]) / 2;
                    }
                    isEven = !isEven;
                }
            }

            return new BoundingBox
            {
                MinLat = latInterval[0],
                MaxLat = latInterval[1],
                MinLng = lonInterval[0],
                MaxLng = lonInterval[1]
            };
        }

        private static void ValidateGeohash(string geohash)
        {
            if (String.IsNullOrEmpty(geohash)) throw new ArgumentNullException("geohash");
            if (geohash.Length > 12) throw new ArgumentException("geohash length > 12");

            foreach (var ch in geohash)
            {
                if (!base32Chars.Contains(ch))
                    throw new ArgumentException("Invalid character in geohash", nameof(geohash));
            }
        }

        /// <summary>
        /// Diagonal neighbors are computed by combining cardinal moves
        /// (e.g., NorthWest = West of North, not a direct calculation).
        /// </summary>
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

        /// <summary>
        /// Finds neighbor by stepping half a cell height in the target direction,
        /// then re-encoding. Pole crossing "bounces" latitude back into valid range.
        /// </summary>
        private string South(string geoHash)
        {
            BoundingBox bbox = GetBoundingBox(geoHash);
            double latDiff = bbox.MaxLat - bbox.MinLat;
            double lat = bbox.MinLat - latDiff / 2;
            double lon = (bbox.MinLng + bbox.MaxLng) / 2;
            lon = NormalizeLongitude(lon);

            // Pole bounce: reflect latitude back from beyond -90
            if (lat < -90)
            {
                lat = (-90 + (-90 - lat)) * -1;
            }

            return Encode(lat, lon, geoHash.Length);
        }

        private string North(string geoHash)
        {
            BoundingBox bbox = GetBoundingBox(geoHash);
            double latDiff = bbox.MaxLat - bbox.MinLat;
            double lat = bbox.MaxLat + latDiff / 2;

            // Pole bounce: reflect latitude back from beyond +90
            if (lat > 90)
            {
                lat = (90 - (lat - 90)) * -1;
            }

            double lon = (bbox.MinLng + bbox.MaxLng) / 2;
            lon = NormalizeLongitude(lon);
            return Encode(lat, lon, geoHash.Length);
        }

        private string West(string geoHash)
        {
            BoundingBox bbox = GetBoundingBox(geoHash);
            double lonDiff = bbox.MaxLng - bbox.MinLng;
            double lat = (bbox.MinLat + bbox.MaxLat) / 2;
            double lon = bbox.MinLng - lonDiff / 2;
            lon = NormalizeLongitude(lon);

            // Antimeridian wrap
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

        /// <summary>
        /// Wraps longitude to [-180, 180) range.
        /// </summary>
        private double NormalizeLongitude(double lng)
        {
            return (lng + 180) % 360 - 180;
        }

        private string East(string geoHash)
        {
            BoundingBox bbox = GetBoundingBox(geoHash);
            double lonDiff = bbox.MaxLng - bbox.MinLng;
            double lat = (bbox.MinLat + bbox.MaxLat) / 2;
            double lon = bbox.MaxLng + lonDiff / 2;
            lon = NormalizeLongitude(lon);

            // Antimeridian wrap
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