using System;
using System.Collections.Generic;
using System.Text;

namespace SRVModTool
{
    /// <summary>
    /// Represents a selectable Campaign within the game's
    /// Campaign menu.
    /// </summary>
    public class Campaign
    {
        public string Name { get; set; }
        public string Prefix { get; set; }
        public string Description { get; set; }
        public string BaseLevel { get; set; }
        public string GameType { get; set; }

        public Campaign() { }

        public Campaign(string payload)
        {

            if(!payload.StartsWith("(") || !payload.EndsWith(")"))
            {
                throw new InvalidOperationException("Campaign payload was not in expected format.");
            }

            var tokens = payload.Substring(1, payload.Length - 2).Split(new string[]{ "\"," }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var token in tokens)
            {
                var pair = token.Split('=');
                var key = pair[0];
                var value = pair[1].Replace("\"","");

                if (key == "CampaignName")
                {
                    this.Name = value;
                }
                else if (key == "CampaignPrefix")
                {
                    this.Prefix = value;
                }
                else if (key == "CampaignDescription")
                {
                    this.Description = value;
                }
                else if (key == "CampaignBaseLevel")
                {
                    this.BaseLevel = value;
                }
                else if (key == "CampaignGameType")
                {
                    this.GameType = value;
                }

            }
        }
        
        /// <summary>
        /// Provides a game-friendly string representation of this
        /// campaign that is ready to be written to the campaign list file.
        /// </summary>
        public override string ToString()
        {
            var result = new StringBuilder();

            result.Append("(");
            result.Append(string.Format("CampaignName=\"{0}\",", this.Scrub(Name)));
            result.Append(string.Format("CampaignPrefix=\"{0}\",", this.Scrub(Prefix)));
            result.Append(string.Format("CampaignDescription=\"{0}\",", this.Scrub(Description)));
            result.Append(string.Format("CampaignBaseLevel=\"{0}\",", this.Scrub(BaseLevel)));
            result.Append(string.Format("CampaignGameType=\"{0}\"", this.Scrub(GameType)));
            result.Append(")");

            return result.ToString();
        }

        private string Scrub(string s)
        {
            return s.Replace("\"", "''")
                    .Replace("\r", "")
                    .Replace("\n", " ");
        }

    }
}
