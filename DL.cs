using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using W = System.Windows;
using WM = System.Windows.Media;
using WMI = System.Windows.Media.Imaging;
using System.Web;
using System.Windows.Media.Media3D;
using System.Threading.Tasks;
using System.Windows;
using System.Runtime.InteropServices;
using Sick.StreamUI.UIImage;

namespace PalletCheck
{
    public class model
    {
        private static string modelPath = @"C:\Users\MICHE\Desktop\Git_Sick\PalletCheckWalmart\DL\maskrcnn_model.onnx";
        private static string modelPath2 = @"C:\Users\MICHE\Desktop\Git_Sick\PalletCheckWalmart\DL\RaisedNailsDetector4.onnx";
        private static InferenceSession session;
        private static InferenceSession session2;

        // Inicializar el modelo solo una vez
        static model()
        {
            try
            {
                session = new InferenceSession(modelPath);
                session2 = new InferenceSession(modelPath2);
                Console.WriteLine("Modelo ONNX cargado correctamente.");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error al cargar el modelo ONNX: " + e.Message);
            }
        }

        // Método para realizar la inferencia
        public static float[][] RunInference(float[] imageData, int heigth, int width)
        {
            // Crear un tensor de entrada para el modelo
            var inputTensor = new DenseTensor<float>(imageData, new[] { 1, 3, heigth, width });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor) // Double check  de que el nombre del input sea el correcto
            };

            float[][] outputData = new float[4][];

            try
            {
                // Ejecutar la inferencia
                using (var results = session.Run(inputs))
                {
                    if (results != null && results.Count > 0)
                    {
                        outputData[0] = results[0].AsTensor<float>()?.ToArray() ?? new float[0]; // Boxes
                        outputData[1] = results[1].AsTensor<float>()?.ToArray() ?? new float[0]; // Labels
                        outputData[2] = results[2].AsTensor<float>()?.ToArray() ?? new float[0]; // Scores
                        outputData[3] = results[3].AsTensor<float>()?.ToArray() ?? new float[0]; // Masks
                    }
                    else
                    {
                        Console.WriteLine("Error: No se obtuvieron resultados válidos de la inferencia.");
                    }
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine("Error durante la inferencia: " + ex.Message);
            }

            return outputData;
        }
        public static float[][] RunInference2(float[] imageData, int heigth, int width)
        {
            // Crear un tensor de entrada para el modelo
            var inputTensor = new DenseTensor<float>(imageData, new[] { 1, 3, heigth, width });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor) // Double check  de que el nombre del input sea el correcto
            };

            float[][] outputData = new float[4][];

            try
            {
                // Ejecutar la inferencia
                using (var results = session2.Run(inputs))
                {
                    if (results != null && results.Count > 0)
                    {
                        outputData[0] = results[0].AsTensor<float>()?.ToArray() ?? new float[0]; // Boxes
                        outputData[1] = results[1].AsTensor<float>()?.ToArray() ?? new float[0]; // Labels
                        outputData[2] = results[2].AsTensor<float>()?.ToArray() ?? new float[0]; // Scores
                        outputData[3] = results[3].AsTensor<float>()?.ToArray() ?? new float[0]; // Masks
                    }
                    else
                    {
                        Console.WriteLine("Error: No se obtuvieron resultados válidos de la inferencia.");
                    }
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine("Error durante la inferencia: " + ex.Message);
            }

            return outputData;
        }

        // Función para leer la imagen y pasarla a la función RunInference
        public static float[] ProcessImage(string imagePath, int heigth, int width)
        {
            // Cargar la imagen
            Bitmap image = LoadImage(imagePath);

            // Preprocesar la imagen (escalado, normalización, etc.)
            float[] imageData = PreprocessImage(image, heigth, width);

            // Llamar a RunInference con los datos de la imagen preprocesada
            return imageData;
        }

        // Función para cargar la imagen desde el disco
        private static Bitmap LoadImage(string imagePath)
        {
            if (File.Exists(imagePath))
            {
                return new Bitmap(imagePath);
            }
            else
            {
                throw new FileNotFoundException("No se encontró la imagen en la ruta especificada.");
            }
        }

        // Función para preprocesar la imagen (escala de grises, normalización, tamaño, etc.)


        private static float[] PreprocessImage(Bitmap image, int heigth, int width)
        {
            // Redimensionar la imagen a  ( según el tamaño esperado por el modelo)
            Bitmap resizedImage = new Bitmap(image, new System.Drawing.Size(width, heigth));
            resizedImage.Save("C:/Users/MICHE/Desktop/Git_Sick/DL/MaskRcnn/Training/data/TopSplit/result.png", ImageFormat.Png);
            // Convertir la imagen a un array de píxeles (canales RGB)

            float[] imageData = new float[3 * width * heigth]; // CHW en lugar de HWC

            int indexR = 0;
            int indexG = width * heigth;
            int indexB = 2 * width * heigth;

            for (int y = 0; y < heigth; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    System.Drawing.Color pixelColor = resizedImage.GetPixel(x, y);

                    imageData[indexR++] = pixelColor.R / 255.0f;
                    imageData[indexG++] = pixelColor.G / 255.0f;
                    imageData[indexB++] = pixelColor.B / 255.0f;
                }
            }


            return imageData;
        }


        public static void DrawTopBoxes(string imagePath, float[] boxes, float[] scores, string savePath)
        {
            using (Bitmap bitmap = new Bitmap(imagePath))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.Red, 3);

                    // Seleccionar los mejores bounding boxes con score mayor a 0.5
                    int[] topIndices = scores
                        .Select((score, index) => new { Score = score, Index = index })
                        .OrderByDescending(x => x.Score)
                        .Where(x => x.Score > 0.5)  // Filtrar solo los que tienen un score mayor a 0.5
                        .Select(x => x.Index)
                        .ToArray();

                    foreach (int i in topIndices)
                    {
                        int x1 = (int)boxes[i * 4];
                        int y1 = (int)boxes[i * 4 + 1];
                        int x2 = (int)boxes[i * 4 + 2];
                        int y2 = (int)boxes[i * 4 + 3];
                        int boxWidth = Math.Abs(x2 - x1);
                        int boxHeight = Math.Abs(y2 - y1);
                        graphics.DrawRectangle(pen, x1, y1, boxWidth, boxHeight);
                    }
                }

                bitmap.Save(savePath, ImageFormat.Png);
                Console.WriteLine($"Imagen guardada en {savePath}");
            }
        }



        public static void DrawTopBoxes2(string imagePath, float[] boxes, float[] scores, string savePath)
        {
            // Cargar imagen original
            // Obtener la ruta del ejecutable
            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Construir la ruta completa de la imagen
            string path = Path.Combine(exeDirectory, imagePath);

            if (!File.Exists(path))
            {
                Console.WriteLine($"Error: La imagen '{path}' no existe.");
                return;
            }
            WMI.BitmapImage bitmapImage = new WMI.BitmapImage(new Uri(path, UriKind.Absolute));
            int width = bitmapImage.PixelWidth;
            int height = bitmapImage.PixelHeight;

            // Crear un WriteableBitmap para modificar la imagen
            WMI.WriteableBitmap writableBitmap = new WMI.WriteableBitmap(bitmapImage);
            WM.DrawingVisual visual = new WM.DrawingVisual();

            using (WM.DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawImage(bitmapImage, new W.Rect(0, 0, width, height));

                // Seleccionar los dos mejores bounding boxes
                int[] topIndices = scores
                    .Select((score, index) => new { Score = score, Index = index })
                    .OrderByDescending(x => x.Score)
                    .Take(2)
                    .Select(x => x.Index)
                    .ToArray();

                WM.Pen pen = new WM.Pen(WM.Brushes.Red, 3);

                foreach (int i in topIndices)
                {
                    int x1 = (int)boxes[i * 4];
                    int y1 = (int)boxes[i * 4 + 1];
                    int x2 = (int)boxes[i * 4 + 2];
                    int y2 = (int)boxes[i * 4 + 3];

                    W.Rect rect = new W.Rect(x1, y1, x2 - x1, y2 - y1);
                    dc.DrawRectangle(null, pen, rect);
                }
            }

            // Renderizar y guardar la imagen modificada
            WMI.RenderTargetBitmap rtb = new WMI.RenderTargetBitmap(width, height, 96, 96, WM.PixelFormats.Pbgra32);
            rtb.Render(visual);

            WMI.PngBitmapEncoder encoder = new WMI.PngBitmapEncoder();
            encoder.Frames.Add(WMI.BitmapFrame.Create(rtb));

            using (FileStream fs = new FileStream("VizDL/TopSplit/"+savePath, FileMode.Create))
            {
                encoder.Save(fs);
            }

            Console.WriteLine($"Imagen guardada en {savePath}");
        }

        public static int[] DrawCentroids(string imagePath, float[] boxes, float[] scores, string savePath)
        {
            // Cargar imagen original
            WMI.BitmapImage bitmapImage = new WMI.BitmapImage(new Uri(imagePath, UriKind.Absolute));
            int width = bitmapImage.PixelWidth;
            int height = bitmapImage.PixelHeight;

            // Crear un WriteableBitmap para modificar la imagen
            WMI.WriteableBitmap writableBitmap = new WMI.WriteableBitmap(bitmapImage);
            WM.DrawingVisual visual = new WM.DrawingVisual();

            List<int> centroids = new List<int>();

            using (WM.DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawImage(bitmapImage, new W.Rect(0, 0, width, height));

                // Seleccionar hasta dos mejores bounding boxes (o menos si no hay suficientes)
                int numObjects = Math.Min(scores.Length, 2); // Evita errores si hay menos de 2 objetos
                int[] topIndices = scores
                    .Select((score, index) => new { Score = score, Index = index })
                    .OrderByDescending(x => x.Score)
                    .Take(numObjects) // Toma 1 o 2 elementos dinámicamente
                    .Select(x => x.Index)
                    .ToArray();

                WM.Pen pen = new WM.Pen(WM.Brushes.Red, 3);

                foreach (int i in topIndices)
                {
                    int x1 = (int)boxes[i * 4];
                    int y1 = (int)boxes[i * 4 + 1];
                    int x2 = (int)boxes[i * 4 + 2];
                    int y2 = (int)boxes[i * 4 + 3];

                    // Calcular el centroide del bounding box
                    int centerX = (x1 + x2) / 2;
                    int centerY = (y1 + y2) / 2;
                    centroids.Add(centerX);
                    centroids.Add(centerY);

                    W.Rect rect = new W.Rect(x1, y1, x2 - x1, y2 - y1);
                    dc.DrawRectangle(null, pen, rect);
                }
            }

            // Renderizar y guardar la imagen modificada
            WMI.RenderTargetBitmap rtb = new WMI.RenderTargetBitmap(width, height, 96, 96, WM.PixelFormats.Pbgra32);
            rtb.Render(visual);

            WMI.PngBitmapEncoder encoder = new WMI.PngBitmapEncoder();
            encoder.Frames.Add(WMI.BitmapFrame.Create(rtb));

            using (FileStream fs = new FileStream(savePath, FileMode.Create))
            {
                encoder.Save(fs);
            }

            Console.WriteLine($"Imagen guardada en {savePath}");

            return centroids.ToArray(); // Devuelve 2 valores si hay 1 objeto, 4 si hay 2.
        }



        public static void DrawTopBoxesAndMasks(string imagePath, float[] boxes, float[] scores, List<bool[,]> masks, string savePath)
        {




            using (Bitmap bitmap = new Bitmap(imagePath))
            {


                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.Red, 3);
                    Random rand = new Random();

                    // Seleccionar los mejores bounding boxes con score mayor a 0.5
                    int[] topIndices = scores
                        .Select((score, index) => new { Score = score, Index = index })
                        .OrderByDescending(x => x.Score)
                        .Take(2)
                        .Select(x => x.Index)
                        .ToArray();

                    foreach (int i in topIndices)
                    {
                        int x1 = (int)boxes[i * 4];
                        int y1 = (int)boxes[i * 4 + 1];
                        int x2 = (int)boxes[i * 4 + 2];
                        int y2 = (int)boxes[i * 4 + 3];
                        int boxWidth = Math.Abs(x2 - x1);
                        int boxHeight = Math.Abs(y2 - y1);

                        // Dibujar el bounding box
                        graphics.DrawRectangle(pen, x1, y1, boxWidth, boxHeight);

                        // Dibujar la máscara con color semitransparente
                        if (masks != null && masks.Count > i)
                        {
                            System.Drawing.Color maskColor = System.Drawing.Color.FromArgb(100, rand.Next(50, 255), rand.Next(50, 255), rand.Next(50, 255));
                            using (SolidBrush brush = new SolidBrush(maskColor))
                            {
                                for (int y = 0; y < bitmap.Height; y++)
                                {
                                    for (int x = 0; x < bitmap.Width; x++)
                                    {
                                        if (masks[i][x, y])
                                        {
                                            graphics.FillRectangle(brush, x, y, 1, 1);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                bitmap.Save(savePath, ImageFormat.Png);
                Console.WriteLine($"Imagen guardada en {savePath}");
            }
        }






        public static float[] ProcessBitmapForInference(Bitmap bitmap, int width, int heigth)
        {
            
            float[] imageData = new float[3 * width * heigth]; // CHW en lugar de HWC

            int indexR = 0;
            int indexG = width * heigth;
            int indexB = 2 * width * heigth;

            for (int y = 0; y < heigth; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    System.Drawing.Color pixelColor = bitmap.GetPixel(x, y);

                    imageData[indexR++] = pixelColor.R / 255.0f;
                    imageData[indexG++] = pixelColor.G / 255.0f;
                    imageData[indexB++] = pixelColor.B / 255.0f;
                }
            }



            return imageData;
        }
        public static List<bool[,]> ConvertMasksToBinary(float[] masks, int width, int height, int numDetections)
        {
            List<bool[,]> binaryMasks = new List<bool[,]>();
            int maskSize = width * height; // Cada máscara tiene este tamaño

            for (int i = 0; i < numDetections; i++)
            {
                bool[,] maskBinary = new bool[width, height];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = i * maskSize + (y * width + x); // Índice en el array 1D
                        maskBinary[x, y] = masks[index] > 0.5f; // Umbral de segmentación
                    }
                }

                binaryMasks.Add(maskBinary);
            }

            return binaryMasks;
        }



        public static List<bool[]> ConvertMasksToBinary2(float[] masks, int width, int height, int numDetections)
        {
            List<bool[]> binaryMasks = new List<bool[]>();
            int maskSize = width * height; // Cada máscara tiene este tamaño

            for (int i = 0; i < numDetections; i++)
            {
                bool[] maskBinary = new bool[maskSize];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = i * maskSize + (y * width + x); // Índice en el array 1D
                        maskBinary[y * width + x] = masks[index] > 0.5f; // Umbral de segmentación
                    }
                }

                binaryMasks.Add(maskBinary);
            }

            return binaryMasks;
        }




        public static void SplitBoard(Bitmap bitmap, float[] boxes, float[] scores, List<bool[,]> masks, string savePath)
        {

            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.Red, 3);
                Random rand = new Random();

                // Seleccionar los 2 mejores objetos
                int[] topIndices = scores
                    .Select((score, index) => new { Score = score, Index = index })
                    .OrderByDescending(x => x.Score)
                    .Take(2)
                    .Select(x => x.Index)
                    .ToArray();

                foreach (int i in topIndices)
                {
                    int x1 = (int)boxes[i * 4];
                    int y1 = (int)boxes[i * 4 + 1];
                    int x2 = (int)boxes[i * 4 + 2];
                    int y2 = (int)boxes[i * 4 + 3];
                    int boxWidth = Math.Abs(x2 - x1);
                    int boxHeight = Math.Abs(y2 - y1);

                    // Dibujar el bounding box
                    graphics.DrawRectangle(pen, x1, y1, boxWidth, boxHeight);

                    // Dibujar la máscara con color semitransparente
                    if (masks != null && masks.Count > i)
                    {
                        System.Drawing.Color maskColor = System.Drawing.Color.FromArgb(100, rand.Next(50, 255), rand.Next(50, 255), rand.Next(50, 255));
                        using (SolidBrush brush = new SolidBrush(maskColor))
                        {
                            for (int y = 0; y < bitmap.Height; y++)
                            {
                                for (int x = 0; x < bitmap.Width; x++)
                                {
                                    if (masks[i][x, y])
                                    {
                                        graphics.FillRectangle(brush, x, y, 1, 1);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            bitmap.Save(savePath, ImageFormat.Png);
            Console.WriteLine($"Imagen guardada en {savePath}");

        }
        public static void SplitBoard2(Bitmap bitmap, float[] boxes, float[] scores, List<bool[,]> masks,
                                string name, ref bool img1Exists, ref bool img2Exists,
                                ref int upperY1, ref int upperY2, ref int lowerY1, ref int lowerY2,bool activateViz=false)

        {


            // Seleccionar los 2 mejores objetos
            int[] topIndices = scores
                .Select((score, index) => new { Score = score, Index = index })
                .OrderByDescending(x => x.Score)
                .Take(2)
                .Select(x => x.Index)
                .ToArray();

            if (scores.Length < 1)
            {

                Console.WriteLine("No se encontraron suficientes objetos.");
                PointF idk = CalculateCentroid(masks[0]);
                if (idk.Y > (bitmap.Height / 2))
                {
                    img2Exists = true;
                    img1Exists = false;
                }
                else
                {
                    img2Exists = false;
                    img1Exists = true;

                }


            }
            else
            {

                img1Exists = true;
                img2Exists = true;
            }



            for (int objIndex = 0; objIndex < 2; objIndex++)
            {
                using (Bitmap objectBitmap = new Bitmap(bitmap))
                using (Graphics graphics = Graphics.FromImage(objectBitmap))

                {
                    // System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.Red, 3);
                    Random rand = new Random();

                    // Determinar cuál objeto excluir
                    int excludeIndex = topIndices[objIndex];

                    for (int i = 0; i < topIndices.Length; i++)
                    {
                        if (topIndices[i] == excludeIndex)
                            continue; // No dibujar este objeto


                        // Dibujar la máscara del objeto permitido
                        if (masks != null && masks.Count > topIndices[i])
                        {
                            System.Drawing.Color maskColor = System.Drawing.Color.FromArgb(0, 0, 0);
                            using (SolidBrush brush = new SolidBrush(maskColor))
                            {
                                for (int y = 0; y < objectBitmap.Height; y++)
                                {
                                    for (int x = 0; x < objectBitmap.Width; x++)
                                    {
                                        if (masks[topIndices[i]][x, y])
                                        {
                                            graphics.FillRectangle(brush, x, y, 1, 1);
                                        }
                                    }
                                }
                            }
                        }
                        int yy2 = 0;
                        int yy1 = 0;
                        if (topIndices[i] == 0)
                        {
                            int x1 = (int)boxes[topIndices[1] * 4];
                            int y1 = (int)boxes[topIndices[1] * 4 + 1];
                            int x2 = (int)boxes[topIndices[1] * 4 + 2];
                            int y2 = (int)boxes[topIndices[1] * 4 + 3];
                            int boxWidth = Math.Abs(x2 - x1);
                            int boxHeight = Math.Abs(y2 - y1);
                            yy2 = y2;
                            yy1 = y1;
                            // Dibujar el bounding box del objeto que sí queremos mostrar
                            // graphics.DrawRectangle(pen, x1, y1, boxWidth, boxHeight);

                        }
                        if (topIndices[i] == 1)
                        {
                            int x1 = (int)boxes[topIndices[0] * 4];
                            int y1 = (int)boxes[topIndices[0] * 4 + 1];
                            int x2 = (int)boxes[topIndices[0] * 4 + 2];
                            int y2 = (int)boxes[topIndices[0] * 4 + 3];
                            int boxWidth = Math.Abs(x2 - x1);
                            int boxHeight = Math.Abs(y2 - y1);
                            yy2 = y2;
                            yy1 = y1;
                            // Dibujar el bounding box del objeto que sí queremos mostrar
                            // graphics.DrawRectangle(pen, x1, y1, boxWidth, boxHeight);

                        }




                        // Guardar la imagen con el objeto excluido
                        PointF centroid = CalculateCentroid(masks[topIndices[i]]);
                        if (centroid.Y > (bitmap.Height / 2))
                        {

                            //////////////////////////Crop Upper
                            if (activateViz) {
                                string savePathObj = "VizDL/TopSplit/Upper_"+name+".png";
                                string savePathObj2 = "VizDL/TopSplit/upperSplit"+name+".png";
                                objectBitmap.Save(savePathObj, ImageFormat.Png);
                                Console.WriteLine($"Imagen guardada en {savePathObj}");
                                Rectangle cropRect = new Rectangle(0, 0, objectBitmap.Width, yy2);
                                Bitmap croppedBitmap = bitmap.Clone(cropRect, objectBitmap.PixelFormat);
                                croppedBitmap.Save(savePathObj2, ImageFormat.Png);

                            } 

                         
                            upperY1 = yy1;
                            upperY2 = yy2;





                        }
                        if (centroid.Y < (bitmap.Height / 2))
                        {

                            if (activateViz)
                            {
                                string savePathObj = "VizDL/TopSplit/Lower_"+name+".png";
                                string savePathObj2 = "VizDL/TopSplit/LowerSplit"+name+".png";
                                objectBitmap.Save(savePathObj, ImageFormat.Png);
                                Console.WriteLine($"Imagen guardada en {savePathObj}");

                                Rectangle cropRect = new Rectangle(0, yy1, objectBitmap.Width, objectBitmap.Height - yy1);
                                Bitmap croppedBitmap = bitmap.Clone(cropRect, objectBitmap.PixelFormat);
                                croppedBitmap.Save(savePathObj2, ImageFormat.Png);
                            }
                            lowerY1 = yy1;
                            lowerY2 = yy2;



                        }


                    }







                }
            }
        }

        public static PointF CalculateCentroid(bool[,] mask)
        {
            int width = mask.GetLength(0);
            int height = mask.GetLength(1);

            int sumX = 0, sumY = 0, count = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[x, y])
                    {
                        sumX += x;
                        sumY += y;
                        count++;
                    }
                }
            }

            if (count == 0)
                return new PointF(-1, -1); // Retornar un valor inválido si no se encontraron píxeles en la máscara

            return new PointF(sumX / (float)count, sumY / (float)count);
        }

        public void ByteArray2bitmap(byte[] byteArray, int width, int height, Bitmap bitmap)
        {
            try
            {
                if (byteArray == null || byteArray.Length != width * height)
                {
                    throw new ArgumentException("El tamaño del byteArray no coincide con las dimensiones de la imagen.");
                }

                // Establecer paleta de grises
                ColorPalette palette = bitmap.Palette;
                for (int i = 0; i < 256; i++)
                {
                    palette.Entries[i] = System.Drawing.Color.FromArgb(i, i, i);
                }
                bitmap.Palette = palette;

                // Bloquear bits y copiar datos
                BitmapData bmpData = bitmap.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format8bppIndexed
                );

                Marshal.Copy(byteArray, 0, bmpData.Scan0, byteArray.Length);
                bitmap.UnlockBits(bmpData);

                // Return image



            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar la imagen: {ex.Message}");
            }
        }







    }





}









