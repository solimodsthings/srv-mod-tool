using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRVModTool.App.Manager
{
    /// <summary>
    /// Used to autodetect where the game is installed if the player permits autodetection
    /// to occur. This class reads Steam's registry keys and Steam's libraryfolders.vdf file
    /// to try and find out where the game folder is located. Please note that autodetection
    /// is not guaranteed to succeed. 
    /// </summary>
    public class GameFolderAutodetector
    {
        /// <summary>
        /// Steam's Windows registry key on 32-bit computers.
        /// </summary>
        private static readonly string SteamRegistryPath32Bit = @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam";

        /// <summary>
        /// Steam's Windows registry key on 64-bit computers.
        /// </summary>
        private static readonly string SteamRegistryPath64Bit = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam";

        /// <summary>
        /// Steam's name for the registry value containing the path to its installation folder.
        /// </summary>
        private static readonly string SteamRegistryInstallPathValue = "InstallPath";

        /// <summary>
        /// The file in Steam's installation folder that contains paths to where Steam games are
        /// installed.
        /// </summary>
        private static readonly string SteamLibraryFoldersFile = @"steamapps\libraryfolders.vdf";

        /// <summary>
        /// The game folder we are trying to find.
        /// </summary>
        private static readonly string GameFolder = @"SteamApps\common\Septaroad Voyager";

        /// <summary>
        /// This is populated with the desired game folder path 
        /// when a call to TryAutodetectGameFolder() succeeds.
        /// </summary>
        public string AutodetectedGameFolder { get; set; }

        public Result TryAutodetect()
        {
            var result = new Result();

            try
            {
                // 1. First we need to find out where Steam is installed. On Windows
                //    machines this is stored in the registry in one of two locations
                //    depending on whether a 32-bit or 64-bit version of Windows is
                //    running.

                var registryValue = Registry.GetValue(SteamRegistryPath32Bit, SteamRegistryInstallPathValue, string.Empty);

                if (registryValue == null || registryValue.Equals(string.Empty))
                {
                    registryValue = Registry.GetValue(SteamRegistryPath64Bit, SteamRegistryInstallPathValue, string.Empty);
                }

                if (registryValue != null && !registryValue.Equals(string.Empty))
                {

                    // 2. Once the Steam installation folder is found, we look for a .vdf
                    //    file containing directory paths of where Steam games are installed.
                    //    Note that Steam can have more than one game installation folder.

                    var libraryFile = Path.Combine(registryValue.ToString(), SteamLibraryFoldersFile);

                    if (File.Exists(libraryFile))
                    {

                        // 3. Extract the paths from the .vdf file. This method assumes installation
                        //    paths will always be on their own line in the file.

                        var libraryPaths = new List<string>();
                        var lines = File.ReadAllLines(libraryFile);

                        foreach (var line in lines)
                        {
                            if (line.Trim().StartsWith("\"path\""))
                            {
                                var path = line.Replace("\"path\"", string.Empty)
                                    .Replace("\\\\", "\\")        // Turn double backslash into single backslash
                                    .Replace("\"", string.Empty)  // Remove any remaining quotes
                                    .Trim();

                                libraryPaths.Add(path);

                            }
                        }

                        // 4. Once we know the folder(s) where all Steam games are installed,
                        //    we simply test whether a subdirectory with the game's name exists.
                        //    Note that this method has no further validation on whether the subdirectory
                        //    with the game's name actually contains the game itself. That is taken
                        //    care of outside this class. (See Validator.)
                        foreach (var path in libraryPaths)
                        {
                            var gamePath = Path.Combine(path, GameFolder);

                            if (Directory.Exists(gamePath))
                            {
                                this.AutodetectedGameFolder = gamePath;
                                result.IsSuccessful = true;
                                break;
                            }
                        }

                        if(!result.IsSuccessful)
                        {
                            if (libraryPaths.Count > 0)
                            {
                                result.ErrorMessage = "Autodetection failed. The game does not appear to be installed yet.";
                            }
                            else
                            {
                                result.ErrorMessage = "Autodetection failed. Could not locate any Steam game library folders.";
                            }

                        }

                    }
                    else
                    {
                        result.ErrorMessage = "Autodetection failed. Could not get game library paths from Steam.";
                    }
                }
                else
                {
                    result.ErrorMessage = "Autodetection failed. Could not access Steam.";
                }

            }
            catch (Exception e)
            {
                e.AppendToLogFile();
                result.ErrorMessage = "Autodetection failed and threw an exception. See error.log for details.";
            }


            return result;
        }
    }
}
