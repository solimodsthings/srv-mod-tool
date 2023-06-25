using System;
using System.Collections.Generic;
using System.Text;

namespace SRVModTool
{
    /// <summary>
    /// Used for exceptions related to mod management, registration,
    /// uninstallation, etc. In general, ModException messages are safe to
    /// display to the user while general Exceptions are not.
    /// </summary>
    [Serializable]
    public class ModException : Exception
    {
        protected string ModId { get; set; }
        protected string ModName { get; set; }
        protected string ModVersion { get; set; }
        protected string ModAuthor { get; set; }

        public ModException() { }
        public ModException(Mod mod)
        {
            this.SetMod(mod);
        }

        public ModException(string message) : base(message) { }
        public ModException(Mod mod, string message) : base(message)
        {
            this.SetMod(mod);
        }

        private void SetMod(Mod mod)
        {
            if (mod != null)
            {
                this.ModId = mod.Id;
                this.ModName = mod.Name;
                this.ModVersion = mod.Version;
                this.ModAuthor = mod.Author;
            }
        }

    }

    [Serializable]
    public class ModRegistrationException : ModException
    {
        public ModRegistrationException() { }
        public ModRegistrationException(Mod mod) : base(mod) { }
        public ModRegistrationException(string message) : base(message) { }
        public ModRegistrationException(Mod mod, string message) : base(mod, message) { }

    }
}
