using System;
using System.IO;
using CrossInstaller.System;

namespace CrossInstaller
{
    public class Iterator
    {
        public event EventHandler<RecurseFileEventArgs> RecurseFileEvent;

        public void IterateDirectoryStart(string path)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            RecurseDirectory(directoryInfo, "");
        }

        private void RecurseDirectory(DirectoryInfo directoryInfo, string relativePath)
        {
            foreach (var dir in directoryInfo.EnumerateDirectories())
                RecurseDirectory(dir, Path.Combine(relativePath, dir.Name));
            foreach (var file in directoryInfo.EnumerateFiles())
            {
                string filePath = Path.Combine(relativePath, file.Name);
                RecurseFileEvent?.Invoke(this, new RecurseFileEventArgs(filePath));
            }
        }
    }
}
