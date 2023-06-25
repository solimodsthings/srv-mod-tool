using SRVModTool;
using SRVModTool.App.WpfControls;
using Ookii.Dialogs.WinForms;
using Steamworks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Text.Json;
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
using Windows.UI.Xaml.Shapes;

namespace SRVModTool.App.Publisher
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly string SettingsFile = "settings.json";
        private static readonly string DefaultAppIdFile = "steam_appid.txt";
        private static readonly int DefaultAppId = 2091620;

        private ModContext _modContext;
        private FileSystemWatcher _modDirectoryWatcher;
        private BackgroundWorker _steamApiThread;
        private PublisherSettings _settings;

        private ulong _steamUploadItemId;
        private bool _steamSubmissionFinished;
        private bool _steamSubmissionSuccessful;
        private DateTime _lastAccessedHelpPopup;

        public MainWindow()
        {
            InitializeComponent();

            _modContext = new ModContext();
            _modDirectoryWatcher = new FileSystemWatcher();
            _modDirectoryWatcher.Created += OnFilesChanged;
            _modDirectoryWatcher.Changed += OnFilesChanged;
            _modDirectoryWatcher.Renamed += OnFilesChanged;
            _modDirectoryWatcher.Deleted += OnFilesChanged;

            _steamApiThread = new BackgroundWorker();
            _steamApiThread.DoWork += PublishSteamMod;
            _steamApiThread.RunWorkerCompleted += OnPublishSteamModComplete;
            _steamApiThread.WorkerSupportsCancellation = true;

            this.DataContext = _modContext;
            _lastAccessedHelpPopup = DateTime.Now;

            this.LoadSettings();
        }

        #region Overlay
        private void ShowOverlay(bool show)
        {
            Dispatcher.Invoke(() =>
            {
                if (show)
                {
                    this.CanvasFadeOut.Visibility = Visibility.Visible;
                }
                else
                {
                    this.CanvasFadeOut.Visibility = Visibility.Collapsed;
                }
            });
            
        }
        private void ShowProgressOverlay(bool show)
        {
            Dispatcher.Invoke(() =>
            {
                if (show)
                {
                    this.ProgressRing.Visibility = Visibility.Visible;
                }
                else
                {
                    this.ProgressRing.Visibility = Visibility.Collapsed;
                }
            });
        }
        private void ShowProgressMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                this.TextboxProgress.Text = message;

                if (string.IsNullOrEmpty(message))
                {
                    this.TextboxProgress.Visibility = Visibility.Collapsed;
                }
                else
                {
                    this.TextboxProgress.Visibility = Visibility.Visible;
                }
            });
        }
        #endregion

        private void RefreshFiles()
        {
            Dispatcher.Invoke(() =>
            {
                this.ListViewFiles.Items.Clear();

                if (_modContext.Mod == null)
                {
                    this.InfoPanel.IsEnabled = false;
                    this.FileListPanel.IsEnabled = false;
                    _modDirectoryWatcher.EnableRaisingEvents = false;
                }
                else
                {
                    this.InfoPanel.IsEnabled = true;
                    this.FileListPanel.IsEnabled = true;
                    _modDirectoryWatcher.EnableRaisingEvents = true;

                    var files = Directory.GetFiles(_modContext.Directory).Select(x => new FileView(x));

                    foreach (var file in files)
                    {
                        this.ListViewFiles.Items.Add(file);
                    }

                }
            });
        }

        public void ShowPopupMessage(string header, string body, bool useOverlay = true)
        {

            Dispatcher.Invoke(() =>
            {
                if (useOverlay)
                {
                    this.ShowOverlay(true);
                }


                var dialog = new MessageWindow(header, body);
                dialog.Owner = this;

                dialog.ShowDialog();

                if (useOverlay)
                {
                    this.ShowOverlay(false);
                }
            });
        }

        private void SaveMod()
        {
            var mod = _modContext.Mod;
            if (mod != null)
            {
                var json = JsonSerializer.Serialize(mod, new JsonSerializerOptions() { WriteIndented = true });
                var path = System.IO.Path.Combine(_modContext.Directory, Mod.InfoFile);
                File.WriteAllText(path, json);
            }
        }

        private void OnNewModButtonClick(object sender, RoutedEventArgs e)
        {
            this.ShowOverlay(true);

            var creation = new NewModWindow(_settings.DefaultFolderToOpen) { Owner = this };
            var result = creation.ShowDialog();
            var noEncounteredIssues = true;

            if(result == true)
            {

                if (!Directory.Exists(creation.ResultModParentFolder))
                {
                    this.ShowPopupMessage("Warning", "Cannot create mod. The working directory does not exist.");
                    noEncounteredIssues = false;
                }

                var modFolder = System.IO.Path.Combine(creation.ResultModParentFolder, creation.ResultMod.Name);

                if (noEncounteredIssues)
                {
                    if (Directory.Exists(modFolder))
                    {
                        this.ShowPopupMessage("Warning", "Cannot create mod. A mod of the same name already exists.");
                        noEncounteredIssues = false;
                    }
                    else
                    {
                        Directory.CreateDirectory(modFolder);
                    }
                }

                // Do not combine with the if-block above
                // There's a chance issues were encountered
                if (noEncounteredIssues) 
                {
                    this.SetCurrentMod(creation.ResultMod, modFolder);
                    this.SaveMod();

                    _settings.DefaultFolderToOpen = creation.ResultModParentFolder;
                    this.SaveSettings();
                }
                
            }

            this.ShowOverlay(false);
        }

        private void OnOpenModButtonClick(object sender, RoutedEventArgs e)
        {
            if(_modContext.Mod != null)
            {
                // TODO Prompt user if they actually want to save before opening another mod package folder
                this.SaveMod();
            }

            var folder = new VistaFolderBrowserDialog();

            folder.RootFolder = Environment.SpecialFolder.Desktop;
            folder.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (_settings != null && _settings.DefaultFolderToOpen != null)
            {
                folder.SelectedPath = _settings.DefaultFolderToOpen + "\\";
            }

            folder.ShowNewFolderButton = true;

            var result = folder.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {

                var path = folder.SelectedPath;
                var modinfo = System.IO.Path.Combine(path, Mod.InfoFile);

                if (!Directory.Exists(path))
                {
                    this.ShowPopupMessage("Warning", "The selected folder does not exist. Could not open the mod package.");
                    return;
                }

                if(!File.Exists(modinfo))
                {
                    this.ShowPopupMessage("Warning", "The selected folder does not contain a mod.json file. Could not open the mod package.");
                    return;
                }

                this.ShowOverlay(true);
                this.ShowProgressOverlay(true);

                var json = File.ReadAllText(modinfo);
                var mod = JsonSerializer.Deserialize<Mod>(json);
                
                if(mod == null)
                {
                    this.ShowPopupMessage("Warning", "Could not deserialize the mod.json file in the selected folder. Could not open mod package.");
                }
                else
                {
                    this.SetCurrentMod(mod, path);

                    _settings.DefaultFolderToOpen = new FileInfo(modinfo).Directory.Parent.FullName;
                    this.SaveSettings();
                }

                this.ShowProgressOverlay(false);
                this.ShowOverlay(false);  

            }

        }

        private void SaveSettings()
        {
            var json = JsonSerializer.Serialize(_settings);
            File.WriteAllText(SettingsFile, json);
        }

        private void LoadSettings()
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var settings = JsonSerializer.Deserialize<PublisherSettings>(json);

                if(settings != null)
                {
                    _settings = settings;
                } 
            }
            else
            {
                _settings = new PublisherSettings();
            }
        }

        private void SetCurrentMod(Mod mod, string directory)
        {
            _modContext.Mod = mod;
            _modContext.Directory = directory;
            _modDirectoryWatcher.Path = directory;
            this.ComboBoxModType.SelectedIndex = 0;

            if (_modContext.IsCampaign)
            {
                this.ComboBoxModType.SelectedIndex = 1;
            }
            
            this.RefreshFiles();
        }

        private void OnSaveButtonClick(object sender, RoutedEventArgs e)
        {
            this.ShowOverlay(true);
            this.SaveMod();
            this.ShowOverlay(false);
        }

        private void OnFilesChanged(object sender, FileSystemEventArgs e)
        {
            this.RefreshFiles();
        }

        private void OnOpenDirectoryButtonClick(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = "explorer.exe",
                Arguments = _modContext.Directory
            });
        }

        private void OnPublishButtonClick(object sender, RoutedEventArgs e)
        {
            
            if (_modContext.Mod == null)
            {
                this.ShowPopupMessage("Warning", "There is no active mod to publish. Please open a mod first.");
                return;
            }

            var mod = _modContext.Mod;

            if(_modContext.IsCampaign)
            {
                if (string.IsNullOrEmpty(_modContext.CampaignName) 
                    || string.IsNullOrEmpty(_modContext.CampaignDescription)
                    || string.IsNullOrEmpty(_modContext.CampaignBaseLevel)
                    || string.IsNullOrEmpty(_modContext.CampaignGameType)
                    || string.IsNullOrEmpty(_modContext.CampaignPrefix))
                {
                    this.ShowPopupMessage("Warning", "Cannot publish a campaign with any of name, description, base level, game type, or prefix missing.");
                    return;
                }

                _modContext.Description = _modContext.CampaignDescription;
            }
            else
            {
                // The constraint on Description is a requirement for Steam only, but we might
                // as well enforce it for standalone mods too
                if (string.IsNullOrEmpty(_modContext.Name) || string.IsNullOrEmpty(_modContext.Description))
                {
                    this.ShowPopupMessage("Warning", "Cannot publish a mod with a missing name or description.");
                    return;
                }
            }

            if (string.IsNullOrEmpty(_modContext.Author) || string.IsNullOrEmpty(_modContext.Description))
            {
                this.ShowPopupMessage("Warning", "You must provide an author's name for this mod before publishing.");
                return;
            }

            var files = Directory.GetFiles(_modContext.Directory);
            var thumbnail = files.FirstOrDefault(x => new FileInfo(x.ToLower()).Name.Equals("thumbnail.jpg"));

            if (thumbnail == null)
            {
                var confirmContinue = new MessageWindow("No Thumbnail", "Your mod has no workshop item thumbnail. You can add one by including a file called 'thumbnail.jpg'. Do you still want to publish your mod?", true);
                confirmContinue.Owner = this;
                confirmContinue.SetCancelButtonText("No, cancel");

                if (confirmContinue.ShowDialog() != true)
                {
                    return;
                }
            }


            this.ShowOverlay(true);

            if (string.IsNullOrEmpty(mod.Id))
            {
                _modContext.ModId = string.Format(
                    "{0}-{1}",
                    mod.Author.Replace(" ", string.Empty),
                    mod.Name.Replace(" ", string.Empty)
                );

                this.SaveMod();
            }

            this.ShowProgressOverlay(true);

            // It's a requirement for the Steam API that this
            // file exists in the application directory
            if (!File.Exists(DefaultAppIdFile))
            {
                File.WriteAllText(DefaultAppIdFile, DefaultAppId.ToString());
            }

            while (_steamApiThread.IsBusy)
            {
                if (!_steamApiThread.CancellationPending)
                {
                    _steamApiThread.CancelAsync();
                }
                Thread.Sleep(1000);
            }

            _steamApiThread.RunWorkerAsync();

        }

        private void PublishSteamMod(object sender, DoWorkEventArgs e)
        {

            try
            {
                var mod = _modContext.Mod;
                var isExistingSteamWorkshopItem = mod.SteamWorkshopId.HasValue;

                _steamUploadItemId = 0;
                _steamSubmissionFinished = false;
                _steamSubmissionSuccessful = false;

                if (isExistingSteamWorkshopItem)
                {
                    _steamUploadItemId = mod.SteamWorkshopId.Value;
                }

                this.ShowProgressMessage("Accessing Steam");
                
                if (SteamAPI.Init())
                {
                    var appId = new AppId_t((uint)DefaultAppId);

                    if (!isExistingSteamWorkshopItem)
                    {

                        this.ShowProgressMessage("Creating new Steam Workshop item");

                        var createItemCall = SteamUGC.CreateItem(appId, EWorkshopFileType.k_EWorkshopFileTypeCommunity);
                        var createResult = CallResult<CreateItemResult_t>.Create(OnSteamWorkshopItemCreation);
                        createResult.Set(createItemCall);

                        while (_steamUploadItemId == 0 && !_steamApiThread.CancellationPending)
                        {
                            SteamAPI.RunCallbacks();
                            Thread.Sleep(1000);
                        }

                        _modContext.Mod.SteamWorkshopId = _steamUploadItemId;
                        _modContext.SteamId = _steamUploadItemId.ToString();
                        this.SaveMod();
                    }

                    this.ShowProgressMessage("Updating Steam Workshop item");

                    var updateHandle = SteamUGC.StartItemUpdate(appId, new PublishedFileId_t(_steamUploadItemId));

                    if(!isExistingSteamWorkshopItem)
                    {
                        SteamUGC.SetItemTitle(updateHandle, mod.Name);
                        SteamUGC.SetItemDescription(updateHandle, mod.Description);
                        SteamUGC.SetItemVisibility(updateHandle, ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic);
                    }

                    var thumbnail = System.IO.Path.Combine(_modContext.Directory, "thumbnail.jpg");

                    if (File.Exists(thumbnail)){
                        SteamUGC.SetItemPreview(updateHandle, thumbnail);
                    }
                    
                    SteamUGC.SetItemContent(updateHandle, _modContext.Directory);

                    var submitItemCall = SteamUGC.SubmitItemUpdate(updateHandle, "Updated on " + DateTime.Now.ToString());
                    var submitResult = CallResult<SubmitItemUpdateResult_t>.Create(OnSteamWorkshopItemSubmission);
                    submitResult.Set(submitItemCall);

                    this.ShowProgressMessage("Uploading mod files");

                    while (!_steamSubmissionFinished && !_steamApiThread.CancellationPending)
                    {
                        SteamAPI.RunCallbacks();

                        // ulong bytesDone;
                        // ulong bytesTotal;
                        // EItemUpdateStatus status = SteamUGC.GetItemUpdateProgress(updateHandle, out bytesDone, out bytesTotal);

                        Thread.Sleep(1000);
                    }

                    SteamAPI.Shutdown();
                    this.SaveMod();

                    Dispatcher.Invoke(() =>
                    {
                        this.ShowProgressMessage(string.Empty);
                        this.ShowProgressOverlay(false);

                        if(_steamSubmissionSuccessful)
                        {
                            if (isExistingSteamWorkshopItem)
                            {
                                this.ShowPopupMessage("Success", "Successfully updated the Steam Workshop item for this mod!"
                                    + " Visit your workshop item in Steam to edit your change notes.", false);
                            }
                            else
                            {
                                this.ShowPopupMessage("Success", "Successfully uploaded your mod as a new Steam Workshop item!"
                                    + " Visit your new workshop item in Steam to add screenshots.", false);
                            }

                            var ConfirmOpenSteam = new MessageWindow("Steam Workshop", "Would you like to visit your mod's Steam Workshop page?", true);
                            ConfirmOpenSteam.Owner = this;
                            ConfirmOpenSteam.SetCancelButtonText("Not Right Now");

                            if(ConfirmOpenSteam.ShowDialog() == true)
                            {
                                Game.OpenSteamWorkshopItem(mod.SteamWorkshopId.Value);
                            }
                        }
                    });
                    
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        this.ShowProgressOverlay(false);
                        this.ShowPopupMessage("Warning", "Steam must be running in order to publish mods to the Steam Workshop.");
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    this.ShowProgressOverlay(false);
                    this.ShowPopupMessage("Error", ex.Message, false);
                });

                SteamAPI.Shutdown();
            }
        }


        private void OnPublishSteamModComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                this.ShowProgressMessage(string.Empty);
                this.ShowProgressOverlay(false);
                this.ShowOverlay(false);
            });
        }

        private void OnSteamWorkshopItemCreation(CreateItemResult_t result, bool failure)
        {
            if (result.m_eResult == EResult.k_EResultOK)
            {
                _steamUploadItemId = ulong.Parse(result.m_nPublishedFileId.ToString());
            }
            else
            {
                this.ShowPopupMessage("Error", "Failed to create a new Steam Workshop item for your mod.", false);
                this.OnPublishSteamModComplete(null, null);
            }
        }

        private void OnSteamWorkshopItemSubmission(SubmitItemUpdateResult_t result, bool failure)
        {

            _steamSubmissionFinished = true;

            if (result.m_eResult == EResult.k_EResultOK)
            {
                _steamSubmissionSuccessful = true;
            }
            else
            {
                this.ShowPopupMessage("Error", 
                    string.Format("Failed to upload mod files to the Steam Workshop. There either does not exist a workshop item with ID {0} or you don't have permissions to update it.", _modContext.SteamId), false);
                this.OnPublishSteamModComplete(null, null);
            }
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            SteamAPI.Shutdown();

            while(_steamApiThread.IsBusy)
            {

                if (!_steamApiThread.CancellationPending)
                {
                    _steamApiThread.CancelAsync();
                }

                Thread.Sleep(1000);
            }
        }

        private void ComboBoxModType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selection = ((ComboBoxItem)this.ComboBoxModType.SelectedItem).Content.ToString();

            _modContext.IsCampaign = false;

            if (selection == "New Campaign")
            {
                _modContext.IsCampaign = true;
            }

            if (selection != "New Campaign")
            {
                _modContext.CampaignName = null;
                _modContext.CampaignPrefix = null;
                _modContext.CampaignDescription = null;
                _modContext.CampaignBaseLevel = null;
            }

        }

    }
}
