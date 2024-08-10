using ImageTools;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace PalletCheck
{

    public partial class CaptureBuffer
    {
        public enum DataTypes
        {
            Unknown = 0,
            Range = 1,
            Reflectance = 2,
            Scatter = 3
        };

        public UInt16[]     Buf;
        public string       Name;
        public int          Width;
        public int          Height;
        public int          BPP = 16;
        public DataTypes    DataType = DataTypes.Unknown;
        //public static int   SensorWidth = 2560;

        public enum PaletteTypes
        {
            Auto = 0,
            Gray = 1,
            Baselined = 2,
        };
        public PaletteTypes PaletteType = PaletteTypes.Auto;
        private float[] range;
        private short v;

        //=====================================================================



        //=====================================================================

        public void Clear()
        {
            Buf = null;
            Width = 0;
            Height = 0;
            Name = "";
        }

        //=====================================================================
        public CaptureBuffer(byte[] _intensity, object width)
        {
            Clear();
        }

        public CaptureBuffer(UInt16[] RawBuf, int Width, int Height)
        {
            Clear();
            this.Width = Width;
            this.Height = Height;
            Buf = (UInt16[])RawBuf.Clone();
        }

        public CaptureBuffer(byte[] RawBuf, int Width, int Height)
        {
            Clear();
            this.Width = Width;
            this.Height = Height;
            Buf = new ushort[Width * Height];
            long sum = 0;
            for (int i = 0; i < RawBuf.Length; i++)
            {
                Buf[i] = (ushort)(((ushort)RawBuf[i]) * 100);
                sum += Buf[i];
            }
            Logger.WriteLine("CaptureBuffer() of Refl: " + sum.ToString());
        }

        public CaptureBuffer(CaptureBuffer SourceCB)
        {
            Clear();
            Buf = (UInt16[])SourceCB.Buf.Clone();
            Width = SourceCB.Width;
            Height = SourceCB.Height;
            Name = SourceCB.Name;
            PaletteType = SourceCB.PaletteType;
        }

        public CaptureBuffer(float[] range, int width, int height)
        {
            this.range = range;
            Width = width;
            Height = height;
        }

        public CaptureBuffer()
        {
        }

        public CaptureBuffer(short v, int width, int height)
        {
            this.v = v;
            Width = width;
            Height = height;
        }

        //=====================================================================

        public void FlipTopBottom()
        {
            for (int i=0; i<Height; i++)
            {
                int t = i;
                int b = Height - 1 - i;
                if (t >= b) break;
                int toff = t * Width;
                int boff = b * Width;

                for(int x=0; x<Width; x++)
                {
                    UInt16 v = Buf[toff + x];
                    Buf[toff + x] = Buf[boff + x];
                    Buf[boff + x] = v;
                }
            }
        }

        //=====================================================================

        public void ClearBuffer()
        {
            for (int i = 0; i < Buf.Length; i++)
                Buf[i] = 0;
        }

        //=====================================================================
        public void Load(string Filename)
        {
            if (Filename.EndsWith(".r3"))
            {
                BinaryReader BR = new BinaryReader(new FileStream(Filename, FileMode.Open));
                List<UInt16> Cap = new List<ushort>();
                BR.BaseStream.Seek(0, SeekOrigin.End);
                long sz = BR.BaseStream.Position / 2;
                BR.BaseStream.Seek(0, SeekOrigin.Begin);

                for (long i = 0; i < sz; i++)
                {
                    UInt16 V = BR.ReadUInt16();
                    Cap.Add(V);
                }

                Buf = Cap.ToArray();
                Width = 3136;
                Height = Buf.Length / Width;
            }

            if (Filename.EndsWith(".cap"))
            {
                BinaryReader BR = new BinaryReader(new FileStream(Filename, FileMode.Open));


                if(BR.ReadChar()=='C' && BR.ReadChar()=='P' && BR.ReadChar()=='B' && BR.ReadChar()=='F')
                {

                    int Version = BR.ReadInt32();
                    if (Version == 1000)
                    {
                        Width = BR.ReadInt32();
                        Height = BR.ReadInt32();
                        BPP = BR.ReadInt32();
                        DataType = (DataTypes)BR.ReadInt32();

                        // Reserve space for 32 bytes of other info
                        for (int i = 0; i < 8; i++) BR.ReadUInt32();

                        int nBytes = BR.ReadInt32();
                        Buf = new UInt16[nBytes / 2];
                        byte[] byteArray = BR.ReadBytes(nBytes);
                        unsafe
                        {
                            fixed (ushort* ushortArrayPtr = Buf)
                            {
                                IntPtr ushortIntPtr = new IntPtr(ushortArrayPtr);
                                Marshal.Copy(byteArray, 0, ushortIntPtr, byteArray.Length);
                            }
                        }
                        byteArray = null;
                    }                    
                }


            }
        }

        //=====================================================================

        //public void InitFromBuf(UInt16[] _Buf)
        //{
        //    Buf = (UInt16[])_Buf.Clone();
        //    Width = SensorWidth;
        //    Height = Buf.Length / SensorWidth;
        //}

        //=====================================================================
        public bool Save(string Filename)
        {
            if (Filename.EndsWith(".r3"))
            {
                try
                {
                    BinaryWriter BW = new BinaryWriter(new FileStream(Filename, FileMode.Create));
                    foreach (ushort V in Buf) BW.Write(V);
                    BW.Close();
                }
                catch (Exception)
                {
                    return false;
                }
                return true;
            }

            if (Filename.EndsWith(".cap"))
            {
                try
                {
                    BinaryWriter BW = new BinaryWriter(new FileStream(Filename, FileMode.Create));

                    // Write signature so we can confirm file type
                    BW.Write((char)'C');
                    BW.Write((char)'P');
                    BW.Write((char)'B');
                    BW.Write((char)'F');

                    // Write header info
                    BW.Write((int)1000);    // Version
                    BW.Write((int)Width);   // Width in pixels
                    BW.Write((int)Height);  // Height in pixels
                    BW.Write((int)16);      // Bits per pixel
                    BW.Write((int)DataType);  // Data Type (0=Unknown)

                    // Reserve space for 32 bytes of other info
                    for (int i = 0; i < 8; i++) BW.Write((UInt32)i);


                    // Write data
                    if (true)
                    {
                        byte[] byteArray = new byte[Buf.Length * 2];
                        unsafe
                        {
                            fixed (ushort* ushortArrayPtr = Buf)
                            {
                                IntPtr ushortIntPtr = new IntPtr(ushortArrayPtr);
                                Marshal.Copy(ushortIntPtr, byteArray, 0, byteArray.Length);
                            }
                        }
                        BW.Write((int)byteArray.Length);
                        BW.Write(byteArray);
                        byteArray = null;
                    }
                    else
                    {
                        //foreach (ushort V in Buf) BW.Write(V);
                    }


                    BW.Close();
                }
                catch (Exception)
                {
                    return false;
                }
                return true;
            }

            return false;

        }

        //=====================================================================
        public bool SaveImage(string Filename, bool UseGray = false)
        {
            Logger.WriteLine("SaveImage: " + Filename);

            FastBitmap FB = BuildFastBitmap(0,0,UseGray);

            if ((FB != null) && (FB.Bmp != null))
            {
                Logger.WriteLine("SaveImage: " + "FB and FB.Bmp are available");

                try
                {
                    FB.Bmp.Save(Filename, ImageFormat.Png);
                    Logger.WriteLine("SaveImage: " + "FB.Bmp.Save()");
                }
                catch (Exception exp)
                {
                    Logger.WriteException(exp);
                    return false;
                }
            }

            return true;
        }
        



        //=====================================================================

        public FastBitmap BuildFastBitmap(UInt16 MinV=0, UInt16 MaxV=0, bool UseGray=false)
        {
            //if(MinV==0 && MaxV==0) GetMinMax(out MinV, out MaxV);
            Color[] ColorTable = null;

            //if (Name == "Filtered")
            //{
            //    MinV = 800;
            //    MaxV = 1200;
            //}
            if (UseGray)
            {
                if (MinV == 0 && MaxV == 0) GetMinMax(out MinV, out MaxV);
                ColorTable = BuildColorTableGray((int)(MinV), (int)(MaxV));
            }
            else
            if (PaletteType == PaletteTypes.Auto)
            {
                if (MinV == 0 && MaxV == 0) GetMinMax(out MinV, out MaxV);
                ColorTable = BuildColorTable((int)(MinV), (int)(MaxV));
            }
            else
            if (PaletteType == PaletteTypes.Gray)
            {
                if (MinV == 0 && MaxV == 0) GetMinMax(out MinV, out MaxV);
                ColorTable = BuildColorTableGray((int)(MinV), (int)(MaxV));
            }
            else
            if (PaletteType == PaletteTypes.Baselined)
            {
                ColorTable = BuildColorTableBaselined();
            }




            FastBitmap FB = new FastBitmap(Width, Height);

            FB.Lock();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    FB.SetPixel(x, y, ColorTable[Buf[y * Width + x]]);
                }
            }
            FB.Unlock();

            return FB;
        }

        public BitmapSource BuildWPFBitmap(UInt16 MinV= 0, UInt16 MaxV = 0)
        {
            FastBitmap FB = BuildFastBitmap(MinV, MaxV);
            BitmapSource Bmp = Utility.Winforms2WPF(FB.Bmp);
            return Bmp;
        }

        //=====================================================================
        public void GetMinMax(out UInt16 Min, out UInt16 Max)
        {
            UInt16 MinV = UInt16.MaxValue - 100;
            UInt16 MaxV = 100;

            // Quick value range measurement
            for (long i = 0; i < Buf.Length; i += 16)
            {
                UInt16 V = Buf[i];
                if (V == 0) continue;
                if (V < MinV) MinV = V;
                if (V > MaxV) MaxV = V;
            }

            Min = MinV;
            Max = MaxV;
        }

        //=========================================================================
        private Color[] BuildColorTable(int StartIdx, int EndIdx)
        {
            Color[] ColorTable = new Color[65536];
            int RowsPerColor = ((EndIdx - StartIdx) / 6);

            int Start1 = StartIdx;
            int Start2 = StartIdx + (RowsPerColor * 1);
            int Start3 = StartIdx + (RowsPerColor * 2);
            int Start4 = StartIdx + (RowsPerColor * 3);
            int Start5 = StartIdx + (RowsPerColor * 4);
            int Start6 = StartIdx + (RowsPerColor * 5);

            BuildColorBlend(ColorTable, 0, Start1, Colors.Black, Colors.Black);
            BuildColorBlend(ColorTable, Start1, Start2, Colors.Blue, Colors.Green);
            BuildColorBlend(ColorTable, Start2, Start3, Colors.Green, Colors.Red);
            BuildColorBlend(ColorTable, Start3, Start4, Colors.Red, Colors.Purple);
            BuildColorBlend(ColorTable, Start4, Start5, Colors.Purple, Colors.Orange);
            BuildColorBlend(ColorTable, Start5, Start6, Colors.Orange, Colors.Yellow);
            BuildColorBlend(ColorTable, Start6, 65535, Colors.Yellow, Colors.White);

            // Fixed "magic" colors
            ColorTable[15] = Color.FromRgb(80, 80, 80);
            ColorTable[16] = Colors.Red;
            ColorTable[0] = Colors.Black;

            return ColorTable;
        }

        private Color[] BuildColorTableGray(int StartIdx, int EndIdx)
        {
            Color[] ColorTable = new Color[65536];
            BuildColorBlend(ColorTable, StartIdx, EndIdx, Colors.Black, Colors.White);
            ColorTable[0] = Colors.Black;
            ColorTable[65535] = Colors.White;
            return ColorTable;
        }

        private Color[] BuildColorTableBaselined()
        {
            Color[] ColorTable = new Color[65536];

            BuildColorBlend(ColorTable, 0,      800, Colors.Black, Colors.Black);
            BuildColorBlend(ColorTable, 800, 975, Colors.Blue, Colors.Green);
            BuildColorBlend(ColorTable, 975, 1025, Colors.Green, Colors.Red);
            BuildColorBlend(ColorTable, 1025, 1200, Colors.Red, Colors.Yellow);
            BuildColorBlend(ColorTable, 1200, 65535,  Colors.White, Colors.White);

            // Fixed "magic" colors
            //ColorTable[15] = Color.FromRgb(80, 80, 80);
            //ColorTable[16] = Colors.Red;
            ColorTable[0] = Colors.Black;

            return ColorTable;
        }

        //=========================================================================
        private void BuildColorBlend(Color[] ColorArray, int StartIdx, int EndIdx, Color Start, Color End)
        {
            for (int i = StartIdx; i <= EndIdx; i++)
            {
                float pct = (i - StartIdx) / (float)(EndIdx - StartIdx);

                byte R = (byte)(Start.R + ((End.R - Start.R) * pct));
                byte G = (byte)(Start.G + ((End.G - Start.G) * pct));
                byte B = (byte)(Start.B + ((End.B - Start.B) * pct));

                ColorArray[i] = Color.FromRgb(R, G, B);
            }
        }

        //=========================================================================
    }
}
