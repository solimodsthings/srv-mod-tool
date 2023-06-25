using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRVModTool
{
    /// <summary>
    /// An instance of this class represents a key-value pair and
    /// single line item of an .ini file section.
    /// </summary>
    public class GameConfigurationItem
    {
        /// <summary>
        /// The key of a key-value pair. This is what is to the left of the first '=' character.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// The value of a key-value pair. This is what is to the right of the first '=' character.
        /// </summary>
        public string Value { get; set; }
    }
}
