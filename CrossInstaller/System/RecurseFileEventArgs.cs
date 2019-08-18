using System;

namespace CrossInstaller.System
{
    public class RecurseFileEventArgs : EventArgs
    {
        public string RelativePath { get; }

        public RecurseFileEventArgs(string relativePath)
        {
            RelativePath = relativePath;
        }
    }
}
