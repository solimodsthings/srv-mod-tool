using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using SRVModTool;
using SRVModTool.App.WpfControls;
using Microsoft.Win32;

namespace SRVModTool.App.Manager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static double SteamWorkshopMaximumSleepTime = 60; // in seconds
        private static int SteamWorkshopCreationSleepTime = 250;  // in milliseconds
        private static int SteamWorkshopDeletionSleepTime = 1000; // in milliseconds

        public ModManager Manager { get; set; }
        private FileSystemWatcher SteamWorkshopDirectoryWatcher { get; set; } // Used to check if a new mod has been subscribed to
        public ObservableCollection<ModViewModel> ModViewModels { get; set; }
        public ModViewModel SelectedMod { get; set; }
        private List<ModConfigurationSnapshot> ModConfigurationSnapshots { get; set; }


        public MainWindow()
        {
            this.InitializeComponent();
            this.InitializeContextMenuComponent();

            this.Manager = new ModManager();
            var result = this.Manager.Load();

            if(!result.IsSuccessful)
            {
                this.ShowMessage("Warning", result.ErrorMessage);
            }

            this.SteamWorkshopDirectoryWatcher = new FileSystemWatcher();
            this.SteamWorkshopDirectoryWatcher.IncludeSubdirectories = true;
            this.SteamWorkshopDirectoryWatcher.Filter = Mod.InfoFile;
            this.SteamWorkshopDirectoryWatcher.Created += OnSteamWorkshopDirectoryChanged;
            this.SteamWorkshopDirectoryWatcher.Deleted += OnSteamWorkshopDirectoryChanged;

            this.TryStartSteamWorkshopDirectoryWatcher();

            this.ModViewModels = new ObservableCollection<ModViewModel>();
            this.ListAvailableMods.ItemsSource = this.ModViewModels;
            this.RebuildModViewModels();

            this.ModConfigurationSnapshots = new List<ModConfigurationSnapshot>();
            this.TakeModConfigurationSnapshot();

            this.SelectedMod = new ModViewModel(new ModConfiguration());
            // this.ModInfoPanel.DataContext = this.SelectedMod;
            this.ModStatePanel.DataContext = this.SelectedMod;

            if(this.Manager.ModConfigurations.Count > 0)
            {
                // This should get the mod info panel to update automatically
                this.ListAvailableMods.SelectedIndex = 0;
            }

            

            var style = new Style();
                        
            style.TargetType = typeof(ListViewItem);
            style.Setters.Add(new Setter(ListViewItem.ForegroundProperty, new SolidColorBrush(Color.FromArgb((byte)255, (byte)150, (byte)150, (byte)150))));


            var trigger = new DataTrigger();
            trigger.Binding = new Binding("State");
            trigger.Value = "Enabled";
            trigger.Setters.Add(new Setter(ListViewItem.ForegroundProperty, Brushes.White));
            style.Triggers.Add(trigger);

            this.ExtendItemContainerStyle(style);

            var listStyle = new Style();
            listStyle.TargetType = typeof(ListView);
            listStyle.Setters.Add(new Setter(ListView.BackgroundProperty, new SolidColorBrush(Color.FromArgb((byte)255, (byte)25, (byte)25, (byte)25))));

            listStyle.BasedOn = this.ListAvailableMods.Style;
            this.ListAvailableMods.Style = listStyle;
        }

        private void ExtendItemContainerStyle(Style style)
        {
            style.BasedOn = this.ListAvailableMods.ItemContainerStyle;
            this.ListAvailableMods.ItemContainerStyle = style;
        }

        // The context menu for the main ListView is declared in XAML.
        // This method makes it so the context menu only appears if a ListViewItem
        // is right-clicked, not anywhere in the ListView (including empty space).
        // If this can all be moved to XAML in the future, it should.
        private void InitializeContextMenuComponent()
        {
            var menu = this.ListAvailableMods.ContextMenu;

            var style = new Style();
            style.TargetType = typeof(ListViewItem);
            style.Setters.Add(new Setter(ListViewItem.ContextMenuProperty, menu));

            this.ExtendItemContainerStyle(style);
            this.ListAvailableMods.ContextMenu = null;
        }

        private void RebuildModViewModels()
        {
            this.ModViewModels.Clear();
            foreach (var mod in this.Manager.ModConfigurations)
            {
                var vm = new ModViewModel(mod);
                this.ModViewModels.Add(vm);
            }
        }

        private void RefreshModList()
        {
            this.ListAvailableMods.Items.Refresh();
        }

        private void RefreshModInfoPanel()
        {
            this.SelectedMod.Refresh();
        }

        // This is a method in case we need to put an task-in-progress animation
        // when data is being serialized to disk
        private void SaveModManager()
        {
            this.Manager.Save();
        }

        /// <summary>
        /// Shows or hides a dark, transparent overlay across the entirety of the application window.
        /// </summary>
        /// <param name="show">True to show the overlay or false to turn it off.</param>
        private void ShowDarkOverlay(bool show)
        {
            if (show)
            {
                this.CanvasFadeOut.Visibility = Visibility.Visible;
            }
            else
            {
                this.CanvasFadeOut.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowProgressRing(bool show)
        {
            if (show)
            {
                this.ProgressRing.Visibility = Visibility.Visible;
            }
            else
            {
                this.ProgressRing.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowMessage(string header, string body)
        {
            this.ShowDarkOverlay(true);
            var dialog = new MessageWindow(header, body);

            if (this.IsVisible)
            {
                dialog.Owner = this;
            }

            dialog.ShowDialog();

            this.ShowDarkOverlay(false);

        }

        private void ShowGameFolderDialog()
        {
            this.ShowDarkOverlay(true);

            var dialog = new GameFolderWindow(this.Manager);
            dialog.Owner = this;

            var result = dialog.ShowDialog();

            // Exit the application if the user doesn't want
            // to provide the game folder path and one hasn't been
            // defined yet (basically a first-time use scenario)
            if (result != true && string.IsNullOrEmpty(this.Manager.GameFolderPath))
            {
                this.Close();
            }
            else if (result == true && !string.IsNullOrEmpty(this.Manager.GameFolderPath))
            {
                TryStartSteamWorkshopDirectoryWatcher();

                this.RebuildModViewModels();
                this.RefreshModList();
                this.RefreshModInfoPanel();
                this.SaveModManager();
            }

            this.ShowDarkOverlay(false);

        }

        private void OnSelectedModChanged(object sender, SelectionChangedEventArgs e)
        {
            int selection = this.ListAvailableMods.SelectedIndex;

            if (selection >= 0 && selection < this.Manager.ModConfigurations.Count)
            {
                var mod = this.Manager.ModConfigurations[selection];
                this.SelectedMod.Set(mod);
                this.InfoPanel.IsEnabled = true;

                /*
                if(mod.RegistrationType == RegistrationType.SteamWorkshopItem)
                {
                    // Future release:
                    // this.LabelSteamWorkshopPage.Visibility = Visibility.Visible;
                    // this.TextBlockSteamWorkshopPage.Visibility = Visibility.Visible;

                    this.ButtonRemoveMod.IsEnabled = false;
                    this.ButtonRemoveMod.ToolTip = "Unsubscribe from the Steam Workshop item to remove this mod.";
                }
                else
                {
                    // Future release:
                    // this.LabelSteamWorkshopPage.Visibility = Visibility.Collapsed;
                    // this.TextBlockSteamWorkshopPage.Visibility = Visibility.Collapsed;

                    this.ButtonRemoveMod.IsEnabled = true;
                    this.ButtonRemoveMod.ToolTip = null;
                }
                */

                /*
                if(this.SelectedMod.SteamWorkshopId != null)
                {
                    this.LabelVisitSteamPage.Visibility = Visibility.Visible;
                    this.HyperlinkVisitSteamPage.Visibility = Visibility.Visible;
                }
                else
                {
                    this.LabelVisitSteamPage.Visibility = Visibility.Collapsed;
                    this.HyperlinkVisitSteamPage.Visibility = Visibility.Collapsed;
                }
                */

            }
            else
            {
                this.SelectedMod.Set(new ModConfiguration());
                this.InfoPanel.IsEnabled = false;
            }
        }

        private void OnSetGameFolderButtonClick(object sender, RoutedEventArgs e)
        {
            this.ShowGameFolderDialog();
        }

        private void OnWindowContentRendered(object sender, EventArgs e)
        {
            if(string.IsNullOrEmpty(this.Manager.GameFolderPath))
            {
                this.ShowGameFolderDialog();
            }
        }

        /// <summary>
        /// In this context, controller refers to the Radio Button controls
        /// that change mod state.
        /// </summary>
        private void OnControllerUsed(object sender, RoutedEventArgs e)
        {
            this.RefreshModList();
        }

        private void OnMoveModOrderUp(object sender, RoutedEventArgs e)
        {
            int selection = this.ListAvailableMods.SelectedIndex;

            if (selection > 0)
            {
                var configuration = this.Manager.ModConfigurations[selection];
                this.Manager.ShiftModOrderUp(selection);
                this.RebuildModViewModels();
                this.ListAvailableMods.SelectedIndex = configuration.OrderIndex;
            }

        }

        private void OnMoveModOrderDown(object sender, RoutedEventArgs e)
        {
            int selection = this.ListAvailableMods.SelectedIndex;

            if (selection < this.Manager.ModConfigurations.Count - 1)
            {
                var configuration = this.Manager.ModConfigurations[selection];
                this.Manager.ShiftModOrderDown(selection);
                this.RebuildModViewModels();
                this.ListAvailableMods.SelectedIndex = configuration.OrderIndex;
            }
        }

        // Only for standalone mods
        private void OnAddNewModByDragDrop(object sender, DragEventArgs e)
        {
            /*
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];

            if(files != null)
            {
                foreach (var filepath in files)
                {
                    var result = this.Manager.RegisterMod(filepath);
                    this.HandleRegistrationResult(result);
                }
            }
            */

        }

        
        private void OnAddNewModButtonClick(object sender, RoutedEventArgs e)
        {
            var message = new MessageWindow("Steam", "Mods are available through the Steam Workshop. Mods that you subscribe to will appear in this mod list. \n\nWould you like to open the Steam Workshop?", true);
            message.Owner = this;

            this.ShowDarkOverlay(true);
            if(message.ShowDialog() == true)
            {
                Game.OpenSteamWorkshop();
            }

            this.ShowDarkOverlay(false);

            /* 
            // Prior to Steam Workshop integration, this button showed a file dialog
            // to select an .hsmod file to load. Here was the code:
            
            var browse = new OpenFileDialog();
            browse.CheckFileExists = true;
            browse.DefaultExt = ".hsmod"; // Default file extension
            browse.Filter = "Himeko Sutori Mod (.hsmod)|*.hsmod"; // Filter files by extension

            if (browse.ShowDialog() == true)
            {
                var result = this.Manager.RegisterMod(browse.FileName);
                this.HandleRegistrationResult(result);
            }
            */
        }

        // Only for standalone mods. Steam mods should only be
        // removed by unsubscribing from them in the Steam Workshop
        private void OnRemoveMod(object sender, RoutedEventArgs e)
        {
            /*
            var selectedIndex = this.ListAvailableMods.SelectedIndex;

            if(selectedIndex >= 0 && selectedIndex < this.Manager.ModConfigurations.Count)
            {
                this.ShowDarkOverlay(true);
                
                var configuration = this.Manager.ModConfigurations[selectedIndex];
                var mod = configuration.Mod;
                var message = string.Format("Are you sure you want to uninstall and permanently remove mod '{0}' version {1}?", mod.Name, mod.Version);
                
                var confirmation = new ConfirmationWindow("Confirmation", message);
                confirmation.Owner = this;

                var result = confirmation.ShowDialog();

                if(result == true)
                {
                    this.Manager.UnregisterMod(configuration);
                    this.Manager.Save();
                    this.RebuildModViewModels();
                    this.ListAvailableMods.SelectedIndex = -1;
                }

                this.ShowDarkOverlay(false);
            }
            */

        }

        /*
        private void HandleRegistrationResult(Result result)
        {
            if (result.IsSuccessful)
            {
                this.RebuildModViewModels();
                this.ListAvailableMods.SelectedIndex = this.Manager.ModConfigurations.Count - 1;
            }
            else
            {
                this.ShowMessage("Warning", result.ErrorMessage);
            }
        }
        */

        /// <summary>
        /// In this context, "Save" means "Apply Mods to Game".
        /// </summary>
        private void OnSaveButtonClick(object sender, RoutedEventArgs e)
        {
            this.ShowDarkOverlay(true);
            this.ShowProgressRing(true);

            if(Game.IsRunning())
            {
                this.ShowProgressRing(false);
                this.ShowMessage("Warning", "Cannot apply mods right now because the game is currently running.");
                this.ShowDarkOverlay(false);
            }
            else
            {
                var worker = new BackgroundWorker();
                worker.DoWork += ApplyModsAsync;
                worker.RunWorkerCompleted += OnApplyModsCompleted;
                worker.RunWorkerAsync();
            }

        }

        private void ApplyModsAsync(object sender, DoWorkEventArgs e)
        {
            var start = DateTime.Now;

            this.Manager.Save();
            var result = this.Manager.ApplyMods();

            if (!result.IsSuccessful)
            {
                Dispatcher.Invoke(() =>
                {
                    this.ShowMessage("Warning", result.ErrorMessage + "  See error.log for more details.");
                });
            }
            else
            {
                // This gives user some visual feedback that something actually happened
                // if applying mods occurred too quickly to show a loading animation
                var duration = (DateTime.Now - start).TotalSeconds;
                if (duration < 0.5)
                {
                    Thread.Sleep((int)(0.5 * 1000) - (int)(duration * 1000));
                }
            }

            Dispatcher.Invoke(() =>
            {
                this.Manager.Save();
                this.RefreshModList();
                this.RefreshModInfoPanel();
                this.TakeModConfigurationSnapshot();
            });

        }

        private void OnApplyModsCompleted(object s, RunWorkerCompletedEventArgs args)
        {
            Dispatcher.Invoke(() => {
                this.ShowDarkOverlay(false);
                this.ShowProgressRing(false);
            });
        }

        private void OnLaunchGameButtonClick(object sender, RoutedEventArgs e)
        {
            if (Game.IsRunning())
            {
                this.ShowMessage("Warning", "Cannot launch the game because the game is already running.");
                this.ShowProgressRing(false);
                this.ShowDarkOverlay(false);
            }
            else
            {
                // Process.Start(this.Manager.GetPathToGameExecutable());

                if(this.HasUnappliedChanges())
                {
                    this.ShowMessage("Warning", "Apply your recent mod changes first before launching the game.");
                }
                else
                {

                    this.ShowDarkOverlay(true);
                    this.ShowProgressRing(true);

                    this.TextboxProgress.Text = "Exiting mod loader...";
                    this.TextboxProgress.Visibility = Visibility.Visible;
                    

                    // Running the game executable directly is more desirable than using the
                    // Steam protocol to start the game because using the protocol makes
                    // Steam think we are launching the same game twice and complain about
                    // not being able to sync files to the Steam Cloud properly.

                    // Game.StartGame();
                    Process.Start(this.Manager.GetPathToGameExecutable());

                    // We want to keep the mod loader up for a few seconds as the game
                    // normally does not launch instantly. This way the player knows there
                    // is something still loading...
                    var timer = new System.Timers.Timer();
                    timer.Interval = 10 * 1000;
                    timer.Elapsed += (_, __) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            Application.Current.Shutdown();
                        });
                        
                    };

                    timer.AutoReset = false;
                    timer.Enabled = true;
                }

            }
            
        }

        private void OnRightClickMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var index = this.ListAvailableMods.SelectedIndex;

            if(index >= 0 && index < this.Manager.ModConfigurations.Count)
            {
                var configuration = this.Manager.ModConfigurations[index];
                
                this.MenuItemEnableMod.IsEnabled = true;
                this.MenuItemSoftDisableMod.IsEnabled = true;
                this.MenuItemDisableMod.IsEnabled = true;

                if (configuration.State == ModState.Enabled)
                {
                    this.MenuItemEnableMod.IsEnabled = false;
                }
                if (configuration.State == ModState.SoftDisabled)
                {
                    this.MenuItemSoftDisableMod.IsEnabled = false;
                }
                if (configuration.State == ModState.Disabled)
                {
                    this.MenuItemDisableMod.IsEnabled = false;
                }
                
            }
            else
            {
                // e.Handled = true;
            }
        }

        /// <summary>
        /// In context, menu item refers to a right-click menu item.
        /// </summary>
        private void OnMenuItemEnableMod(object sender, RoutedEventArgs e)
        {
            var index = this.ListAvailableMods.SelectedIndex;

            if (index >= 0 && index < this.Manager.ModConfigurations.Count)
            {
                var configuration = this.Manager.ModConfigurations[index];
                configuration.State = ModState.Enabled;
                this.RefreshModList();
                this.RefreshModInfoPanel();
            }
        }

        private void OnMenuItemSoftDisableMod(object sender, RoutedEventArgs e)
        {
            var index = this.ListAvailableMods.SelectedIndex;

            if (index >= 0 && index < this.Manager.ModConfigurations.Count)
            {
                var configuration = this.Manager.ModConfigurations[index];
                configuration.State = ModState.SoftDisabled;
                this.RefreshModList();
                this.RefreshModInfoPanel();
            }
        }

        private void OnMenuItemDisableMod(object sender, RoutedEventArgs e)
        {
            var index = this.ListAvailableMods.SelectedIndex;

            if (index >= 0 && index < this.Manager.ModConfigurations.Count)
            {
                var configuration = this.Manager.ModConfigurations[index];
                configuration.State = ModState.Disabled;
                this.RefreshModList();
                this.RefreshModInfoPanel();
            }
        }

        private void OnSteamWorkshopDirectoryChanged(object sender, FileSystemEventArgs e)
        {
            
            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                var start = DateTime.Now;

                while (e.FullPath.IsFileLocked() == FileExtensions.FileState.Locked)
                {
                    // We need to wait until the mod.json file is finished downloading
                    // and can actually be read.
                    Thread.Sleep(SteamWorkshopCreationSleepTime);

                    if ((DateTime.Now - start).TotalSeconds > SteamWorkshopMaximumSleepTime)
                    {
                        return;
                    }

                }
            }
            else if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                // Give a chance for the mod storage folder to fully be deleted since we're
                // detecting changes on mod.json only. This way the view refreshes with the
                // deleted mod removed from the list
                Thread.Sleep(SteamWorkshopDeletionSleepTime);
            }

            var changes = this.Manager.ScanSteamWorkshopModsFolder();

            if (changes.Count > 0)
            {
                Dispatcher.Invoke(() =>
                {
                    this.RebuildModViewModels();
                    this.RefreshModList();
                    this.RefreshModInfoPanel();
                });
            }

        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.SaveModManager();
        }

        private void OnSteamWorkshopPageLinkClick(object sender, RoutedEventArgs e)
        {
            // Future release:
            //var index = this.ListAvailableMods.SelectedIndex;

            //if (index >= 0 && index < this.Manager.ModConfigurations.Count)
            //{
            //    var configuration = this.Manager.ModConfigurations[index];
            //    var mod = configuration.Mod;

            //    if (mod != null && mod.SteamWorkshopId.HasValue)
            //    {
            //        Game.OpenSteamWorkshopItem(mod.SteamWorkshopId.Value);
            //    }

            //}
        }

        private void OnVisitSteamPage(object sender, RoutedEventArgs e)
        {
            var steamId = this.SelectedMod.SteamWorkshopId;

            if(steamId != null && steamId.HasValue)
            {
                Game.OpenSteamWorkshopItem(steamId.Value);
            }
        }

        private void TakeModConfigurationSnapshot()
        {
            this.ModConfigurationSnapshots.Clear();
            for(int i = 0; i < this.Manager.ModConfigurations.Count; i++)
            {
                this.ModConfigurationSnapshots.Add(new ModConfigurationSnapshot(this.Manager.ModConfigurations[i]));
            }
        }

        private bool HasUnappliedChanges()
        {
            for (int i = 0; i < this.Manager.ModConfigurations.Count; i++)
            {
                var modConfiguration = this.Manager.ModConfigurations[i];
                var snapshot = this.ModConfigurationSnapshots[i];

                if(modConfiguration != snapshot.Mod || modConfiguration.OrderIndex != snapshot.OrderIndexSnapshot || modConfiguration.State != snapshot.ModStateSnapshot)
                {
                    return true;
                }
            }

            return false;
        }

        private void TryStartSteamWorkshopDirectoryWatcher()
        {
            try
            {
                if (!string.IsNullOrEmpty(this.Manager.GameFolderPath))
                {
                    this.SteamWorkshopDirectoryWatcher.Path = this.Manager.GetPathToSteamWorkshopMods();
                    this.SteamWorkshopDirectoryWatcher.EnableRaisingEvents = true;
                }
            }
            catch (Exception e)
            {
                e.AppendToLogFile();
            }
            finally
            {
                if (this.SteamWorkshopDirectoryWatcher != null)
                {
                    this.SteamWorkshopDirectoryWatcher.EnableRaisingEvents = false;
                }
            }
        }


        private void OnOpenSteamWorkshopButtonClick(object sender, RoutedEventArgs e)
        {
            Game.OpenSteamWorkshop();
        }

        private void OnUnhandledAction(object sender, RoutedEventArgs e)
        {

        }

        private void OnSettingsButtonClick(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            if (e.Equals(Mouse.RightButton))
            {
                e.Handled = true;
                return;
            }

            this.ContextMenuAppSettings.IsOpen = true;
        }
    }
}
