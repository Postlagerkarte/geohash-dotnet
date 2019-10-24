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
        public double QueueSize { get; set; }
        public double HashesProcessed { get; set; }
        public TimeSpan RunningSince { get; set; }
    }
}
