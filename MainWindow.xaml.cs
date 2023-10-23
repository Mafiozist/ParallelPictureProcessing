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

            //Converting img to desire format
            var standardBitmapAndBytes = originalBmp.ToByte24BppRgbArray();
            var colors = standardBitmapAndBytes.Item1.ToYCbCrJpegFormat();
            YCbCrBytes = colors[0];
            picesOfYCbCrBytes = new List<byte[]>(colors.Skip(1));

            transformedImg.Source = Imaging.CreateBitmapSourceFromHBitmap(YCbCrBytes.ToBitmap(standardBitmapAndBytes.Item2.Width, standardBitmapAndBytes.Item2.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb).GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }

        private void Revert_Click(object sender, RoutedEventArgs e)
        {
            if (YCbCrBytes is null) return;
            transformedImg.Source = Imaging.CreateBitmapSourceFromHBitmap(
                YCbCrBytes.ToRgbFromYCbCrJpegFormat().ToBitmap(Convert.ToInt32(original.Width),
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
            transformedImg.Source = Imaging.CreateBitmapSourceFromHBitmap(picesOfYCbCrBytes[index].ToBitmap((int) original.Width, (int) original.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb).GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
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
                byte r = 0, g = 0, b = 0;

                try { b = oBytes[i]; } catch { }
                try { g = oBytes[i + 1]; } catch { }
                try { r = oBytes[i + 2]; } catch { }


                int Y = (int)( 0.299 * r + 0.587 * g + 0.114 * b),
                    Cb = (int) (-0.1168736 * r - 0.331264 * g + 0.5 * b + 128),
                    Cr = (int) (128 + 0.5 * r - 0.418688 * g - 0.081312 * b);

                Y = Math.Max(0, Math.Min(255, Y));
                Cb = Math.Max(0, Math.Min(255, Cb));
                Cr = Math.Max(0, Math.Min(255, Cr));

                try { 
                    nBytes[i] = (byte)Y; 
                    YBytes[i] = (byte)Y;
                    CrBytes[i] = 0;
                    CbBytes[i] = 0;
                } catch { }

                try { 
                    nBytes[i + 1] = (byte) Cb;
                    CbBytes[i + 1] = (byte) Cb;
                    YBytes[i + 1] = 0;
                    CrBytes[i + 1] = 0;
                } catch { }

                try { 
                    nBytes[i + 2] = (byte) Cr;
                    CrBytes[i + 2] = (byte) Cr;
                    CbBytes[i + 2] = 0;
                    YBytes[i + 2] = 0; 
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
