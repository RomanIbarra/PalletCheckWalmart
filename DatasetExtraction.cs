using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalletCheck
{
    internal class DatasetExtraction
    {

        private static string _Range = "../../../Dataset/Range/";
        private static string _Reflectance = "../../../Dataset/Reflectance/";

        /*
        private static string Top_Range = "../../../Dataset/Top/Range/";
        private static string Top_Reflectance = "../../../Dataset/Top/Reflectance/";

        private static string Bottom_Range = "../../../Dataset/Bottom/Range/";
        private static string Bottom_Reflectance = "../../../Dataset/Bottom/Reflectance/";

        private static string Left_Range = "../../../Dataset/Left/Range/";
        private static string Left_Reflectance = "../../../Dataset/Left/Reflectance/";     

        private static string Right_Range = "../../../Dataset/Right/Range/";
        private static string Right_Reflectance = "../../../Dataset/Right/Reflectance/";

        private static string Front_Range = "../../../Dataset/Front/Range/";
        private static string Front_Reflectance = "../../../Dataset/Front/Reflectance/";

        private static string Back_Range = "../../../Dataset/Back/Range/";
        private static string Back_Reflectance = "../../../Dataset/Back/Reflectance/";

        */




        public static void ExtractRangeForDataset(int PalletPos, byte[] byteArray, float[] floatArray,int width,int height,float XResolution,float YResolution,int cntr,bool enabled) 
        {




            if (enabled) {

                //Reflectance- Convert to ushort
                ushort[] ushortRef = new ushort[byteArray.Length];
                for (int i = 0; i < ushortRef.Length; i++)
                {
                    ushortRef[i] = byteArray[i];
                }

                //Range- Convert to ushort 
                ushort[] ushortRange = new ushort[floatArray.Length];
                for (int i = 0; i < floatArray.Length; i++)
                {
                    ushortRange[i] = (ushort)(floatArray[i] + 1000);
                }

                CaptureBuffer Refl = new CaptureBuffer
                {
                    Buf = ushortRef,
                    PaletteType = CaptureBuffer.PaletteTypes.Gray,
                    Width = width,
                    Height = height,
                    xScale = XResolution,
                    yScale = YResolution

                };
                CaptureBuffer Range = new CaptureBuffer
                {
                    Buf = ushortRange,
                    PaletteType = CaptureBuffer.PaletteTypes.Gray,
                    Width = width,
                    Height = height,
                    xScale = XResolution,
                    yScale = YResolution

                };

                String format = Convert.ToString(cntr) + ".png";
                 Range.SaveImage(_Range + format, true); Refl.SaveImage(_Reflectance + format, true);

            }
  

        }

    }
}
