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

        //StatEntry WholeE = new StatEntry("Whole");
        //StatEntry BH1E = new StatEntry("Bottom_H1");
        //StatEntry BH2E = new StatEntry("Bottom_H2");
        //StatEntry BV1E = new StatEntry("Bottom_V1");
        //StatEntry BV2E = new StatEntry("Bottom_V2");
        //StatEntry BV3E = new StatEntry("Bottom_V3");


        //StatEntry TH1E = new StatEntry("Top_H1");
        //StatEntry TH2E = new StatEntry("Top_H2");
        //StatEntry TH3E = new StatEntry("Top_H3");
        //StatEntry TH4E = new StatEntry("Top_H4");
        //StatEntry TH5E = new StatEntry("Top_H5");
        //StatEntry TH6E = new StatEntry("Top_H6");
        //StatEntry TH7E = new StatEntry("Top_H7");

        //StatEntry LB1E = new StatEntry("Left_B1");
        //StatEntry LB2E = new StatEntry("Left_B2");
        //StatEntry LB3E = new StatEntry("Left_B3");

        //StatEntry RB1E = new StatEntry("Right_B1");
        //StatEntry RB2E = new StatEntry("Right_B2");
        //StatEntry RB3E = new StatEntry("Right_B3");


        public Statistics()
        {
            Entries.Add(TotalCountE);
            Entries.Add(TotalPassE);
            Entries.Add(TotalFailE);
            

            //Entries.Add(WholeE);
            //Entries.Add(TH1E);
            //Entries.Add(TH2E);
            //Entries.Add(TH3E);
            //Entries.Add(TH4E);
            //Entries.Add(TH5E);
            //Entries.Add(TH6E);
            //Entries.Add(TH7E);

            //Entries.Add(BH1E);
            //Entries.Add(BH2E);
            //Entries.Add(BV1E);
            //Entries.Add(BV2E);
            //Entries.Add(BV3E);


            //Entries.Add(LB1E);
            //Entries.Add(LB2E);
            //Entries.Add(LB3E);

            //Entries.Add(RB1E);
            //Entries.Add(RB2E);
            //Entries.Add(RB3E);
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

        public void OnNewPallet(bool isPass)
        {

            TotalCountE.Count1 += 1;
            TotalCountE.Percent1 = 100;

            if (isPass)
            {
                TotalPassE.Count1 += 1;
                TotalPassE.Percent1 = (float)Math.Round((double)(100.0 * TotalPassE.Count1 / TotalCountE.Count1), 2);
                TotalFailE.Percent1 = (float)Math.Round((double)(100.0 * TotalFailE.Count1 / TotalCountE.Count1), 2);
            }
            else
            {
                TotalFailE.Count1 += 1;
                TotalPassE.Percent1 = (float)Math.Round((double)(100.0 * TotalPassE.Count1 / TotalCountE.Count1), 2);
                TotalFailE.Percent1 = (float)Math.Round((double)(100.0 * TotalFailE.Count1 / TotalCountE.Count1), 2);
            }


            //foreach(PalletDefect PD in Pallet.CombinedDefects)
            //{
            //    if (PD.Location == PalletDefect.DefectLocation.Pallet) WholeE.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Top_H1) TH1E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Top_H2) TH2E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Top_H3) TH3E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Top_H4) TH4E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Top_H5) TH5E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Top_H6) TH6E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Top_H7) TH7E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Bottom_H1) BH1E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Bottom_H2) BH1E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Bottom_V1) BV1E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Bottom_V2) BV1E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Bottom_V3) BV1E.Count1 += 1;

            //    if (PD.Location == PalletDefect.DefectLocation.Left_B1) BV1E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Bottom_V3) BV1E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Bottom_V3) BV1E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Bottom_V3) BV1E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Bottom_V3) BV1E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Bottom_V3) BV1E.Count1 += 1;

            //    if (PD.Location == PalletDefect.DefectLocation.Left_B1) LB1E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Left_B2) LB2E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Left_B3) LB3E.Count1 += 1;

            //    if (PD.Location == PalletDefect.DefectLocation.Right_B1) RB1E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Right_B2) RB2E.Count1 += 1;
            //    if (PD.Location == PalletDefect.DefectLocation.Right_B3) RB3E.Count1 += 1;

            //}
        }

        public void Reset()
        {
            foreach (var entry in Entries)
            {
                entry.Count1 = 0;
                entry.Count2 = 0;
                entry.Percent1 = 0;
                entry.Percent2 = 0;
            }
        }
    }

    
}
