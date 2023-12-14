namespace Geohash
{
    /// <summary>
    ///     Named precision values for passing to GeoHasher.Encode
    /// </summary>
    public static class Precision
    {
        /// <summary>
        ///     Represents an area of approximate width 5000km and height 5000km
        /// </summary>
        public const int Size_km_5000x5000 = 1;

        /// <summary>
        ///     Represents an area of approximate width 1250km and height 625km
        /// </summary>
        public const int Size_km_1250x625 = 2;

        /// <summary>
        ///     Represents an area of approximate width 156km and height 156km
        /// </summary>
        public const int Size_km_156x156 = 3;

        /// <summary>
        ///     Represents an area of approximate width 39km and height 20km
        /// </summary>
        public const int Size_km_39x20 = 4;

        /// <summary>
        ///     Represents an area of approximate width 5km and height 5km
        /// </summary>
        public const int Size_km_5x5 = 5;

        /// <summary>
        ///     Represents an area of approximate width 1km and height 1km
        /// </summary>
        public const int Size_km_1x1 = 6;

        /// <summary>
        ///     Represents an area of approximate width 153m and height 153m
        /// </summary>
        public const int Size_m_153x153 = 7;

        /// <summary>
        ///     Represents an area of approximate width 38m and height 19m
        /// </summary>
        public const int Size_m_38x19 = 8;

        /// <summary>
        ///     Represents an area of approximate width 5m and height 5m
        /// </summary>
        public const int Size_m_5x5 = 9;

        /// <summary>
        ///     Represents an area of approximate width 1m and height 1m
        /// </summary>
        public const int Size_m_1x1 = 10;

        /// <summary>
        ///     Represents an area of approximate width 149mm and height 149mm
        /// </summary>
        public const int Size_mm_149x149 = 11;

        /// <summary>
        ///     Represents an area of approximate width 37mm and height 19mm
        /// </summary>
        public const int Size_mm_37x19 = 12;
    }
}