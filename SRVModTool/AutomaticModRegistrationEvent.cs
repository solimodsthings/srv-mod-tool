using System;
using System.Collections.Generic;
using System.Text;

namespace SRVModTool
{
    /// <summary>
    /// Captures automatic registration and unregistration
    /// events for mods. These events occur when a mod is placed directly
    /// in a mod storage location by a user, without the ModLoader's help.
    /// Automatic registration events are also expected for
    /// recently subscribed Steam Workshop mods that get automatically
    /// downloaded.
    /// </summary>
    public class AutomaticModRegistrationEvent
    {
        public RegistrationAction Type { get; set; }
        public ModConfiguration Configuration { get; set; }

        public AutomaticModRegistrationEvent(RegistrationAction type, ModConfiguration configuration)
        {
            this.Type = type;
            this.Configuration = configuration;
        }
    }
}
