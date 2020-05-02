using System;
using System.Collections;
using System.Text;
using System.Threading;

namespace RoSchmi.AWS.Models
{
    public class CommandClass
    {  
        public State state { get; set; }
        public Metadata metadata { get; set; }

        public int version { get; set; }

        public int timestamp;

    }
}
