using System;

namespace Slamby.Common.Exceptions
{
    public class SlambyException : Exception
    {
        public SlambyException(string message, Exception ex = null) : base(message, ex) { }

    }
}
