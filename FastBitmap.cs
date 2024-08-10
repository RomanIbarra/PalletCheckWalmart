using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;


namespace ImageTools
{
    public class FastBitmap: IDisposable
    {
        public bool m_bLocked;
        public Bitmap m_Bmp = null;
        public BitmapData m_BData;
        public IntPtr m_pStart;
        public int m_Stride;
        public int m_Width;
        public int m_Height;
        public int m_BytesPerPixel;
        public bool m_Disposed;
        public Graphics m_Graphics = null;

        public int Width { get { return m_Width; } }
        public int Height { get { return m_Height; } }
        public Bitmap Bmp { get { return m_Bmp; } }
        public Graphics G { get { return this.Graphics; } }
        public Graphics Graphics
        {
            get
            {
                if (m_Graphics != null)
                    return m_Graphics;

                Unlock();
                m_Graphics = Graphics.FromImage(m_Bmp);
                return m_Graphics;
            }
        }

        public FastBitmap(string Filename)
        {
            Bitmap Bmp = (Bitmap)Bitmap.FromFile(Filename);
            InitFromBitmap(Bmp);
        }

        public FastBitmap(int aWidth, int aHeight)
        {
            Bitmap Bmp = new Bitmap(aWidth, aHeight, PixelFormat.Format32bppArgb);
            InitFromBitmap(Bmp);
        }

        public FastBitmap( Bitmap Src )
        {
            InitFromBitmap(Src);
        }

        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!m_Disposed)
            {
                // If disposing equals true, dispose all managed 
                // and unmanaged resources.
                if (disposing)
                {
                    // Dispose managed resources.
                    Unlock();

                    if (m_Graphics != null) { m_Graphics.Dispose(); m_Graphics = null; }
                    if (m_Bmp != null) { m_Bmp.Dispose(); m_Bmp = null; }
                    m_Bmp = null;
                    m_BData = null;
                }
            }
            m_Disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void InitFromBitmap(Bitmap Src)
        {
            m_bLocked = false;
            m_Bmp = Src;

            if (m_Bmp.PixelFormat == PixelFormat.Format24bppRgb)
                m_BytesPerPixel = 3;
            else
                if (m_Bmp.PixelFormat == PixelFormat.Format32bppArgb)
                    m_BytesPerPixel = 4;
                else
                    Debug.Assert(false);

            // Enforce this for the time being
            //Debug.Assert(m_Bmp.PixelFormat == PixelFormat.Format32bppArgb);

            m_Width = m_Bmp.Width;
            m_Height = m_Bmp.Height;
        }


        public Color GetPixel(int X, int Y)
        {
            if (!m_bLocked) Lock();

            unsafe
            {
                Byte* pTemp = (Byte*)m_pStart.ToPointer();
                Byte* pPixel = pTemp + (Y * m_Stride) + (X * m_BytesPerPixel);
                if( m_BytesPerPixel == 4 )
                    return Color.FromArgb(pPixel[3], pPixel[2], pPixel[1], pPixel[0]);
                else
                    return Color.FromArgb(pPixel[2], pPixel[1], pPixel[0]);
            }
        }


        public void SetPixel(int I, Color C)
        {
            if (!m_bLocked) Lock();

            unsafe
            {
                Byte* pTemp = (Byte*)m_pStart.ToPointer();
                Byte* pPixel = pTemp + (I * m_BytesPerPixel);
                if (m_BytesPerPixel == 4)
                {
                    pPixel[0] = C.B;
                    pPixel[1] = C.G;
                    pPixel[2] = C.R;
                    pPixel[3] = C.A;
                }
                else
                {
                    pPixel[0] = C.B;
                    pPixel[1] = C.G;
                    pPixel[2] = C.R;
                }
            }
        }

        public void SetPixel(int I, System.Windows.Media.Color C)
        {
            if (!m_bLocked) Lock();

            unsafe
            {
                Byte* pTemp = (Byte*)m_pStart.ToPointer();
                Byte* pPixel = pTemp + (I * m_BytesPerPixel);
                if (m_BytesPerPixel == 4)
                {
                    pPixel[0] = C.B;
                    pPixel[1] = C.G;
                    pPixel[2] = C.R;
                    pPixel[3] = C.A;
                }
                else
                {
                    pPixel[0] = C.B;
                    pPixel[1] = C.G;
                    pPixel[2] = C.R;
                }
            }
        }



        public void SetPixel(int X, int Y, Color C)
        {
            if (!m_bLocked) Lock();

            unsafe
            {
                Byte* pTemp = (Byte*)m_pStart.ToPointer();
                Byte* pPixel = pTemp + (Y * m_Stride) + (X * m_BytesPerPixel);
                if (m_BytesPerPixel == 4)
                {
                    pPixel[0] = C.B;
                    pPixel[1] = C.G;
                    pPixel[2] = C.R;
                    pPixel[3] = C.A;
                }
                else
                {
                    pPixel[0] = C.B;
                    pPixel[1] = C.G;
                    pPixel[2] = C.R;
                }
            }
        }

        public void SetPixel(int X, int Y, System.Windows.Media.Color C)
        {
            if (!m_bLocked) Lock();

            unsafe
            {
                Byte* pTemp = (Byte*)m_pStart.ToPointer();
                Byte* pPixel = pTemp + (Y * m_Stride) + (X * m_BytesPerPixel);
                if (m_BytesPerPixel == 4)
                {
                    pPixel[0] = C.B;
                    pPixel[1] = C.G;
                    pPixel[2] = C.R;
                    pPixel[3] = C.A;
                }
                else
                {
                    pPixel[0] = C.B;
                    pPixel[1] = C.G;
                    pPixel[2] = C.R;
                }
            }
        }


        public void Unlock()
        {
            if (m_bLocked && (m_Bmp!=null))
            {
                m_Bmp.UnlockBits(m_BData);
                m_bLocked = false;
            }
        }

        public void Lock()
        {
            if (!m_bLocked)
            {
                if (m_Bmp != null)
                {
                    m_BData = m_Bmp.LockBits(new Rectangle(0, 0, m_Bmp.Width, m_Bmp.Height), ImageLockMode.ReadWrite, m_Bmp.PixelFormat);
                    m_Stride = m_BData.Stride;
                    m_pStart = m_BData.Scan0;
                }

                if (m_Graphics != null)
                {
                    m_Graphics.Dispose();
                    m_Graphics = null;
                }

                m_bLocked = true;
            }
        }

        public void Save(string Filename, ImageFormat Format)
        {
            m_Bmp.Save(Filename, Format);
        }

        public void Load(string Filename)
        {
            Bitmap Bmp = (Bitmap)Bitmap.FromFile(Filename);
            InitFromBitmap(Bmp);
        }
        
    }
}
