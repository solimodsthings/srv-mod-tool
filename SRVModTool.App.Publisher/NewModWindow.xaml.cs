using Microsoft.Win32;
using Ookii.Dialogs.WinForms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SRVModTool.App.Publisher
{
    /// <summary>
    /// This is a dialog window that allows a user to set the
    /// path to the game.
    /// </summary>
    public partial class NewModWindow : Window
    {
        private static readonly string PlaceholderModName = "New Mod ";
        
        private string _currentTargetPath;
        public Result Result { get; set; }

        public Mod ResultMod { get; set; }
        public string ResultModParentFolder { get; set; }

        public NewModWindow(string folder = null)
        {
            InitializeComponent();

            // var dir = Directory.GetCurrentDirectory();
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (folder != null && Directory.Exists(folder))
            {
                dir = folder;
            }

            _currentTargetPath = dir;

            this.TextBoxModLocation.Text = dir;

            int index = 1;
            var suggestedModName = PlaceholderModName + index;
            var path = System.IO.Path.Combine(dir, suggestedModName);
            
            while (File.Exists(path) || Directory.Exists(path))
            {
                index++;
                suggestedModName = PlaceholderModName + index;
                path = System.IO.Path.Combine(dir, suggestedModName);
            }

            this.TextBoxModName.Text = suggestedModName;

        }

        private void OnBrowseButtonClick(object sender, RoutedEventArgs e)
        {
            var folder = new VistaFolderBrowserDialog();

            // folder.RootFolder = Environment.SpecialFolder.Desktop;
            folder.SelectedPath = this.TextBoxModLocation.Text;
            folder.ShowNewFolderButton = true;

            var result = folder.ShowDialog();

            if(result == System.Windows.Forms.DialogResult.OK)
            {
                this.TextBoxModLocation.Text = folder.SelectedPath;
            }
        }

        private void OnCreateButtonClick(object sender, RoutedEventArgs e)
        {
            var modName = this.TextBoxModName.Text;
            var parentFolder = this.TextBoxModLocation.Text;

            if (string.IsNullOrEmpty(modName) || string.IsNullOrEmpty(parentFolder))
            {
                // Might want to give some UI feedback, but this is ok as the window
                // won't simply vanish.
                return;
            }

            this.ResultMod = new Mod() { Name = modName }; // Base game mod by default
            this.ResultModParentFolder = parentFolder;
            this.DialogResult = true;
            this.Close();

        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
