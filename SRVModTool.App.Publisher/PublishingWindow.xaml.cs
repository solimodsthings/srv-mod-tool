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

namespace SRVModTool.App.Publisher
{
    /// <summary>
    /// Interaction logic for MessageWindow.xaml
    /// </summary>
    public partial class PublishingWindow : Window
    {

        public PublishingMode SelectedPublishingMode { get; set; }

        public PublishingWindow()
        {
            InitializeComponent();
        }

        private void OnOkButtonClicked(object sender, RoutedEventArgs e)
        {

            if (this.RadioButtonDistributeAsStandaloneMod.IsChecked == true)
            {
                this.SelectedPublishingMode = PublishingMode.Standalone;
            }
            else if(this.RadioButtonDistributeAsSteamMod.IsChecked == true)
            {
                this.SelectedPublishingMode = PublishingMode.Steam;
            }
            else
            {
                this.SelectedPublishingMode = PublishingMode.None;
            }

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
