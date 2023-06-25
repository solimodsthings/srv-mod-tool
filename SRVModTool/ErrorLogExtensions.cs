using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRVModTool
{
    /// <summary>
    /// Used to send information to an error log file.
    /// </summary>
    public static class ErrorLogExtensions
    {
        private static readonly string ErrorLog = "error.log";

        /// <summary>
        /// Appends the specified Exception's message to error.log with a datetime stamp.
        /// </summary>
        public static void AppendToLogFile(this Exception e)
        {
            File.AppendAllText(ErrorLog, string.Format("\n\n[{0}]\n{1}\n{2}", DateTime.Now.ToString(), e.Message, e.StackTrace));
        }

        /// <summary>
        /// Appends the specified string to error.log with a datetime stamp.
        /// </summary>
        public static void AppendToLogFile(string s)
        {
            File.AppendAllText(ErrorLog, string.Format("\n\n[{0}]\n{1}", DateTime.Now.ToString(), s));
        }

    }
}
