using System;
using System.Collections.Generic;
using System.Text;

namespace CrossInstaller.System
{
    class CompressFailException : Exception
    {
        public CompressFailException() : base() { }
        public CompressFailException(string message) : base(message) { }
        public CompressFailException(string message, Exception innerException) : base(message, innerException) { }
    }
}
