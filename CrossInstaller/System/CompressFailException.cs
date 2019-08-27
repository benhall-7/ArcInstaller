using System;

namespace CrossInstaller.System
{
    public class CompressFailException : Exception
    {
        public CompressFailException() : base() { }
        public CompressFailException(string message) : base(message) { }
        public CompressFailException(string message, Exception innerException) : base(message, innerException) { }
    }
}
