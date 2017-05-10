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

namespace LeagueAutoAccept
{
    /// <summary>
    /// Interaction logic for CustomMessageBox.xaml
    /// </summary>
    public partial class CustomMessageBox : Window
    {
        public CustomMessageBox(string title, string text)
        {
            InitializeComponent();
            ResizeMode = ResizeMode.NoResize;
            textLabel.Content = text;
            Title = title;
            SizeChanged += CustomMessageBox_SizeChanged;
        }

        private void CustomMessageBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            button.Margin = new Thickness((this.ActualWidth / 2), button.Margin.Top, 0, button.Margin.Bottom);
        }

        public static void Show(string title, string text)
        {
            CustomMessageBox box = new CustomMessageBox(title, text);
            box.SizeToContent = SizeToContent.WidthAndHeight;
            box.ShowDialog();
            box.Focus();
        }

        private void button_Click_1(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
