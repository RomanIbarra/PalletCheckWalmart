using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalletCheck
{
    public static class Benchmark
    {
        static Dictionary<string, Times> AllTimes = new Dictionary<string, Times>();

        public class Times
        {
            public DateTime Start;
            public DateTime End;
        }

        public static void Clear()
        {
            AllTimes.Clear();
        }

        public static void Start(string Section)
        {
            if (!AllTimes.ContainsKey(Section))
                AllTimes[Section] = new Times();

            AllTimes[Section].Start = DateTime.Now;
        }

        public static void Stop(string Section)
        {
            if ( AllTimes.ContainsKey(Section) )
            {
                AllTimes[Section].End = DateTime.Now;
            }
        }

        public static string Report()
        {
            StringBuilder SB = new StringBuilder();

            foreach( KeyValuePair<string,Times> T in AllTimes )
            {
                Times TV = T.Value;
                SB.AppendLine(string.Format("{0} - {1:0.0000} mSec", T.Key, (TV.End - TV.Start).TotalMilliseconds));
            }

            return SB.ToString();
        }
    }
}
