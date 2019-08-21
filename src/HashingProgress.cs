using System;
using System.Collections.Generic;
using System.Text;

namespace Geohash
{
    public class HashingProgress
    {
        public double QueueSize { get; set; }
        public double HashesProcessed { get; set; }
        public DateTime RunningSince { get; set; }
    }
}
