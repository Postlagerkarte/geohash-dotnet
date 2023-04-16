namespace Geohash
{

    /// <summary>
    /// The BoundingBox class represents a rectangular region defined by a pair of latitude-longitude coordinates
    /// </summary>
    public class BoundingBox
    {
        /// <summary>
        /// Property for the minimum latitude value of the bounding bo
        /// </summary>
        public double MinLat { get; set; }
        /// <summary>
        /// Property for the minimum longitude value of the bounding box
        /// </summary>
        public double MinLng { get; set; }
        /// <summary>
        /// Property for the maximum latitude value of the bounding box
        /// </summary>
        public double MaxLat { get; set; } 
        /// <summary>
        /// Property for the maximum longitude value of the bounding box
        /// </summary>
        public double MaxLng { get; set; }  
    }

}
