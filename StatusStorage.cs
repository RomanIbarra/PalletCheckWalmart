using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalletCheck
{
    public static class StatusStorage
    {

        public static string LastFilename;
        public static bool HasChanged;

        public delegate void StatusChangedCB(string category, string field, string value);
        public static event StatusChangedCB OnStatusChanged_Callback;

        //=====================================================================
        public static Dictionary<string, Dictionary<string, string>> Categories = new Dictionary<string, Dictionary<string, string>>();


        //=====================================================================
        public static void Save(string FileName)
        {
            Logger.WriteBorder("StatusStorage::Save()");
            Logger.WriteLine(FileName);

            StringBuilder SB = new StringBuilder();

            foreach (KeyValuePair<string, Dictionary<string, string>> KVP in Categories)
            {
                SB.AppendLine("[" + KVP.Key + "]");

                foreach (KeyValuePair<string, string> PV in KVP.Value)
                {
                    SB.AppendLine(PV.Key + " = " + PV.Value);
                }
                SB.AppendLine("");
            }
            System.IO.File.WriteAllText(FileName, SB.ToString());

            Logger.WriteLine(SB.ToString());

            Logger.WriteBorder("StatusStorage::Save() COMPLETE");
        }

        //=====================================================================

        public static void Set(string fieldName, string Value)
        {
            Set("Misc", fieldName, Value);
        }
        public static void Set(string Category, string fieldName, string Value)
        {
            if (!Categories.ContainsKey(Category))
                Categories.Add(Category, new Dictionary<string, string>());

            string PrevValue = "n\\a";
            if(Categories[Category].ContainsKey(fieldName)) PrevValue = Categories[Category][fieldName];

            Categories[Category][fieldName] = Value;

            if(PrevValue != Value)
            {
                HasChanged = true;
                if (OnStatusChanged_Callback != null)
                    OnStatusChanged_Callback(Category, fieldName, Value);
            }


            //Logger.WriteLine("StatusStorage Value Changed [" + Category + "]  " + fieldName + "\nfrom: "+PrevValue+"\nto:   "+Value);
            
        }

        //=====================================================================
        public static string Get(string Category, string Field)
        {
            if (Categories.ContainsKey(Category))
            {
                if (Categories[Category].ContainsKey(Field))
                    return Categories[Category][Field];
            }

            return "";
        }

        //=====================================================================
        public static bool Contains(string Field)
        {
            string Category = FindCategory(Field);
            if (Categories.ContainsKey(Category))
            {
                if (Categories[Category].ContainsKey(Field))
                    return true;
            }

            return false;
        }

        //=====================================================================
        static string FindCategory(string Field)
        {
            foreach (KeyValuePair<string, Dictionary<string, string>> KVP in Categories)
            {
                if (KVP.Value.ContainsKey(Field))
                    return KVP.Key;
            }

            return "";
        }


        //=====================================================================
        public static int GetInt(string Field)
        {
            string Category = FindCategory(Field);

            string R = Get(Category, Field);

            int V;
            
            int.TryParse(R, out V);            

            return V;
        }

        //=====================================================================
        public static float GetFloat(string Field)
        {
            string Category = FindCategory(Field);

            string R = Get(Category, Field);

            float V;

            float.TryParse(R, out V);

            return V;
        }
        //=====================================================================
        public static string GetString(string Field)
        {
            string Category = FindCategory(Field);

            string R = Get(Category, Field);

            return R;
        }

        //=====================================================================
        public static ushort[] GetArray(string Field)
        {
            string Category = FindCategory(Field);

            string R = Get(Category, Field);

            string[] col = R.Split(',');
            ushort[] vals = new ushort[col.Length];

            for ( int i = 0; i < col.Length; i++ )
            {
                vals[i] = ushort.Parse(col[i]);
            }

            return vals;
        }

    }
}
