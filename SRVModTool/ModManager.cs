
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SRVModTool
{

    public class ModManager
    {

        private static readonly string ConfigurationFile = "config.json";           // relative to app folder
        private static readonly string FileChangesLogFile = "filechanges.log";      // relative to app folder
        private static readonly string StandaloneModsFolder = @"RPGTacGame\Mods";   // relative to GameFolderPath
        private static readonly string RelativePathToStandaloneModsFolder = @"..\..\RPGTacGame\Mods"; // relative to the game executable and used in .ini key-value pairs

        public List<ModConfiguration> ModConfigurations { get; set; }

        public string GameFolderPath { get; set; }

        [JsonIgnore]
        private GameFilesChangeLog FileChangesLog { get; set; }

        public ModManager()
        {
            this.ModConfigurations = new List<ModConfiguration>();
            this.FileChangesLog = new GameFilesChangeLog(FileChangesLogFile);
        }

        /// <summary>
        /// Deserializes this ModManager from disk. Also checks the
        /// mods folder and subscribed steam mods folder for new mods or
        /// mods that have been removed.
        /// </summary>
        public Result Load()
        {
            var result = new Result();

            if (File.Exists(ConfigurationFile))
            {
                var json = File.ReadAllText(ConfigurationFile);

                try
                {
                    bool missingStandaloneModFolders = false;
                    var m = JsonSerializer.Deserialize<ModManager>(json);

                    this.ModConfigurations = m.ModConfigurations;
                    this.GameFolderPath = m.GameFolderPath;

                    if (string.IsNullOrEmpty(m.GameFolderPath))
                    {
                        throw new ModException("Cannot load any mods. File 'config.json' was loaded with no value for game folder path. ");
                    }

                    // Instantiate mod configurations from the mod manager's json file.
                    foreach (var configuration in this.ModConfigurations)
                    {
                        if (!Directory.Exists(configuration.ModStorageFolder))
                        {
                            if (configuration.RegistrationType == RegistrationType.Standalone)
                            {
                                // A missing standalone mod will be unregistered through an upcoming call to ScanForMods()
                                missingStandaloneModFolders = true;

                                var assumedModName = new DirectoryInfo(configuration.ModStorageFolder).Name;
                                var shortMessage = string.Format("Standalone mod '{0}' cannot be loaded because its storage folder longer exists.", assumedModName);
                                var longMessage = string.Format("{0} It will be unregistered from this application. It's storage location was originally {1}.", shortMessage, configuration.ModStorageFolder);

                                result.ErrorMessage += " " + shortMessage;
                                ErrorLogExtensions.AppendToLogFile(longMessage);

                            }
                            else if (configuration.RegistrationType == RegistrationType.SteamWorkshopItem)
                            {
                                // Pass through. It's possible the Steam mod folder doesn't exist anymore because the
                                // player simply unsubscribed from the mod. In that case, we don't raise any errors.
                                // The mod will be unregistered through the upcoming call to ScanForMods()
                            }
                        }
                        else
                        {
                            var modinfo = Path.Combine(configuration.ModStorageFolder, Mod.InfoFile);

                            if (!File.Exists(modinfo))
                            {
                                throw new ModException(string.Format("Registered mod '{0}' is missing its mod.json file. Expected the file to exist at {1}.", configuration.Mod?.Name, modinfo));
                            }

                            var contents = File.ReadAllText(modinfo);
                            configuration.Mod = JsonSerializer.Deserialize<Mod>(contents);
                            configuration.IsManaged = true;
                        }

                    }

                    result.ErrorMessage = result.ErrorMessage?.Trim();
                    result.IsSuccessful = !missingStandaloneModFolders;

                    this.ScanStandaloneModsFolder();
                    this.ScanSteamWorkshopModsFolder();
                }
                catch (ModException e)
                {
                    result.ErrorMessage = e.Message;
                    e.AppendToLogFile();
                }
                catch (Exception e)
                {
                    result.ErrorMessage = "Failed to load mod manager configuration file. See error.log.";
                    e.AppendToLogFile();
                }

            }
            else
            {
                // No action. It is a valid scenario for this file to not exist yet.
                result.IsSuccessful = true;
            }

            return result;

        }

        /// <summary>
        /// Serializes this ModManager and current ModConfigurations to disk.
        /// </summary>
        public void Save()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigurationFile, json);
        }

        /// <summary>
        /// Sets the root directory of the game and initializes the game folder
        /// path. This method will fail if the specified path doesn't actually contain the game.
        /// </summary>
        public Result RegisterGameFolderPath(string path)
        {
            var result = new Result();

            if(Game.IsInsideFolder(path))
            {
                this.GameFolderPath = path;
                this.InitializeGameModsFolder();
                this.ScanStandaloneModsFolder();
                this.ScanSteamWorkshopModsFolder();
                result.IsSuccessful = true;
            }
            else
            {
                result.ErrorMessage = "The specified folder does not contain the game.";
            }

            return result;
        }

        /// <summary>
        /// Creates a subdirectory in the game directory that will contain all mod files
        /// if it doesn't yet already exist. The method should only be called once <see cref="GameFolderPath">
        /// GameFolderPath</see> is set.
        /// </summary>
        private void InitializeGameModsFolder()
        {

            if(string.IsNullOrEmpty(this.GameFolderPath) || !Directory.Exists(this.GameFolderPath))
            {
                throw new InvalidOperationException("Cannot initialize game mods folder because the path to the game folder has not been defined.");
            }

            var mods = Path.Combine(this.GameFolderPath, StandaloneModsFolder);

            if (!Directory.Exists(mods))
            {
                Directory.CreateDirectory(mods);
            }

        }

        /// <summary>
        /// Checks the standalone mods folder to see if any new mods
        /// were added directly to the folder (and registers them) or
        /// if any previously registered mods are missing (this method also
        /// unregisters them).
        /// </summary>
        /// <returns>
        /// A list of automatic mod registration events describing what has been
        /// added or removed.
        /// </returns>
        /// TODO: This method could be made public if file change detection is implemented
        /// for the standalone mods folder like it is for the Steam mods folder.
        private List<AutomaticModRegistrationEvent> ScanStandaloneModsFolder()
        {
            var changes = new List<AutomaticModRegistrationEvent>();
            var modsFolder = Path.Combine(this.GameFolderPath, StandaloneModsFolder);

            if (Directory.Exists(modsFolder))
            {
                var subfolders = Directory.GetDirectories(modsFolder);

                // Look for new mods
                foreach (var unmanagedSubfolder in subfolders)
                {
                    var modinfo = Path.Combine(unmanagedSubfolder, Mod.InfoFile);

                    if (File.Exists(modinfo))
                    {
                        var contents = File.ReadAllText(modinfo);
                        var mod = JsonSerializer.Deserialize<Mod>(contents);

                        if (!this.IsRegistered(mod))
                        {
                            var newConfiguration = this.RegisterStandaloneMod(mod, unmanagedSubfolder);
                            newConfiguration.State = this.AssessFileState(newConfiguration);
                            newConfiguration.IsManaged = false;

                            changes.Add(new AutomaticModRegistrationEvent(RegistrationAction.Registered, newConfiguration));
                        }
                    }
                }

                // Look for mods were previously registered, but don't exist anymore
                var managedStandaloneMods = this.ModConfigurations.Where(x => x.RegistrationType == RegistrationType.Standalone).ToArray();

                foreach (var managedStandaloneMod in managedStandaloneMods)
                {
                    if (string.IsNullOrEmpty(managedStandaloneMod.ModStorageFolder) || !Directory.Exists(managedStandaloneMod.ModStorageFolder))
                    {
                        var unregistered = this.UnregisterMod(managedStandaloneMod);

                        if (unregistered.IsSuccessful)
                        {
                            changes.Add(new AutomaticModRegistrationEvent(RegistrationAction.Unregistered, managedStandaloneMod));
                        }
                    }
                }
            } 
            else
            {
                // It is a valid scenario for the mods folder to not have been created yet.
            }

            return changes;
        }

        /// <summary>
        /// Checks the Steam workshop mods folder to see if any new mods
        /// have been subscribed to (these will be automatically registered)
        /// and if any previously registered Steam mods have been
        /// unsubscribed (these will be automatically unregistered).
        /// </summary>
        /// <returns>
        /// A list of automatic mod registration events describing what has been
        /// added or removed.
        /// </returns>
        public List<AutomaticModRegistrationEvent> ScanSteamWorkshopModsFolder()
        {
            var changes = new List<AutomaticModRegistrationEvent>();
            var steamModsFolder = Path.Combine(this.GameFolderPath, Game.RelativePathToSteamModsFolder);

            // Note: It is a valid scenario for the mods folder to have been created yet.
            if (Directory.Exists(steamModsFolder))
            {
                var subfolders = Directory.GetDirectories(steamModsFolder);

                foreach (var subfolder in subfolders)
                {
                    var modinfo = Path.Combine(subfolder, Mod.InfoFile);

                    if (File.Exists(modinfo))
                    {
                        var contents = File.ReadAllText(modinfo);
                        var mod = JsonSerializer.Deserialize<Mod>(contents);

                        if (!this.IsRegistered(mod))
                        {
                            var newConfiguration = this.RegisterSteamMod(mod, subfolder);
                            newConfiguration.State = this.AssessFileState(newConfiguration);
                            newConfiguration.IsManaged = false;

                            changes.Add(new AutomaticModRegistrationEvent(RegistrationAction.Registered, newConfiguration));
                        }
                    }
                }
            }

            /// Handle mods being unsubscribed to as well
            var managedSteamMods = this.ModConfigurations.Where(x => x.RegistrationType == RegistrationType.SteamWorkshopItem).ToArray();

            foreach (var managedSteamMod in managedSteamMods)
            {
                if (string.IsNullOrEmpty(managedSteamMod.ModStorageFolder) || !Directory.Exists(managedSteamMod.ModStorageFolder))
                {
                    var unregistered = this.UnregisterMod(managedSteamMod);

                    if(unregistered.IsSuccessful)
                    {
                        changes.Add(new AutomaticModRegistrationEvent(RegistrationAction.Unregistered, managedSteamMod));
                    }
                    
                }
            }

            return changes;
        }

        /// <summary>
        /// Returns a ModState value representing the specified mod's current enabled/disabled state
        /// based on if they have paths defined in RPGTacEngine.ini and RPGTacMods.ini.
        /// </summary>
        private ModState AssessFileState(ModConfiguration configuration)
        {
            var mod = configuration.Mod;

            // See if this mod has paths defined in RPGTacEngine.ini...
            var gameEngineConfigPath = Path.Combine(this.GameFolderPath, Game.RelativePathToConfigurationsFolder, Game.EngineConfigurationFile);
            var gameEngineConfig = new GameConfiguration(gameEngineConfigPath);
            gameEngineConfig.Load();

            var relativePath = this.GetRelativeModStoragePath(configuration);
            var hasContentPathDefined = gameEngineConfig.IsIncluded(Game.EnginePathSection, Game.EngineContentPathKey, relativePath);
            var hasLocalizationPathDefined= gameEngineConfig.IsIncluded(Game.EnginePathSection, Game.EngineLocalizationPathKey, relativePath);
            var hasScriptPathDefined = gameEngineConfig.IsIncluded(Game.EnginePathSection, Game.EngineScriptPathKey, relativePath);

            var hasAllPathsDefined = hasContentPathDefined && hasLocalizationPathDefined && hasScriptPathDefined;

            // See if this mod has a mutator defined in RPGTacMods.ini...
            var mutatorConfigPath = Path.Combine(this.GameFolderPath, Game.RelativePathToConfigurationsFolder, Game.MutatorConfigurationFile);
            var mutatorConfig = new GameConfiguration(mutatorConfigPath);
            mutatorConfig.Load();

            var hasMutator = configuration.Mod.HasMutator;
            var hasMutatorEnabled = false;

            if(configuration.Mod.HasMutator)
            {
                var mutators = mutatorConfig.FindItem(Game.MutatorLoaderSection, Game.MutatorLoaderKey)?.Value?.Split(',');
                if(mutators != null && mutators.Contains(configuration.Mod.MutatorClass))
                {
                    hasMutatorEnabled = true;
                }
            }

            // Determine the mod's state...
            if(hasAllPathsDefined && (!hasMutator || hasMutatorEnabled))
            {
                return ModState.Enabled;
            }
            else if(hasAllPathsDefined && (hasMutator && !hasMutatorEnabled))
            {
                return ModState.SoftDisabled;
            }
            else if(!hasAllPathsDefined && (!hasMutator || !hasMutatorEnabled))
            {
                return ModState.Disabled;
            }
            else
            {
                return ModState.Undetermined;
            }

        }

        /// <summary>
        /// Registers the standalone mod package file at the specified file path
        /// with this mod manager.
        /// </summary>
        public Result RegisterMod(string filepath)
        {
            var result = new Result() { IsSuccessful = false };

            if (!File.Exists(filepath))
            {
                result.ErrorMessage = "Mod package file could not be loaded because it cannot be accessed or does not exist.";
            }
            else
            {
                var temporaryDestination = Path.Combine(this.GameFolderPath, StandaloneModsFolder, Path.GetRandomFileName());
                ModConfiguration configuration = null;

                try
                {
                    Directory.CreateDirectory(temporaryDestination);
                    ZipFile.ExtractToDirectory(filepath, temporaryDestination);

                    var modinfo = Path.Combine(temporaryDestination, Mod.InfoFile);

                    if (!File.Exists(modinfo))
                    {
                        throw new ModRegistrationException("Could not load mod. Could not find and extract mod.json within the mod package file.");
                    }

                    var contents = File.ReadAllText(modinfo);
                    var mod = JsonSerializer.Deserialize<Mod>(contents);
                    configuration = this.RegisterStandaloneMod(mod, temporaryDestination);
                    configuration.IsManaged = true;
                    result.IsSuccessful = true;

                }
                catch (ModRegistrationException e)
                {
                    // If we land in here, we know exactly what the problem was. The
                    // error message in the result object can be surfaced to the user
                    // and it should make sense.
                    e.AppendToLogFile();
                    result.ErrorMessage = e.Message;
                }
                catch (Exception e)
                {
                    // If we land in here, we weren't expecting the problem. The error
                    // message might be too cryptic and so the message surfaced to the
                    // user is genericized.
                    e.AppendToLogFile();
                    result.ErrorMessage = string.Format("Could not load mod at '{0}'. See error.log file.", filepath);
                }

                // The rest of this method is just folder clean-up
                if(!result.IsSuccessful)
                {
                    if (configuration != null && this.ModConfigurations.Contains(configuration))
                    {
                        this.ModConfigurations.Remove(configuration);
                    }

                    if (configuration != null && Directory.Exists(configuration.ModStorageFolder))
                    {
                        Directory.Delete(configuration.ModStorageFolder, true);
                    }
                }

                if(Directory.Exists(temporaryDestination))
                {
                    Directory.Delete(temporaryDestination, true);
                }

            }

            return result;

        }

        /// <summary>
        /// Note: this method will throw ModRegistrationExceptions.
        /// Note: the specified path for unmanagedStorageFolder will get moved.
        /// </summary>
        private ModConfiguration RegisterStandaloneMod(Mod newMod, string unmanagedStandaloneStorageFolder = null)
        {
            if (string.IsNullOrEmpty(newMod.Id))
            {
                throw new ModRegistrationException(newMod, "Cannot register a mod that has no defined ID.");
            }

            if (this.IsRegistered(newMod))
            {
                throw new ModRegistrationException(newMod, string.Format("Could not register mod because '{0}' version {1} already exists.", newMod.Name, newMod.Version));
            }

            if (unmanagedStandaloneStorageFolder != null && !Directory.Exists(unmanagedStandaloneStorageFolder))
            {
                throw new ModRegistrationException(newMod, string.Format("The specified unmanaged storage folder doesn't exist for mod with ID '{0}'", newMod.Id));
            }

            var newModStorageFolder = Path.Combine(this.GameFolderPath, StandaloneModsFolder, newMod.Id);

            if (unmanagedStandaloneStorageFolder != newModStorageFolder)
            {
                if (Directory.Exists(newModStorageFolder))
                {
                    throw new ModRegistrationException(newMod, string.Format("Could not register mod because a managed storage folder already exists for mod with ID '{0}'", newMod.Id));
                }

                if (unmanagedStandaloneStorageFolder != null)
                {
                    Directory.Move(unmanagedStandaloneStorageFolder, newModStorageFolder);
                }
            }
            
            var configuration = new ModConfiguration()
            {
                Mod = newMod,
                ModStorageFolder = newModStorageFolder,
                RegistrationType = RegistrationType.Standalone,
                State = ModState.Disabled
            };

            // order matters for next two statements
            this.ModConfigurations.Add(configuration);
            configuration.OrderIndex = this.ModConfigurations.Count - 1;

            return configuration;

        }

        /// <summary>
        /// Registers a Steam Workshop mod with this mod manager.
        /// </summary>
        private ModConfiguration RegisterSteamMod(Mod steamMod, string modLocation)
        {
            if (string.IsNullOrEmpty(steamMod.Id))
            {
                throw new ModRegistrationException(steamMod, "Cannot register a steam mod that has no defined ID.");
            }

            /* // Commented out to permit two mods of the same ID to exist if one is standalone and the other is a steam workshop item
            if (this.IsRegistered(newMod))
            {
                throw new ModRegistrationException(newMod, string.Format("Could not register mod because '{0}' version {1} already exists.", newMod.Name, newMod.Version));
            }
            */

            var configuration = new ModConfiguration()
            {
                Mod = steamMod,
                ModStorageFolder = modLocation,
                RegistrationType = RegistrationType.SteamWorkshopItem,
                State = ModState.Disabled
            };

            // order matters for next two statements
            this.ModConfigurations.Add(configuration);
            configuration.OrderIndex = this.ModConfigurations.Count - 1;

            return configuration;
        }


        /// <summary>
        /// Note: This is used to prevent a new standalone mod from being registered
        /// when an existing standalone or steam mod exists with the same ID. An exception is
        /// for steam mods which are not prevented from being registered when the mod ID collides.
        /// This is so mod authors can still manage development versions of their mod side by side
        /// with steam versions. Also, steam mods can get their uniqueness from steam workshop IDs
        /// rather than mod IDs which is a property generated by the mod publishing tool.
        /// </summary>
        private bool IsRegistered(Mod newMod)
        {
            if (newMod != null)
            {
                foreach (var mod in ModConfigurations.Select(x => x.Mod))
                {
                    if (mod != null)
                    {
                        if (string.Equals(mod.Id, newMod.Id, StringComparison.OrdinalIgnoreCase)
                            || (string.Equals(mod.Name, newMod.Name, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(mod.Version, newMod.Version, StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Unregisters the specified mod configuration from this mod manager.
        /// This method will automatically uninstall the mod if it is currently
        /// in an enabled state.
        /// </summary>
        public Result UnregisterMod(ModConfiguration configuration)
        {
            var result = new Result();

            try
            {
                var gameEngineConfigPath = Path.Combine(this.GameFolderPath, Game.RelativePathToConfigurationsFolder, Game.EngineConfigurationFile);
                var gameEngineConfig = new GameConfiguration(gameEngineConfigPath);

                gameEngineConfig.Load();
                this.DisableModStoragePaths(gameEngineConfig, configuration);
                gameEngineConfig.Save();

                // Note: In order to delete steam mod files properly the player must unsubscribe
                // from the associated steam workshop item.
                if(configuration.RegistrationType != RegistrationType.SteamWorkshopItem)
                {
                    if (Directory.Exists(configuration.ModStorageFolder))
                    {
                        Directory.Delete(configuration.ModStorageFolder, true);
                    }
                }

                this.ModConfigurations.Remove(configuration);
                this.UpdateModOrderValue();
                result.IsSuccessful = true;
            }
            catch (Exception e)
            {
                e.AppendToLogFile();
                result.ErrorMessage = "Mod was not uninstalled correctly. See error.log.";
            }


            return result;
        }

        /// <summary>
        /// Updates the game folder to reflect each mod's state (ie. enabled, soft disabled, or disabled). 
        /// The method does not modify existing base game files except for RPGTacMods.ini and RPGTacEngine.ini.
        /// </summary>
        public Result ApplyMods()
        {

            var result = new Result();
            this.FileChangesLog.Start();
            this.FileChangesLog.Record("Beginning file changes...");

            try
            {
                
                this.UpdateMutatorIniFile();
                // this.UpdateCampaignLocalizationFile(); TODO Re-visit
                var update = this.UpdateGameEngineIniFile();

                if(!update.IsSuccessful)
                {
                    result.ErrorMessage += update.ErrorMessage;
                    this.FileChangesLog.Record(result.ErrorMessage);
                }
                else
                {
                    result.IsSuccessful = true;
                }
                
            }
            catch(Exception e)
            {
                e.AppendToLogFile();
                result.ErrorMessage = "Encountered an error while trying to update mod/game files.";
                this.FileChangesLog.Record(result.ErrorMessage);
            }

            this.FileChangesLog.Record("File changes finished.");
            this.FileChangesLog.End();

            return result;
        }

        /// <summary>
        /// Note that for the game, the campaign list is defined in a localization (!) file.
        /// Thankfully, the localization file has the same format as a .ini file.
        /// </summary>
        private void UpdateCampaignLocalizationFile() // TODO Test - it was not working last time
        {
            var campaignConfigPath = Path.Combine(this.GameFolderPath, Game.RelativePathToEnglishLocalizationFolder, Game.CampaignLocalizationFile);
            var campaignConfigPathBackup = campaignConfigPath + ".backup";

            if (!File.Exists(campaignConfigPathBackup))
            {
                this.FileChangesLog.Record("Creating backup of campaign localization file.");
                File.Copy(campaignConfigPath, campaignConfigPathBackup);
            }

            var campaignConfig = new CampaignConfiguration(campaignConfigPath);
            campaignConfig.Load();

            var mainCampaign = campaignConfig.Campaigns.FirstOrDefault(x => x.Prefix == "Main");
            var srvCampaign = campaignConfig.Campaigns.FirstOrDefault(x => x.Prefix == "SRV");

            if(mainCampaign == null || srvCampaign == null)
            {
                this.FileChangesLog.Record("The campaign list has been modified and does not contain the main or SRV campaign.");
            }

            campaignConfig.Campaigns.Clear();

            if (mainCampaign != null)
            {
                campaignConfig.Campaigns.Add(mainCampaign);
            }
            
            if(srvCampaign != null)
            {
                campaignConfig.Campaigns.Add(srvCampaign);
            }

            foreach (var config in this.ModConfigurations)
            {
                if(config.Mod.IsCampaign && config.State == ModState.Enabled)
                {
                    var c = new Campaign()
                    {
                        Name = config.Mod.CampaignName,
                        Description = config.Mod.CampaignDescription,
                        BaseLevel = config.Mod.CampaignBaseLevel,
                        Prefix = config.Mod.CampaignPrefix,
                        GameType = config.Mod.CampaignGameType
                    };

                    campaignConfig.Campaigns.Add(c);

                }
            }

            campaignConfig.Save();
            this.FileChangesLog.Record("Updated campaign localization file to reflect enabled custom campaigns");
        }

        /// <summary>
        /// Updates the list of mutators in RPGTacMods.ini to reflect which mods are enabled,
        /// soft-disabled, or disabled. Mods use mutator classes as an entry point to their
        /// scripts. Thus, a mod with a mutator listed in RPGTacMods.ini has scripts running
        /// during the game.
        /// </summary>
        private void UpdateMutatorIniFile()
        {
            var mutatorsConfigPath = Path.Combine(this.GameFolderPath, Game.RelativePathToConfigurationsFolder, Game.MutatorConfigurationFile);
            var mutatorsConfig = new GameConfiguration(mutatorsConfigPath);
            mutatorsConfig.Load();

            // This list of all available mutator classes will be used to preserve unmanaged
            // mods the player may have added manually
            var allAvailableMutators = this.ModConfigurations
                .Select(x => x.Mod)
                .Where(x => x.HasMutator && !string.IsNullOrEmpty(x.MutatorClass))
                .Select(x => x.MutatorClass);

            // First we find out what exactly is enabled now
            var enabledMutatorClasses = new List<string>();

            foreach (var config in this.ModConfigurations)
            {
                if (config.State == ModState.Enabled) // SoftDisabled mods will not have their mutator class included in RPGTacMods.ini
                {
                    if (config.Mod.HasMutator && !string.IsNullOrEmpty(config.Mod.MutatorClass))
                    {
                        enabledMutatorClasses.Add(config.Mod.MutatorClass);
                        this.FileChangesLog.Record("Mod {0} is enabled and has a mutator, and needs to be added to the mutator loader list...", config.Mod.Id);
                    }
                }
            }

            // Begin manipulating the .ini file. This is built to work even if new config sections or
            // items are added to the .ini file.
            var newMutatorList = string.Join(",", enabledMutatorClasses);

            var configSection = mutatorsConfig.FindSection(Game.MutatorLoaderSection);

            if (configSection == null)
            {
                mutatorsConfig.Sections.Add(new GameConfigurationSection() { Name = Game.MutatorLoaderSection });
            }

            var configItem = mutatorsConfig.FindItem(Game.MutatorLoaderSection, Game.MutatorLoaderKey);

            if (configItem == null)
            {
                configSection.Items.Add(new GameConfigurationItem() { Key = Game.MutatorLoaderKey, Value = newMutatorList });
            }
            else
            {

                // Attempt to preserve any mutator classes that were manually added
                // by player to .ini file if they are not already managed by this mod loader
                var unmanagedMutatorClasses = new List<string>();

                if (allAvailableMutators != null)
                {
                    var existingMutatorList = configItem.Value;
                    var existingMutatorClasses = existingMutatorList.Split(',').ToList();

                    foreach (var existingClass in existingMutatorClasses)
                    {
                        if (!string.IsNullOrEmpty(existingClass) && !allAvailableMutators.Contains(existingClass))
                        {
                            unmanagedMutatorClasses.Add(existingClass);
                            this.FileChangesLog.Record("Encountered unmanaged mutator '{0}' in mutator loader list. Attempting to preserve the entry...", existingClass);
                        }
                    }
                }

                // Finally, append the unmanaged mutators to the list
                if (unmanagedMutatorClasses.Count > 0)
                {
                    if (enabledMutatorClasses.Count > 0)
                    {
                        newMutatorList += ",";
                    }

                    newMutatorList += string.Join(",", unmanagedMutatorClasses);
                }

                configItem.Value = newMutatorList;
                this.FileChangesLog.Record("New mutator list will be: {0}", configItem.Value);
            }


            mutatorsConfig.Save();
            this.FileChangesLog.Record("Mutator list changes saved to file: {0}", mutatorsConfig.FileName);
        }

        /// <summary>
        /// Updates the paths in RPGTacEngine.ini to reflect which mods are enabled,
        /// soft-disabled, or disabled. 
        /// </summary>
        private Result UpdateGameEngineIniFile()
        {
            var result = new Result() { IsSuccessful = true };

            var gameEngineConfigPath = Path.Combine(this.GameFolderPath, Game.RelativePathToConfigurationsFolder, Game.EngineConfigurationFile);
            var gameEngineConfig = new GameConfiguration(gameEngineConfigPath);
            gameEngineConfig.Load();

            foreach (var configuration in this.ModConfigurations)
            {
                configuration.IsManaged = true;

                // A reminder here that the "soft-disabled" state for mods means their scripts won't run,
                // but content files and script files remain available to allow save files to work correctly
                if (configuration.State == ModState.Enabled || configuration.State == ModState.SoftDisabled)
                {
                    this.FileChangesLog.Record("Mod {0} needs to be added to the game directory...", configuration.Mod.Id);

                    var enable = this.EnableModStoragePaths(gameEngineConfig, configuration);

                    if(!enable.IsSuccessful)
                    {
                        result.IsSuccessful = false;
                        result.ErrorMessage += enable.ErrorMessage;
                        this.DisableModStoragePaths(gameEngineConfig, configuration); // in case some files managed to be added
                        configuration.State = ModState.Disabled;
                    }
                }
                else if (configuration.State == ModState.Disabled)
                {
                    this.FileChangesLog.Record("Mod {0} needs to be removed from game directory...", configuration.Mod.Id);

                    var disable = this.DisableModStoragePaths(gameEngineConfig, configuration);
                    if(!disable.IsSuccessful)
                    {
                        result.IsSuccessful = false;
                        result.ErrorMessage += disable.ErrorMessage;
                    }
                }
            }

            gameEngineConfig.Save();

            return result;
        }

        /// <summary>
        /// Updates RPGTacEngine.ini and adds paths for the specified mod (configuration).
        /// </summary>
        private Result EnableModStoragePaths(GameConfiguration gameEngineConfig, ModConfiguration configuration)
        {
            var result = new Result();

            try
            {
                if(configuration.State == ModState.Disabled)
                {
                    throw new ModException("Mod is not in an enabled or soft-disabled state.");
                }

                if (!Directory.Exists(configuration.ModStorageFolder))
                {
                    throw new ModException(string.Format("Mod '{0}' doesn't have an existing mod storage folder at '{1}'", configuration.Mod.Id, configuration.ModStorageFolder));
                }

                var relativePath = this.GetRelativeModStoragePath(configuration);
                gameEngineConfig.Include(Game.EnginePathSection, Game.EngineContentPathKey, relativePath);
                gameEngineConfig.Include(Game.EnginePathSection, Game.EngineLocalizationPathKey, relativePath);

                if(configuration.State == ModState.Enabled || configuration.State == ModState.SoftDisabled)
                {
                    gameEngineConfig.Include(Game.EnginePathSection, Game.EngineScriptPathKey, relativePath);
                }
                /*
                else if(configuration.State == ModState.SoftDisabled)
                {
                    gameEngineConfig.Exclude(GameEnginePathSection, GameEngineScriptPathKey, relativePath);
                }
                */

                result.IsSuccessful = true;
                this.FileChangesLog.Record("RPGTacMods.ini will include paths for mod '{0}' for folder '{1}'", configuration.Mod.Id, configuration.ModStorageFolder);
                
            }
            catch(ModException e)
            {
                // Exception message can be surfaced to user here
                e.AppendToLogFile();
                result.ErrorMessage = e.Message;
                this.FileChangesLog.Record(e.Message);
            }
            catch(Exception e)
            {
                // Exception message should not be surfaced to user here
                e.AppendToLogFile();
                result.ErrorMessage = string.Format("Installation failed for mod '{0}'.", configuration.Mod.Id);
                this.FileChangesLog.Record(result.ErrorMessage);
            }

            return result;

        }

        /// <summary>
        /// Updates RPGTacEngine.ini and removes paths for the specified mod (configuration).
        /// </summary>
        private Result DisableModStoragePaths(GameConfiguration gameEngineConfig, ModConfiguration configuration)
        {
            var result = new Result();

            try
            {
                var relativePath = this.GetRelativeModStoragePath(configuration);
                gameEngineConfig.Exclude(Game.EnginePathSection, Game.EngineContentPathKey, relativePath);
                gameEngineConfig.Exclude(Game.EnginePathSection, Game.EngineScriptPathKey, relativePath);
                gameEngineConfig.Exclude(Game.EnginePathSection, Game.EngineLocalizationPathKey, relativePath);
                
                result.IsSuccessful = true;
                this.FileChangesLog.Record("RPGTacMods.ini will be updated to *not* include paths for mod '{0}' for folder '{1}'", configuration.Mod?.Id, configuration.ModStorageFolder);
            }
            catch(ModException e)
            {
                e.AppendToLogFile();
                result.ErrorMessage = e.Message; // Safe to display to user
                this.FileChangesLog.Record(e.Message);
            }
            catch(Exception e)
            {
                e.AppendToLogFile();
                result.ErrorMessage = string.Format("Could not disable mod {0}.", configuration.Mod?.Id);
                this.FileChangesLog.Record(result.ErrorMessage);
            }

            return result;

        }

        /// <summary>
        /// Returns a path to the mod's storage folder relative to
        /// the game's executable.
        /// </summary>
        private string GetRelativeModStoragePath(ModConfiguration configuration)
        {
            if(configuration.RegistrationType == RegistrationType.Standalone)
            {
                if (!Directory.Exists(configuration.ModStorageFolder))
                {
                    throw new ModException("Cannot extract relative mod storage path becuse the specified mod does not have a storage folder that exists.");
                }

                return Path.Combine(RelativePathToStandaloneModsFolder, new DirectoryInfo(configuration.ModStorageFolder).Name);

            }
            else if(configuration.RegistrationType == RegistrationType.SteamWorkshopItem)
            {
                return Path.Combine(Game.RelativeIniPathToSteamModsFolder, new DirectoryInfo(configuration.ModStorageFolder).Name);
            }
            else
            {
                throw new ModException("Cannot extract relative mod storage path because the specified mod has an unhandled registration type.");
            }
            
            
        }

        /// <summary>
        /// Shifts a mod up the order list by one.
        /// </summary>
        /// <param name="index">Index of the mod to be shifted up the order list.</param>
        public void ShiftModOrderUp(int index)
        {
            if (index > 0 && index < this.ModConfigurations.Count)
            {
                var configuration = this.ModConfigurations[index];
                this.ModConfigurations.RemoveAt(index);
                this.ModConfigurations.Insert(index - 1, configuration);
                this.UpdateModOrderValue();
            }
        }

        /// <summary>
        /// Shifts a mod down the order list by one.
        /// </summary>
        /// <param name="index">Index of the mod to be shifted down the order list.</param>
        public void ShiftModOrderDown(int index)
        {
            if (index >= 0 && index + 1 < this.ModConfigurations.Count)
            {
                var configuration = this.ModConfigurations[index];
                this.ModConfigurations.RemoveAt(index);
                this.ModConfigurations.Insert(index + 1, configuration);
                this.UpdateModOrderValue();
            }
        }


        private void UpdateModOrderValue()
        {
            int order = 0;
            foreach (var configuration in this.ModConfigurations)
            {
                configuration.OrderIndex = order++;
            }
        }

        /// <summary>
        /// Returns the path to the game's executable.
        /// </summary>
        public string GetPathToGameExecutable(bool Use64Bit = true)
        {
            if (!string.IsNullOrEmpty(this.GameFolderPath))
            {
                if (Use64Bit)
                {
                    return Path.Combine(this.GameFolderPath, Game.RelativePathToExecutable64Bit);
                }
                else
                {
                    return Path.Combine(this.GameFolderPath, Game.RelativePathToExecutable32Bit);
                }
            }
            else
            {
                throw new InvalidOperationException("Cannot provide a path to the game executable because the game golder path has not yet been set.");
            }
        }
    
        public string GetPathToSteamWorkshopMods()
        {
            if (!string.IsNullOrEmpty(this.GameFolderPath))
            {
                return Path.Combine(this.GameFolderPath, Game.RelativePathToSteamModsFolder);
            }
            else
            {
                throw new InvalidOperationException("Cannot provide a path to Steam Workshop mods folder because the game golder path has not yet been set.");
            }
        }
    
    }
}
