using CrossInstaller.System;
using System;
using System.Collections.Generic;
using System.IO;

namespace CrossInstaller
{
    public class Iterator
    {
        private Stack<string> PathParts { get; set; }
        private string root { get; set; }

        /// <summary>
        /// The event invoked when the recusion reaches a file
        /// </summary>
        public event EventHandler<RecurseFileEventArgs> RecurseFileEvent;

        public Iterator()
        {
            PathParts = new Stack<string>();
            root = "";
        }

        public void IterateDirectoryStart(string path)
        {
            root = path;
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            RecurseDirectory(directoryInfo);
        }

        private void RecurseDirectory(DirectoryInfo directoryInfo)
        {
            foreach (var dir in directoryInfo.EnumerateDirectories())
            {
                PathParts.Push(dir.Name);
                RecurseDirectory(dir);
            }
            foreach (var file in directoryInfo.EnumerateFiles())
            {
                RecurseFileEvent?.Invoke(this, new RecurseFileEventArgs(root, PathParts.ToArray(), file.Name));
            }
        }
    }
}
