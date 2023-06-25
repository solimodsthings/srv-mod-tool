using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SRVModTool
{
    // Convenience class for logging modding events that are not related
    // to Exceptions and Errors. This one is specific to game file changes.
    public class GameFilesChangeLog
    {
        private string FilePath { get; set; }
        private StringBuilder Log { get; set; }

        public GameFilesChangeLog(string filepath)
        {
            this.FilePath = filepath;
            this.Log = new StringBuilder();
        }

        /// <summary>
        /// Clears the file change log. Subsequent calls to Record() wil be 
        /// written to disk once End() is called.
        /// </summary>
        public void Start()
        {
            this.Log.Clear();
        }
        
        /// <summary>
        /// Used to record file changes in the game folder.
        /// </summary>
        public void Record(string message)
        {
            this.Log.AppendLine(string.Format("[{0}] {1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), message));
        }

        public void Record(string formattedMessage, params object[] args)
        {
            this.Record(string.Format(formattedMessage, args));
        }

        /// <summary>
        /// Writes recent changes to filechange.log.
        /// </summary>
        public void End()
        {
            File.WriteAllText(this.FilePath, this.Log.ToString());
        }

    }
}
