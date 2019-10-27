using System;
using System.Collections.Generic;
using System.Text;

namespace Geohash
{
    /// <summary>
    /// Reports the progress of hashing polygons
    /// </summary>
    public class HashingProgress
    {
        /// <summary>
        /// Remaining Queue Size
        /// </summary>
        public double QueueSize { get; set; }

        /// <summary>
        /// Number of hashes processed
        /// </summary>
        public double HashesProcessed { get; set; }

        /// <summary>
        /// Return the timespan since starting the hashing opertation
        /// </summary>
        public TimeSpan RunningSince { get; set; }
    }
}
