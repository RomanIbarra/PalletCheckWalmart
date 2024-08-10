using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ImageTools
{
    public static class Utility
    {
        public static BitmapSource Winforms2WPF(System.Drawing.Image Src)
        {
            MemoryStream drawingStream = new MemoryStream();

            Src.Save(drawingStream, System.Drawing.Imaging.ImageFormat.Png);
            drawingStream.Seek(0, SeekOrigin.Begin);

            return System.Windows.Media.Imaging.BitmapFrame.Create(drawingStream);
        }

        public static System.Drawing.Image WPF2Winforms(System.Windows.Media.ImageSource image)
        {
            MemoryStream ms = new MemoryStream();
            var encoder = new System.Windows.Media.Imaging.BmpBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image as System.Windows.Media.Imaging.BitmapSource));
            encoder.Save(ms);
            ms.Flush();
            return System.Drawing.Image.FromStream(ms);
        }

    }
}
