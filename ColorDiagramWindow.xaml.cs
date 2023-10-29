using LiveCharts.Defaults;
using LiveCharts.Wpf;
using LiveCharts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ParallelPictureProcessing
{
    /// <summary>
    /// Логика взаимодействия для ColorDiagramWindow.xaml
    /// </summary>
    public partial class ColorDiagramWindow : Window
    {
        public SeriesCollection SeriesCollection { get; set; } = new SeriesCollection();
        public List<string> Labels { get; set; }
        public Func<double, string> Formatter { get; set; }
        List<byte[]> picesOfYCbCrBytes;
        byte empty;
        bool ignoreEmptyVal=true;

        public ColorDiagramWindow(List<byte[]> picesOfYCbCrBytes, byte empty = 128)
        {
            InitializeComponent();
            this.picesOfYCbCrBytes = picesOfYCbCrBytes;
            this.empty = empty;
        }

        private void ChannelSelected_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (picesOfYCbCrBytes is null || picesOfYCbCrBytes.Count == 0) return;

            var select = sender as ComboBox;
            var index = Convert.ToInt32((select.SelectedValue as ComboBoxItem).Content);

            var dict = Utils.CountBytes(picesOfYCbCrBytes[index]);
            
            if(ignoreEmptyVal) dict.Remove(empty);
            
            SeriesCollection.Clear();

            ChartValues<ObservablePoint> List1Points = new ChartValues<ObservablePoint>();

            foreach (var kvp in dict.OrderBy(a => a.Key)) List1Points.Add(new ObservablePoint(kvp.Key, kvp.Value));

            SeriesCollection.Add(new ColumnSeries() { Title = "Байты", Values = List1Points });

            //Labels = dict.Keys.Select(i=> i.ToString()).ToList();
            //Formatter = value => value.ToString("C");
            DataContext = this;
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var check = sender as CheckBox;
            this.ignoreEmptyVal = check.IsChecked ?? false;
        }
    }

   
}
