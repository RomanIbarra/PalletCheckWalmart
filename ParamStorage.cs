using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PalletCheck
{
    public class ParamStorage
    {
        public string LastFilename;
        public bool HasChangedSinceLastSave;
        public object ObjectLock = new object();
        public string ParamStoragePositionName;
        public Dictionary<string, Dictionary<string, string>> Categories = new Dictionary<string, Dictionary<string, string>>();

        public void SaveInHistory(string Reason)
        {
            DateTime DT = DateTime.Now;
            string dt_str = String.Format("{0:0000}{1:00}{2:00}_{3:00}{4:00}{5:00}", DT.Year, DT.Month, DT.Day, DT.Hour, DT.Minute, DT.Second);

            string SaveDir = MainWindow.SettingsHistoryRootDir;

            Save(SaveDir + "\\" + dt_str + "_SETTINGS_" + Reason + ".txt", false);
        }

        public void Load(string FileName)
        {
            lock (ObjectLock)
            {
                LastFilename = FileName;

                Logger.WriteBorder("ParamStorage::Load()");
                Logger.WriteLine(FileName);

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
                        Category = Category.Replace("[", "").Replace("]", "");
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
                SaveInHistory("LOADED");
            }
        }

        public void LoadXML(string fileName)
        {
            XmlDocument file = new XmlDocument();
            file.Load(fileName);
            Categories.Clear();

            foreach (XmlNode node in file.DocumentElement.ChildNodes)
            {
                Categories.Add(node.Name, new Dictionary<string, string>());
                foreach (XmlNode innerNode in node.ChildNodes)
                {
                    Categories[node.Name].Add(innerNode.Name, innerNode.InnerText);
                }
            }
        }

        public void LoadXMLParameters(string fileName, string cameraPosition)
        {
            XmlDocument paramsFile = new XmlDocument();
            paramsFile.Load(fileName);
            var node = paramsFile.SelectSingleNode("Parameters/" + cameraPosition);

            foreach (XmlNode innerNode in node)
            {
                Categories.Add(innerNode.Name, new Dictionary<string, string>());
                foreach (XmlNode param in innerNode.ChildNodes)
                {
                    Categories[innerNode.Name].Add(param.Name, param.InnerText);
                }
            }
        }

        public void Save(string FileName, bool ClearHasChanged = true)
        {
            lock (ObjectLock)
            {
                Logger.WriteBorder("ParamStorage::Save()");
                Logger.WriteLine(FileName);

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
        public void SaveXML(string filePath)
        {
            lock (ObjectLock)
            {
                Logger.WriteBorder("ParamStorage::Save()");
                Logger.WriteLine(filePath);
                XElement positionNode = new XElement(this.ParamStoragePositionName);

                XElement parametersFile = XElement.Load(MainWindow.ConfigRootDir + "\\Parameters.xml");
                XElement targetNode = parametersFile.Descendants(this.ParamStoragePositionName).FirstOrDefault();

                if (targetNode != null)
                {
                    // Create a new node with updated child elements
                    foreach (KeyValuePair<string, Dictionary<string, string>> KVP in Categories)
                    {
                        Dictionary<string, string> childNodes = KVP.Value;
                        XElement categoryElement = new XElement(KVP.Key, childNodes.Select(kv => new XElement(kv.Key, kv.Value)));
                        positionNode.Add(categoryElement);
                    }

                    targetNode.ReplaceWith(positionNode); // Replace the old node with the new one
                    parametersFile.Save(filePath); // Save the modified XML back to the file
                }
            }
        }

        public void Set(string Category, string fieldName, string Value, bool Loading = false)
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

        public string Get(string Category, string Field)
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

        public bool Contains(string Field)
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

        private string FindCategory(string Field)
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

        public int GetInt(string Field)
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

        public float GetFloat(string Field)
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

        public string GetString(string Field)
        {
            lock (ObjectLock)
            {
                string Category = FindCategory(Field);

                string R = Get(Category, Field);

                return R;
            }
        }

        public ushort[] GetArray(string Field)
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

        public  float GetPPIX()
        {
            return (1 / (GetFloat("MMPerPixelX") / 25.4f));
        }

        public float GetPPIY()
        {
            return (1 / (GetFloat("MMPerPixelY") / 25.4f));
        }

        public   float GetPPIZ()
        {
            return (1 / (GetFloat("MMPerPixelZ") / 25.4f));
        }

        public float GetInchesX(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("(mm)")) f /= 25.4f;
            if (Field.Contains("(px)")) f *= GetFloat("MMPerPixelX") / 25.4f;
            return f;
        }

        public float GetInchesY(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("(mm)")) f /= 25.4f;
            if (Field.Contains("(px)")) f *= GetFloat("MMPerPixelY") / 25.4f;
            return f;
        }

        public  float GetInchesZ(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("(mm)")) f /= 25.4f;
            if (Field.Contains("_px)")) f *= GetFloat("MMPerPixelZ") / 25.4f;
            return f;
        }

        public float GetMMX(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("_in")) f *= 25.4f;
            if (Field.Contains("_px")) f *= GetFloat("MMPerPixelX");
            return f;
        }

        public float GetMMY(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("_in")) f *= 25.4f;
            if (Field.Contains("_px")) f *= GetFloat("MMPerPixelY");
            return f;
        }

        public  float GetMMZ(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("_in")) f *= 25.4f;
            if (Field.Contains("_px")) f *= GetFloat("MMPerPixelZ");
            return f;
        }


        public  int GetPixX(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("_in")) f /= (GetFloat("MMPerPixelX") / 25.4f);
            if (Field.Contains("_mm")) f /= GetFloat("MMPerPixelX");
            return (int)f;
        }

        public  int GetPixY(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("_in")) f /= (GetFloat("MMPerPixelY") / 25.4f);
            if (Field.Contains("_mm")) f /= GetFloat("MMPerPixelY");
            return (int)f;
        }

        public float GetPixZ(string Field)
        {
            float f = GetFloat(Field);
            if (Field.Contains("_in")) f /= (GetFloat("MMPerPixelZ") / 25.4f);
            if (Field.Contains("_mm")) f /= GetFloat("MMPerPixelZ");
            return (float)f;
        }

        //=====================================================================



    }
}
