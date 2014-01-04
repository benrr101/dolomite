using System;

namespace DolomiteModel.PublicRepresentations
{
    public struct UserCreationRequest
    {
        /// <summary>
        /// The optional authorization key from the admin for creating a new user
        /// </summary>
        public Guid? AuthKey { get; set; }

        /// <summary>
        /// The email of the user
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// The password for the user
        /// </summary>
        public string Password { get; set; }
    }
}
