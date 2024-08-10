using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PalletCheck
{

    public class StatEntry
    {
        public string Description { get; set; }
        public int Count1 { get; set; }
        public int Count2 { get; set; }
        public float Percent1 { get; set; }
        public float Percent2 { get; set; }

        public StatEntry( string desc )
        {
            Description = desc;
        }
    }

    public class Statistics
    {
        public List<StatEntry> Entries = new List<StatEntry>();

        StatEntry TotalPassE    = new StatEntry("Pallets Passed");
        StatEntry TotalFailE    = new StatEntry("Pallets Failed");
        StatEntry TotalCountE   = new StatEntry("Pallets Total");

        StatEntry WholeE = new StatEntry("Whole");
        StatEntry H1E = new StatEntry("H1");
        StatEntry H2E = new StatEntry("H2");
        StatEntry H3E = new StatEntry("H3");
        StatEntry V1E = new StatEntry("V1");
        StatEntry V2E = new StatEntry("V2");

        public Statistics()
        {
            Entries.Add(TotalPassE);
            Entries.Add(TotalFailE);
            Entries.Add(TotalCountE);

            Entries.Add(WholeE);
            Entries.Add(H1E);
            Entries.Add(H2E);
            Entries.Add(H3E);
            Entries.Add(V1E);
            Entries.Add(V2E);
        }


        //public enum DefectLocation
        //{
        //    Pallet,
        //    H1,
        //    H2,
        //    H3,
        //    V1,
        //    V2
        //}

        public void OnNewPallet(Pallet P)
        {

            TotalCountE.Count1 += 1;

            if (P.State == Pallet.InspectionState.Pass)
            {
                TotalPassE.Count1 += 1;
                TotalPassE.Percent1 = (float)Math.Round((double)(100.0 * TotalPassE.Count1 / TotalCountE.Count1), 2);
            }
            if (P.State == Pallet.InspectionState.Fail)
            {
                TotalFailE.Count1 += 1;
                TotalFailE.Percent1 = (float)Math.Round((double)(100.0 * TotalFailE.Count1 / TotalCountE.Count1), 2);
            }


            foreach(PalletDefect PD in P.AllDefects)
            {
                if (PD.Location == PalletDefect.DefectLocation.Pallet) WholeE.Count1 += 1;
                if (PD.Location == PalletDefect.DefectLocation.H1) H1E.Count1 += 1;
                if (PD.Location == PalletDefect.DefectLocation.H2) H2E.Count1 += 1;
                if (PD.Location == PalletDefect.DefectLocation.H3) H3E.Count1 += 1;
                if (PD.Location == PalletDefect.DefectLocation.V1) V1E.Count1 += 1;
                if (PD.Location == PalletDefect.DefectLocation.V2) V2E.Count1 += 1;

            }
        }
    }

    
}
