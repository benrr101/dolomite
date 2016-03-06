using System;

namespace DolomiteManagement.Exceptions
{
    /// <summary>
    /// Wrapper for internal exceptions while processing files in the background.
    /// </summary>
    public class DolomiteInternalException : Exception
    {
        /// <summary>
        /// Error that will be provided to the user
        /// </summary>
        public string UserError { get; set; }

        /// <summary>
        /// Creates a new internal exception with a user error message
        /// </summary>
        /// <param name="internalException"></param>
        /// <param name="userError"></param>
        public DolomiteInternalException(Exception internalException, string userError)
            : base(internalException.Message, internalException)
        {
            UserError = userError;
        }

        public DolomiteInternalException(Exception internalException, string userError, string adminError) :
            base(adminError, internalException)
        {
            UserError = userError;
        }
    }
}
