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
        /// Does processing on the user information to 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="email"></param>
        /// <param name="password"></param>
        /// <param name="userKey"></param>
        public void CreateUser(string username, string email, string password, Guid? userKey)
        {
            // 1) Hash the users password
            // The algo: use sha256 to hash the user's email, use that as a salt and bcrypt the whole thing
            SHA256 saltHasher = new SHA256Cng();
            byte[] salt = saltHasher.ComputeHash(Encoding.Default.GetBytes(email));

            //string hashedPassword = Crypt.BCrypt.HashPassword(password + salt, Crypt.BCrypt.GenerateSalt());

            // 2) Check to see if a userkey is required
            if (UserKeysEnabled)
            {
                if (!userKey.HasValue || DatabaseManager.ValidateUserKey(userKey.Value, email))
                {
                    throw new ArgumentNullException("userKey",
                        "You must use a user sign up key that has been provided by the administrator");
                }

                // Wrap the user creation with claim and unclaim keys
                DatabaseManager.ClaimUserKey(userKey.Value, true);
                // 3) Create the user
                try
                {
                    DatabaseManager.CreateUser(username, "haha!I'mOnTheInternet!", email);
                }
                catch (Exception)
                {
                    // Unclaim the key
                    DatabaseManager.ClaimUserKey(userKey.Value, false);
                    throw;
                }
            }
            else
            {
                // 3) Create the user
                DatabaseManager.CreateUser(username, "haha!I'mOnTheInternet!", email);
            }
        }

        #endregion

        //return StringComparer.Ordinal.Compare(hashed, HashPassword(plaintext, hashed)) == 0;
    }
}
