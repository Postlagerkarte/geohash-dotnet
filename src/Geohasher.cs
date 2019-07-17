using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace GeoTools
{
    public class Geohasher
    {
        private char[] base32Chars = "0123456789bcdefghjkmnpqrstuvwxyz".ToCharArray();

        private int[] bits= { 16, 8, 4, 2, 1 };

        /// <summary>
        /// Encodes coordinates to a geohash string.
        /// </summary>
        /// <param name="latitude">latitude</param>
        /// <param name="longitude">longitude</param>
        /// <param name="precision">Length of the geohash. Must be between 1 and 12. Defaults to 6.</param>
        /// <returns>The created geoash for the given coordinates.</returns>
        public string Encode(double latitude, double longitude, int precision = 6)
        {
            Validate(latitude, longitude);

            if (precision < 1 || precision > 12)
            {
                throw new ArgumentException("precision must be between 1 and 12");
            }

            double[] latInterval = { -90.0, 90.0 };
            double[] lonInterval = { -180.0, 180.0 };

            var geohash = new StringBuilder();
            bool isEven = true;
            int bit = 0;
            int ch = 0;

            while (geohash.Length < precision)
            {
                double mid;

                if (isEven)
                {
                    mid = (lonInterval[0] + lonInterval[1]) / 2;

                    if (longitude > mid)
                    {
                        ch |= bits[bit];
                        lonInterval[0] = mid;
                    }
                    else
                    {
                        lonInterval[1] = mid;
                    }

                }
                else
                {
                    mid = (latInterval[0] + latInterval[1]) / 2;

                    if (latitude > mid)
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
                    geohash.Append(base32Chars[ch]);
                    bit = 0;
                    ch = 0;
                }
            }

            return geohash.ToString();
        }

        /// <summary>
        /// Return the 32 subhashes for the given geohash string.
        /// </summary>
        /// <param name="geoHash">geohash for which to get the subhashes.</param>
        /// <returns>subhashes</returns>
        public string[] GetSubhashes(string geohash)
        {
            if (String.IsNullOrEmpty(geohash)) throw new ArgumentNullException("geohash");
            if (geohash.Length > 12) throw new ArgumentException("geohash length > 12");

            return base32Chars.Select(x => $"geohash{x}").ToArray();
        }

        /// <summary>
        /// Decodes a geohash to the corresponding coordinates.
        /// </summary>
        /// <param name="geohash">geohash for which to get the coordinates</param>
        /// <returns>Tuple with latitude and longitude</returns>
        public Tuple<double, double> Decode(string geohash)
        {
            if (String.IsNullOrEmpty(geohash)) throw new ArgumentNullException("geohash");
            if (geohash.Length > 12) throw new ArgumentException("geohash length > 12");

            double[] bbox = DecodeAsBox(geohash);

            double latitude = (bbox[0] + bbox[1]) / 2;
            double longitude = (bbox[2] + bbox[3]) / 2;

            return Tuple.Create(latitude, longitude);
        }


        /// <summary>
        /// Returns the neighbor for a given geohash and directions.
        /// </summary>
        /// <param name="geohash">geohash for which to find the neighbor</param>
        /// <param name="direction">direction of the neighbor</param>
        /// <returns>geohash</returns>
        public string GetNeighbor(string geohash, Direction direction)
        {
            if (String.IsNullOrEmpty(geohash)) throw new ArgumentNullException("geohash");
            if (geohash.Length > 12) throw new ArgumentException("geohash length > 12");
            var neighbors = CreateNeighbors(geohash);
            return neighbors[direction];
        }

        /// <summary>
        /// Returns all neighbors for a given geohash.
        /// </summary>
        /// <param name="geohash">geohash for which to find the neighbors</param>
        /// <returns>Dictionary with direction and geohash</returns>
        public Dictionary<Direction,string> GetNeighbors(string geohash)
        {
            if (String.IsNullOrEmpty(geohash)) throw new ArgumentNullException("geohash");
            if (geohash.Length > 12) throw new ArgumentException("geohash length > 12");
            return CreateNeighbors(geohash);
        }

        /// <summary>
        /// Returns the parent of the given geohash.
        /// </summary>
        /// <param name="geohash">geohash for which to get the parent.</param>
        /// <returns>parent geohash</returns>
        public string GetParent(string geohash)
        {
            if (String.IsNullOrEmpty(geohash)) throw new ArgumentNullException("geohash");
            if (geohash.Length > 12) throw new ArgumentException("geohash length > 12");
            return geohash.Substring(0, geohash.Length - 1);
        }

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

        private double[] DecodeAsBox(string geohash)
        {
            double[] latInterval = { -90.0, 90.0 };
            double[] lonInterval = { -180.0, 180.0 };

            bool isEven = true;
            for (int i = 0; i < geohash.Length; i++)
            {

                int currentCharacter = Array.IndexOf(base32Chars, geohash[i]);

                for (int z = 0; z < bits.Length; z++)
                {
                    int mask = bits[z];
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
                    isEven = !isEven;
                }
            }

            return new double[] { latInterval[0], latInterval[1], lonInterval[0], lonInterval[1] };
        }


        private string South(string geoHash)
        {
            double[] bbox = DecodeAsBox(geoHash);
            double latDiff = bbox[1] - bbox[0];
            double lat = bbox[0] - latDiff / 2;
            double lon = (bbox[2] + bbox[3]) / 2;
            return Encode(lat, lon, geoHash.Length);
        }

        private string North(string geoHash)
        {
            double[] bbox = DecodeAsBox(geoHash);
            double latDiff = bbox[1] - bbox[0];
            double lat = bbox[1] + latDiff / 2;

            if (lat > 90)
            {
                lat = (90 - (lat - 90)) * -1; 
            }

            double lon = (bbox[2] + bbox[3]) / 2;
            return Encode(lat, lon, geoHash.Length);
        }

        private string West(string geoHash)
        {
            double[] bbox = DecodeAsBox(geoHash);
            double lonDiff = bbox[3] - bbox[2];
            double lat = (bbox[0] + bbox[1]) / 2;
            double lon = bbox[2] - lonDiff / 2;
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

        private string East(string geoHash)
        {
            double[] bbox = DecodeAsBox(geoHash);
            double lonDiff = bbox[3] - bbox[2];
            double lat = (bbox[0] + bbox[1]) / 2;
            double lon = bbox[3] + lonDiff / 2;

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

        private string adjacentHashAtBorder(String hashn)
        {
            // check if hash is on edge and direction would push us over the edge
            // if so, wrap round to the other limit for longitude
            // or if at latitude boundary (a pole) then spin longitude around 180
            // degrees.
            //var centre = Decode(hash);

            //// if rightmost hash
            //if (Direction.RIGHT.equals(direction))
            //{
            //    if (Math.abs(centre.getLon() + widthDegrees(hash.length()) / 2 - 180) < PRECISION)
            //    {
            //        return encodeHash(centre.getLat(), -180, hash.length());
            //    }
            //}
            //// if leftmost hash
            //else if (Direction.LEFT.equals(direction))
            //{
            //    if (Math.abs(centre.getLon() - widthDegrees(hash.length()) / 2 + 180) < PRECISION)
            //    {
            //        return encodeHash(centre.getLat(), 180, hash.length());
            //    }
            //}
            //// if topmost hash
            //else if (Direction.TOP.equals(direction))
            //{
            //    if (Math.abs(centre.getLat() + widthDegrees(hash.length()) / 2 - 90) < PRECISION)
            //    {
            //        return encodeHash(centre.getLat(), centre.getLon() + 180, hash.length());
            //    }
            //}
            //// if bottommost hash
            //else
            //{
            //    if (Math.abs(centre.getLat() - widthDegrees(hash.length()) / 2 + 90) < PRECISION)
            //    {
            //        return encodeHash(centre.getLat(), centre.getLon() + 180, hash.length());
            //    }
            //}

            return null;
        }
    }

    public enum Direction
    {
        North = 0,
        NorthEast = 1,
        East = 2,
        SouthEast = 3,
        South = 4,
        SouthWest = 5,
        West = 6,
        NorthWest = 7
    }
}
