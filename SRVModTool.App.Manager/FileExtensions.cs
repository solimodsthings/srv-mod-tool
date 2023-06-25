using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRVModTool.App.Manager
{
    public static class FileExtensions
    {
        public enum FileState{
            Unlocked,
            Locked,
            DoesNotExist
        }


        /// <summary>
        /// Checks whether the file at the specified path is being used
        /// by another application (eg. if Steam is still downloading the
        /// file because it was recently subscribed to).
        /// </summary>
        public static FileState IsFileLocked(this string filepath)
        {
            if(!File.Exists(filepath))
            {
                return FileState.DoesNotExist;
            }

            try
            {
                using (var s = new StreamReader(filepath))
                {
                    return FileState.Unlocked;
                }
            }
            catch(Exception e)
            {
                return FileState.Locked;
            }

        }
    }
}
