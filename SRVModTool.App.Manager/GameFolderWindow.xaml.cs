using Microsoft.Win32;
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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SRVModTool.App.Manager
{
    /// <summary>
    /// This is a dialog window that allows a user to set the
    /// path to the game.
    /// </summary>
    public partial class GameFolderWindow : Window
    {
        private ModManager Manager;

        public GameFolderWindow(ModManager manager)
        {
            InitializeComponent();

            this.Manager = manager;

            if(!string.IsNullOrEmpty(Manager.GameFolderPath))
            {
                this.TextBoxGameFolderPath.Text = this.Manager.GameFolderPath;
                this.ButtonCancelGameFolder.Visibility = Visibility.Visible;
            }
        }
        private void OnAutodetectButtonClick(object sender, RoutedEventArgs e)
        {

            try
            {
                var autodetector = new GameFolderAutodetector();
                var result = autodetector.TryAutodetect();

                if(result.IsSuccessful)
                {
                    this.TextBoxGameFolderPath.Text = autodetector.AutodetectedGameFolder;
                    this.ShowErrorMessage(false);
                }
                else
                {
                    this.ShowErrorMessage(true, result.ErrorMessage);
                }

            }
            catch(Exception ex)
            {
                ex.AppendToLogFile();
                this.ShowErrorMessage(true, "Autodetection failed. See error.log");
            }

        }

        private void OnSaveButtonClick(object sender, RoutedEventArgs e)
        {
            var folder = this.TextBoxGameFolderPath.Text;
            var result = this.Manager.RegisterGameFolderPath(folder);

            if (result.IsSuccessful)
            {
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                // Thes specified path wasn't accepted by ModManager so 
                // check to see if the user selected a subdirectory of the main game
                // folder. If they did then it is posisble to extract the correct path.

                var possibleMatch = Game.FindFolder(folder, 3);
                if(!string.IsNullOrEmpty(possibleMatch))
                {
                    result = this.Manager.RegisterGameFolderPath(possibleMatch);

                    if(result.IsSuccessful)
                    {
                        this.DialogResult = true;
                        this.Close();
                    }
                }
            }

            if(!result.IsSuccessful)
            {
                this.TextBoxGameFolderPath.Text = string.Empty;
                this.ShowErrorMessage(true, "The selected folder cannot be accessed, does not contain Septaroad Voyager, or is not a folder.");
            }
        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ShowErrorMessage(bool visible, string message = "")
        {
            if(visible)
            {
                this.TextBlockErrorMessage.Text = message;
                this.TextBlockErrorMessage.Visibility = Visibility.Visible;
            }
            else
            {
                this.TextBlockErrorMessage.Visibility = Visibility.Collapsed;
                this.TextBlockErrorMessage.Text = message;
            }
        }

        private void OnTextBoxChanged(object sender, TextChangedEventArgs e)
        {
            /*
            if(string.IsNullOrEmpty(this.TextBoxGameFolderPath.Text))
            {
                this.ButtonAutodetectGameGolder.Visibility = Visibility.Visible;
            }
            else
            {
                this.ButtonAutodetectGameGolder.Visibility = Visibility.Hidden;
            }
            */
        }
    }
}
