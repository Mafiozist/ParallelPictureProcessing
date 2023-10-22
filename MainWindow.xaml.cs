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

        public MainWindow()
        {
            InitializeComponent();
        }
        private void SelectImage_Click(object sender, RoutedEventArgs e)
        {
            var fd = new OpenFileDialog();

            fd.DefaultExt = "*.png";
            fd.Filter = "JPEG Files (*.jpeg)|*.jpeg|PNG Files (*.png)|*.png|JPG Files (*.jpg)|*.jpg|GIF Files (*.gif)|*.gif|CMYK files (*.CMYK)|*.CMYK)";

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
            YCbCrBytes = standardBitmapAndBytes.Item1.ToYCbCrJpegFormat();
            
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

        public static byte[] ToYCbCrJpegFormat(this byte[] oBytes)
        {
            var nBytes = new byte[oBytes.Length];

            for (int i = 0; i < oBytes.Length; ++i)//8 - a, 8 - b, 8 - r, 8 - g
            {
                byte r = 0, g = 0, b = 0;

                try { r = oBytes[i]; } catch { }
                try { g = oBytes[i + 8]; } catch { }
                try { b = oBytes[i + 16]; } catch { }


                int Y = (int)( 0.299 * r + 0.587 * g + 0.114 * b),
                    Cb = (int) (128 - 0.1168736 * r - 0.331264 * g + 0.5 * b),
                    Cr = (int) (128 + 0.5 * r - 0.418688 * g - 0.081312 * b);

                Y = Math.Max(0, Math.Min(255, Y));
                Cb = Math.Max(0, Math.Min(255, Cb));
                Cr = Math.Max(0, Math.Min(255, Cr));

                try { nBytes[i] = Convert.ToByte(Y); } catch { }
                try { nBytes[i + 8] = Convert.ToByte(Cb); } catch { }
                try { nBytes[i + 16] = Convert.ToByte(Cr); } catch { }
            }

            return nBytes;
        }

        public static byte[] ToRgbFromYCbCrJpegFormat(this byte[] oBytes)
        {
            var nBytes = new byte[oBytes.Length];

            for (int i = 0; i < oBytes.Length; ++i)//8 - a, 8 - b, 8 - r, 8 - g
            {
                byte y = 0, cr = 0, cb = 0;

                try { y = oBytes[i]; } catch { }
                try { cr = oBytes[i + 8]; } catch { }
                try { cb = oBytes[i + 16]; } catch { }


                int r = (int) (y + 1.402 * (cr - 128)),
                    g = (int) (y - 0.34414 * (cb - 128) - 0.71414 * (cr -128)),
                    b = (int)(y + 1.772 * (cb - 128));

                r = Math.Max(0, Math.Min(255, r));
                g = Math.Max(0, Math.Min(255, g));
                b = Math.Max(0, Math.Min(255, b));

                try { nBytes[i] = Convert.ToByte(r); } catch { }
                try { nBytes[i + 8] = Convert.ToByte(g); } catch { }
                try { nBytes[i + 16] = Convert.ToByte(b); } catch { }
            }

            return nBytes;
        }
    }
}
