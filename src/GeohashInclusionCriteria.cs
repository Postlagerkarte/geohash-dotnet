namespace Geohash
{
    /// <summary>
    /// Criteria deciding whether a geohash cell belongs to a coverage result.
    /// </summary>
    public enum GeohashInclusionCriteria
    {
        /// <summary>The geohash cell must be entirely within the shape.</summary>
        Contains,

        /// <summary>The geohash cell may partially overlap the shape's boundary.</summary>
        Intersects
    }
}