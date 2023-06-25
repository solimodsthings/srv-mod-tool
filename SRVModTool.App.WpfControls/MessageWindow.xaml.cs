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

namespace SRVModTool.App.WpfControls
{
    /// <summary>
    /// Interaction logic for MessageWindow.xaml
    /// </summary>
    public partial class MessageWindow : Window
    {
        public MessageWindow(string header, string body, bool showCancel = false)
        {
            InitializeComponent();

            this.LabelMessageHeader.Content = header;
            this.TextBoxMessageBody.Text = body;

            if(showCancel)
            {
                this.ButtonCancel.Visibility = Visibility.Visible;
            }

        }

        public void SetCancelButtonText(string text)
        {
            this.ButtonCancel.Content = text;
        }

        private void OnOkButtonClicked(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void OnCancelButtonClicked(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
