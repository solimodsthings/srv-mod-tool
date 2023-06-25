using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRVModTool
{
    /// <summary>
    /// An instance of this class represents a section inside 
    /// an .ini file. (Sections start with a section name
    /// surrounded by square brackets.)
    /// </summary>
    public class GameConfigurationSection
    {
        public string Name { get; set; }
        public List<GameConfigurationItem> Items { get; set; }
        public List<string> Comments { get; set; }

        public GameConfigurationSection()
        {
            this.Items = new List<GameConfigurationItem>();
            this.Comments = new List<string>();
        }
    }
}
