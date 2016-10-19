using System;

namespace Slamby.Common.Exceptions
{
    public class OutOfResourceException : SlambyException
    {
        public OutOfResourceException(string message, Exception ex = null) : base(message, ex) { }

    }
}
