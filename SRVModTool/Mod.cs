using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SRVModTool
{
    /// <summary>
    /// An instance of this class represents a mod
    /// that can be installed into Septaroad Voyager.
    /// </summary>
    public class Mod
    {
        /// <summary>
        /// Name of a mod package's information file.
        /// </summary>
        public static readonly string InfoFile = "mod.json";

        /// <summary>
        /// An identifier unique to this mod. Identifiers
        /// must not contain any spaces. A recommended convention
        /// is [author]-[modname].
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The name of this mod.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// A string describing the version of this mod.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// A description of what this mod does and how
        /// it works.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The url to a project site for this mod, if one exists.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// The name of the mod author.
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// The url to a site for the mod author, if one exists.
        /// </summary>
        public string AuthorUrl { get; set; }

        /// <summary>
        /// If this mod adds a new mutator, this property
        /// will contain the name of the mutator class.
        /// </summary>
        public string MutatorClass { get; set; }

        /// <summary>
        /// The workshop ID of this mod if it is part of the
        /// Steam Workshop as a published item.
        /// </summary>
        public ulong? SteamWorkshopId { get; set; }

        // v1.3
        public bool IsCampaign { get; set; }
        public string CampaignName { get; set; }
        public string CampaignPrefix { get; set; }
        public string CampaignDescription { get; set; }
        public string CampaignBaseLevel { get; set; }
        public string CampaignGameType { get; set; }

        /// <summary>
        /// Read-only property indicating whether this mod adds a new mutator.
        /// </summary>
        [JsonIgnore]
        public bool HasMutator
        {
            get
            {
                return !string.IsNullOrEmpty(this.MutatorClass);
            }
        }

        /// <summary>
        /// Read-only property indicating whether this mod is meant to be
        /// published to or subscribed from the Steam Workshop.
        /// </summary>
        [JsonIgnore]
        public bool IsSteamWorkshopItem
        {
            get
            {
                return this.SteamWorkshopId != null;
            }
        }

        

    }
}
