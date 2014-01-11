using System;

namespace DolomiteWcfService.Exceptions
{
    class InvalidSessionException : Exception
    {
        public InvalidSessionException(string message) : base(message)
        {
        }
    }
}
