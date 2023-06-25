using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SRVModTool.App.Manager
{
    /// <summary>
    /// Represents information that needs to be 
    /// displayed in the main window's mod list.
    /// </summary>
    public class ModViewModel : INotifyPropertyChanged
    {
        public ModConfiguration Configuration { get; private set; }

        public int Order
        {
            get
            {
                return Configuration.OrderIndex + 1;
            }
        }

        public ulong? SteamWorkshopId
        {
            get
            {
                return Configuration.Mod?.SteamWorkshopId ?? null;
            }
        }

        public string Name
        {
            get
            {
                var name = Configuration.Mod?.Name ?? string.Empty;

                if(!this.IsManaged && !string.IsNullOrEmpty(name))
                {
                    name += " (New)";
                }

                return name;
            }
        }

        public string TruncatedName
        {
            get
            {
                /*
                if (this.Name.Length <= 32)
                {
                    return this.Name;
                }
                else
                {
                    return this.Name.Substring(0, 29) + "...";
                }
                */

                return this.Name;
            }
        }

        public string Version
        {
            get
            {
                return Configuration.Mod?.Version ?? string.Empty;
            }
        }

        public string Author
        {
            get
            {
                return Configuration.Mod?.Author ?? string.Empty;
            }
        }

        public bool IsManaged
        {
            get
            {
                return Configuration.IsManaged;
            }
            
        }

        public string RegistrationType
        {
            get
            {
                if (Configuration.Mod == null) 
                { 
                    return string.Empty; 
                }
                else if (Configuration.RegistrationType == SRVModTool.RegistrationType.Standalone)
                {
                    return "Standalone";
                }
                else if (Configuration.RegistrationType == SRVModTool.RegistrationType.SteamWorkshopItem)
                {
                    return "Steam";
                }
                else
                {
                    return "Undetermined";
                }
            }
        }

        public bool IsEnabled
        {
            get
            {
                return Configuration.State == ModState.Enabled;
            }
            set {
                if (value)
                {
                    Configuration.State = ModState.Enabled;
                    this.Refresh();
                }
            }
        }

        public bool IsDisabled
        {
            get
            {
                return Configuration.State == ModState.Disabled;
            }
            set
            {
                if (value)
                {
                    Configuration.State = ModState.Disabled;
                    this.Refresh();
                }
            }
        }

        public bool IsSoftDisabled
        {
            get
            {
                return Configuration.State == ModState.SoftDisabled;
            }
            set
            {
                if (value)
                {
                    Configuration.State = ModState.SoftDisabled;
                    this.Refresh();
                }
            }
        }

        public string State
        {
            get
            {
                if(Configuration.State == ModState.Enabled)
                {
                    return "Enabled";
                }
                else if(Configuration.State == ModState.Disabled)
                {
                    return "Disabled";
                }
                else if (Configuration.State == ModState.SoftDisabled)
                {
                    return "Soft-Disabled";
                }
                else if (Configuration.State == ModState.Undetermined)
                {
                    return "Undetermined";
                }
                else
                {
                    return "Unknown";
                }
            }
        }

        public string Compatibility
        {
            get
            {
                var result = string.Empty;

                if (Configuration.Mod.IsCampaign)
                {
                    result += "Custom Campaign";
                }
                else
                {
                    result += "Septaroad Voyager Mod";
                }

                /*
                else if (Configuration.Mod.CompatibleWithBaseGame)
                {
                    // result = "Official Campaign, ";
                    result = "Official Campaign Mod";
                }
                else if (Configuration.Mod.CompatibleWithSrvGame)
                {
                    result += "Septaroad Voyager Mod";
                }
                */
                
                /*
                if(result.EndsWith(", "))
                {
                    result = result.Substring(0, result.Length - 2);
                }
                */

                if (string.IsNullOrEmpty(result))
                {
                    result = "Not Specified";
                }

                return result;
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        public ModViewModel(ModConfiguration configuration) 
        {
            this.Configuration = configuration;
        }

        public void Set(ModConfiguration configuration)
        {
            this.Configuration = configuration;
            this.Refresh();

        }

        public void Refresh()
        {
            // Assumes all properties have changed
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }

    }
}
