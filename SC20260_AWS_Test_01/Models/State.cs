using System;
using System.Collections;
using System.Text;
using System.Threading;

namespace RoSchmi.AWS.Models
{
    public class State 
    {
        public Predicate desired { get; set; }

        public class Predicate
        {    
            public string Leftcolor;
            public string Rightcolor;
        }

    }
}
