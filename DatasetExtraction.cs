using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalletCheck
{
    internal class DatasetExtraction
    {


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
                if (PalletPos == 0) Range.SaveImage(Top_Range + format, true); Refl.SaveImage(Top_Reflectance + format, true);
                if (PalletPos == 1) Range.SaveImage(Bottom_Range + format, true); Refl.SaveImage(Bottom_Reflectance + format, true);
                if (PalletPos == 2) Range.SaveImage(Left_Range + format, true); Refl.SaveImage(Left_Reflectance + format, true);
                if (PalletPos == 3) Range.SaveImage(Right_Range + format, true); Refl.SaveImage(Right_Reflectance + format, true);
                if (PalletPos == 4) Range.SaveImage(Front_Range + format, true); Refl.SaveImage(Front_Reflectance + format, true);
                if (PalletPos == 5) Range.SaveImage(Back_Range + format, true); Refl.SaveImage(Back_Reflectance + format, true);






            }
  

        }

    }
}
