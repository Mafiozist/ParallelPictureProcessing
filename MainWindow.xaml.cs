﻿using LiveCharts.Wpf;
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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Collections.Concurrent;
using Iced.Intel;
using Perfolizer.Mathematics.RangeEstimators;
using System.Data;
using System.Windows.Media.Media3D;
using System.Numerics;
using System;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using MathNet.Numerics.Statistics;
using Microsoft.Diagnostics.Runtime.Utilities;
using Perfolizer.Mathematics.Thresholds;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Security.Policy;
using ExcelLibrary.SpreadSheet;
using Color = System.Drawing.Color;

namespace ParallelPictureProcessing
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        BitmapImage original;
        Bitmap originalBmp;

        byte[] YCbCrBytes;
        List<byte[]> picesOfYCbCrBytes;
        List<HSVPixel[]> picesOfHSV;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            this.WindowState = WindowState.Maximized;
        }

        private void SelectImage_Click(object sender, RoutedEventArgs e)
        {
            var fd = new OpenFileDialog();

            fd.Filter = "Image Files(*.jpg; *.png; *.jpeg; *.gif; *.bmp)|*.jpg; *.png; *.jpeg; *.gif; *.bmp\"";

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
                bImage.CreateOptions = BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreImageCache;
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
            //NoiseLevel.Value = 0;
            SelectedChannel.SelectedIndex = 0;
            Logs.Text = "";
            //LogsRaw.Text = "";

            var standardBitmapAndBytes = originalBmp.ToByte24BppRgbArray();
            var val = Convert.ToInt32((ColorModelType.SelectedItem as ComboBoxItem).Tag);

            switch (val)
            {
                default:
                case 0:

                    //Converting img to YCbCr format
                    var colors = standardBitmapAndBytes.Item1.ToYCbCrJpegFormat();
                    YCbCrBytes = colors[0];
                    picesOfYCbCrBytes = new List<byte[]>(colors);

                    break;


                case 3:
                case 1:
                    picesOfHSV = standardBitmapAndBytes.Item1.ToHSVFormat();
                    var tasks = new Task[4];
                    picesOfYCbCrBytes = new List<byte[]>();

                    var allBytes = new byte[4][];

                    var res = Parallel.For(0, picesOfHSV.Count, i =>
                    {
                        if (i == 0 || i == 1) tasks[i] = Task.Run(() => allBytes[i] = picesOfHSV[i].ToColorBytesFromHSV());
                        else tasks[i] = Task.Run(() => allBytes[i] = picesOfHSV[i].ToBytesFromHSV());
                    });

                    try
                    {
                        Task.WaitAll(tasks);

                        foreach (var bytes in allBytes)
                        {
                            if (val != 3) picesOfYCbCrBytes.Add(bytes);
                            else picesOfYCbCrBytes.Add(allBytes[allBytes.Length - 1]);
                        }

                        YCbCrBytes = picesOfYCbCrBytes[0];

                    }
                    catch (Exception err) { MessageBox.Show(err.Message); }
                    break;

                case 2:
                    var rgb = standardBitmapAndBytes.Item1.SplitRGB();

                    try {
                        picesOfYCbCrBytes.Clear();
                    } catch
                    {
                        picesOfYCbCrBytes = new List<byte[]>();
                    }

                    picesOfYCbCrBytes.Add(standardBitmapAndBytes.Item1);
                    picesOfYCbCrBytes.Add(rgb.Item1);
                    picesOfYCbCrBytes.Add(rgb.Item2);
                    picesOfYCbCrBytes.Add(rgb.Item3);

                    YCbCrBytes = standardBitmapAndBytes.Item1;
                    break;

            }

            if (YCbCrBytes != null) SetTransformedImageFromBytes(YCbCrBytes, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            else MessageBox.Show("Базовые байты не установлены");
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

            lock (picesOfYCbCrBytes[0])
            {

                Parallel.For(0, picesOfYCbCrBytes[0].Length, parallelOptions, i =>
                {
                    var value = coef * Math.Log(1 + (int)picesOfYCbCrBytes[0][i]);
                    picesOfYCbCrBytes[0][i] = (byte)Math.Max(0, Math.Min(255, value));
                });

            }

            stopwatch.Stop();
            AddLogs("ParallelLogCorrectionProcess_Click", $"выполнился за {stopwatch.Elapsed} в {threadsNum} потоках", Convert.ToString(stopwatch.Elapsed.TotalMilliseconds));
            SetTransformedImageFromBytes(picesOfYCbCrBytes[0], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }

        public void AddLogs(string method, string text, string rawVal)
        {
            var nString = $"Message from {method}: {text};\n";
            StringBuilder sb = new StringBuilder(Logs.Text);

            sb.Append(nString);
            Logs.Text = sb.ToString();

            StringBuilder sbRaw = new StringBuilder(LogsRaw.Text);
            sbRaw.Append(rawVal + '\n');
            LogsRaw.Text = sbRaw.ToString();
        }

        public void SetTransformedImageFromBytes(byte[] bytes, System.Drawing.Imaging.PixelFormat format)
        {
            try
            {
                transformedImg.Source = Imaging.CreateBitmapSourceFromHBitmap(bytes.ToBitmap((int)original.Width, (int)original.Height, format).GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void Revert_Click(object sender, RoutedEventArgs e)
        {
            int index = Convert.ToInt32((ColorModelType.SelectedItem as ComboBoxItem).Tag);

            if (YCbCrBytes is null) return;

            byte[] bytes = index == 0 ? YCbCrBytes.ToRgbFromYCbCrJpegFormat() : YCbCrBytes;

            transformedImg.Source = Imaging.CreateBitmapSourceFromHBitmap(
                bytes
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
            var select = sender as ComboBox;
            var index = Convert.ToInt32((select.SelectedValue as ComboBoxItem).Content);
            if (picesOfYCbCrBytes is null) return;
            SetTransformedImageFromBytes(picesOfYCbCrBytes[index], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }

        private void ShowHistogramm_Click(object sender, RoutedEventArgs e)
        {

            if (picesOfYCbCrBytes is null || picesOfYCbCrBytes.Count == 0)
            {
                MessageBox.Show("Каналы пусты. Необходимо их заполнить!");
                return;
            };
            ColorDiagramWindow colorDiagram = new ColorDiagramWindow(picesOfYCbCrBytes);
            colorDiagram.Show();
            colorDiagram.WindowState = WindowState.Maximized;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var slider = sender as Slider;
            var val = slider.Value;
            Brightness.Content = Math.Round(val);
            var index = 0;

            try { index = Convert.ToInt32((SelectedChannel.SelectedItem as ComboBoxItem).Content); } catch { }


            if (picesOfYCbCrBytes is null || picesOfYCbCrBytes.Count == 0) return;

            Stopwatch sw = Stopwatch.StartNew();

            Utils.IncreaseChannelValue(ref picesOfYCbCrBytes, channelIndex: index, (int)Math.Round(val));//0 исходное изображение преобр в ycbcr
            SetTransformedImageFromBytes(picesOfYCbCrBytes[index], System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            sw.Stop();
            AddLogs("BrightSlider_ValueChanged", $"выполнился за {sw.Elapsed} в {1} потоке", Convert.ToString(sw.Elapsed.TotalMilliseconds));

        }

        private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var slider = sender as Slider;
            var val = slider.Value;
            Contrast.Content = val;
            var index = 0;

            try { index = Convert.ToInt32((SelectedChannel.SelectedItem as ComboBoxItem).Content); } catch (Exception ex) { }


            if (picesOfYCbCrBytes is null || picesOfYCbCrBytes.Count == 0) return;

            Stopwatch sw = Stopwatch.StartNew();
            Utils.IncreaseContrastValue(ref picesOfYCbCrBytes, channelIndex: index, val);//0 исходное изображение преобр в ycbcr
            SetTransformedImageFromBytes(picesOfYCbCrBytes[index], System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            sw.Stop();
            AddLogs("ContrastSlider_ValueChanged", $"выполнился за {sw.Elapsed} в {1} потоке", Convert.ToString(sw.Elapsed.TotalMilliseconds));

        }

        private void ParallelCorrectionByIntensity_Click(object sender, RoutedEventArgs e)
        {
            if (!Regex.IsMatch(coefVar.Text, @"^\d+,\d+;(\s*\d+,\d+;)*$")) return;

            var threadsNum = 1;
            var val = 1;
            try { threadsNum = Convert.ToInt32(ThreadsCount.Value); }
            catch { }

            string[] points = coefVar.Text.Split(';');


            var sw = Stopwatch.StartNew();
            picesOfYCbCrBytes[0] = Utils.IncreaseIntensityValueByPoints(ref picesOfYCbCrBytes, channelIndex: 0, points.SkipLast(1).ToArray(), threadsNum);
            sw.Stop();

            AddLogs("ParallelCorrectionByIntensityPoints_Click", $"выполнился за {sw.Elapsed} в {threadsNum} потоках", Convert.ToString(sw.Elapsed.TotalMilliseconds));
            SetTransformedImageFromBytes(picesOfYCbCrBytes[0], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }

        private void ParallelCorrectionByIntensityFromClear_Click(object sender, RoutedEventArgs e)//ParallelCorrectionByIntensityFromClear_Click
        {
            if (!Regex.IsMatch(coefVar.Text, @"^\d+,\d+;(\s*\d+,\d+;)*$")) return;
            Process_Click(sender, e);

            var threadsNum = 1;
            var val = 1;
            try { threadsNum = Convert.ToInt32(ThreadsCount.Value); }
            catch { }

            string[] points = coefVar.Text.Split(';');

            var sw = Stopwatch.StartNew();
            picesOfYCbCrBytes[0] = Utils.IncreaseIntensityValueByPoints(ref picesOfYCbCrBytes, channelIndex: 0, points.SkipLast(1).ToArray(), threadsNum);
            sw.Stop();

            AddLogs("ParallelCorrectionByIntensityPoints_Click", $"выполнился за {sw.Elapsed} в {threadsNum} потоках", Convert.ToString(sw.Elapsed.TotalMilliseconds));
            SetTransformedImageFromBytes(picesOfYCbCrBytes[0], System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        }

        private void ThreadsCount_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ThreadsCountLabel.Content = Math.Round((sender as Slider).Value);
        }

        private void ColorModelType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Convert.ToInt32(((sender as ComboBox).SelectedItem as ComboBoxItem).Tag) == 1)
            {
                picesOfYCbCrBytes = null;
                YCbCrBytes = null;
                return;
            }

            if (Convert.ToInt32(((sender as ComboBox).SelectedItem as ComboBoxItem).Tag) == 0)
            {
                picesOfHSV = null;
                return;
            }
        }

        //Лаба2
        private void NoiseLevel_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var textContent = (NoiseLevelLabel.Content as AccessText);
            var sliderVal = (int)(sender as Slider).Value;
            textContent.Text = textContent.Text.Remove(textContent.Text.IndexOf(':') + 1);
            textContent.Text += $"{sliderVal}%";
        }

        private void AddNoiseBtn_Click(object sender, RoutedEventArgs e)
        {
            var index = Convert.ToInt32((SelectedChannel.SelectedItem as ComboBoxItem).Content);

            if (picesOfYCbCrBytes == null || picesOfYCbCrBytes.Count == 0) return;

            picesOfYCbCrBytes[index] = ImageNoiseAndFilteringExtensions.AddImpulseNoise(ref picesOfYCbCrBytes, index, (int)NoiseLevel.Value, (int)ThreadsCount.Value, (int)WhiteToBlackPercent.Value, RandomNoise.IsChecked ?? false);
            SetTransformedImageFromBytes(picesOfYCbCrBytes[index], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }

        private void WhiteToBlackPercent_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var textContent = (WhiteToBlackPercentLabel.Content as AccessText);
            var sliderVal = (int)(sender as Slider).Value;
            textContent.Text = textContent.Text.Remove(textContent.Text.IndexOf(':') + 1);
            textContent.Text += $"{sliderVal}%";
        }

        private void AddMultyNoiseBtn_Click(object sender, RoutedEventArgs e)
        {
            var index = Convert.ToInt32((SelectedChannel.SelectedItem as ComboBoxItem).Content);

            try
            {

                if (picesOfYCbCrBytes == null || picesOfYCbCrBytes.Count == 0) return;

                var min = Convert.ToDouble(minMultyCoef.Text);
                var max = Convert.ToDouble(maxMultyCoef.Text);

                picesOfYCbCrBytes[index] = ImageNoiseAndFilteringExtensions.AddMultyNoise(ref picesOfYCbCrBytes, index, min, max, (int)ThreadsCount.Value);
                SetTransformedImageFromBytes(picesOfYCbCrBytes[index], System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void AddAdditiveNoiseBtn_Click(object sender, RoutedEventArgs e)
        {
            var index = Convert.ToInt32((SelectedChannel.SelectedItem as ComboBoxItem).Content);


            try
            {

                if (picesOfYCbCrBytes == null || picesOfYCbCrBytes.Count == 0) return;
                double val = Convert.ToDouble(maxAdditiveCoef.Text);
                picesOfYCbCrBytes[index] = ImageNoiseAndFilteringExtensions.AddAdditiveNoise(ref picesOfYCbCrBytes, index, val, (int)ThreadsCount.Value);
                SetTransformedImageFromBytes(picesOfYCbCrBytes[index], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void transformedImg_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (transformedImg.Source is null) return;

            // Получаем изображение из элемента Image
            System.Windows.Controls.Image image = (System.Windows.Controls.Image)sender;
            BitmapSource bitmapSource = (BitmapSource)image.Source;

            // Создаем кодек для сохранения изображения в файл (например, в формате PNG)
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

            // Создаем диалоговое окно для выбора места сохранения файла
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "PNG Files (*.png)|*.png|All files (*.*)|*.*";
            if (saveFileDialog.ShowDialog() == true)
            {
                // Сохраняем изображение в выбранное место
                using (FileStream fs = new FileStream(saveFileDialog.FileName, FileMode.Create))
                {
                    encoder.Save(fs);
                }
            }

            var form = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
            var index = Convert.ToInt32((SelectedChannel.SelectedItem as ComboBoxItem).Content);

            DetailImageShow colorDiagram = new DetailImageShow(picesOfYCbCrBytes[index], (int)original.Width, (int)original.Height, form);
            colorDiagram.Show();
            colorDiagram.WindowState = WindowState.Maximized;
        }

        private void LinearFilter_Click(object sender, RoutedEventArgs e)
        {
            if (picesOfYCbCrBytes == null || picesOfYCbCrBytes.Count == 0) return;
            var index = Convert.ToInt32((SelectedChannel.SelectedItem as ComboBoxItem).Content);

            int ftype = Convert.ToInt32((FilterType.SelectedItem as ComboBoxItem).Tag);
            var currImg = picesOfYCbCrBytes[index];

            byte[] orig = new byte[currImg.Length];
            Array.Copy(picesOfYCbCrBytes[index], orig, orig.Length);

            int width = (int)original.Width, height = (int)original.Height;
            int threads = Convert.ToInt32(ThreadsCount.Value);

            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                if (ftype == 0) currImg = ImageNoiseAndFilteringExtensions.ApplyConvolutionFilterParallel(currImg, Utils.StringToDoubleArray(Kernel.Text), width, height, 3, threads);
                else if (ftype == 1) currImg = ImageNoiseAndFilteringExtensions.ApplyHarmonicMeanFilterParallel(currImg, Convert.ToInt32(Kernel.Text.Split(' ')[0]), Convert.ToInt32(Kernel.Text.Split(' ')[1]), width, height, 3, threads);
                else if (ftype == 2) currImg = ImageNoiseAndFilteringExtensions.Apply2DGaussianFilter(currImg, Utils.StringToDoubleArray(Kernel.Text), width, height, 3, threads);
                else if (ftype == 3) currImg = ImageNoiseAndFilteringExtensions.ApplyLocalHistogrammFilter(currImg, width, height, 3, Convert.ToInt32(Kernel.Text), threads);
                else if (ftype == 4) currImg = ImageNoiseAndFilteringExtensions.ApplyParallelRecursiveFilter(currImg, width, height, 3, Convert.ToInt32(Kernel.Text), threads);
            } catch (Exception ex) { MessageBox.Show(ex.Message); }

            sw.Stop();

            Logs.Text += $"EstimatedFilteringTime: {sw.ElapsedMilliseconds}\n";
            Logs.Text += $"DEL: {ImageNoiseAndFilteringExtensions.CalculateDelta(orig, currImg)}\n";
            Logs.Text += $"MSAD: {ImageNoiseAndFilteringExtensions.CalculateMSAD(orig, currImg)}\n";
            Logs.Text += $"MSE: {ImageNoiseAndFilteringExtensions.CalculateMSE(orig, currImg)}\n";

            LogsRaw.Text += $"{sw.ElapsedMilliseconds}\n";
            FilterLogsRaw.Text += $"{ImageNoiseAndFilteringExtensions.CalculateDelta(orig, currImg)}\n";
            FilterLogsRaw.Text += $"{ImageNoiseAndFilteringExtensions.CalculateMSAD(orig, currImg)}\n";
            FilterLogsRaw.Text += $"{ImageNoiseAndFilteringExtensions.CalculateMSE(orig, currImg)}\n";

            picesOfYCbCrBytes[index] = currImg;
            SetTransformedImageFromBytes(picesOfYCbCrBytes[index], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }


        private void originalImg_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var form = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
            DetailImageShow colorDiagram = new DetailImageShow(originalBmp.ToByte24BppRgbArray().Item1, (int)original.Width, (int)original.Height, form);
            colorDiagram.Show();
            colorDiagram.WindowState = WindowState.Maximized;
        }

        private void Edging_Click(object sender, RoutedEventArgs e)
        {
            if (picesOfYCbCrBytes == null || picesOfYCbCrBytes.Count == 0) return;
            var index = Convert.ToInt32((SelectedChannel.SelectedItem as ComboBoxItem).Content);
            int edgingType = Convert.ToInt32((EdgingType.SelectedItem as ComboBoxItem).Tag);
            int threads = Convert.ToInt32(ThreadsCount.Value);
            int width = (int)original.Width, height = (int)original.Height;

            try
            {

                switch (edgingType)
                {
                    case 0:
                        picesOfYCbCrBytes[index] = EdgeDetection.RobertsOperator(
                            picesOfYCbCrBytes[index],
                            width,
                            height,
                            3,
                            threads,
                            Convert.ToDouble(PowerCoefTB.Text),
                            Convert.ToDouble(LimitCoefTB.Text)
                            );


                        break;

                    case 1:
                        picesOfYCbCrBytes[index] = EdgeDetection.SobelOperator(
                            picesOfYCbCrBytes[index],
                            width,
                            height,
                            3,
                            threads,
                            Convert.ToDouble(PowerCoefTB.Text),
                            Convert.ToDouble(LimitCoefTB.Text),
                            Convert.ToDouble(BalanceCoefTB.Text)
                            );


                        break;

                    case 2:
                        picesOfYCbCrBytes[index] = EdgeDetection.LaplaceOperator(
                            picesOfYCbCrBytes[index],
                            width,
                            height,
                            3,
                            threads,
                            Convert.ToDouble(PowerCoefTB.Text),
                            Convert.ToDouble(LimitCoefTB.Text),
                            Utils.StringToDoubleArray(BalanceCoefTB.Text)
                            );

                        break;

                    default: return;
                }

                SetTransformedImageFromBytes(picesOfYCbCrBytes[index], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            } catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void ProcessBtn_KeyDown(object sender, KeyEventArgs e)
        {

            switch (e.Key)
            {
                case Key.LeftCtrl:
                    Process_Click(sender, e);
                    break;

                case Key.H:
                    ShowHistogramm_Click(sender, e);
                    break;

            }
        }

        private void PositiveKernel_Checked(object sender, RoutedEventArgs e)
        {
            BalanceCoefTB.Text = "0 1 0;1 -4 1;0 1 0;";
        }

        private void NegativeKernel_Checked(object sender, RoutedEventArgs e)
        {
            BalanceCoefTB.Text = "0 -1 0;-1 -4 -1;0 -1 0;";
        }

        private void GloabalBtn_Click(object sender, RoutedEventArgs e)
        {
            if (picesOfYCbCrBytes == null || picesOfYCbCrBytes.Count == 0) return;
            var index = Convert.ToInt32((SelectedChannel.SelectedItem as ComboBoxItem).Content);
            int threads = Convert.ToInt32(ThreadsCount.Value);
            int width = (int)original.Width, height = (int)original.Height;

            try
            {
                picesOfYCbCrBytes[index] = EdgeDetection.GlobalThresholding(
                picesOfYCbCrBytes[index],
                width,
                height,
                3,
                threads,
                Convert.ToInt32(BinarizationVal.Text)
                );
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            SetTransformedImageFromBytes(picesOfYCbCrBytes[index], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }


        private void LocalBtn_Click(object sender, RoutedEventArgs e)
        {
            if (picesOfYCbCrBytes == null || picesOfYCbCrBytes.Count == 0) return;
            var index = Convert.ToInt32((SelectedChannel.SelectedItem as ComboBoxItem).Content);
            int threads = Convert.ToInt32(ThreadsCount.Value);
            int width = (int)original.Width, height = (int)original.Height;

            try
            {
                picesOfYCbCrBytes[index] = EdgeDetection.LocalNiblackThresholding(
                picesOfYCbCrBytes[index],
                width,
                height,
                3,
                threads,
                Convert.ToDouble(BinarizationVal.Text.Split(" ")[0]),
                Convert.ToInt32(BinarizationVal.Text.Split(" ")[1])
                );
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            SetTransformedImageFromBytes(picesOfYCbCrBytes[index], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }

        private void WiderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (picesOfYCbCrBytes == null || picesOfYCbCrBytes.Count == 0) return;
            var index = Convert.ToInt32((SelectedChannel.SelectedItem as ComboBoxItem).Content);
            int threads = Convert.ToInt32(ThreadsCount.Value);
            int width = (int)original.Width, height = (int)original.Height;

            try
            {
                picesOfYCbCrBytes[index] = EdgeDetection.Dilation(
                picesOfYCbCrBytes[index],
                width,
                height,
                3,
                threads,
                Utils.StringToDoubleArray(MorphVal.Text)
                );
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            SetTransformedImageFromBytes(picesOfYCbCrBytes[index], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }

        private void LowerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (picesOfYCbCrBytes == null || picesOfYCbCrBytes.Count == 0) return;
            var index = Convert.ToInt32((SelectedChannel.SelectedItem as ComboBoxItem).Content);
            int threads = Convert.ToInt32(ThreadsCount.Value);
            int width = (int)original.Width, height = (int)original.Height;

            try
            {
                picesOfYCbCrBytes[index] = EdgeDetection.Erosion(
                picesOfYCbCrBytes[index],
                width,
                height,
                3,
                threads,
                Utils.StringToDoubleArray(MorphVal.Text)
                );
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            SetTransformedImageFromBytes(picesOfYCbCrBytes[index], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }

        private void Segmentation_Click(object sender, RoutedEventArgs e)
        {
            if (picesOfYCbCrBytes == null || picesOfYCbCrBytes.Count == 0) return;
            var index = Convert.ToInt32((SelectedChannel.SelectedItem as ComboBoxItem).Content);
            int threads = Convert.ToInt32(ThreadsCount.Value);
            int width = (int)original.Width, height = (int)original.Height;

            
            
            try
            {
                if (Convert.ToInt32((SegmentationType.SelectedItem as ComboBoxItem).Tag) == 0)
                {
                    var weightMatrix = EdgeDetection.CreateWeightMatrix(picesOfYCbCrBytes[index], width, height, 3);
                    var img = EdgeDetection.SegmentImage(picesOfYCbCrBytes[index], weightMatrix, width, height, Convert.ToInt32(SegmentationVal.Text));//
                    picesOfYCbCrBytes[index] = img;
                    SetTransformedImageFromBytes(img, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                }
                else
                {

                }
            }
            catch(Exception ex) { 
                MessageBox.Show(ex.Message);
            }
        }

        private void FindLines_Click(object sender, RoutedEventArgs e)
        {
            if (picesOfYCbCrBytes == null || picesOfYCbCrBytes.Count == 0) return;
            var index = Convert.ToInt32((SelectedChannel.SelectedItem as ComboBoxItem).Content);
            int threads = Convert.ToInt32(ThreadsCount.Value);
            int width = (int)original.Width, height = (int)original.Height;

            try
            {
                picesOfYCbCrBytes[index] = TextureLab.DetectLinesAndCircles(picesOfYCbCrBytes[index], width, height, 3, false, Convert.ToInt32(VotesTB.Text));
                SetTransformedImageFromBytes(picesOfYCbCrBytes[index], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            }catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void FindCircle_Click(object sender, RoutedEventArgs e)
        {
            if (picesOfYCbCrBytes == null || picesOfYCbCrBytes.Count == 0) return;
            var index = Convert.ToInt32((SelectedChannel.SelectedItem as ComboBoxItem).Content);
            int threads = Convert.ToInt32(ThreadsCount.Value);
            int width = (int)original.Width, height = (int)original.Height;

            try
            {
                picesOfYCbCrBytes[index] = TextureLab.DetectLinesAndCircles(picesOfYCbCrBytes[index], width, height, 3, true, Convert.ToInt32(VotesTB.Text));
                SetTransformedImageFromBytes(picesOfYCbCrBytes[index], System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }

    public class Utils
    {

        public static byte[] ConvertToGrayscale(byte[] original)
        {
            byte[] grayscale = new byte[original.Length];

            for (int i = 0; i < original.Length; i += 3)
            {
                int grayScale = (int)((original[i] * 0.3) + (original[i + 1] * 0.59) + (original[i + 2] * 0.11));

                grayscale[i] = (byte)grayScale;
                grayscale[i + 1] = (byte)grayScale;
                grayscale[i + 2] = (byte)grayScale;
            }

            return grayscale;
        }

        public static void IncreaseChannelValue(ref List<byte[]> bytes, int channelIndex, int val)
        {
            if (bytes == null) return;

            lock (bytes)
            {
                for (int i = 0; i < bytes[channelIndex].Length; ++i)
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
                        byte prevY = (byte)(d == 0 ? 0 : Math.Max(0, Math.Min(255, Convert.ToInt32(points[d - 1].Split(',')[1]))));

                        //Current points
                        byte x = (byte)Math.Max(0, Math.Min(255, Convert.ToInt32(points[d].Split(',')[0])));
                        byte y = (byte)Math.Max(0, Math.Min(255, Convert.ToInt32(points[d].Split(',')[1])));

                        //Если значение входит в текущий диапазон перобразований
                        if (nBytes[i] > prevX && nBytes[i] < x) {

                            double k = (double)(y - prevY) / (x - prevX); //Вычисляем угол наклона k
                            double b = (double)y - (k * x);//Вычисляем смещение b
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

        public static double[,] StringToDoubleArray(string input)
        {
            // Разделение строки на строки элементов
            string[] rows = input.Split(';');

            // Определение размерности массива
            int rowCount = rows.Length - 1;
            int colCount = rows[0].Split(' ').Length;

            // Создание двумерного массива
            double[,] result = new double[rowCount, colCount];

            // Заполнение массива значениями
            try
            {
                for (int i = 0; i < rowCount; i++)
                {
                    // Разделение строки на элементы
                    string[] elements = rows[i].Split(' ');

                    for (int j = 0; j < colCount; j++)
                    {
                        // Преобразование строки в double
                        if (elements[j].Contains('/'))
                        {
                            var vals = elements[j].Split("/");
                            result[i, j] = Convert.ToDouble(vals[0]) / Convert.ToDouble(vals[1]);
                        }
                        else if (double.TryParse(elements[j].Trim().Replace('.', ','), out double value))
                        {
                            result[i, j] = value;
                        }
                        else
                        {
                            // Обработка ошибки преобразования
                            throw new Exception($"Ошибка преобразования элемента {elements[j]} в строке {i + 1}, столбце {j + 1}.");
                        }
                    }
                }
            } catch (Exception ex) { MessageBox.Show(ex.Message); }

            return result;
        }
    }

    public static class ImageExtensions //Лаба1
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

        public static Bitmap ToBitmap24BppRgb(this byte[] bytes, System.Drawing.Image image)
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
            if (bytes == null) throw new ArgumentNullException("bytes");

            var nBitmap = new Bitmap(width, height);

            var rect = new System.Drawing.Rectangle(0, 0, nBitmap.Width, nBitmap.Height);
            BitmapData btData = nBitmap.LockBits(rect, ImageLockMode.WriteOnly, pFormat);
            Marshal.Copy(bytes, 0, btData.Scan0, bytes.Length);

            nBitmap.UnlockBits(btData);

            return nBitmap;
        }

        public static (byte[], byte[], byte[]) SplitRGB(this byte[] rgbData)
        {
            if (rgbData == null || rgbData.Length % 3 != 0)
            {
                throw new ArgumentException("Invalid RGB data.");
            }


            byte[] redChannel = new byte[rgbData.Length];
            byte[] greenChannel = new byte[rgbData.Length];
            byte[] blueChannel = new byte[rgbData.Length];
            byte blank = 0;

            for (int i = 0; i < rgbData.Length; i += 3)
            {
                redChannel[i] = rgbData[i];
                redChannel[i + 1] = blank;
                greenChannel[i + 2] = blank;

                greenChannel[i] = blank;
                greenChannel[i + 1] = rgbData[i + 1];
                greenChannel[i + 2] = blank;

                blueChannel[i] = blank;
                blueChannel[i + 1] = blank;
                blueChannel[i + 2] = rgbData[i + 2];
            }

            return (redChannel, greenChannel, blueChannel);
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

            for (int i = 0; i < oBytes.Length; i += 3)//8 - a, 8 - b, 8 - r, 8 - g
            {
                byte r = 0, g = 0, b = 0, empty = 128;

                try { b = oBytes[i]; } catch { }
                try { g = oBytes[i + 1]; } catch { }
                try { r = oBytes[i + 2]; } catch { }

                int Y = (int)((0.299 * r) + (0.587 * g) + (0.114 * b)),
                    Cb = (int)(128 - (0.1168736 * r) - (0.331264 * g) + (0.5 * b)),
                    Cr = (int)(128 + (0.5 * r) - (0.418688 * g) - 0.081312 * b);

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
                    nBytes[i + 1] = (byte)Cb;
                    CbBytes[i + 1] = (byte)Cb;
                    YBytes[i + 1] = (byte)Y;
                    CrBytes[i + 1] = empty;
                } catch { }

                try {
                    nBytes[i + 2] = (byte)Cr;
                    CrBytes[i + 2] = (byte)Cr;
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
    }

    public static class ImageNoiseAndFilteringExtensions
    {
        volatile static int CountOfNoises = 0;
        public volatile static Dictionary<int, bool> randomIndecies = new();

        public static byte[] AddImpulseNoise(ref List<byte[]> bytes, int channelIndex, int percent, int threadsNum, int blackPercent, bool random)
        {
            if (bytes == null || percent == 0) return bytes[channelIndex];
            CountOfNoises = 0;

            byte[] curBytes = bytes[channelIndex];
            var ct = new CancellationTokenSource();//ForThreadsStopping

            ParallelOptions parallelOptions = new ParallelOptions()
            {
                MaxDegreeOfParallelism = threadsNum,
                CancellationToken = ct.Token,
            };

            var noises = (int)Math.Round((random ? curBytes.Length : curBytes.Length / 3) * (percent / 100d));
            var blackCount = Convert.ToInt32(((blackPercent / 100d)) * noises);
            var whiteCount = noises - blackCount;
            if (random) GenerateRandomIndices(curBytes.Length, noises, threadsNum);

            lock (curBytes)
            {
                try
                {
                    bool isWhite = true;
                    for (int i = 0; i < curBytes.Length; i += 3)
                    {
                        if (random)
                        {
                            if (randomIndecies.ContainsKey(i) && blackCount > 0 && isWhite || randomIndecies.ContainsKey(i) && blackCount > 0 && whiteCount <= 0)//i % 2 == 0 && 
                            {
                                curBytes[i] = 255;
                                curBytes[i + 1] = 255;
                                curBytes[i + 2] = 255;
                                isWhite = false;
                                --whiteCount;
                                CountOfNoises++;
                            }
                            else if (randomIndecies.ContainsKey(i) && !isWhite && whiteCount > 0 || randomIndecies.ContainsKey(i) && whiteCount > 0 && blackCount <= 0)//i % 2 != 0 && 
                            {
                                curBytes[i] = 0;
                                curBytes[i + 1] = 0;
                                curBytes[i + 2] = 0;
                                isWhite = true;
                                --whiteCount;
                                --blackCount;
                                CountOfNoises++;
                            }

                        }
                        else
                        {

                            if (blackCount <= 0)
                            {
                                curBytes[i] = 255;
                                curBytes[i + 1] = 255;
                                curBytes[i + 2] = 255;
                                CountOfNoises++;
                            }
                            else if (blackCount != 0)
                            {
                                curBytes[i] = 0;
                                curBytes[i + 1] = 0;
                                curBytes[i + 2] = 0;
                                --blackCount;
                                CountOfNoises++;
                            }

                        }

                        if (CountOfNoises >= noises) break;
                    };
                }
                catch { }
            }

            return curBytes;
        }

        static void GenerateRandomIndices(int arrayLength, int n, int tNum)
        {
            if (n > arrayLength)
            {
                throw new ArgumentException("n cannot be greater than the array length.");
            }

            Random rand = new Random();
            randomIndecies = new();

            CancellationTokenSource ct = new CancellationTokenSource();
            ParallelOptions options = new ParallelOptions() { MaxDegreeOfParallelism = 1, CancellationToken = ct.Token };

            try
            {
                Parallel.For(0, arrayLength, options, i =>
                {
                    int randomIndex;
                    do
                    {
                        randomIndex = rand.Next(arrayLength);
                    } while (randomIndecies.ContainsKey(randomIndex)); // Ensure uniqueness

                    randomIndecies[randomIndex] = true;

                    if (randomIndecies.Count == n) ct.Cancel();
                });
            }
            catch (Exception ex) {
                if (!ct.IsCancellationRequested) MessageBox.Show(ex.Message);
            }

        }

        public static byte[] AddMultyNoise(ref List<byte[]> bytes, int channelIndex, double minCoef, double maxCoef, int threadsNum)
        {
            if (bytes == null) return bytes[channelIndex];
            CountOfNoises = 0;

            byte[] curBytes = bytes[channelIndex];
            var ct = new CancellationTokenSource();//ForThreadsStopping

            ParallelOptions parallelOptions = new ParallelOptions()
            {
                MaxDegreeOfParallelism = threadsNum,
                CancellationToken = ct.Token,
            };

            Parallel.For(0, curBytes.Length, parallelOptions, i =>
            {
                Random random = new Random();
                double val = curBytes[i] * (random.NextDouble() * (maxCoef - minCoef) + minCoef);
                curBytes[i] = Convert.ToByte(Math.Max(0, Math.Min(255, val)));
            });

            return curBytes;
        }

        public static byte[] AddAdditiveNoise(ref List<byte[]> bytes, int channelIndex, double max, int threadsNum)
        {
            if (bytes == null) return bytes[channelIndex];
            CountOfNoises = 0;

            byte[] curBytes = bytes[channelIndex];
            var ct = new CancellationTokenSource();//ForThreadsStopping

            ParallelOptions parallelOptions = new ParallelOptions()
            {
                MaxDegreeOfParallelism = threadsNum,
                CancellationToken = ct.Token,
            };

            Random r = new Random();

            lock (bytes)
            {
                Parallel.For(0, curBytes.Length, i =>
                {
                    int noiseVal = (int)(r.NextDouble() * max - max / 2);
                    curBytes[i] = (byte)Math.Max(0, Math.Min(255, curBytes[i] + noiseVal));
                });

            }

            return curBytes;
        }

        public static List<HSVPixel[]> ToHSVFormat(this byte[] oBytes)
        {
            var allTheBytes = new List<HSVPixel[]>();

            int size = oBytes.Length / 3;
            var nBytes = new HSVPixel[size];
            var HBytes = new HSVPixel[size];
            var SBytes = new HSVPixel[size];
            var VBytes = new HSVPixel[size];

            for (int i = 0, d = 0; i < oBytes.Length; i += 3, d += 1)//8 - a, 8 - b, 8 - r, 8 - g
            {
                double r = 0.0d, g = 0.0d, b = 0.0d;

                try { r = oBytes[i] / 255.0d; } catch { }
                try { g = oBytes[i + 1] / 255.0d; } catch { }
                try { b = oBytes[i + 2] / 255.0d; } catch { }

                double maxRGB = Math.Max(r, Math.Max(g, b));
                double minRGB = Math.Min(r, Math.Min(g, b));

                double S = maxRGB == 0 ? 0 : 1 - (minRGB / maxRGB),
                       V = maxRGB;

                double H = GetH(r, g, b, maxRGB, minRGB);

                //while (H > 360) H -= 360;

                H = Math.Max(0, Math.Min(360, H));
                S = Math.Max(0, Math.Min(1, S));
                V = Math.Max(0, Math.Min(1, V));

                try
                {
                    //All the values
                    nBytes[d] = new HSVPixel() { H = H, S = S, V = V };

                    //H bytes
                    HBytes[d] = new HSVPixel() { H = H, S = 1, V = 1 };

                    //SV
                    SBytes[d] = new HSVPixel() { H = S, S = S, V = S };
                    VBytes[d] = new HSVPixel() { H = V, S = V, V = V };
                }
                catch { }
            }

            allTheBytes.Add(nBytes);
            allTheBytes.Add(HBytes);
            allTheBytes.Add(SBytes);
            allTheBytes.Add(VBytes);

            return allTheBytes;
        }

        public static byte[] ToColorBytesFromHSV(this HSVPixel[] channel)
        {

            byte[] bytes = new byte[channel.Length * 3];

            for (int i = 0, d = 0; i < channel.Length; i++, d += 3)
            {

                if (channel[i].S == 0)
                { // находимся на оси симметрии - оттенки серого 
                    bytes[d] = Convert.ToByte(Math.Max(0, Math.Min(255, channel[i].V * 255))); // если V=0 черный цвет 
                    bytes[d + 1] = Convert.ToByte(Math.Max(0, Math.Min(255, channel[i].V * 255)));
                    bytes[d + 2] = Convert.ToByte(Math.Max(0, Math.Min(255, channel[i].V * 255)));
                }
                else
                {
                    bytes[d] = bytes[d + 1] = bytes[d + 2] = 0;

                    int sector = (int)Math.Floor(channel[i].H / 60); // floor(x) возвращает наибольшее целое <= x 
                    double frac = channel[i].H / 60d - sector; // дробная часть H/60 
                    double T = channel[i].V * (1 - channel[i].S);
                    double P = channel[i].V * (1 - channel[i].S * frac);
                    double Q = channel[i].V * (1 - channel[i].S * (1 - frac));

                    byte bT = Convert.ToByte(Math.Max(0, Math.Min(255, T * 255))),
                         bP = Convert.ToByte(Math.Max(0, Math.Min(255, P * 255))),
                         bQ = Convert.ToByte(Math.Max(0, Q * 255)),
                         bV = Convert.ToByte(Math.Max(0, Math.Min(255, channel[i].V * 255)));

                    try
                    {
                        switch (sector)
                        {
                            case 0: bytes[d] = bV; bytes[d + 1] = bQ; bytes[d + 2] = bT; break;
                            case 1: bytes[d] = bP; bytes[d + 1] = bV; bytes[d + 2] = bT; break;
                            case 2: bytes[d] = bT; bytes[d + 1] = bV; bytes[d + 2] = bQ; break;
                            case 3: bytes[d] = bT; bytes[d + 1] = bP; bytes[d + 2] = bV; break;
                            case 4: bytes[d] = bQ; bytes[d + 1] = bT; bytes[d + 2] = bV; break;
                            case 5: bytes[d] = bV; bytes[d + 1] = bT; bytes[d + 2] = bP; break;
                        }
                    }
                    catch { }
                }

            }

            return bytes;
        }

        public static byte[] ToBytesFromHSV(this HSVPixel[] channel)
        {
            int size = (channel.Length * 3);
            byte[] bytes = new byte[size];

            for (int i = 0, d = 0; i < channel.Length; i++, d += 3)
            {
                if (d + 3 > size) break;
                try { bytes[d] = bytes[d + 1] = bytes[d + 2] = Convert.ToByte(Math.Max(0, Math.Min(255, channel[i].H * 255.0))); }
                catch { }
            }

            return bytes;
        }

        public static double GetH(double r, double g, double b, double maxRGB, double minRGB)
        {
            double h = 0.0;

            if (maxRGB == minRGB) return 0.0d;

            if (maxRGB == r && g >= b) return 60 * ((g - b) / (maxRGB - minRGB)) + 0;

            else if (maxRGB == r && g < b) return 60 * ((g - b) / (maxRGB - minRGB)) + 360;

            if (maxRGB == g) return 60 * ((b - r) / (maxRGB - minRGB)) + 120;

            if (maxRGB == b) return 60 * ((r - g) / (maxRGB - minRGB)) + 240;

            return h;
        }

        public static byte[] ToRgbFromYCbCrJpegFormat(this byte[] oBytes)
        {
            var nBytes = new byte[oBytes.Length];

            for (int i = 0; i < oBytes.Length; i += 3)//8 - a, 8 - b, 8 - r, 8 - g
            {
                byte y = 0, cr = 0, cb = 0;

                try { y = oBytes[i]; } catch { }
                try { cr = oBytes[i + 1]; } catch { }
                try { cb = oBytes[i + 2]; } catch { }


                int r = (int)(y + 1.402 * (cr - 128)),
                    g = (int)(y - 0.344136 * (cb - 128) - 0.714136 * (cr - 128)),
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

        public static byte[] ApplyConvolutionFilterParallel(byte[] image, double[,] kernel, int width, int height, int ppb, int threads = 1)
        {
            int kernelSize = kernel.GetLength(0);
            int padding = (kernelSize - 1) / 2;

            Parallel.For(padding, height - padding, new ParallelOptions { MaxDegreeOfParallelism = threads }, y =>
            {
                for (int x = padding; x < width - padding; x++)
                {
                    byte r = 0, g = 0, b = 0;

                    for (int i = -padding, row = 0; i <= padding; i++, row++)
                    {
                        for (int j = -padding, col = 0; j <= padding; j++, col++)
                        {
                            int pixelX = Math.Min(Math.Max(0, x + j), width - 1);
                            int pixelY = Math.Min(Math.Max(0, y + i), height - 1);

                            int index = (pixelY * width + pixelX) * ppb;

                            r += Convert.ToByte(Math.Max(0, Math.Min(255, kernel[row, col] * image[index])));
                            g += Convert.ToByte(Math.Max(0, Math.Min(255, kernel[row, col] * image[index + 1])));
                            b += Convert.ToByte(Math.Max(0, Math.Min(255, kernel[row, col] * image[index + 2])));
                        }
                    }

                    int outerIndex = (y * width + x) * ppb;
                    image[outerIndex] = r;
                    image[outerIndex + 1] = g;
                    image[outerIndex + 2] = b;
                }
            });

            return image;
        }

        public static byte[] ApplyHarmonicMeanFilterParallel(byte[] image, int maskWidth, int maskHeight, int width, int height, int ppb, int threads = 1)
        {
            int paddingX = (maskWidth - 1) / 2;
            int paddingY = (maskHeight - 1) / 2;

            Parallel.For(paddingY, height - paddingY, new ParallelOptions { MaxDegreeOfParallelism = threads }, y =>
            {
                for (int x = paddingX; x < width - paddingX; x++)
                {
                    double r = 0, g = 0, b = 0;
                    int count = 0;

                    for (int i = 0, row = 0; i < maskHeight; i++, row++)
                    {
                        for (int j = 0, col = 0; j < maskWidth; j++, col++)
                        {
                            int pixelX = Math.Min(Math.Max(0, x + j), width - 1);
                            int pixelY = Math.Min(Math.Max(0, y + i), height - 1);

                            int index = (pixelY * width + pixelX) * ppb;

                            r += (1.0d / (image[index] == 0 ? 1 : image[index]));
                            g += (1.0d / (image[index + 1] == 0 ? 1 : image[index + 1]));
                            b += (1.0d / (image[index + 2] == 0 ? 1 : image[index + 2]));

                            count++;
                        }
                    }

                    int outerIndex = (y * width + x) * ppb;

                    image[outerIndex] = (byte)(count / r);
                    image[outerIndex + 1] = (byte)(count / g);
                    image[outerIndex + 2] = (byte)(count / b);
                }
            });

            return image;
        }

        public static byte[] Apply2DGaussianFilter(byte[] image, double[,] kernel, int imgWidth, int imgHeight, int pixelPerByte, int threads)
        {
            int kernelSize = kernel.GetLength(0);
            int halfKernelSize = kernelSize / 2;
            var result = new byte[image.Length];

            Parallel.For(0, imgWidth, new ParallelOptions() { MaxDegreeOfParallelism = threads }, i =>
            {
                for (int j = 0; j < imgHeight; j++)
                {
                    double sumR = 0, sumG = 0, sumB = 0;

                    for (int ki = 0; ki < kernelSize; ki++)
                    {
                        for (int kj = 0; kj < kernelSize; kj++)
                        {
                            int ni = i + ki - halfKernelSize;
                            int nj = j + kj - halfKernelSize;

                            if (ni >= 0 && ni < imgWidth && nj >= 0 && nj < imgHeight)
                            {
                                int index = (nj * imgWidth + ni) * pixelPerByte;

                                sumR += image[index] * kernel[ki, kj];
                                sumG += image[index + 1] * kernel[ki, kj];
                                sumB += image[index + 2] * kernel[ki, kj];
                            }
                        }
                    }

                    int resultIndex = (j * imgWidth + i) * pixelPerByte;
                    result[resultIndex] = (byte)sumR;
                    result[resultIndex + 1] = (byte)sumG;
                    result[resultIndex + 2] = (byte)sumB;
                }
            });

            return result;
        }


        public static double CalculateDelta(byte[] original, byte[] filtered)
        {
            double delta = 0;

            for (int i = 0; i < original.Length; i++)
            {
                delta += Math.Pow(original[i] - filtered[i], 2);
            }

            return Math.Sqrt(delta);
        }

        public static double CalculateMSE(byte[] original, byte[] filtered)
        {
            double mse = 0;

            for (int i = 0; i < original.Length; i++)
            {
                mse += Math.Pow(original[i] - filtered[i], 2);
            }

            return mse / original.Length;
        }

        public static double CalculateMSAD(byte[] original, byte[] filtered)
        {
            double msad = 0;

            for (int i = 0; i < original.Length; i++)
            {
                msad += Math.Abs(original[i] - filtered[i]);
            }

            return msad / original.Length;
        }

        public static byte[] ApplyLocalHistogrammFilter(byte[] image, int width, int height, int pixelPerByte, int filterSize, int threads = 1)
        {
            int imageSize = image.Length;
            byte[] result = new byte[imageSize];

            Parallel.For(0, height, new ParallelOptions { MaxDegreeOfParallelism = threads }, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * pixelPerByte;

                    int[] histogramR = new int[256];
                    int[] histogramG = new int[256];
                    int[] histogramB = new int[256];

                    // Собираем гистограммы
                    for (int i = -filterSize; i <= filterSize; i++)
                    {
                        for (int j = -filterSize; j <= filterSize; j++)
                        {
                            int newX = x + i;
                            int newY = y + j;

                            if (newX >= 0 && newX < width && newY >= 0 && newY < height)
                            {
                                int neighborIndex = (newY * width + newX) * pixelPerByte;
                                histogramR[image[neighborIndex]]++;
                                histogramG[image[neighborIndex + 1]]++;
                                histogramB[image[neighborIndex + 2]]++;
                            }
                        }
                    }

                    // Находим медианы
                    byte medianR = FindMedian(histogramR);
                    byte medianG = FindMedian(histogramG);
                    byte medianB = FindMedian(histogramB);

                    result[index] = medianR;
                    result[index + 1] = medianG;
                    result[index + 2] = medianB;
                }
            });

            return result;
        }

        private static byte FindMedian(int[] histogram)
        {
            int sum = 0;
            int medianIndex = histogram.Length / 2;

            for (byte i = 0; i < histogram.Length; i++)
            {
                sum += histogram[i];
                if (sum > medianIndex)
                {
                    return i;
                }
            }

            return 0;
        }

        public static byte[] ApplyParallelRecursiveFilter(byte[] image, int width, int height, int pixelPerByte, int filterSize, int threads = 1)
        {
            int imageSize = image.Length;
            var result = new byte[imageSize];

            Parallel.For(0, height, new ParallelOptions { MaxDegreeOfParallelism = threads }, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * pixelPerByte;
                    double[] sum = new double[pixelPerByte];

                    ApplyRecursiveFilter(image, width, height, pixelPerByte, filterSize, x, y, sum, 1);

                    for (int i = 0; i < pixelPerByte; i++)
                    {
                        result[index + i] = (byte)(sum[i] / ((filterSize * 2 + 1) * (filterSize * 2 + 1)));
                    }
                }
            });

            return result;
        }

        private static void ApplyRecursiveFilter(byte[] image, int width, int height, int pixelPerByte, int filterSize, int x, int y, double[] sum, int recursionDepth)
        {
            if (recursionDepth <= 0)
            {
                int index = (y * width + x) * pixelPerByte;

                for (int i = 0; i < pixelPerByte; i++)
                {
                    sum[i] += image[index + i];
                }
            }
            else
            {
                for (int i = -filterSize; i <= filterSize; i++)
                {
                    for (int j = -filterSize; j <= filterSize; j++)
                    {
                        int newX = x + i;
                        int newY = y + j;

                        if (newX >= 0 && newX < width && newY >= 0 && newY < height)
                        {
                            ApplyRecursiveFilter(image, width, height, pixelPerByte, filterSize, newX, newY, sum, recursionDepth - 1);
                        }
                    }
                }
            }
        }
    }

    public class ColorType
    {
        public int Key { get; set; }
        public string Value { get; set; } = System.String.Empty;
    }

    public class HSVPixel
    {
        public double H { get; set; }
        public double S { get; set; }
        public double V { get; set; }
    }


    public static class EdgeDetection//lab3
    {

        public static byte[] RobertsOperator(byte[] image, int width, int height, int ppb, int threads, double gain, double threshold)
        {
            byte[] result = new byte[image.Length];

            Parallel.For(0, height, new ParallelOptions() { MaxDegreeOfParallelism = threads }, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * ppb;

                    // Игнорирование граничных пикселей
                    if (x + 1 >= width || y + 1 >= height)
                    {
                        result[index] = 0;
                        result[index + 1] = 0;
                        result[index + 2] = 0;
                    }
                    else
                    {
                        double intensity1 = image[index];
                        double intensity2 = image[index + ppb];
                        double intensity3 = image[index + width * ppb];
                        double intensity4 = image[index + (width + 1) * ppb];

                        double gradient = Math.Abs(intensity1 - intensity4) + Math.Abs(intensity2 - intensity3);

                        // Применение усиления и порогового значения
                        result[index] = (byte)(gradient > threshold ? Math.Min(255, gain * gradient) : 0);
                        result[index + 1] = (byte)(gradient > threshold ? Math.Min(255, gain * gradient) : 0);
                        result[index + 2] = (byte)(gradient > threshold ? Math.Min(255, gain * gradient) : 0);
                    }
                }
            });

            return result;
        }

        public static byte[] SobelOperator(byte[] image, int width, int height, int ppb, int threads, double gain, double threshold, double orientationCoeff)
        {
            byte[] result = new byte[image.Length];
            int kernelSize = 3;

            // Ядра Собеля для обработки по горизонтали и вертикали
            double[,] sobelKernelX = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
            double[,] sobelKernelY = { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };

            Parallel.For(1, height - 1, new ParallelOptions() { MaxDegreeOfParallelism = threads }, y =>
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int index = (y * width + x) * ppb;

                    double intensityX = 0;
                    double intensityY = 0;

                    // Применение ядра Собеля по горизонтали
                    for (int i = 0; i < kernelSize; i++)
                    {
                        for (int j = 0; j < kernelSize; j++)
                        {
                            int imageX = x + j - 1;
                            int imageY = y + i - 1;
                            int imageIndex = (imageY * width + imageX) * ppb;

                            intensityX += sobelKernelX[i, j] * image[imageIndex];
                            intensityY += sobelKernelY[i, j] * image[imageIndex];
                        }
                    }

                    // Вычисление градиента
                    double gradient = Math.Sqrt(Math.Pow(intensityX, 2) + Math.Pow(orientationCoeff * intensityY, 2));

                    // Применение усиления и порогового значения
                    result[index] = (byte)(gradient > threshold ? Math.Min(255, gain * gradient) : 0);
                    result[index + 1] = (byte)(gradient > threshold ? Math.Min(255, gain * gradient) : 0);
                    result[index + 2] = (byte)(gradient > threshold ? Math.Min(255, gain * gradient) : 0);
                }
            });

            return result;
        }

        public static byte[] LaplaceOperator(byte[] image, int width, int height, int ppb, int threads, double gain, double threshold, double[,] laplaceKernel)
        {
            byte[] result = new byte[image.Length];
            int kernelSize = laplaceKernel.GetLength(0);

            Parallel.For(1, height - 1, new ParallelOptions() { MaxDegreeOfParallelism = threads }, y =>
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int index = (y * width + x) * ppb;

                    double intensity = 0;

                    // Применение ядра Лапласа
                    for (int i = 0; i < kernelSize; i++)
                    {
                        for (int j = 0; j < kernelSize; j++)
                        {
                            int imageX = x + j - (kernelSize - 1) / 2;
                            int imageY = y + i - (kernelSize - 1) / 2;
                            int imageIndex = (imageY * width + imageX) * ppb;

                            intensity += laplaceKernel[i, j] * image[imageIndex];
                        }
                    }

                    // Применение усиления и порогового значения
                    result[index] = (byte)(Math.Abs(intensity) > threshold ? Math.Min(255, gain * Math.Abs(intensity)) : 0);
                    result[index + 1] = (byte)(Math.Abs(intensity) > threshold ? Math.Min(255, gain * Math.Abs(intensity)) : 0);
                    result[index + 2] = (byte)(Math.Abs(intensity) > threshold ? Math.Min(255, gain * Math.Abs(intensity)) : 0);
                }
            });

            return result;
        }

        public static byte[] GlobalThresholding(byte[] image, int width, int height, int ppb, int threads, int globalThreshold)
        {
            byte[] result = new byte[image.Length];

            Parallel.For(0, height, new ParallelOptions() { MaxDegreeOfParallelism = threads }, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * ppb;

                    // Глобальная бинаризация
                    int intensity = (int)(0.299 * image[index] + 0.587 * image[index + 1] + 0.114 * image[index + 2]);
                    byte binaryValue = (byte)(intensity > globalThreshold ? 255 : 0);

                    // Применение результатов бинаризации
                    result[index] = binaryValue;
                    result[index + 1] = binaryValue;
                    result[index + 2] = binaryValue;
                }
            });

            return result;
        }

        public static byte[] LocalNiblackThresholding(byte[] image, int width, int height, int ppb, int threads, double niblackK, int windowSize)
        {
            byte[] result = new byte[image.Length];

            Parallel.For(0, height, new ParallelOptions() { MaxDegreeOfParallelism = threads }, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * ppb;

                    // Локальная бинаризация (метод Ниблэка)
                    int intensity = (int)(0.299 * image[index] + 0.587 * image[index + 1] + 0.114 * image[index + 2]);

                    double localThreshold = 0;

                    for (int i = -windowSize / 2; i <= windowSize / 2; i++)
                    {
                        for (int j = -windowSize / 2; j <= windowSize / 2; j++)
                        {
                            int neighborX = Math.Max(0, Math.Min(width - 1, x + j));
                            int neighborY = Math.Max(0, Math.Min(height - 1, y + i));
                            int neighborIndex = (neighborY * width + neighborX) * ppb;

                            int neighborIntensity = (int)(0.299 * image[neighborIndex] + 0.587 * image[neighborIndex + 1] + 0.114 * image[neighborIndex + 2]);
                            localThreshold += neighborIntensity;
                        }
                    }

                    localThreshold /= (windowSize * windowSize);
                    byte binaryValue = (byte)(intensity > localThreshold - niblackK ? 255 : 0);

                    // Применение результатов бинаризации
                    result[index] = binaryValue;
                    result[index + 1] = binaryValue;
                    result[index + 2] = binaryValue;
                }
            });

            return result;
        }

        public static byte[] Dilation(byte[] image, int width, int height, int ppb, int threads, double[,] mask)
        {
            byte[] result = new byte[image.Length];

            Parallel.For(0, height, new ParallelOptions() { MaxDegreeOfParallelism = threads }, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * ppb;

                    int maxIntensity = 0;

                    for (int i = 0; i < mask.GetLength(0); i++)
                    {
                        for (int j = 0; j < mask.GetLength(1); j++)
                        {
                            int neighborX = x + j - (mask.GetLength(1) - 1) / 2;
                            int neighborY = y + i - (mask.GetLength(0) - 1) / 2;

                            if (neighborX >= 0 && neighborX < width && neighborY >= 0 && neighborY < height)
                            {
                                int neighborIndex = (neighborY * width + neighborX) * ppb;
                                int neighborIntensity = (int)(0.299 * image[neighborIndex] + 0.587 * image[neighborIndex + 1] + 0.114 * image[neighborIndex + 2]);
                                maxIntensity = Math.Max(maxIntensity, neighborIntensity);
                            }
                        }
                    }

                    result[index] = (byte)maxIntensity;
                    result[index + 1] = (byte)maxIntensity;
                    result[index + 2] = (byte)maxIntensity;
                }
            });

            return result;
        }

        public static byte[] Erosion(byte[] image, int width, int height, int ppb, int threads, double[,] mask)
        {
            byte[] result = new byte[image.Length];

            Parallel.For(0, height, new ParallelOptions() { MaxDegreeOfParallelism = threads }, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * ppb;

                    int minIntensity = 255;

                    for (int i = 0; i < mask.GetLength(0); i++)
                    {
                        for (int j = 0; j < mask.GetLength(1); j++)
                        {
                            int neighborX = x + j - (mask.GetLength(1) - 1) / 2;
                            int neighborY = y + i - (mask.GetLength(0) - 1) / 2;

                            if (neighborX >= 0 && neighborX < width && neighborY >= 0 && neighborY < height)
                            {
                                int neighborIndex = (neighborY * width + neighborX) * ppb;
                                int neighborIntensity = (int)(0.299 * image[neighborIndex] + 0.587 * image[neighborIndex + 1] + 0.114 * image[neighborIndex + 2]);
                                minIntensity = Math.Min(minIntensity, neighborIntensity);
                            }
                        }
                    }

                    result[index] = (byte)minIntensity;
                    result[index + 1] = (byte)minIntensity;
                    result[index + 2] = (byte)minIntensity;
                }
            });

            return result;
        }

        public static double[] CreateWeightMatrix(byte[] image, int width, int height, int ppb, int channel = 0)
        {
            int nWidth = width + width, nHeight = height * 2;
            int n = nWidth * nHeight;
            double[] weightMatrix = new double[n];

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    int pixelIndex = (i * width + j) * ppb;
                    byte channelValue = image[pixelIndex + channel];

                    // Индексы соседних пикселей
                    int rightPixelIndex = j < width - 1 ? (i * width + (j + 1)) * ppb : -1;
                    int bottomPixelIndex = i < height - 1 ? ((i + 1) * width + j) * ppb : -1;

                    // Индекс текущего пикселя в матрице весов
                    int wRowIndex = i * 2;
                    int wPixelIndex = j == 0 ? wRowIndex * nWidth : wRowIndex * nWidth + j * 2;

                    // Устанавливаем значение текущей яркости пикселя в матрицу весов
                    weightMatrix[wPixelIndex] = channelValue;

                    // Записываем разницу между текущим и правым пикселем
                    if (j < width - 1 && rightPixelIndex >= 0) // правый пиксель
                        weightMatrix[wPixelIndex + 1] = Math.Abs(channelValue - image[rightPixelIndex + channel]);

                    // Записываем разницу между текущим и нижним пикселем
                    if (i < height - 1 && bottomPixelIndex >= 0) // нижний пиксель
                        weightMatrix[wPixelIndex + nWidth] = Math.Abs(channelValue - image[bottomPixelIndex + channel]);
                }
            }


            Workbook wb = new Workbook();
            Worksheet ws = new Worksheet("ws");
            wb.Worksheets.Add(ws);

            for (int i = 0; i < nHeight; i++)
            {
                for (int j = 0; j < nWidth; j++)
                {
                    ws.Cells[i, j] = new Cell(weightMatrix[i * nWidth + j]);
                }

            }

            Worksheet ws2 = new Worksheet("image");
            wb.Worksheets.Add(ws2);


            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width * ppb; j += ppb)
                {
                    ws2.Cells[i, j / ppb] = new Cell(image[(i * width + j / ppb) * ppb + j % ppb]);
                }
            }


            wb.Save("w.xls");

            return weightMatrix;
        }

        public static byte[] SegmentImage(byte[] image, double[] weightMatrix, int width, int height, int threshold)
        {
            int ppb = 3; // количество каналов в изображении (RGB)
            byte[] segmentedImage = new byte[width * height * ppb];

            // Проходим по каждому пикселю матрицы весов
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    int wPixelIndex = i * width * 2 + j * 2;

                    // Проверяем значение текущего пикселя и его дельты с соседями
                    int pixelValue = (int)weightMatrix[wPixelIndex];
                    int delta = (int)weightMatrix[wPixelIndex + 1];

                    // Если значение дельты меньше порогового значения, выделяем область
                    if (delta < threshold)
                    {
                        int outputPixelIndex = (i * width + j) * ppb;

                        // Записываем цвет выделенной области в выходной массив
                        segmentedImage[outputPixelIndex] = Convert.ToByte(pixelValue); 
                        segmentedImage[outputPixelIndex + 1] = 0; 
                        segmentedImage[outputPixelIndex + 2] = 0; 
                    }
                    else
                    {
                        int outputPixelIndex = (i * width + j) * ppb;

                        // Записываем оригинальный цвет пикселя в выходной массив
                        segmentedImage[outputPixelIndex] = image[outputPixelIndex]; 
                        segmentedImage[outputPixelIndex + 1] = image[outputPixelIndex + 1];
                        segmentedImage[outputPixelIndex + 2] = image[outputPixelIndex + 2]; 
                    }
                }
            }

            return segmentedImage;
        }

    }

    public static class TextureLab{

        public static byte[] DetectLinesAndCircles(byte[] image, int width, int height, int ppb, bool isCircle, int votes = 70)
        {
            // Преобразование изображения в двумерный массив
            int[,] imageData = new int[height, width];
            int index = 0;
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    imageData[i, j] = image[index];
                    index += ppb;
                }
            }

            // Расчет параметров для пространства Хафа
            int rMax = (int)Math.Ceiling(Math.Sqrt(width * width + height * height));
            int thetaMax = 180;
            int[,] accumulator = new int[rMax, thetaMax];

            // Проход по изображению для поиска контуров
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    if (imageData[y, x] == 255) // Проверяем, что пиксель является контурным
                    {
                        // Проход по всем возможным значениям радиуса и угла
                        for (int theta = 0; theta < thetaMax; theta++)
                        {
                            double radians = theta * (Math.PI / 180);
                            int r = (int)Math.Round(x * Math.Cos(radians) + y * Math.Sin(radians));
                            if (r >= 0 && r < rMax)
                            {
                                lock (accumulator)
                                {
                                    accumulator[r, theta]++;
                                }
                            }
                        }
                    }
                }
            });

            // Находим порог для количества голосов, чтобы определить линию или окружность
            int threshold = votes;

            // Создаем список для хранения координат линий и окружностей
            List<(int, int, int)> linesAndCircles = new List<(int, int, int)>();

            // Поиск линий или окружностей с количеством голосов выше порога
            for (int r = 0; r < rMax; r++)
            {
                for (int theta = 0; theta < thetaMax; theta++)
                {
                    if (accumulator[r, theta] > threshold)
                    {
                        linesAndCircles.Add((r, theta, accumulator[r, theta]));
                    }
                }
            }

            // Создание результирующего изображения с контурами
            byte[] resultImage = new byte[width * height * ppb];
            Array.Copy(image, resultImage, image.Length);

            // Отрисовка найденных линий и окружностей
            Parallel.ForEach(linesAndCircles, lineOrCircle =>
            {
                int r = lineOrCircle.Item1;
                int theta = lineOrCircle.Item2;
                if (isCircle)
                {
                    DrawCircle(resultImage, width, height, r, theta);
                }
                else
                {
                    DrawLine(resultImage, width, height, r, theta);
                }
            });

            return resultImage;
        }

        private static void DrawLine(byte[] image, int width, int height, int r, int theta)
        {
            double radians = theta * (Math.PI / 180);
            for (int x = 0; x < width; x++)
            {
                int y = (int)Math.Round((r - x * Math.Cos(radians)) / Math.Sin(radians));
                if (y >= 0 && y < height)
                {
                    int index = y * width * 3 + x * 3;
                    image[index] = 255;
                    image[index + 1] = 0;
                    image[index + 2] = 0;
                }
            }
        }

        private static void DrawCircle(byte[] image, int width, int height, int r, int theta)
        {
            double radians = theta * (Math.PI / 180);
            int centerX = width / 2;
            int centerY = height / 2;
            int radius = r;
            for (int angle = 0; angle < 360; angle++)
            {
                int x = (int)Math.Round(centerX + radius * Math.Cos(angle * (Math.PI / 180)));
                int y = (int)Math.Round(centerY + radius * Math.Sin(angle * (Math.PI / 180)));
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    int index = y * width * 3 + x * 3;
                    image[index] = 255;
                    image[index + 1] = 0;
                    image[index + 2] = 0;
                }
            }
        }


    }





}
