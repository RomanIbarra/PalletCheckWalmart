using PalletCheck;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace PalletCheck
{
    public partial class ParamConfig : Form
    {
        public static string LastLoadedParamFile = "";
        System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
        System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
        Color SaveSettingsButtonColor;
        //ParamStorage ParamStorageGeneral = MainWindow.ParamStorageGeneral;
        ParamStorage ParamStorageCurrent;
        string CurrentLastUsedFileName;
        public ParamConfig(ParamStorage ParamStorageGeneral,string LastUsedFile)
        {
            InitializeComponent();

            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;

            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            ParamStorageCurrent = ParamStorageGeneral;
            CurrentLastUsedFileName = LastUsedFile;
            BuildPagesFromParamStorage();

            System.Windows.Forms.Timer T = new System.Windows.Forms.Timer();
            T.Interval = 500;
            T.Tick += SaveSettingsColorUpdate;
            T.Start();
            SaveSettingsButtonColor = btnSaveSettings.BackColor;
        }

        private void SaveSettingsColorUpdate(object sender, EventArgs e)
        {
            if (ParamStorageCurrent.HasChangedSinceLastSave)
            {
                if (btnSaveSettings.BackColor == SaveSettingsButtonColor)
                    btnSaveSettings.BackColor = Color.FromArgb(196, 196, 0);
                else
                    btnSaveSettings.BackColor = SaveSettingsButtonColor;
            }
            else
            {
                btnSaveSettings.BackColor = SaveSettingsButtonColor;
            }
        }

        private void BuildPagesFromParamStorage()
        {
            tabCategories.TabPages.Clear();

            foreach (KeyValuePair<string, Dictionary<string, string>> kvp in ParamStorageCurrent.Categories)
            {
                TabPage TP = new TabPage(kvp.Key);
                TP.Tag = kvp.Value;
                tabCategories.TabPages.Add(TP);

                DataGridView DGV = new DataGridView();
                DGV.Columns.Clear();
                DGV.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                DGV.Columns.Add("Parameter", "Parameter");
                DGV.Columns[0].ReadOnly = true;
                DGV.Columns.Add("Value", "Value");
                DGV.AllowUserToAddRows = false;
                DGV.AllowUserToOrderColumns = false;
                DGV.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
                DGV.DefaultCellStyle = dataGridViewCellStyle2;
                DGV.RowHeadersVisible = false;
                DGV.Dock = DockStyle.Fill;

                // 设置表头高度
                DGV.Columns[0].Width = 120;
                DGV.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                // 启用单元格文字换行
                DGV.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

                // 自动调整行高
                DGV.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

                // 设置表头高度
                DGV.ColumnHeadersHeight = 50;

                // 设置交替行颜色
                DGV.RowPrePaint += (sender, e) =>
                {
                    if (e.RowIndex % 2 == 0)
                        DGV.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.White;
                    else
                        DGV.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightBlue;
                };

                TP.Controls.Add(DGV);
                DGV.Rows.Clear();

                foreach (KeyValuePair<string, string> Params in kvp.Value)
                {
                    if (string.IsNullOrEmpty(Params.Key))
                        continue;

                    int Row = DGV.Rows.Add();
                    DGV.Rows[Row].SetValues(new string[] { Params.Key, Params.Value });
                    DGV.Rows[Row].Tag = kvp.Value;
                }

                DGV.CellValueChanged += DGV_CellValueChanged;
            }
        }




        private void DGV_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView DGV = (DataGridView)sender;
            DataGridViewRow DVR = DGV.Rows[e.RowIndex];

            Dictionary<string, string> Param = DVR.Tag as Dictionary<string, string>;

            Param[DVR.Cells[0].Value as string] = DVR.Cells[1].Value as string;

            //ParamStorage.Save(ParamStorage.LastFilename);

            ParamStorageCurrent.HasChangedSinceLastSave = true;
            ParamStorageCurrent.SaveInHistory("CHANGED");

        }



        //private void btnSaveSettings_Click(object sender, EventArgs e)
        //{

        //}

        private void btnLoadSettings_Click(object sender, EventArgs e)
        {
            Logger.ButtonPressed("ParamConfig.btnLoadSettings");
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Text files|*.txt";
            ofd.FileName = LastLoadedParamFile;
            ofd.InitialDirectory = MainWindow.ConfigRootDir;
            MainWindow.SetLastUsedParamFileName(CurrentLastUsedFileName);

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                ParamStorageCurrent.Load(ofd.FileName);
                MainWindow.LastUsedParamFile = ofd.FileName;
                BuildPagesFromParamStorage();
            }
        }

        //=====================================================================
        private void btnSaveSettings_Click(object sender, EventArgs e)
        {
            Logger.ButtonPressed("ParamConfig.btnSaveSettings");
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Text files|*.txt";
            sfd.FileName = LastLoadedParamFile;
            sfd.InitialDirectory = MainWindow.ConfigRootDir;
            MainWindow.SetLastUsedParamFileName(CurrentLastUsedFileName);

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                MainWindow.LastUsedParamFile = sfd.FileName;
                ParamStorageCurrent.Save(sfd.FileName);
            }
        }


        //private void ParamConfig_Load(object sender, EventArgs e)
        //{

        //}

        private void btnCalcBaseline_Click(object sender, EventArgs e)
        {
            Logger.ButtonPressed("ParamConfig.btnCalcBaseline");

            int Width = ParamStorageCurrent.GetInt("StitchedWidth");;

            System.Windows.Forms.FolderBrowserDialog OFD = new System.Windows.Forms.FolderBrowserDialog();
            OFD.RootFolder = Environment.SpecialFolder.MyComputer;
            OFD.SelectedPath = MainWindow.RecordingRootDir;
            OFD.ShowNewFolderButton = false;
            if (OFD.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (System.IO.Directory.Exists(OFD.SelectedPath))
                {
                    string[] Files = System.IO.Directory.GetFiles(OFD.SelectedPath, "*_rng.r3", SearchOption.AllDirectories);

                    Logger.WriteLine("BaseLine Pallet Dir:   " + OFD.SelectedPath);
                    Logger.WriteLine("BaseLine Pallet Count: " + Files.Length.ToString());

                    if (Files.Length == 0)
                    {
                        MessageBox.Show("No pallets were found in this folder.");
                        return;
                    }
                    else
                    {
                        if( MessageBox.Show("Found "+Files.Length.ToString()+" pallets to calculate baseline.\nContinue?","Start Calculating Baseline",MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                        {
                            return;
                        }
                        

                    }

                    List<UInt16[]> Vals = new List<ushort[]>();

                    for (int i = 0; i < Width; i++)
                        Vals.Add(new UInt16[UInt16.MaxValue]);

                    for (int i = 0; i < Files.Length; i++)
                    {
                        BinaryReader BR = new BinaryReader(new FileStream(Files[i], FileMode.Open));
                        List<UInt16> Cap = new List<ushort>();
                        BR.BaseStream.Seek(0, SeekOrigin.End);
                        long sz = BR.BaseStream.Position / 2;
                        BR.BaseStream.Seek(0, SeekOrigin.Begin);

                        for (long j = 0; j < sz; j++)
                        {
                            UInt16 V = BR.ReadUInt16();
                            Cap.Add(V);
                        }
                        BR.Close();

                        UInt16[] Buf = Cap.ToArray();
                        int Height = Buf.Length / Width;


                        // Apply cleanup values before gathering column values
                        //if (true)
                        //{
                        //    int leftEdge = ParamStorage.GetInt("Raw Capture ROI Left (px)");
                        //    int rightEdge = ParamStorage.GetInt("Raw Capture ROI Right (px)");
                        //    int clipMin = ParamStorage.GetInt("Raw Capture ROI Min Z (px)");
                        //    int clipMax = ParamStorage.GetInt("Raw Capture ROI Max Z (px)");
                            
                        //    for (int y = 0; y < Height; y++)
                        //        for (int x = 0; x < Width; x++)
                        //        {
                        //            int j = x + y * Width;
                        //            int v = Buf[j];
                        //            if ((v > clipMax) || (v < clipMin)) Buf[j] = 0;
                        //            if ((x < leftEdge) || (x > rightEdge)) Buf[j] = 0;
                        //        }
                        //}


                        for (int y = 0; y < Height; y++)
                        {
                            // Check if this row has a long continuous span
                            int best_start_x = -1;
                            int best_end_x = -1;
                            int start_x = -1;
                            for (int x = 0; x < Width; x++)
                            {
                                int v = Buf[y * Width + x];
                                if((v!=0) && (x<Width-1))
                                {
                                    if (start_x != -1)
                                    {
                                        // just continue
                                    }
                                    else
                                    {
                                        // new start
                                        start_x = x;
                                    }
                                }
                                else
                                {
                                    if (start_x != -1)
                                    {
                                        int end_x = x - 1;
                                        if ((end_x - start_x) > (best_end_x - best_start_x))
                                        {
                                            best_start_x = start_x;
                                            best_end_x = end_x;
                                        }
                                        start_x = -1;
                                    }
                                }
                            }

                            int best_len = best_end_x - best_start_x;
                            if (best_len >= 1800)
                            {
                                Logger.WriteLine("found baseline row");
                                for (int x = 0; x < Width; x++)
                                {
                                    UInt16 V = Buf[y * Width + x];
                                    Vals[x][V]++;
                                }
                            }
                        }
                    }

                    // Find mode for each col
                    UInt16[] NewBaseline = new UInt16[Width];
                    for (int i = 0; i < Width; i++)
                    {
                        int MaxVal = 0;
                        int MaxIdx = 0;

                        for (int y = 1; y < UInt16.MaxValue; y++)
                        {
                            if (Vals[i][y] > MaxVal)
                            {
                                MaxIdx = y;
                                MaxVal = Vals[i][y];
                            }
                        }

                        NewBaseline[i] = (UInt16)MaxIdx;
                    }

                    // smooth the baseline
                    UInt16[] NewBaselineSmoothed = new UInt16[Width];
                    for (int i = 10; i < (Width - 10); i++)
                    {
                        int Avg = 0;
                        for (int j = (i - 10); j < (i + 10); j++)
                        {
                            Avg += NewBaseline[j];
                        }

                        NewBaselineSmoothed[i] = (UInt16)(Avg / 20);
                    }


                    // Save test profile
                    StringBuilder SB = new StringBuilder();

                    for (int i = 0; i < Width; i++)
                    {
                        SB.Append(NewBaselineSmoothed[i].ToString());
                        if (i< Width - 1) SB.Append(",");
                    }

                    ParamStorageCurrent.Set("Baseline", "BaselineData", SB.ToString());
                    BuildPagesFromParamStorage();

                    //ParamStorage.GetArray(" ") = 
                    //string p = ParamStorage.GetString("Auto Baseline Path");
                    //string outputPath = System.IO.Path.Combine(p, "TestScan.txt");

                    //System.IO.File.WriteAllText(outputPath, SB.ToString());

                    Logger.WriteLine("BaseLine Calculation Complete");

                    MessageBox.Show("Baseline Calculated from " + Files.Length.ToString() + " Pallets.", "CALCULATE BASELINE COMPLETE");
                }
            }
        }
    }
}
