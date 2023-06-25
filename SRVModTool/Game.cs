
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SRVModTool
{
    /// <summary>
    /// Shared constants and utility methods for interacting with
    /// the game.
    /// </summary>
    public class Game
    {
        /// <summary>
        /// This is the process name for the game. Used to avoid making
        /// changes to game files while the game itself is running.
        /// </summary>
        public static readonly string ProcessName = "Septaroad Voyager";

        /// <summary>
        /// Relative to game root folder. The 64-bit game executable.
        /// </summary>
        public static readonly string RelativePathToExecutable64Bit = @"Binaries\Win64\SRVGame.exe";

        /// <summary>
        /// Relative to game root folder. The 32-bit game executable.
        /// </summary>
        public static readonly string RelativePathToExecutable32Bit = @"Binaries\Win32\SRVGame.exe";

        /// <summary>
        /// Relative to game root folder. This folder contains .ini files.
        /// </summary>
        public static readonly string RelativePathToConfigurationsFolder = @"SRVGame\Config";

        /// <summary>
        /// Relative to game root folder. This folder contains .ini files.
        /// </summary>
        public static readonly string RelativePathToEnglishLocalizationFolder = @"SRVGame\Localization\INT";

        public static readonly string CampaignLocalizationFile = "SRVGame.int";

        public static readonly string SteamGameID = "2091620";

        /// <summary>
        /// Relative to game root folder. Used for Steam Workshop integration.
        /// </summary>
        public static readonly string RelativePathToSteamModsFolder = @"..\..\workshop\content\" + SteamGameID;

        /// <summary>
        /// Relative to location of game executable. Used for Steam Workshop integration, specifically for paths in SRVEngine.ini.
        /// </summary>
        public static readonly string RelativeIniPathToSteamModsFolder = @"..\..\..\..\workshop\content\" + SteamGameID; // relative to the game executable and used in .ini key-value pairs

        /// <summary>
        /// Name of the configuration file where content, script, and localization paths need to be defined for mods.
        /// </summary>
        public static readonly string EngineConfigurationFile = "SRVEngine.ini";

        /// <summary>
        /// The section of <see cref="EngineConfigurationFile"/> that contains content, script, and localization paths.
        /// </summary>
        public static readonly string EnginePathSection = "Core.System";

        /// <summary>
        /// The key in section <see cref="EnginePathSection"/> used to define content paths.
        /// </summary>
        public static readonly string EngineContentPathKey = "Paths";

        /// <summary>
        /// The key in section <see cref="EnginePathSection"/> used to define script paths.
        /// </summary>
        public static readonly string EngineScriptPathKey = "ScriptPaths";

        /// <summary>
        /// The key in section <see cref="EnginePathSection"/> used to define localization paths.
        /// </summary>
        public static readonly string EngineLocalizationPathKey = "LocalizationPaths";

        /// <summary>
        /// Name of the configuration file where mutator classes are defined for mods.
        /// </summary>
        public static readonly string MutatorConfigurationFile = "SRVMods.ini";

        /// <summary>
        /// The section of <see cref="MutatorConfigurationFile"/> where mutator classes are defined.
        /// </summary>
        public static readonly string MutatorLoaderSection = "SRVGame.RPGTacMutatorLoader";

        /// <summary>
        /// The key in section <see cref="MutatorLoaderSection"/> where mutator classes are defined.
        /// </summary>
        public static readonly string MutatorLoaderKey = "MutatorsLoaded";

        /// <summary>
        /// Checks to see if the game is already running.
        /// </summary>
        public static bool IsRunning()
        {
            return Process.GetProcessesByName(Game.ProcessName).Length > 0;
        }
        
        /// <summary>
        /// Checks whether the specified path is the root folder
        /// containing the game.
        /// </summary>
        /// <param name="path">The path to validate.</param>
        /// <returns>True if the path is the root folder containing the game, else false.</returns>
        public static bool IsInsideFolder(string path)
        {
            try
            {
                var attributes = File.GetAttributes(path);
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    if (Directory.Exists(path)
                        && File.Exists(Path.Combine(path, RelativePathToConfigurationsFolder, MutatorConfigurationFile))
                        && File.Exists(Path.Combine(path, RelativePathToConfigurationsFolder, EngineConfigurationFile))
                        && File.Exists(Path.Combine(path, RelativePathToExecutable64Bit))
                        && File.Exists(Path.Combine(path, RelativePathToExecutable32Bit))
                    )
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                // If the path is not a file or something that cannot be accessed, an exception is thrown
                ex.AppendToLogFile();
            }

            return false;
        }

        /// <summary>
        /// Checks if the parent folder of the specified path is the root
        /// folder containing the game. This method can recurse up the directory
        /// hierarchy.
        /// </summary>
        /// <param name="path">The path whose parent folder needs to be validated.</param>
        /// <param name="recurse">The number of times this method should recurse up the directory hierarchy.</param>
        /// <returns>The path to a parent directory that is actually the game directory. If no such parent exists, the return value is null.</returns>
        public static string FindFolder(string path, int recurse = 0)
        {
            try
            {
                var directory = new DirectoryInfo(path);

                int limit = recurse;

                if (directory.Exists)
                {
                    while (directory != null && limit >= 0)
                    {
                        if (Game.IsInsideFolder(directory.FullName))
                        {
                            return directory.FullName;
                        }

                        directory = directory.Parent;

                        limit--;
                    }
                }
            }
            catch (Exception e)
            {
                // If the path is not a file or something that cannot be accessed, an exception is thrown
                e.AppendToLogFile();
            }

            return null;
        }

        /// <summary>
        /// Launches the Steam Workshop page within Steam itself.
        /// </summary>
        public static void OpenSteamWorkshop()
        {
            try
            {
                var p = new ProcessStartInfo("steam://url/SteamWorkshopPage/" + SteamGameID) { UseShellExecute = true, Verb = "open" };
                Process.Start(p);
            }
            catch(Exception e)
            {
                e.AppendToLogFile();
            }
        }

        /// <summary>
        /// Launches the Steam Workshop and goes directly to the page with the specified id.
        /// </summary>
        public static void OpenSteamWorkshopItem(ulong id)
        {
            try
            {
                var p = new ProcessStartInfo(string.Format("steam://url/CommunityFilePage/{0}", id)) { UseShellExecute = true, Verb = "open" };
                Process.Start(p);
            }
            catch(Exception e)
            {
                e.AppendToLogFile();
            }
            
        }

    }
}
