using System;

namespace DolomiteManagement.Exceptions
{
    public class InvalidLoginCredentialsException : Exception
    {
        internal enum InvalidCredentialType
        {
            /// <summary>
            /// The password provided did not match the hashed password in the db
            /// </summary>
            Password,

            /// <summary>
            /// The username does not exist in the database
            /// </summary>
            Username
        }

        /// <summary>
        /// Internal use only, used for tracking what credential the user provided was incorrect.
        /// </summary>
        internal InvalidCredentialType Type { get; set; }

        internal InvalidLoginCredentialsException(InvalidCredentialType type)
        {
            Type = type;
        }
    }

    public class InvalidApiKeyException : Exception
    {
    }
}
