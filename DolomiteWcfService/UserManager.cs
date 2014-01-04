using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DolomiteModel;

namespace DolomiteWcfService
{
    class UserManager
    {

        #region Constant

        public const string UserKeysEnabledKey = "userKeysEnabled";

        #endregion

        #region Properties and Member Variables

        private bool UserKeysEnabled { get; set; }

        private UserDbManager DatabaseManager { get; set; }

        #endregion

        #region Singleton Instance Code

        private static UserManager _instance;

        /// <summary>
        /// Singleton instance of the user manager
        /// </summary>
        public static UserManager Instance
        {
            get { return _instance ?? (_instance = new UserManager()); }
        }

        /// <summary>
        /// Singleton constructor for the Track Manager
        /// </summary>
        private UserManager() 
        {
            // Check if the user keys checking is enabled in the configuration file
            if (Properties.Settings.Default[UserKeysEnabledKey] as Boolean? == null)
            {
                throw new InvalidDataException("Track storage container key not set in settings.");
            }
            UserKeysEnabled = (bool)Properties.Settings.Default[UserKeysEnabledKey];

            // Grab an instance of the User database manager
            DatabaseManager = UserDbManager.Instance;
        }

        #endregion

        #region Creation Methods

        /// <summary>
        /// Does processing on the user information to create a user. This
        /// includes performing the hashing operations for the username. The
        /// changing of the status for the user auth key (if required) is also
        /// performed in this method.
        /// </summary>
        /// <param name="username">The username for the user to create</param>
        /// <param name="email">The email address of the user</param>
        /// <param name="password">The password for the user</param>
        /// <param name="userKey">
        /// The auth key for creating the user. This is only required if the
        /// UserKeysEnabled flag is set in the application configuration. If not
        /// required, the parameter can be set to null.
        /// </param>
        public void CreateUser(string username, string email, string password, Guid? userKey)
        {
            // 1) Hash the users password
            // The algo: use RIPEMD160 to hash the user's email, use that as a salt and SHA256 the whole thing
            RIPEMD160 saltHasher = new RIPEMD160Managed();
            SHA256 passHasher = new SHA256Cng();
            byte[] salt = saltHasher.ComputeHash(Encoding.Default.GetBytes(email));
            byte[] hashBytes = passHasher.ComputeHash(Encoding.Default.GetBytes(password + salt));

            // Convrt the password bytes to a string.
            // Why use the bitconverter for this and not the encoding.default.getstring?
            // Because we bitconverter gives us a hex string, instead of unintelligble
            // unicode characters.
            string hashString = BitConverter.ToString(hashBytes).Replace("-",String.Empty);

            // 2) Check to see if a userkey is required
            if (UserKeysEnabled)
            {
                if (!userKey.HasValue || !DatabaseManager.ValidateUserKey(userKey.Value, email))
                {
                    throw new ArgumentNullException("userKey",
                        "You must use a user sign up key that has been provided by the administrator");
                }

                // Wrap the user creation with claim and unclaim keys
                DatabaseManager.SetUserKeyClaimStatus(userKey.Value, true);
                // 3) Create the user
                try
                {
                    DatabaseManager.CreateUser(username, hashString, email);
                }
                catch (Exception)
                {
                    // Unclaim the key
                    DatabaseManager.SetUserKeyClaimStatus(userKey.Value, false);
                    throw;
                }
            }
            else
            {
                // 3) Create the user
                DatabaseManager.CreateUser(username, hashString, email);
            }
        }

        #endregion
    }
}
