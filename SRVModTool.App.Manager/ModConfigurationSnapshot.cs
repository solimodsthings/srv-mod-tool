using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRVModTool.App.Manager
{
    /// <summary>
    /// A snapshot in time of a mod's current state. This is useful
    /// for detecting configuration changes made through the Mod Loader
    /// that haven't been saved yet.
    /// </summary>
    public class ModConfigurationSnapshot
    {
        public ModConfiguration Mod { get; set; }
        public int OrderIndexSnapshot { get; set; }
        public ModState ModStateSnapshot { get; set; }

        public ModConfigurationSnapshot(ModConfiguration mod)
        {
            this.Mod = mod;
            this.OrderIndexSnapshot = mod.OrderIndex;
            this.ModStateSnapshot = mod.State;
        }
    }
}
