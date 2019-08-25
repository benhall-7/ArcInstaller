using System;
using System.IO;

namespace CrossInstaller.System
{
    public class RecurseFileEventArgs : EventArgs
    {
        /// <summary>
        /// The individual folder names leading up to the file in the event
        /// </summary>
        public string[] PathParts { get; }
        /// <summary>
        /// The filename (with extension) of the file in the event
        /// </summary>
        public string FileName { get; }

        private string Root { get; }

        /// <summary>
        /// The full path string of the file in the event
        /// </summary>
        public string FullPath => Path.Combine(Root, Path.Combine(PathParts), FileName);

        public RecurseFileEventArgs(string root, string[] pathParts, string fileName)
        {
            Root = root;
            PathParts = pathParts;
            FileName = fileName;
        }
    }
}
