using System;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DolomiteModel;
using DolomiteModel.PublicRepresentations;
using DolomiteWcfService.Exceptions;

namespace DolomiteWcfService
{
    class UserManager
    {

        #region Constant

        public const string UserKeysEnabledKey = "userKeysEnabled";

        public const string AbsoluteTimeoutKey = "absoluteTimeout";

        public const string IdleTimeoutKey = "IdleTimeout";

        #endregion

        #region Properties and Member Variables

        private bool UserKeysEnabled { get; set; }

        private TimeSpan AbsoluteTimeoutInterval { get; set; }

        private TimeSpan IdleTimeoutInterval { get; set; }

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
                throw new InvalidDataException("Track storage container key not set in settings.");

            UserKeysEnabled = (bool)Properties.Settings.Default[UserKeysEnabledKey];

            // Determine the absolute and idle timeout intervals
            if (Properties.Settings.Default[IdleTimeoutKey] as TimeSpan? == null)
                throw new InvalidDataException("Session idle timeout interval not set in settings.");

            IdleTimeoutInterval = (TimeSpan) Properties.Settings.Default[IdleTimeoutKey];

            if (Properties.Settings.Default[AbsoluteTimeoutKey] as TimeSpan? == null)
                throw new InvalidDataException("Session absolute timeout interval not set in settings.");

            AbsoluteTimeoutInterval = (TimeSpan)Properties.Settings.Default[AbsoluteTimeoutKey];

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
            string hashString = CreatePasswordHash(email, password);

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

        /// <summary>
        /// Invalidates a given session by setting the timeouts on the session
        /// to one hour in the past. If the session doesn't exist, we ignore it
        /// </summary>
        /// <param name="sessionToken">The token identifier for the session</param>
        public void InvalidateSession(string sessionToken)
        {
            // Calculate new timeouts. Set them for the past to expire the session.
            DateTime expiredTimeout = DateTime.Now - TimeSpan.FromHours(1);

            // Invalidate the session in the server, swallow invalid session errors
            // since we don't want to expose the existence of a session
            try
            {
                DatabaseManager.SetSessionAbsoluteTimeout(sessionToken, expiredTimeout);
                DatabaseManager.SetSessionIdleTimeout(sessionToken, expiredTimeout);
            }
            catch (ObjectNotFoundException)
            {
            }
        }

        /// <summary>
        /// Performs all the actions necessary to validate a login attempt.
        /// Upon successful validation, a new session is created.
        /// </summary>
        /// <param name="apiKey">The API key that the request came from</param>
        /// <param name="ipAddress">The IP address that initialized the request</param>
        /// <param name="username">The name of the user that is attempting to login</param>
        /// <param name="password">The password that is being used for the login</param>
        /// <returns></returns>
        public string ValidateLogin(string apiKey, string ipAddress, string username, string password)
        {
            // Validate the apiKey
            if (!DatabaseManager.ValidateApiKey(apiKey))
                throw new ApplicationException("Invalid API key.");

            // Validate the login credentials
            User user = DatabaseManager.GetUserByUsername(username);
            if (user == null)
                throw new ObjectNotFoundException(String.Format("User with username {0} does not exist", username));

            // Compare the password hashes
            string hash = CreatePasswordHash(user.Email, password);
            if (!user.PasswordHash.Equals(hash, StringComparison.Ordinal))
                throw new ObjectNotFoundException(String.Format("User {0} passwords do not match", username));

            // Everything looks good, fire up a session
            // Determine the timeout times
            DateTime absTimeout = DateTime.Now + AbsoluteTimeoutInterval;
            DateTime idleTimeout = DateTime.Now + IdleTimeoutInterval;

            // Create a unique token for the session and create the session
            string token = CreateSessionToken(username);
            DatabaseManager.CreateSession(user, apiKey, token, ipAddress, idleTimeout, absTimeout);

            return token;
        }

        /// <summary>
        /// Retrieves a username from a session using the given session token
        /// and api key. This also verifies that the session is valid.
        /// </summary>
        /// <exception cref="InvalidSessionException">
        /// Thrown if the session has expired or the api key does not match
        /// </exception>
        /// <param name="sessionToken">The token for identifying the session</param>
        /// <param name="apiKey">The apikey that the request came from</param>
        /// <returns>The username from the associated valid session</returns>
        public string GetUsernameFromSession(string sessionToken, string apiKey)
        {
            // Ask the database for the session
            Session session;
            try
            {
                session = DatabaseManager.GetSession(sessionToken);
            }
            catch (ObjectNotFoundException)
            {
                throw new InvalidSessionException("The session does not exist.");
            }

            // Perform validation
            // Are the timeouts in the future?
            
            if (session.AbsoluteTimeout <= DateTime.Now || session.IdleTimeout <= DateTime.Now) 
                throw new InvalidSessionException("The session has expired.");
            
            // Is the API key the same?
            if (session.ApiKey != apiKey)
                throw new InvalidSessionException("The session's apikey does not match the given api key.");

            // We're good to go!
            return session.Username;
        }

        #region Private Methods

        /// <summary>
        /// Creates a password hash, suitable for storing in the database, or
        /// for validating a login.
        /// </summary>
        /// <param name="email">The email of the user, used for the salt.</param>
        /// <param name="password">The password of the user.</param>
        /// <returns>A hashed password</returns>
        private static string CreatePasswordHash(string email, string password)
        {
            // The algo: use RIPEMD160 to hash the user's email, use that as a salt and SHA256 the whole thing
            RIPEMD160 saltHasher = new RIPEMD160Managed();
            SHA256 passHasher = new SHA256Cng();
            byte[] salt = saltHasher.ComputeHash(Encoding.Default.GetBytes(email));
            byte[] hashBytes = passHasher.ComputeHash(Encoding.Default.GetBytes(password + salt));

            // Convrt the password bytes to a string.
            // Why use the bitconverter for this and not the encoding.default.getstring?
            // Because we bitconverter gives us a hex string, instead of unintelligble
            // unicode characters.
            return BitConverter.ToString(hashBytes).Replace("-", String.Empty);
        }

        /// <summary>
        /// Generates a unique session token using a SHA256 hash
        /// </summary>
        /// <param name="username">A username to add to the token</param>
        /// <returns>A simple, 64-character session token</returns>
        private static string CreateSessionToken(string username)
        {
            // The algo: sha256 the username + the current timestamp + a new guid
            SHA256 hasher = new SHA256Cng();
            byte[] toHash = Encoding.Default.GetBytes(username + DateTime.Now + Guid.NewGuid());
            byte[] hashBytes = hasher.ComputeHash(toHash);

            return BitConverter.ToString(hashBytes).Replace("-", String.Empty);
        }

        #endregion
    }
}
