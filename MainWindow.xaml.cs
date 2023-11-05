using LiveCharts.Wpf;
using LiveCharts;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Cache;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;
using System.Reflection.Emit;
using LiveCharts.Defaults;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Text.RegularExpressions;

namespace ParallelPictureProcessing
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {
        BitmapImage original;
        Bitmap originalBmp;
        byte[] YCbCrBytes;
        List<byte[]> picesOfYCbCrBytes;


        public MainWindow()
        {
            InitializeComponent();
            
        }

        private void SelectImage_Click(object sender, RoutedEventArgs e)
        {
            var fd = new OpenFileDialog();

            fd.Filter = "Image Files(*.jpg; *.jpeg; *.gif; *.bmp)|*.jpg; *.jpeg; *.gif; *.bmp\"";

            Nullable<bool> result = fd.ShowDialog();

            if (result == true)
            {
                // Open document 
                string filename = fd.FileName;
                var file = new Uri(filename);

                originalBmp = new Bitmap(filename);

                //initializing bitmap image
                var bImage = new BitmapImage();
                bImage.BeginInit();
                bImage.UriSource = file;
                bImage.CacheOption = BitmapCacheOption.OnLoad;
                bImage.CreateOptions= BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreImageCache;
                bImage.EndInit();
                original = bImage;

                //setup original content
                originalImg.Source = original;
                format.Content = $"{original.Format}";
            }

        }

        //Convert RBG TO YUV (YCbCr) (jpeg)
        private void Process_Click(object sender, RoutedEventArgs e)
        {
            if (original is null) return;
            
            BrightSlider.Value = 0;
            ContrastSlider.Value = 1;

            //Converting img to desire format
            var standardBitmapAndBytes = originalBmp.ToByte24BppRgbArray();
            var colors = standardBitmapAndBytes.Item1.ToYCbCrJpegFormat();
            YCbCrBytes = colors[0];
            picesOfYCbCrBytes = new List<byte[]>(colors);

            SetTransformedImageFromBytes(YCbCrBytes, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }

        //Function for parallel lof transformation of image
        private void ParallelLogCorrectionProcess_Click(object sender, RoutedEventArgs e)
        {
            if (picesOfYCbCrBytes is null || picesOfYCbCrBytes.Count == 0) return;
            
            Stopwatch stopwatch = Stopwatch.StartNew();
            var threadsNum = 1;
            var coef = 1d;
            try
            {
                threadsNum = Convert.ToInt32(ThreadsCount.Value);
                coef = Convert.ToDouble(LogCoef.Text);
            }
            catch
            {

            }

            ParallelOptions parallelOptions = new ParallelOptions()
            {
                MaxDegreeOfParallelism = threadsNum
            };

            lock (picesOfYCbCrBytes[0]) { 

                Parallel.For(0, picesOfYCbCrBytes[0].Length, parallelOptions, i =>
                {
                    var value = coef * Math.Log(1 + (int)picesOfYCbCrBytes[0][i]);
                    picesOfYCbCrBytes[0][i] = (byte)Math.Max(0, Math.Min(255, value));
                });

            }

            stopwatch.Stop();
            AddLogs("ParallelLogCorrectionProcess_Click", $"выполнился за {stopwatch.Elapsed} в {threadsNum} потоках");
            SetTransformedImageFromBytes(picesOfYCbCrBytes[0], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }

        public void AddLogs(string method,string text)
        {
            var nString = $"Message from {method}: {text};\n";
            StringBuilder sb = new StringBuilder(Logs.Text);

            sb.Append(nString);
            Logs.Text = sb.ToString();
        }

        public void SetTransformedImageFromBytes(byte[] bytes, System.Drawing.Imaging.PixelFormat format)
        {
            transformedImg.Source = Imaging.CreateBitmapSourceFromHBitmap(bytes.ToBitmap((int)original.Width, (int)original.Height, format).GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }

        private void Revert_Click(object sender, RoutedEventArgs e)
        {
            if (YCbCrBytes is null) return;
            transformedImg.Source = Imaging.CreateBitmapSourceFromHBitmap(
                YCbCrBytes.ToRgbFromYCbCrJpegFormat()
                .ToBitmap(
                    Convert.ToInt32(original.Width),
                    Convert.ToInt32(original.Height),
                    System.Drawing.Imaging.PixelFormat.Format24bppRgb).GetHbitmap(),
                    IntPtr.Zero, 
                    Int32Rect.Empty, 
                    BitmapSizeOptions.FromEmptyOptions()
                );
        }

        private void ColorSpace_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (picesOfYCbCrBytes is null) return;

            var select = sender as ComboBox;
            var index = Convert.ToInt32((select.SelectedValue as ComboBoxItem).Content);
            SetTransformedImageFromBytes(picesOfYCbCrBytes[index], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }

        private void ShowHistogramm_Click(object sender, RoutedEventArgs e)
        {
            if (picesOfYCbCrBytes is null || picesOfYCbCrBytes.Count == 0) {
                MessageBox.Show("Каналы пусты. Необходимо их заполнить!");
                return;
            };
            ColorDiagramWindow colorDiagram = new ColorDiagramWindow(picesOfYCbCrBytes);
            colorDiagram.Show();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (picesOfYCbCrBytes is null || picesOfYCbCrBytes.Count == 0) return;
            
            var slider = sender as Slider;
            var val = slider.Value; 
            Brightness.Content = Math.Round(val);

            Utils.IncreaseChannelValue(ref picesOfYCbCrBytes, channelIndex: 0, (int) Math.Round(val));//0 исходное изображение преобр в ycbcr
            SetTransformedImageFromBytes(picesOfYCbCrBytes[0], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }

        private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (picesOfYCbCrBytes is null || picesOfYCbCrBytes.Count == 0) return;

            var slider = sender as Slider;
            var val = slider.Value;
            Contrast.Content = val;

            Utils.IncreaseContrastValue(ref picesOfYCbCrBytes, channelIndex: 0, val);//0 исходное изображение преобр в ycbcr
            SetTransformedImageFromBytes(picesOfYCbCrBytes[0], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }

        private void ParallelCorrectionByIntensity_Click(object sender, RoutedEventArgs e)
        {
            var sw = Stopwatch.StartNew();

            var threadsNum = 1;
            var val = 1;
            try{  threadsNum = Convert.ToInt32(ThreadsCount.Value);}
            catch{}

            if(!Regex.IsMatch(coefVar.Text, @"^\d+,\d+;(\s*\d+,\d+;)*$")) return;

            string[] points = coefVar.Text.Split(';');
            
            picesOfYCbCrBytes[0] = Utils.IncreaseIntensityValueByPoints(ref picesOfYCbCrBytes, channelIndex: 0, points.SkipLast(1).ToArray(), threadsNum);
            sw.Stop();

            AddLogs("ParallelCorrectionByIntensityPoints_Click", $"выполнился за {sw.Elapsed} в {threadsNum} потоках");
            SetTransformedImageFromBytes(picesOfYCbCrBytes[0], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }

        private void ThreadsCount_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ThreadsCountLabel.Content = Math.Round((sender as Slider).Value);
        }

    }

    public class ChangeIntensityByPointsDTO
    {
        public int PixelSize { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int ThreadsCount { get; set; }
        public System.Drawing.Point Start { get; set; }
        public System.Drawing.Point End { get; set; }
        public byte Val { get; set; }
        public int ChannelIndex { get; set; }
    }

    public class Utils
    {
        public static void IncreaseChannelValue(ref List<byte[]> bytes, int channelIndex, int val)
        {
            if (bytes == null) return;

            lock (bytes)
            {
                for (int i=0; i < bytes[channelIndex].Length; ++i)
                {
                    bytes[channelIndex][i] = (byte)Math.Max(0, Math.Min(255, bytes[channelIndex][i] + val));
                }
            }
        }

        public static byte[] IncreaseIntensityValueByPoints(ref List<byte[]> bytes, int channelIndex, string[] points, int threadsNum)
        {
            var nBytes = new byte[bytes[channelIndex].Length];
            Array.Copy(bytes[channelIndex], nBytes, nBytes.Length);



            ParallelOptions parallelOptions = new ParallelOptions()
            {
                MaxDegreeOfParallelism = threadsNum,
            };


            lock (nBytes)
            {
                Parallel.For(0, nBytes.Length, parallelOptions, i =>
                {
                    for (int d = 0; d < points.Length; ++d)
                    {
                        //Previous points
                        byte prevX = (byte)(d == 0 ? 0 : Math.Max(0, Math.Min(255, Convert.ToInt32(points[d - 1].Split(',')[0])))); 
                        byte prevY = (byte)(d == 0 ? 0 : Math.Max(0, Math.Min(255, Convert.ToInt32(points[d-1].Split(',')[1]))));

                        //Current points
                        byte x = (byte) Math.Max(0, Math.Min(255,Convert.ToInt32(points[d].Split(',')[0])));
                        byte y = (byte) Math.Max(0, Math.Min(255, Convert.ToInt32(points[d].Split(',')[1])));

                        //Если значение входит в текущий диапазон перобразований
                        if (nBytes[i] > prevX && nBytes[i] < x) {

                            double k = (double) (y - prevY) / (x - prevX); //Вычисляем угол наклона k
                            double b = (double) y - (k * x);//Вычисляем смещение b
                            nBytes[i] = (byte)Math.Max(0, Math.Min(255, k * nBytes[i] + b));

                            break;
                        } else continue;
                    }
                });
            }

            return nBytes;
        }


        public static void IncreaseContrastValue(ref List<byte[]> bytes, int channelIndex, double val)
        {
            if (bytes == null) return;

            lock (bytes)
            {
                for (int i = 0; i < bytes[channelIndex].Length; ++i)
                {
                    bytes[channelIndex][i] = (byte)Math.Max(0, Math.Min(255, (val * (bytes[channelIndex][i] - 127.5)) + 127.5));
                }
            }

        }

        public static Dictionary<byte, int> CountBytes(byte[] bytes)
        {
            Dictionary<byte, int> byteCounts = new Dictionary<byte, int>();

            foreach (byte b in bytes)
            {
                if (!byteCounts.ContainsKey(b)) byteCounts.Add(b, 1);
                else byteCounts[b] += 1;
            }

            return byteCounts;
        }
    }

    public static class ImageExtensions
    {
        public static byte[] ToByteArray(this System.Drawing.Image image, ImageFormat format)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, format);
                return ms.ToArray();
            }
        }

        public static Tuple<byte[], Bitmap> ToByte24BppRgbArray(this System.Drawing.Image image)
        {
            var nBitmap = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using var gr = Graphics.FromImage(nBitmap);
            var rect = new System.Drawing.Rectangle(0, 0, nBitmap.Width, nBitmap.Height);
            gr.DrawImage(image, rect);

            BitmapData btData = nBitmap.LockBits(rect, ImageLockMode.ReadOnly, nBitmap.PixelFormat);

            var bytes = new byte[nBitmap.Width * nBitmap.Height * 3];
            Marshal.Copy(btData.Scan0, bytes, 0, nBitmap.Width * nBitmap.Height * 3);

            nBitmap.UnlockBits(btData);

            return new Tuple<byte[], Bitmap>(bytes, nBitmap);
        }

        public static Bitmap ToBitmap24BppRgb(this byte[] bytes,  System.Drawing.Image image)
        {
            var nBitmap = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            var rect = new System.Drawing.Rectangle(0, 0, nBitmap.Width, nBitmap.Height);
            BitmapData btData = nBitmap.LockBits(rect, ImageLockMode.WriteOnly, nBitmap.PixelFormat);
            Marshal.Copy(bytes, 0, btData.Scan0, bytes.Length);

            nBitmap.UnlockBits(btData);

            return nBitmap;
        }

        public static Bitmap ToBitmap(this byte[] bytes, int width, int height, System.Drawing.Imaging.PixelFormat pFormat)
        {
            var nBitmap = new Bitmap(width, height);

            var rect = new System.Drawing.Rectangle(0, 0, nBitmap.Width, nBitmap.Height);
            BitmapData btData = nBitmap.LockBits(rect, ImageLockMode.WriteOnly, pFormat);
            Marshal.Copy(bytes, 0, btData.Scan0, bytes.Length);

            nBitmap.UnlockBits(btData);

            return nBitmap;
        }

        /// <summary>
        /// Returns an array of transformed data with separated color instances
        /// </summary>
        public static List<byte[]> ToYCbCrJpegFormat(this byte[] oBytes)
        {
            var allTheBytes = new List<byte[]>();

            var nBytes = new byte[oBytes.Length];
            var YBytes = new byte[nBytes.Length];
            var CbBytes = new byte[nBytes.Length];
            var CrBytes = new byte[nBytes.Length];

            for (int i = 0; i < oBytes.Length; i+=3 )//8 - a, 8 - b, 8 - r, 8 - g
            {
                byte r = 0, g = 0, b = 0, empty = 128;

                try { b = oBytes[i]; } catch { }
                try { g = oBytes[i + 1]; } catch { }
                try { r = oBytes[i + 2]; } catch { }

                int Y = (int)((0.299 * r) + (0.587 * g) + (0.114 * b)),
                    Cb = (int)(128 - (0.1168736 * r) - (0.331264 * g) + (0.5 * b)),
                    Cr = (int)(128 + (0.5 * r) - (0.418688 * g) - 0.081312 * b);

                //const float scale = 257.0f / 65535.0f;
                //const float offset = 257.0f;

                //int Y = (int)((65.481f * r * scale) + (128.553f * g * scale) + (24.996 * b * scale) + (16.0f * offset)),
                //    Cb = (int)((r * -37.797f * scale) + (g * -74.203f * scale) + (b * 112.0f * scale) + (128.0f * offset)),
                //    Cr = (int)((r * 112.0f * scale) + (g * -93.786f * scale) + (b * -18.214f * scale) + (128.0f * offset)) ;

                Y = Math.Max(0, Math.Min(255, Y));
                Cb = Math.Max(0, Math.Min(255, Cb));
                Cr = Math.Max(0, Math.Min(255, Cr));

                try { 
                    nBytes[i] = (byte)Y; 
                    YBytes[i] = (byte)Y;
                    CrBytes[i] = empty;
                    CbBytes[i] = empty;
                } catch { }

                try { 
                    nBytes[i + 1] = (byte) Cb;
                    CbBytes[i + 1] = (byte) Cb;
                    YBytes[i + 1] = (byte)Y;
                    CrBytes[i + 1] = empty;
                } catch { }

                try { 
                    nBytes[i + 2] = (byte) Cr;
                    CrBytes[i + 2] = (byte) Cr;
                    CbBytes[i + 2] = empty;
                    YBytes[i + 2] = (byte)Y; 
                } catch { }

            }

            allTheBytes.Add(nBytes);//Transformed bytes
            allTheBytes.Add(YBytes);
            allTheBytes.Add(CbBytes);
            allTheBytes.Add(CrBytes);

            return allTheBytes;
        }

        public static byte[] ToRgbFromYCbCrJpegFormat(this byte[] oBytes)
        {
            var nBytes = new byte[oBytes.Length];

            for (int i = 0; i < oBytes.Length; i+=3)//8 - a, 8 - b, 8 - r, 8 - g
            {
                byte y = 0, cr = 0, cb = 0;

                try { y = oBytes[i]; } catch { }
                try { cr = oBytes[i + 1]; } catch { }
                try { cb = oBytes[i + 2]; } catch { }


                int r = (int) (y + 1.402 * (cr - 128)),
                    g = (int) (y - 0.344136 * (cb - 128) - 0.714136 * (cr -128)),
                    b = (int)(y + 1.772 * (cb - 128));

                r = Math.Max(0, Math.Min(255, r));
                g = Math.Max(0, Math.Min(255, g));
                b = Math.Max(0, Math.Min(255, b));

                try { nBytes[i] = Convert.ToByte(r); } catch { }
                try { nBytes[i + 1] = Convert.ToByte(g); } catch { }
                try { nBytes[i + 2] = Convert.ToByte(b); } catch { }
            }

            return nBytes;
        }
    }
}
