using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalletCheck
{
    public class InspectionReport
    {
        string OutName = "";
        List<string> AllCodes = new List<string>();

        public class PalRecord
        {
            public string PalletFile;
            public Dictionary<string, int> Defects = new Dictionary<string, int>();

            public void Defect( string Def )
            {
                if (!Defects.ContainsKey(Def))
                    Defects.Add(Def,0);

                Defects[Def]++;
            }

            public PalRecord(List<string> All)
            {
                foreach (string S in All)
                    Defects.Add(S, 0);
            }
        }

        List<PalRecord> AllPallets = new List<PalRecord>();

        public InspectionReport( string OutputFilename )
        {
            OutName = OutputFilename;
            string[] Codes = PalletDefect.GetCodes();

            for (int i = 0; i < Codes.Length; i++)
            {
                AllCodes.Add(Codes[i]);
            }
        }

        public void AddPallet(Pallet P)
        {
            string Filename = P.Filename.Replace(',', '_');
            PalRecord PR = new PalRecord(AllCodes);

            int nDefects = 0;

            for (int i = 0; i < P.BList.Count; i++)
            {
                foreach (PalletDefect BD in P.BList[i].AllDefects)
                {
                    PR.Defect(BD.Code);
                    nDefects++;
                }
            }

            if (nDefects == 0)
                PR.Defect("NONE");

            PR.PalletFile = Filename;

            AllPallets.Add(PR);
        }

        public void Save()
        {
            StringBuilder SB = new StringBuilder();

            SB.Append("Pallet file name,");

            for (int i = 0; i < AllCodes.Count; i++)
            {
                string DefectName = PalletDefect.CodeToName(AllCodes[i]);
                SB.Append(DefectName);

                if (i < AllCodes.Count - 1)
                    SB.Append(", ");
                else
                    SB.AppendLine(",Total,");
            }

            for ( int i = 0; i < AllPallets.Count; i++ )
            {
                PalRecord PR = AllPallets[i];

                SB.Append(PR.PalletFile+", ");
                int total = 0;
                for (int j = 0; j < AllCodes.Count; j++)
                {
                    int n = PR.Defects[AllCodes[j]];
                    total += n;

                    SB.Append( n.ToString() );
                    if (j < AllCodes.Count - 1)
                        SB.Append(", ");
                    else
                        SB.AppendLine(","+total.ToString()+",");
                }
            }

            //SB.Append("Totals,");

            //for (int i = 0; i < AllCodes.Count; i++)
            //{
            //    SB.Append(string.Format("=SUM({0}2:{1}{2})", (char)('B' + i), (char)('B' + i), AllPallets.Count+1));

            //    if (i < AllCodes.Count - 1)
            //        SB.Append(", ");
            //    else
            //        SB.AppendLine("");
            //}

            System.IO.File.WriteAllText(OutName, SB.ToString());
        }
    }
}
