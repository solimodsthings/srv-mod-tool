using System;

namespace SRVModTool
{
    /// <summary>
    /// The permitted states for a mod.
    /// <para><b>Enabled</b>: all scripts, content packages, and localization files are active and installed into the game directory</para>
    /// <para><b>Soft Disabled</b>: mod scripts are disabled, but content packages and localization files are still active and installed into the game directory</para>
    /// <para><b>Disabled</b>: all mod content is inactive and not installed in the game directory</para>
    /// <para><b>Undetermined</b>: The installation status of scripts, content packages, and localization files does not match one of
    /// the previous three states. This status can sometimes be given to mods that existed in the game directory prior to this mod manager being used.</para>
    /// </summary>
    public enum ModState
    {
        Disabled,
        SoftDisabled,
        Enabled,
        Undetermined
    }

    /// <summary>
    /// The ways a mod can be registered with a mod loader.
    /// <para><b>Standalone</b>: the mod was an .hsmod file that was manually added to the loader</para>
    /// <para><b>SteamWorkshopItem</b>: the mod was a Steam Workshop item that was subscribed to</para>
    /// </summary>
    public enum RegistrationType
    {
        Standalone,
        SteamWorkshopItem
    }

    /// <summary>
    /// The different types an <see cref="AutomaticModRegistrationEvent">AutomaticModRegistrationEvent</see> can be.
    /// </summary>
    public enum RegistrationAction
    {
        Registered,
        Unregistered
    }

}
