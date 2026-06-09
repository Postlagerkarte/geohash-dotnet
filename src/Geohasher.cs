using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Geohash
{
    /// <summary>
    /// Encodes/decodes geohashes — a hierarchical spatial index.
    /// Handles antimeridian (date line) wrapping and clamps latitude at the poles.
    /// All members are thread-safe and the class is stateless.
    /// </summary>
    public sealed class Geohasher
    {
        /// <summary>Maximum supported geohash precision.</summary>
        public const int MaxPrecision = 12;

        private const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";

        // O(1) reverse lookup. -1 = invalid character. Accepts upper- and lowercase.
        private static readonly sbyte[] DecodeMap = BuildDecodeMap();

        private static readonly Direction[] AllDirections =
        {
            Direction.North, Direction.NorthEast, Direction.East, Direction.SouthEast,
            Direction.South, Direction.SouthWest, Direction.West, Direction.NorthWest
        };

        // (dLat, dLng) offsets indexed by (int)Direction.
        private static readonly (int dLat, int dLng)[] Offsets =
        {
            (1, 0),   // North
            (1, 1),   // NorthEast
            (0, 1),   // East
            (-1, 1),  // SouthEast
            (-1, 0),  // South
            (-1, -1), // SouthWest
            (0, -1),  // West
            (1, -1),  // NorthWest
        };

        private static sbyte[] BuildDecodeMap()
        {
            var map = new sbyte[128];
            for (int i = 0; i < map.Length; i++) map[i] = -1;
            for (sbyte i = 0; i < Base32.Length; i++)
            {
                map[Base32[i]] = i;
                map[char.ToUpperInvariant(Base32[i])] = i; // be lenient on input
            }
            return map;
        }

        /// <summary>
        /// Encodes a latitude/longitude pair into a geohash string.
        /// Longitude is wrapped into [-180, 180); latitude is clamped to [-90, 90].
        /// </summary>
        /// <param name="latitude">Latitude in degrees.</param>
        /// <param name="longitude">Longitude in degrees.</param>
        /// <param name="precision">Length of the resulting hash (1–12). See <see cref="Precision"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Precision out of range.</exception>
        /// <exception cref="ArgumentException">Coordinate is NaN.</exception>
        public string Encode(double latitude, double longitude, int precision = 6)
        {
            if (precision < 1 || precision > MaxPrecision)
                throw new ArgumentOutOfRangeException(nameof(precision), precision,
                    $"Precision must be between 1 and {MaxPrecision}.");
            if (double.IsNaN(latitude) || double.IsNaN(longitude))
                throw new ArgumentException("Coordinates must not be NaN.");

            latitude = ClampLatitude(latitude);
            longitude = NormalizeLongitude(longitude);

            double latMin = -90.0, latMax = 90.0, lonMin = -180.0, lonMax = 180.0;
            Span<char> buffer = stackalloc char[MaxPrecision];

            bool isLon = true;
            int ch = 0, bit = 0, written = 0;

            while (written < precision)
            {
                if (isLon)
                {
                    double mid = (lonMin + lonMax) * 0.5;
                    if (longitude >= mid) { ch = (ch << 1) | 1; lonMin = mid; }
                    else { ch <<= 1; lonMax = mid; }
                }
                else
                {
                    double mid = (latMin + latMax) * 0.5;
                    if (latitude >= mid) { ch = (ch << 1) | 1; latMin = mid; }
                    else { ch <<= 1; latMax = mid; }
                }

                isLon = !isLon;

                if (++bit == 5)
                {
                    buffer[written++] = Base32[ch];
                    bit = 0;
                    ch = 0;
                }
            }

            return buffer.Slice(0, precision).ToString();
        }

        /// <summary>Returns the (latitude, longitude) center point of the geohash cell.</summary>
        public (double latitude, double longitude) Decode(string geohash)
        {
            var bbox = GetBoundingBox(geohash);
            return (bbox.CenterLat, bbox.CenterLng);
        }

        /// <summary>Returns the exact bounding box of the geohash cell.</summary>
        public BoundingBox GetBoundingBox(string geohash)
        {
            ValidateGeohash(geohash);

            double latMin = -90.0, latMax = 90.0, lonMin = -180.0, lonMax = 180.0;
            bool isLon = true;

            for (int i = 0; i < geohash.Length; i++)
            {
                int cd = DecodeMap[geohash[i]]; // validated above, always >= 0

                for (int shift = 4; shift >= 0; shift--)
                {
                    bool high = ((cd >> shift) & 1) != 0;
                    if (isLon)
                    {
                        double mid = (lonMin + lonMax) * 0.5;
                        if (high) lonMin = mid; else lonMax = mid;
                    }
                    else
                    {
                        double mid = (latMin + latMax) * 0.5;
                        if (high) latMin = mid; else latMax = mid;
                    }
                    isLon = !isLon;
                }
            }

            return new BoundingBox(latMin, lonMin, latMax, lonMax);
        }

        /// <summary>
        /// Returns the neighboring geohash in the given direction.
        /// Longitude wraps across the antimeridian; latitude is clamped, so the
        /// North neighbor of a top-row cell is the cell itself (geohashes do not wrap over the poles).
        /// </summary>
        public string GetNeighbor(string geohash, Direction direction)
        {
            var bbox = GetBoundingBox(geohash); // validates input
            return NeighborFromBox(in bbox, direction, geohash.Length);
        }

        /// <summary>Returns all 8 neighbors. Decodes the source hash only once.</summary>
        public Dictionary<Direction, string> GetNeighbors(string geohash)
        {
            var bbox = GetBoundingBox(geohash); // validates input
            var result = new Dictionary<Direction, string>(8);
            foreach (var dir in AllDirections)
                result[dir] = NeighborFromBox(in bbox, dir, geohash.Length);
            return result;
        }

        /// <summary>Returns all 32 child sub-hashes (precision + 1) in base-32 order.</summary>
        public string[] GetSubhashes(string geohash)
        {
            ValidateGeohash(geohash);
            if (geohash.Length >= MaxPrecision)
                throw new ArgumentException($"Cannot generate subhashes for precision {MaxPrecision}.", nameof(geohash));

            var result = new string[32];
            for (int i = 0; i < 32; i++)
                result[i] = geohash + Base32[i];
            return result;
        }

        /// <summary>Returns the parent geohash (precision - 1).</summary>
        public string GetParent(string geohash)
        {
            ValidateGeohash(geohash);
            if (geohash.Length <= 1)
                throw new ArgumentException("Cannot get parent of a precision-1 geohash.", nameof(geohash));
            return geohash.Substring(0, geohash.Length - 1);
        }

        /// <summary>Returns true if the string is a syntactically valid geohash (length 1–12, valid base-32 chars).</summary>
        public bool IsValid(string geohash)
        {
            if (string.IsNullOrEmpty(geohash) || geohash.Length > MaxPrecision) return false;
            for (int i = 0; i < geohash.Length; i++)
            {
                char c = geohash[i];
                if (c >= 128 || DecodeMap[c] < 0) return false;
            }
            return true;
        }

        // --- Internals ---

        private string NeighborFromBox(in BoundingBox bbox, Direction direction, int precision)
        {
            var (dLat, dLng) = Offsets[(int)direction];
            double lat = bbox.CenterLat + dLat * bbox.Height;
            double lng = bbox.CenterLng + dLng * bbox.Width;
            // Encode normalizes longitude (date line wrap) and clamps latitude (poles).
            return Encode(lat, lng, precision);
        }

        /// <summary>Wraps longitude into [-180, 180). E.g. -185 → 175, 185 → -175.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double NormalizeLongitude(double lng)
        {
            double result = (lng + 180.0) % 360.0;
            if (result < 0) result += 360.0;
            return result - 180.0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ClampLatitude(double lat) =>
            lat > 90.0 ? 90.0 : (lat < -90.0 ? -90.0 : lat);

        internal static void ValidateGeohash(string geohash)
        {
            if (string.IsNullOrEmpty(geohash))
                throw new ArgumentException("Geohash must not be null or empty.", nameof(geohash));
            if (geohash.Length > MaxPrecision)
                throw new ArgumentException($"Geohash length cannot exceed {MaxPrecision}.", nameof(geohash));

            for (int i = 0; i < geohash.Length; i++)
            {
                char c = geohash[i];
                if (c >= 128 || DecodeMap[c] < 0)
                    throw new ArgumentException($"Invalid character '{c}' at position {i}.", nameof(geohash));
            }
        }
    }
}