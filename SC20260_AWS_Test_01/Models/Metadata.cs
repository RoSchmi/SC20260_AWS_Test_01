using System;
using System.Collections;
using System.Text;
using System.Threading;

namespace RoSchmi.AWS.Models
{
    public class Metadata
    {
        public MetaPredicate desired { get; set; }

        public class MetaPredicate
        {
            public MetaSubject color { get; set; }

            public class MetaSubject
            {
                public int timestamp;
            }
        }

    }
}