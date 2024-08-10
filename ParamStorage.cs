using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalletCheck
{
    public static class ParamStorage
    {

        public static string LastFilename;
        public static bool HasChangedSinceLastSave;
        public static object ObjectLock = new object();

        //=====================================================================
        public static Dictionary<string, Dictionary<string, string>> Categories = new Dictionary<string, Dictionary<string, string>>();

        //=====================================================================
        public static void SaveInHistory(string Reason)
        {
            DateTime DT = DateTime.Now;
            string dt_str = String.Format("{0:0000}{1:00}{2:00}_{3:00}{4:00}{5:00}", DT.Year, DT.Month, DT.Day, DT.Hour, DT.Minute, DT.Second);

            string SaveDir = MainWindow.SettingsHistoryRootDir;

            ParamStorage.Save(SaveDir + "\\" + dt_str + "_SETTINGS_" + Reason + ".txt", false);
        }

        //=====================================================================
        public static void Load(string FileName)
        {
            lock (ObjectLock)
            {
                LastFilename = FileName;

                Logger.WriteBorder("ParamStorage::Load()");
                Logger.WriteLine(FileName);

                // Early exit if no file exists
                if (!System.IO.File.Exists(FileName))
                {
                    Logger.WriteLine("No file exists");
                    return;
                }

                string[] Lines = System.IO.File.ReadAllLines(FileName);

                string Category = "Default";

                foreach (string L in Lines)
                {
                    Logger.WriteLine(L);

                    if (string.IsNullOrEmpty(L))
                        continue;

                    if (L.Contains("["))
                    {
                        Category = L.Trim();
                        Category = Category.Replace("[", "");
                        Category = Category.Replace("]", "");
                    }
                    else
                    {
                        string[] Fields = L.Split('=');

                        string Param = Fields[0].Trim();
                        string Value = Fields[1].Trim();

                        Set(Category, Param, Value, true);
                    }
                }

                HasChangedSinceLastSave = false;
                Logger.WriteBorder("ParamStorage::Load() COMPLETE");
                ParamStorage.SaveInHistory("LOADED");
            }
        }

        //=====================================================================
        public static void Save(string FileName, bool ClearHasChanged=true)
        {
            lock (ObjectLock)
            {
                Logger.WriteBorder("ParamStorage::Save()");
                Logger.WriteLine(FileName);

                // Save a backup of existing file
                if (System.IO.File.Exists(FileName))
                {
                    if (System.IO.File.Exists(FileName + ".bak"))
                        System.IO.File.Delete(FileName + ".bak");

                    System.IO.File.Copy(FileName, FileName + ".bak");
                }

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

                if (ClearHasChanged)
                {
                    Logger.WriteLine(SB.ToString());
                    HasChangedSinceLastSave = false;
                }

                Logger.WriteBorder("ParamStorage::Save() COMPLETE");
            }
        }

        //=====================================================================
        public static void Set(string Category, string fieldName, string Value, bool Loading=false)
        {
            lock (ObjectLock)
            {
                if (!Categories.ContainsKey(Category))
                    Categories.Add(Category, new Dictionary<string, string>());

                string PrevValue = "n\\a";
                if (!Loading && Categories[Category].ContainsKey(fieldName)) PrevValue = Categories[Category][fieldName];

                Categories[Category][fieldName] = Value;

                HasChangedSinceLastSave = true;

                if (!Loading)
                    Logger.WriteLine("ParamStorage Value Changed [" + Category + "]  " + fieldName + "\nfrom: " + PrevValue + "\nto:   " + Value);
            }
        }

        //=====================================================================
        public static string Get(string Category, string Field)
        {
            lock (ObjectLock)
            {
                if (Categories.ContainsKey(Category))
                {
                    if (Categories[Category].ContainsKey(Field))
                        return Categories[Category][Field];
                }

                return "";
            }
        }

        //=====================================================================
        public static bool Contains(string Field)
        {
            lock (ObjectLock)
            {
                string Category = FindCategory(Field);
                if (Categories.ContainsKey(Category))
                {
                    if (Categories[Category].ContainsKey(Field))
                        return true;
                }

                return false;
            }
        }

        //=====================================================================
        static string FindCategory(string Field)
        {
            lock (ObjectLock)
            {
                foreach (KeyValuePair<string, Dictionary<string, string>> KVP in Categories)
                {
                    if (KVP.Value.ContainsKey(Field))
                        return KVP.Key;
                }

                return "";
            }
        }


        //=====================================================================
        public static int GetInt(string Field)
        {
            lock (ObjectLock)
            {
                string Category = FindCategory(Field);

                string R = Get(Category, Field);

                int V;

                int.TryParse(R, out V);

                return V;
            }
        }

        //=====================================================================

        public static float GetPPIX()
        {
            return (1 / (GetFloat("MM Per Pixel X") / 25.4f));
        }

        public static float GetPPIY()
        {
            return (1 / (GetFloat("MM Per Pixel Y") / 25.4f));
        }

        public static float GetPPIZ()
        {
            return (1 / (GetFloat("MM Per Pixel Z") / 25.4f));
        }

        public static float GetInchesX(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("(mm)")) f /= 25.4f;
            if (Field.Contains("(px)")) f *= GetFloat("MM Per Pixel X")/25.4f;
            return f;
        }

        public static float GetInchesY(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("(mm)")) f /= 25.4f;
            if (Field.Contains("(px)")) f *= GetFloat("MM Per Pixel Y") / 25.4f;
            return f;
        }

        public static float GetInchesZ(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("(mm)")) f /= 25.4f;
            if (Field.Contains("(px)")) f *= GetFloat("MM Per Pixel Z") / 25.4f;
            return f;
        }

        public static float GetMMX(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("(in)")) f *= 25.4f;
            if (Field.Contains("(px)")) f *= GetFloat("MM Per Pixel X");
            return f;
        }

        public static float GetMMY(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("(in)")) f *= 25.4f;
            if (Field.Contains("(px)")) f *= GetFloat("MM Per Pixel Y");
            return f;
        }

        public static float GetMMZ(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("(in)")) f *= 25.4f;
            if (Field.Contains("(px)")) f *= GetFloat("MM Per Pixel Z");
            return f;
        }


        public static int GetPixX(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("(in)")) f /= (GetFloat("MM Per Pixel X")/25.4f);
            if (Field.Contains("(mm)")) f /= GetFloat("MM Per Pixel X");
            return (int)f;
        }

        public static int GetPixY(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("(in)")) f /= (GetFloat("MM Per Pixel Y")/25.4f);
            if (Field.Contains("(mm)")) f /= GetFloat("MM Per Pixel Y");
            return (int)f;
        }

        public static int GetPixZ(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("(in)")) f /= (GetFloat("MM Per Pixel Z")/25.4f);
            if (Field.Contains("(mm)")) f /= GetFloat("MM Per Pixel Z");
            return (int)f;
        }

        //=====================================================================
        public static float GetFloat(string Field)
        {
            lock (ObjectLock)
            {
                string Category = FindCategory(Field);

                string R = Get(Category, Field);

                float V;

                float.TryParse(R, out V);

                return V;
            }
        }
        //=====================================================================
        public static string GetString(string Field)
        {
            lock (ObjectLock)
            {
                string Category = FindCategory(Field);

                string R = Get(Category, Field);

                return R;
            }
        }

        //=====================================================================
        public static ushort[] GetArray(string Field)
        {
            lock (ObjectLock)
            {
                string Category = FindCategory(Field);

                string R = Get(Category, Field);

                string[] col = R.Split(',');
                ushort[] vals = new ushort[col.Length];

                for (int i = 0; i < col.Length; i++)
                {
                    vals[i] = ushort.Parse(col[i]);
                }

                return vals;
            }
        }

    }
}
