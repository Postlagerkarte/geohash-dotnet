namespace Geohash
{
    /// <summary>
    /// An immutable rectangular region defined by minimum/maximum latitude and longitude.
    /// </summary>
    /// <param name="MinLat">Minimum latitude of the bounding box.</param>
    /// <param name="MinLng">Minimum longitude of the bounding box.</param>
    /// <param name="MaxLat">Maximum latitude of the bounding box.</param>
    /// <param name="MaxLng">Maximum longitude of the bounding box.</param>
    public readonly record struct BoundingBox(double MinLat, double MinLng, double MaxLat, double MaxLng)
    {
        /// <summary>Latitude of the center point.</summary>
        public double CenterLat => (MinLat + MaxLat) * 0.5;

        /// <summary>Longitude of the center point.</summary>
        public double CenterLng => (MinLng + MaxLng) * 0.5;

        /// <summary>Latitudinal extent (height) in degrees.</summary>
        public double Height => MaxLat - MinLat;

        /// <summary>Longitudinal extent (width) in degrees.</summary>
        public double Width => MaxLng - MinLng;

        /// <summary>Returns true if the point lies within (or on the border of) this box.</summary>
        public bool Contains(double latitude, double longitude) =>
            latitude >= MinLat && latitude <= MaxLat &&
            longitude >= MinLng && longitude <= MaxLng;

        /// <summary>Returns true if this box intersects <paramref name="other"/>.</summary>
        public bool Intersects(in BoundingBox other) =>
            MinLat <= other.MaxLat && MaxLat >= other.MinLat &&
            MinLng <= other.MaxLng && MaxLng >= other.MinLng;
    }
}