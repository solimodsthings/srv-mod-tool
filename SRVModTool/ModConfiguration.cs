using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SRVModTool
{
    /// <summary>
    /// An instance of this class represents a mod and its
    /// current configuration (load order, enabled state, etc.) 
    /// with respect to the game.
    /// </summary>
    public class ModConfiguration
    {

        [JsonIgnore]
        public Mod Mod { get; set; }

        /// <summary>
        /// The location of unpackaged mod files that will be
        /// applied to the game folder if this mod is enabled.
        /// This is also the location of the mod.json file for
        /// this mod.
        /// </summary>
        public string ModStorageFolder { get; set; }

        /// <summary>
        /// The state of the mod.
        /// </summary>
        public ModState State { get; set; }

        /// <summary>
        /// This affects mutator class load order.
        /// </summary>
        public int OrderIndex { get; set; }

        /// <summary>
        /// A value indicating how this mod was registered.
        /// </summary>
        public RegistrationType RegistrationType { get; set; }

        /// <summary>
        /// This value will be true unless the mod was
        /// dropped directly into the mods folder and has
        /// yet to be explicitly enabled or disabled.
        /// </summary>
        [JsonIgnore]
        public bool IsManaged { get; set; }

    }
}
