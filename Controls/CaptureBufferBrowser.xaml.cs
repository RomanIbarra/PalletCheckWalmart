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

namespace PalletCheck.Controls
{
    /// <summary>
    /// Interaction logic for CaptureBufferBrowser.xaml
    /// </summary>
    public partial class CaptureBufferBrowser : UserControl
    {
        public CaptureBuffer CB;
        public FastBitmap   FB;


        bool LeftMouseIsDown = false;
        bool RightMouseIsDown = false;
        bool LeftShiftIsDown = false;
        int COUNT = 0;
        Point PanPoint;
        Point PanXPoint;
        ParamStorage paramStorageCapture = new ParamStorage();

        bool RedrawBuffer;

        ScaleTransform myScale = new ScaleTransform(1, 0.75);
        TranslateTransform myXfrm = new TranslateTransform(0, 0);
        TransformGroup myTGroup = new TransformGroup();

        ScaleTransform renderScale = new ScaleTransform(0.8, 0.8);
        private TranslateTransform renderXfrm = new TranslateTransform(0, 0);
        private TransformGroup renderGroup = new TransformGroup();


        Rectangle SelRect = new Rectangle();
        Line SelLine = new Line();
        

        Point MouseDownCanvasPos1 = new Point();
        Point MouseDownCanvasPos2 = new Point();
        Point MouseDownBufferPos1 = new Point();
        Point MouseDownBufferPos2 = new Point();

        List<Shape> Defects = new List<Shape>();

        SolidColorBrush MyDefectBrush;

        System.Windows.Forms.Timer RedrawTimer;

        //static List<CaptureBufferBrowser> AllBrowsers = new List<CaptureBufferBrowser>();


        bool HasInitialized = false;

        //=====================================================================
        //public static void ClearAll()
        //{
        //    //while (AllBrowsers.Count > 0)
        //    //    AllBrowsers[0].Clear();
        //}

        public void Clear()
        {
            //if (AllBrowsers.Contains(this))
            //    AllBrowsers.Remove(this);
            RedrawTimer.Stop();
            ImageCanvas.Children.Clear();
            Defects.Clear();
            CB = null;
            CapBufImg.Source = null;
            if (FB != null)
            {
                FB.Bmp.Dispose();
                FB = null;
            }
        }

        //=====================================================================
        public CaptureBufferBrowser()
        {
            InitializeComponent();
            InvalidateMeasure();
            UpdateLayout();
            Loaded += CaptureBufferBrowser_Loaded;            
            //AllBrowsers.Add(this);
            Unloaded += CaptureBufferBrowser_Unloaded;

            OuterCanvas.LayoutUpdated += OuterCanvas_LayoutUpdated;
            

            // CapBufImg XFRM
            myTGroup.Children.Add(myScale);
            CapBufImg.RenderTransform = myTGroup;

            renderGroup.Children.Add(myXfrm);
            renderGroup.Children.Add(renderScale);
            ImageCanvas.RenderTransform = renderGroup;

            RenderOptions.SetBitmapScalingMode(CapBufImg, BitmapScalingMode.NearestNeighbor);

            // Defect brush animation
            if (MyDefectBrush == null)
            {
                MyDefectBrush = new SolidColorBrush(Colors.Red);
                NameScope.SetNameScope(this, new NameScope());
                this.RegisterName("_MyDefectBrush", MyDefectBrush);

                ColorAnimationUsingKeyFrames myColorAnimation = new ColorAnimationUsingKeyFrames();
                myColorAnimation.Duration = TimeSpan.FromSeconds(0.75);

                myColorAnimation.KeyFrames.Add(
                    new LinearColorKeyFrame(
                        Colors.Transparent, 
                        KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.3))) // KeyTime
                    );

                myColorAnimation.KeyFrames.Add(
                    new LinearColorKeyFrame(
                        Colors.Red,
                        KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.45))) // KeyTime
                    );

                Storyboard.SetTargetName(myColorAnimation, "_MyDefectBrush");
                Storyboard.SetTargetProperty(myColorAnimation, new PropertyPath(SolidColorBrush.ColorProperty));
                Storyboard myStoryboard = new Storyboard();
                myStoryboard.Children.Add(myColorAnimation);

                myStoryboard.RepeatBehavior = RepeatBehavior.Forever;

                this.Loaded += delegate (object sender, RoutedEventArgs e)
                {
                    myStoryboard.Begin(this);
                };

                //myStoryboard.Begin();
            }


            //AnimateView(1, 0, 0);

            // Selection tool
            ImageCanvas.Children.Add(SelRect);
            ImageCanvas.Children.Add(SelLine);

            // Selection tool event handlers
            ImageCanvas.MouseDown += ImageCanvas_MouseDown;
            ImageCanvas.MouseMove += ImageCanvas_MouseMove;
            ImageCanvas.MouseUp += ImageCanvas_MouseUp;
            ImageCanvas.MouseWheel += ImageCanvas_MouseWheel;
            //ImageCanvas.KeyDown += ImageCanvas_KeyDown;

            RedrawTimer = new System.Windows.Forms.Timer();
            RedrawTimer.Interval = 100;
            RedrawTimer.Tick += UpdateTimer;
            RedrawTimer.Start();

            //SelLine.StrokeThickness = 2;
            SelLine.Stroke = Brushes.Yellow;
            SelLine.Visibility = Visibility.Hidden;

            SelRect.Visibility = Visibility.Hidden;
            SelRect.Fill = Brushes.Transparent;
            SelRect.StrokeThickness = 2;
            SelRect.Stroke = Brushes.Yellow;
        }


        private void UpdateTimer(object sender, EventArgs e)
        {
            LeftShiftIsDown = Keyboard.IsKeyDown(Key.LeftShift);
            COUNT += 1;

            if (true && RedrawBuffer)
            {
                if (CB != null)
                {
                    UInt16 MinV, MaxV;
                    CB.GetMinMax(out MinV, out MaxV);
                    double L = MinV + sldLowRange.Value * (MaxV - MinV);
                    double H = MinV + sldHighRange.Value * (MaxV - MinV);
                    BitmapSource B = CB.BuildWPFBitmap((UInt16)(L), (UInt16)(H));
                    //Logger.WriteLine(String.Format("CaptureBufferBrowser Bitmap W,H  {0},{1}", B.Width, B.Height));
                    CapBufImg.Source = B;
                }

                RedrawDefects();

                foreach (Shape S in Defects)
                {
                    if( !ImageCanvas.Children.Contains(S) )
                        ImageCanvas.Children.Add(S);
                }

                RedrawBuffer = false;
            }
        }


        private void OuterCanvas_LayoutUpdated(object sender, EventArgs e)
        {
            if (!HasInitialized)
            {
                if ((OuterCanvas.ActualWidth > 0) && (CapBufImg.ActualWidth > 0))
                {
                    //AnimateView(OuterCanvas.ActualWidth / CapBufImg.ActualWidth, 0, 0);
                    SetScale(0, 0, 0);
                    HasInitialized = true;
                }
            }

            RedrawDefects();
        }
        private void CaptureBufferBrowser_Unloaded(object sender, RoutedEventArgs e)
        {
            //AllBrowsers.Remove(this);
        }

        private void CaptureBufferBrowser_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        //=====================================================================
        public void MarkDefect( Point P, double Radius, string Type )
        {
            //if (MainWindow.ProcessRecordingWindow != null)
              //  return;

            Ellipse E = new Ellipse();

            E.Width = Radius * 2;
            E.Height = Radius * 2;
            E.Stroke = MyDefectBrush;
            E.StrokeThickness = 5;
            E.Fill = Brushes.Transparent;
            E.Tag = P;
            E.ToolTip = Type;
            UpdateEllipseOnCanvas(E);

            //ImageCanvas.Children.Add(E);
            Defects.Add(E);

            //RedrawDefects();
        }

        //=====================================================================
        public void MarkDefect(Point P1, Point P2, string Type)
        {
            //if (MainWindow.ProcessRecordingWindow != null)
            //    return;

            Rectangle R = new Rectangle();
            R.Width = Width;
            R.Height = Height;
            R.Stroke = MyDefectBrush;
            R.StrokeThickness = 5;
            R.Fill = Brushes.Transparent;
            Point[] Corners = new Point[2];
            Corners[0] = P1;
            Corners[1] = P2;
            R.Tag = Corners;
            R.ToolTip = Type;
            UpdateRectOnCanvas(R);

            //ImageCanvas.Children.Add(R);
            Defects.Add(R);

            //RedrawDefects();
        }

        private void UpdateRectOnCanvas(Rectangle R)
        {
            if (CapBufImg.Source == null)
                return;

            //if (MainWindow.ProcessRecordingWindow != null)
            //    return;

            int PW = (CapBufImg.Source as BitmapSource).PixelWidth;
            int PH = (CapBufImg.Source as BitmapSource).PixelHeight;

            if ((PW == 0) || (PH == 0))
                return;

            Point[] Corners = (Point[])R.Tag;

            Point P1 = CapBufImg.TranslatePoint(Corners[0], ImageCanvas);
            Point P2 = CapBufImg.TranslatePoint(Corners[1], ImageCanvas);

            double X1 = (int)((P1.X / PW) * CapBufImg.ActualWidth);
            double Y1 = (int)((P1.Y / PH) * CapBufImg.ActualHeight);
            double X2 = (int)((P2.X / PW) * CapBufImg.ActualWidth);
            double Y2 = (int)((P2.Y / PH) * CapBufImg.ActualHeight);

            Canvas.SetLeft(R, Math.Min(X1, X2));
            Canvas.SetTop(R, Math.Min(Y1, Y2));
            R.Width = Math.Abs(X2 - X1);
            R.Height = Math.Abs(Y2 - Y1);
        }

        private void UpdateEllipseOnCanvas(Ellipse E)
        {
            if (CapBufImg.Source == null)
                return;

            int PW = (CapBufImg.Source as BitmapSource).PixelWidth;
            int PH = (CapBufImg.Source as BitmapSource).PixelHeight;

            if ((PW == 0) || (PH == 0))
                return;

            Point P = CapBufImg.TranslatePoint((Point)E.Tag, ImageCanvas);
            double X = (int)((P.X / PW) * CapBufImg.ActualWidth);
            double Y = (int)((P.Y / PH) * CapBufImg.ActualHeight);
            Canvas.SetLeft(E, X - (E.Width / 2));
            Canvas.SetTop(E, Y - (E.Height / 2));
        }
        
        
        //=====================================================================
        public void RedrawDefects()
        {
            if (CapBufImg.Source == null)
                return;

            //if (MainWindow.ProcessRecordingWindow != null)
            //    return;

            int PW = (CapBufImg.Source as BitmapSource).PixelWidth;
            int PH = (CapBufImg.Source as BitmapSource).PixelHeight;

            if ((PW == 0) || (PH == 0))
                return;

            foreach ( Shape E in Defects )
            {
                if (E.GetType() == typeof(Ellipse))
                {
                    UpdateEllipseOnCanvas((Ellipse)E);
                }
                else
                {
                    UpdateRectOnCanvas((Rectangle)E);
                }
            }


        }

        //=====================================================================
        public void Load(string Filename,ParamStorage paramStorage)
        {
            CaptureBuffer CB = new CaptureBuffer();
            CB.Load(Filename, paramStorage);
            SetCB(CB);
            RedrawBuffer = true;
        }

        //=====================================================================
        public bool Save(string Filename)
        {
            try
            {
                if (CB != null) 
                    CB.Save(Filename);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        //=====================================================================

        private void btnSaveImage_Click(object sender, RoutedEventArgs e)
        {
            Logger.ButtonPressed("CaptureBuffer SaveImage");
            SaveImage(MainWindow.SnapshotsRootDir + "\\SavedPalletImage.png");
        }

        public bool SaveImage(string Filename)
        {
            try
            {
                if (CB != null)
                    CB.SaveImage(Filename);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }
        
        //=====================================================================
        public void SetCB(CaptureBuffer CB)
        {
            this.CB = CB;
            sldLowRange.Value = 0;
            sldHighRange.Value = 1;
            RedrawBuffer = true;
        }


        //=====================================================================
        public void Refresh()
        {
            //ImageCanvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            //ImageCanvas.Arrange(new Rect(0, 0, ImageCanvas.DesiredSize.Width, ImageCanvas.DesiredSize.Height));
            if ((ImageCanvas.ActualWidth > 0) && (ActualWidth > 0))
            {
                //AnimateView(0.70615384615384613, 0, 0);
                //double scale = Math.Min(ActualWidth / ImageCanvas.ActualWidth, ImageCanvas.ActualWidth / ActualWidth);
                //AnimateView(scale, 0, 0);

                SetScale(0, 0, 0);
            }

            RedrawBuffer = true;


        }




        //=====================================================================
        //private void AnimateView(double SX, double TX, double TY)
        //{
        //    int t = 0;// HasInitialized ? 500 : 0;
        //    DoubleAnimation ASX = new DoubleAnimation(SX, new Duration(new TimeSpan(0, 0, 0, 0, t)));
        //    DoubleAnimation ASY = new DoubleAnimation(SX, new Duration(new TimeSpan(0, 0, 0, 0, t)));
        //    DoubleAnimation ATX = new DoubleAnimation(TX, new Duration(new TimeSpan(0, 0, 0, 0, t)));
        //    DoubleAnimation ATY = new DoubleAnimation(TY, new Duration(new TimeSpan(0, 0, 0, 0, t)));

        //    renderScale.BeginAnimation(ScaleTransform.ScaleXProperty, ASX);
        //    renderScale.BeginAnimation(ScaleTransform.ScaleYProperty, ASY);
        //    myXfrm.BeginAnimation(TranslateTransform.XProperty, ATX);
        //    myXfrm.BeginAnimation(TranslateTransform.YProperty, ATY);
        //    //for (int i = 0; i < AllBrowsers.Count; i++)
        //    //{
        //        ////double sx = ActualWidth / 2560.0;
        //        //AllBrowsers[i].renderScale.ScaleX = SX;//SX * 0.6;
        //        //AllBrowsers[i].renderScale.ScaleY = SX;
        //        //AllBrowsers[i].myXfrm.X = TX;
        //        //AllBrowsers[i].myXfrm.Y = TY;

        //        //AllBrowsers[i].RedrawDefects();
        //    //}

        //    //myScale.ScaleX = SX * 0.6;
        //    //myScale.ScaleY = SX;
        //    //myXfrm.X = TX;
        //    //myXfrm.Y = TY;

        //    //RedrawDefects();
        //}


        private void SetScale(double scale, double centerX, double centerY)
        {
            if (scale == 0 && centerX == 0 && centerY == 0)
            {
                // 保持图像的纵横比
                double scaleX = OuterCanvas.ActualWidth / CapBufImg.ActualWidth;
                double scaleY = OuterCanvas.ActualHeight / CapBufImg.ActualHeight;

                // 选择较小的缩放值，以保持纵横比并适应容器
                scale = Math.Min(scaleX, scaleY);

                renderScale.ScaleX = scale;
                renderScale.ScaleY = scale*1.4;

                // 设置初始位移
                myXfrm.X = 0;
                myXfrm.Y = 0;
            }
            else
            {
                renderScale.ScaleX = scale;
                renderScale.ScaleY = scale;

                double scaledWidth = OuterCanvas.ActualWidth / scale;
                double scaledHeight = OuterCanvas.ActualHeight / scale;

                myXfrm.X = -(centerX - scaledWidth / 2);
                myXfrm.Y = -(centerY - scaledHeight / 2);
            }
        }

        //private void SetOnlyScale(double Scale)
        //{
        //    if (Scale == 0)
        //    {
        //        Scale = OuterCanvas.ActualWidth / CapBufImg.ActualWidth;
        //        renderScale.ScaleX = Scale;
        //        renderScale.ScaleY = Scale;
        //        myXfrm.X = 0;
        //        myXfrm.Y = 0;
        //    }
        //    else
        //    {
        //        renderScale.ScaleX = Scale;
        //        renderScale.ScaleY = Scale;

        //        //double scaledW = OuterCanvas.ActualWidth / Scale;
        //        //double scaledH = OuterCanvas.ActualWidth / Scale;

        //        //myXfrm.X = -(CX - scaledW / 2);
        //        //myXfrm.Y = -(CY - scaledH / 2);
        //    }
        //}

        //=====================================================================

        private void ImageCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //if (Keyboard.IsKeyDown(Key.LeftCtrl) ||
            //    Keyboard.IsKeyDown(Key.RightCtrl) ||
            //    Keyboard.IsKeyDown(Key.LeftAlt) ||
            //    Keyboard.IsKeyDown(Key.RightAlt) ||
            //    Keyboard.IsKeyDown(Key.LeftShift) ||
            //    Keyboard.IsKeyDown(Key.RightShift))
            //{
            //    /** do something */
            //}

            LeftShiftIsDown = Keyboard.IsKeyDown(Key.LeftShift);


            if (e.LeftButton == MouseButtonState.Pressed)
            {
                LeftMouseIsDown = true;
                MouseDownCanvasPos1 = e.GetPosition(ImageCanvas);
                MouseDownCanvasPos2 = e.GetPosition(ImageCanvas);
                MouseDownBufferPos1 = e.GetPosition(CapBufImg);
                MouseDownBufferPos2 = e.GetPosition(CapBufImg);

                if (LeftShiftIsDown)
                {
                    // Scale Rectangle
                    SelLine.Visibility = Visibility.Hidden;
                    SelRect.Visibility = Visibility.Visible;
                }
                else
                {
                    // Delta Line
                    SelLine.Visibility = Visibility.Visible;
                    SelRect.Visibility = Visibility.Hidden;
                }

                SelLine.X1 = MouseDownCanvasPos1.X;
                SelLine.Y1 = MouseDownCanvasPos1.Y;
                SelLine.X2 = MouseDownCanvasPos2.X;
                SelLine.Y2 = MouseDownCanvasPos2.Y;



                Canvas.SetLeft(SelRect, Math.Min(MouseDownCanvasPos1.X, MouseDownCanvasPos2.X));
                Canvas.SetTop(SelRect, Math.Min(MouseDownCanvasPos1.Y, MouseDownCanvasPos2.Y));
                SelRect.Width = Math.Abs(MouseDownCanvasPos1.X - MouseDownCanvasPos2.X);
                SelRect.Height = Math.Abs(MouseDownCanvasPos1.Y - MouseDownCanvasPos2.Y);

                //Canvas.SetLeft(SelLine, MP.X);
                //Canvas.SetTop(SelLine, MP.Y);
                //SelLine.X1 = MP.X;
                //SelLine.Y1 = MP.Y;
                //SelLine.X2 = MP.X;
                //SelLine.Y2 = MP.Y;
                //SelLine.Fill = Brushes.Transparent;
                //SelLine.Stroke = Brushes.White;
                //SelLine.Width = 1;
            }

            if (e.RightButton == MouseButtonState.Pressed)
            {
                PanPoint = e.GetPosition(CapBufImg);
                PanXPoint = new Point(myXfrm.X, myXfrm.Y);
                RightMouseIsDown = true;
            }
        }

        //=====================================================================
        private void ImageCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            Canvas canvas = sender as Canvas;

            if (canvas != null)
            {
                // 获取控件的名称
                string controlName = canvas.Name;

                // 输出控件名称
                Console.WriteLine($"The sender's name is: {controlName}");
            }

            if (FB == null)
                FB = CB.BuildFastBitmap();


            MouseDownCanvasPos2 = e.GetPosition(ImageCanvas);
            MouseDownBufferPos2 = e.GetPosition(CapBufImg);

            SelLine.X2 = MouseDownCanvasPos2.X;
            SelLine.Y2 = MouseDownCanvasPos2.Y;

            Canvas.SetLeft(SelRect, Math.Min(MouseDownCanvasPos1.X, MouseDownCanvasPos2.X));
            Canvas.SetTop(SelRect, Math.Min(MouseDownCanvasPos1.Y, MouseDownCanvasPos2.Y));
            SelRect.Width = Math.Abs(MouseDownCanvasPos1.X - MouseDownCanvasPos2.X);
            SelRect.Height = Math.Abs(MouseDownCanvasPos1.Y - MouseDownCanvasPos2.Y);

            // Get Z value in the capture buffer
            UInt16 BPZ2 = 0;
            int BPX2 = 0, BPY2 = 0;
            if (true && (CB != null))
            {
                BPX2 = (int)((MouseDownBufferPos2.X / CapBufImg.ActualWidth) * (CapBufImg.Source as BitmapSource).PixelWidth);
                BPY2 = (int)((MouseDownBufferPos2.Y / CapBufImg.ActualHeight) * (CapBufImg.Source as BitmapSource).PixelHeight);
                if (BPX2 >= 0 && BPX2 < CB.Width && BPY2 >= 0 && BPY2 < FB.Height)
                    BPZ2 = CB.Buf[BPY2 * CB.Width + BPX2];
            }
            double BPZ2mm = Math.Round(BPZ2 * paramStorageCapture.GetFloat("MM Per Pixel Z"), 1);



            String text = "";
            //text = string.Format("X: {0}   Y: {1}   Z: {2}  SHIFT {3}  Z(mm): {4}", BPX, BPY, BPZ, LeftShiftIsDown, BPZmm);

            text = string.Format("{0} {1} {2}", BPX2, BPY2, BPZ2);

            if (LeftMouseIsDown)
            {
                //SelRect.Width = Math.Max(1, MP.X - Canvas.GetLeft(SelRect));
                //SelRect.Height = Math.Max(1, MP.Y - Canvas.GetTop(SelRect));
                //SelLine.X2 = MP.X;
                //SelLine.Y2 = MP.Y;

                //SelLine.X1 = 0;
                //SelLine.Y1 = 0;
                //SelLine.X2 = 200;
                //SelLine.Y2 = 100;

                UInt16 BPZ1 = 0;
                int BPX1 = 0, BPY1 = 0;
                if (true && (CB != null))
                {
                    BPX1 = (int)((MouseDownBufferPos1.X / CapBufImg.ActualWidth) * (CapBufImg.Source as BitmapSource).PixelWidth);
                    BPY1 = (int)((MouseDownBufferPos1.Y / CapBufImg.ActualHeight) * (CapBufImg.Source as BitmapSource).PixelHeight);
                    if (BPX1 >= 0 && BPX1 < CB.Width && BPY1 >= 0 && BPY1 < FB.Height)
                        BPZ1 = CB.Buf[BPY1 * CB.Width + BPX1];
                }
                double BPZ1mm = Math.Round(BPZ1 * paramStorageCapture.GetFloat("MM Per Pixel Z"), 1);

                int DX = (BPX2 - BPX1);
                int DY = (BPY2 - BPY1);
                int DZ = ((int)BPZ2 - (int)BPZ1);
                //int L = (int)(Math.Sqrt((double)DX * DX + (double)DY * DY));

                if (BPZ2 == 0 || BPZ1 == 0) DZ = 0;

                double DXin = Math.Round(DX * paramStorageCapture.GetFloat("MM Per Pixel X") / 25.4, 4);
                double DYin = Math.Round(DY * paramStorageCapture.GetFloat("MM Per Pixel Y") / 25.4, 4);
                double DZin = Math.Round(DZ * paramStorageCapture.GetFloat("MM Per Pixel Z") / 25.4, 4);
                double Lin = Math.Round(Math.Sqrt((double)DXin * DXin + (double)DYin * DYin), 4);
                //text += String.Format(" | dx dy dz ");
                text += String.Format("       | PX dxyz | {0:0.00} {1:0.00} {2:0.00} |", DX, DY, DZ);
                text += String.Format("       | IN dxyz | {0:0.00} {1:0.00} {2:0.00} |", DXin, DYin, DZin);
                text += String.Format("       | MM dxyz | {0:0.00} {1:0.00} {2:0.00} |", DXin * 25.4, DYin * 25.4, DZin * 25.4);


            }
            else if (RightMouseIsDown)
            {
                Vector Delta = PanPoint - MouseDownBufferPos2;

                myXfrm.BeginAnimation(TranslateTransform.XProperty, null);
                myXfrm.BeginAnimation(TranslateTransform.YProperty, null);

                myXfrm.X = PanXPoint.X - Delta.X;
                myXfrm.Y = PanXPoint.Y - Delta.Y;

                PanXPoint = new Point(myXfrm.X, myXfrm.Y);
                RedrawDefects();
            }

            tbInfo.Text = text;
        }


        //=====================================================================
        private void ImageCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (LeftMouseIsDown)
            {
                SelRect.Visibility = Visibility.Hidden;
                SelLine.Visibility = Visibility.Hidden;
            }

            if (LeftMouseIsDown && LeftShiftIsDown)
            {
                MouseDownBufferPos2 = e.GetPosition(CapBufImg);
                MouseDownCanvasPos2 = e.GetPosition(ImageCanvas);

                int PW = (CapBufImg.Source as BitmapSource).PixelWidth;
                int PH = (CapBufImg.Source as BitmapSource).PixelHeight;

                int X = (int)((MouseDownBufferPos1.X / CapBufImg.ActualWidth) * PW);
                int Y = (int)((MouseDownBufferPos1.Y / CapBufImg.ActualHeight) * PH);

                if (Math.Abs(MouseDownCanvasPos2.X - MouseDownCanvasPos1.X) < 3)
                {
                    //AnimateView(OuterCanvas.ActualWidth / FB.Width, 0, 0);
                    //double SX = OuterCanvas.ActualWidth / CapBufImg.ActualWidth;
                    SetScale(0, 0, 0);
                }
                else
                {
                    double Scale = OuterCanvas.ActualWidth / Math.Abs(MouseDownBufferPos2.X - MouseDownBufferPos1.X);
                    double CX = (MouseDownBufferPos1.X + MouseDownBufferPos2.X) / 2;
                    double CY = ((MouseDownBufferPos1.Y + MouseDownBufferPos2.Y) / 2);
                    SetScale(Scale, CX, CY);
                    // Scale to rect
                    //double SX = OuterCanvas.ActualWidth / (BR2.X - BR1.X);
                    //double CY = BR1.Y;// (BR1.Y + BR2.Y) / 2;
                    //double AY = (CY / OuterCanvas.ActualHeight) * FB.Height;
                    //AnimateView(SX, -BR1.X, -AY);
                    //double SX = OuterCanvas.ActualWidth / CapBufImg.ActualWidth;
                    //double CY = BR1.Y;// (BR1.Y + BR2.Y) / 2;
                    //double t = OuterCanvas.ActualHeight / OuterCanvas.ActualWidth;
                    //double AY = (BR1.Y / OuterCanvas.ActualHeight) * FB.Height;
                    //AnimateView(SX, -BR1.X, -BR1.Y*t);

                    //double t = OuterCanvas.ActualHeight / OuterCanvas.ActualWidth;
                    //double NW = (BR2.X - BR1.X);
                    //double CX = (BR1.X + BR2.X) / 2;
                    //double TX = CX - (NW / 2);

                    //double CY = ((BR1.Y + BR2.Y) / 2)-40;
                    ////double NH = (NW / OuterCanvas.ActualWidth) * OuterCanvas.ActualHeight;
                    //double NH = OuterCanvas.ActualHeight / SX;
                    //double TY = CY - (NH / 2);
                    ////double dY = (BR2.X - BR1.X) / t;
                    ////AnimateView(SX, -BR1.X, -(CY-dY*0.5));
                    //AnimateView(SX, -TX, -TY);
                    ////AnimateView(SX, -BR1.X, 0);
                }
            }

            LeftMouseIsDown = false;
            RightMouseIsDown = false;
        }
        //=====================================================================

        private void ImageCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double ScaleStep = 0.05;
            double CurrScale = renderScale.ScaleX;
            double NewScale = 0.0;
            if (e.Delta > 0)
            {
                NewScale = Math.Min(20.0, Math.Max(0.01, renderScale.ScaleX * (1 + ScaleStep)));
                renderScale.ScaleX = NewScale;
                renderScale.ScaleY = NewScale * 1.4;
                myXfrm.X *= (1 + ScaleStep);
                myXfrm.Y *= (1 + ScaleStep);
            }
            if (e.Delta < 0)
            {
                NewScale = Math.Min(20.0, Math.Max(0.01, renderScale.ScaleX * (1 - ScaleStep)));
                renderScale.ScaleX = NewScale;
                renderScale.ScaleY = NewScale * 1.4;
                myXfrm.X *= (1 - ScaleStep);
                myXfrm.Y *= (1 - ScaleStep);
            }

            Logger.WriteLine(String.Format("MouseWheel Scale  {0}  {1}  {2}", e.Delta, CurrScale, NewScale));

        }


        public void SetParamStorage(ParamStorage paramStorage)
        {
            if (paramStorage != null)
            {
                paramStorageCapture = paramStorage;
            }
            else
            {
                throw new ArgumentNullException(nameof(paramStorage), "ParamStorage cannot be null.");
            }
        }

        private void sldLowRange_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            RedrawBuffer = true;
            //Logger.WriteLine("sldLowRange_ValueChanged: " + sldLowRange.Value.ToString());

            if ((sldHighRange != null) && (sldLowRange.Value > sldHighRange.Value))
                sldLowRange.Value = sldHighRange.Value - 0.0001;
        }


        private void sldHighRange_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            RedrawBuffer = true;
            //Logger.WriteLine("sldHighRange_ValueChanged: " + sldHighRange.Value.ToString());
            if((sldLowRange != null) && (sldHighRange.Value < sldLowRange.Value))
                sldHighRange.Value = sldLowRange.Value + 0.0001;
        }
    }
}
