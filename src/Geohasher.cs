using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geohash
{
    /// <summary>
    /// Encodes/decodes geohashes - a hierarchical spatial index.
    /// Fixed version: Handles Date Line wrapping and Pole clamping correctly.
    /// </summary>
    public class Geohasher
    {
        private static readonly char[] Base32Chars = "0123456789bcdefghjkmnpqrstuvwxyz".ToCharArray();
        private static readonly int[] Bits = { 16, 8, 4, 2, 1 };

        /// <summary>
        /// Encodes latitude/longitude to a geohash string.
        /// </summary>
        /// <param name="latitude">Latitude [-90, 90]</param>
        /// <param name="longitude">Longitude [-180, 180]</param>
        /// <param name="precision">Length of the resulting hash (1-12)</param>
        public string Encode(double latitude, double longitude, int precision = 6)
        {
            // 1. Normalize inputs to ensure safety
            longitude = NormalizeLongitude(longitude);
            latitude = ClampLatitude(latitude);

            if (precision < 1 || precision > 12)
                throw new ArgumentException("Precision must be between 1 and 12");

            double[] latInterval = { -90.0, 90.0 };
            double[] lonInterval = { -180.0, 180.0 };

            var geohash = new StringBuilder(precision);
            bool isEven = true;  // Start with longitude
            int bit = 0;
            int ch = 0;

            while (geohash.Length < precision)
            {
                double mid;
                if (isEven) // Longitude
                {
                    mid = (lonInterval[0] + lonInterval[1]) / 2;
                    if (longitude >= mid)
                    {
                        ch |= Bits[bit];
                        lonInterval[0] = mid;
                    }
                    else
                    {
                        lonInterval[1] = mid;
                    }
                }
                else // Latitude
                {
                    mid = (latInterval[0] + latInterval[1]) / 2;
                    if (latitude >= mid)
                    {
                        ch |= Bits[bit];
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
                    geohash.Append(Base32Chars[ch]);
                    bit = 0;
                    ch = 0;
                }
            }

            return geohash.ToString();
        }

        /// <summary>
        /// Returns the (Latitude, Longitude) center point of the geohash.
        /// </summary>
        public (double latitude, double longitude) Decode(string geohash)
        {
            ValidateGeohash(geohash);
            BoundingBox bbox = GetBoundingBox(geohash);
            double latitude = (bbox.MinLat + bbox.MaxLat) / 2;
            double longitude = (bbox.MinLng + bbox.MaxLng) / 2;
            return (latitude, longitude);
        }

        /// <summary>
        /// Returns the exact Bounding Box (Min/Max Lat/Lng) for this geohash.
        /// </summary>
        public BoundingBox GetBoundingBox(string geohash)
        {
            ValidateGeohash(geohash);

            double[] latInterval = { -90.0, 90.0 };
            double[] lonInterval = { -180.0, 180.0 };

            bool isEven = true;
            foreach (char c in geohash)
            {
                int cd = Array.IndexOf(Base32Chars, c);

                for (int j = 0; j < 5; j++)
                {
                    int mask = Bits[j];
                    if (isEven)
                    {
                        if ((cd & mask) != 0)
                            lonInterval[0] = (lonInterval[0] + lonInterval[1]) / 2;
                        else
                            lonInterval[1] = (lonInterval[0] + lonInterval[1]) / 2;
                    }
                    else
                    {
                        if ((cd & mask) != 0)
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

        /// <summary>
        /// Returns the neighbor string in the specified direction.
        /// Handles Antimeridian wrapping (Date Line) automatically.
        /// Limits Latitude at the Poles (does not wrap latitude).
        /// </summary>
        public string GetNeighbor(string geohash, Direction direction)
        {
            ValidateGeohash(geohash);

            // Decode bounds to size the step correctly
            BoundingBox bbox = GetBoundingBox(geohash);
            double latDiff = bbox.MaxLat - bbox.MinLat;
            double lonDiff = bbox.MaxLng - bbox.MinLng;

            double centerLat = (bbox.MinLat + bbox.MaxLat) / 2;
            double centerLon = (bbox.MinLng + bbox.MaxLng) / 2;

            // Calculate new center
            switch (direction)
            {
                case Direction.North:
                    centerLat += latDiff;
                    break;
                case Direction.South:
                    centerLat -= latDiff;
                    break;
                case Direction.East:
                    centerLon += lonDiff;
                    break;
                case Direction.West:
                    centerLon -= lonDiff;
                    break;
                // Diagonals are recursive compositions
                case Direction.NorthEast:
                    return GetNeighbor(GetNeighbor(geohash, Direction.North), Direction.East);
                case Direction.NorthWest:
                    return GetNeighbor(GetNeighbor(geohash, Direction.North), Direction.West);
                case Direction.SouthEast:
                    return GetNeighbor(GetNeighbor(geohash, Direction.South), Direction.East);
                case Direction.SouthWest:
                    return GetNeighbor(GetNeighbor(geohash, Direction.South), Direction.West);
            }

            // Normalization handles the Date Line wrapping (e.g. -185 becomes 175)
            // Encode will internally clamp Latitude if we went past 90/ -90
            return Encode(centerLat, NormalizeLongitude(centerLon), geohash.Length);
        }

        /// <summary>
        /// Returns all 8 neighbors.
        /// </summary>
        public Dictionary<Direction, string> GetNeighbors(string geohash)
        {
            ValidateGeohash(geohash);
            var neighbors = new Dictionary<Direction, string>();
            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                neighbors[dir] = GetNeighbor(geohash, dir);
            }
            return neighbors;
        }

        /// <summary>
        /// Returns all 32 child sub-hashes (precision + 1).
        /// </summary>
        public string[] GetSubhashes(string geohash)
        {
            ValidateGeohash(geohash);
            if (geohash.Length >= 12) throw new ArgumentException("Cannot generate subhashes for precision 12");

            string[] result = new string[32];
            for (int i = 0; i < 32; i++)
            {
                result[i] = geohash + Base32Chars[i];
            }
            return result;
        }

        public string GetParent(string geohash)
        {
            ValidateGeohash(geohash);
            if (geohash.Length <= 1) throw new ArgumentException("Cannot get parent of precision 1");
            return geohash.Substring(0, geohash.Length - 1);
        }

        // --- Helpers ---

        /// <summary>
        /// Valid constant time modulo fix for negative numbers.
        /// e.g. -185 => ((-185 + 180) % 360) - 180
        /// </summary>
        private double NormalizeLongitude(double lng)
        {
            double result = (lng + 180) % 360;
            if (result < 0) result += 360;
            return result - 180;
        }

        private double ClampLatitude(double lat)
        {
            if (lat > 90.0) return 90.0;
            if (lat < -90.0) return -90.0;
            return lat;
        }

        private void ValidateGeohash(string geohash)
        {
            if (string.IsNullOrEmpty(geohash)) throw new ArgumentException(nameof(geohash));
            if (geohash.Length > 12) throw new ArgumentException("Geohash length cannot exceed 12");

            foreach(var c in geohash)
            {
                if(!Base32Chars.Contains(c)) throw new ArgumentException($"Invalid character '{c}' found in geohash", nameof(geohash));
            }
        }
    }
}