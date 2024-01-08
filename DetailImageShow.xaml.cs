using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
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
using System.Windows.Shapes;

namespace ParallelPictureProcessing
{
    /// <summary>
    /// Логика взаимодействия для DetailImageShow.xaml
    /// </summary>
    public partial class DetailImageShow : Window
    {
        public DetailImageShow(byte[] bytes, int width, int height, System.Drawing.Imaging.PixelFormat format)
        {
            InitializeComponent();
            Image.Source = Imaging.CreateBitmapSourceFromHBitmap(bytes.ToBitmap(width, height, format).GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
    }
}
