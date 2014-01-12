using System;
using System.Data;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using DolomiteModel.EntityFramework;
using Pub = DolomiteModel.PublicRepresentations;

namespace DolomiteModel
{
    public class UserDbManager
    {
        #region Singleton Instance Code

        private static UserDbManager _instance;

        /// <summary>
        /// Singleton instance of the user database manager
        /// </summary>
        public static UserDbManager Instance
        {
            get { return _instance ?? (_instance = new UserDbManager()); }
        }

        /// <summary>
        /// Singleton constructor for the user database manager
        /// </summary>
        private UserDbManager() { }

        #endregion

        #region Creation Methods

        /// <summary>
        /// Attempts to create a new session in the database using all the
        /// information provided.
        /// </summary>
        /// <param name="user">The user to associate the session with. User MUST exist.</param>
        /// <param name="apiKey">The API key to associate the session with</param>
        /// <param name="token">The token for identifying the session</param>
        /// <param name="ipAddress">The IP address that initialized the session</param>
        /// <param name="idleExpire">The time to timeout the session if no actions are taken</param>
        /// <param name="absExpire">The time to timeout the session regardless of actions taken</param>
        public void CreateSession(Pub.User user, string apiKey, string token, string ipAddress, DateTime idleExpire, DateTime absExpire)
        {
            using (var context = new DbEntities())
            {
                // Create a new Session object
                Session session = new Session
                {
                    AbsoluteTimeout = absExpire,
                    ApiKey = context.ApiKeys.First(a => a.Key == apiKey).Id,
                    IdleTimeout = idleExpire,
                    Token = token,
                    InitialIP = ipAddress,
                    User = context.Users.First(u => u.Username == user.Username).Id,
                };

                context.Sessions.Add(session);
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Creates a new user in the database using the user's username, email,
        /// and hashed password. Special error handling is done to detect
        /// duplicate usernames or emails.
        /// </summary>
        /// <exception cref="DuplicateNameException">
        /// Thrown if the username or the email is a duplicate of another user.
        /// The message will say which field was duplicate, so it may be used
        /// as-is when generating user-facing error messages.
        /// </exception>
        /// <param name="username">The username selected by the user. Must not be duplicate.</param>
        /// <param name="passwordHash">The hashed password for the user. Should be unique to all users.</param>
        /// <param name="email">The user's email. Must not be duplicate.</param>
        public void CreateUser(string username, string passwordHash, string email)
        {
            using (var context = new DbEntities())
            {
                // Create the new user object
                User user = new User
                {
                    Username = username,
                    PasswordHash = passwordHash,
                    Email = email
                };

                try
                {
                    // Attempt to add the user
                    context.Users.Add(user);
                    context.SaveChanges();
                }
                catch (DbUpdateException ex)
                {
                    // Check for duplicate entry error
                    SqlException sex = ex.InnerException.InnerException as SqlException;
                    if (sex != null && sex.Number == 2627 && sex.Message.Contains("USERNAME"))
                    {
                        throw new DuplicateNameException("The username provided is already being used. Please choose another one.");
                    } 
                    if (sex != null && sex.Number == 2627 && sex.Message.Contains("EMAIL"))
                    {
                        throw new DuplicateNameException("The email address provided is already being used. Please use another one.");
                    }

                    // Default to rethrowing
                    throw;
                }
            }
        }

        #endregion

        #region Retrieval Methods

        /// <summary>
        /// Retrieves a user from the database, based on the username provided
        /// </summary>
        /// <param name="username">The username of the user to retrieve</param>
        /// <returns>A user object if the user exists, null if the user doesn't exist</returns>
        public Pub.User GetUserByUsername(string username)
        {
            using (var context = new DbEntities())
            {
                // Find the matching user
                return (from u in context.Users
                    where u.Username == username
                    select new Pub.User
                    {
                        Email = u.Email,
                        PasswordHash = u.PasswordHash,
                        Username = u.Username
                    }).FirstOrDefault();
            }
        }

        /// <summary>
        /// Grabs a session object using the given token
        /// </summary>
        /// <exception cref="ObjectNotFoundException">
        /// Thrown if the session does not exist.
        /// </exception>
        /// <param name="token">The session token</param>
        /// <returns>A public representation of the session</returns>
        public Pub.Session GetSession(string token)
        {
            using (var context = new DbEntities())
            {
                // Find the matching session
                Pub.Session session = (from s in context.Sessions
                    where s.Token == token
                    select new Pub.Session
                    {
                        AbsoluteTimeout = s.AbsoluteTimeout,
                        ApiKey = s.ApiKey1.Key,
                        IdleTimeout = s.IdleTimeout,
                        InitialIpAddress = s.InitialIP,
                        InitializedTime = s.InitializedTime,
                        Token = s.Token,
                        Username = s.User1.Username
                    }).FirstOrDefault();

                if (session == null)
                    throw new ObjectNotFoundException("A session with the given session token cannot be found.");

                // Fetch the user
                return session;
            }
        }

        /// <summary>
        /// Validates an API key. Essentially it just checks to see if an api
        /// key with the given string exists.
        /// </summary>
        /// <param name="key">A 64-character hex API key</param>
        /// <returns>True if the key exists, false otherwise</returns>
        public bool ValidateApiKey(string key)
        {
            using (var context = new DbEntities())
            {
                // Find the matching API key
                var apiKey = context.ApiKeys.FirstOrDefault(k => k.Key == key);

                // Key must exist
                return apiKey != null;
            }
        } 

        /// <summary>
        /// Validates a user sign-up key. It checks for a matching key based on
        /// the GUID provided. It will also validate if the key has been
        /// claimed and whether or not the email requirements are met.
        /// </summary>
        /// <param name="key">The key for the user sign-up key</param>
        /// <param name="email">The email used to sign up</param>
        /// <returns>True if the key is valid, false otherwise</returns>
        public bool ValidateUserKey(Guid key, string email)
        {
            using (var context = new DbEntities())
            {
                // Find the matching user key
                var userKey = context.UserKeys.FirstOrDefault(k => k.Id == key);

                // Key must:
                // 1) exist
                // 2) either have matching email or no email requirement
                // 3) not be claimed
                return userKey != null && (String.IsNullOrWhiteSpace(userKey.Email) || userKey.Email == email) && !userKey.Claimed;
            }
        }

        #endregion

        #region Update Methods

        /// <summary>
        /// Sets the absolute timeout for a given session
        /// </summary>
        /// <param name="sessionToken">The token for identifying the session</param>
        /// <param name="newTimeout">The new time to set the absolute timeout to</param>
        public void SetSessionAbsoluteTimeout(string sessionToken, DateTime newTimeout)
        {
            using (var context = new DbEntities())
            {
                // Fetch the session
                Session session = context.Sessions.FirstOrDefault(s => s.Token == sessionToken);
                if (session == null)
                    throw new ObjectNotFoundException(String.Format("Session with token {0} not found.", sessionToken));

                // Update the absolute timeout
                session.AbsoluteTimeout = newTimeout;
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Sets the idle timeout for a given session
        /// </summary>
        /// <param name="sessionToken">The token for identifying the session</param>
        /// <param name="newTimeout">The new time to set the idle timeout to</param>
        public void SetSessionIdleTimeout(string sessionToken, DateTime newTimeout)
        {
            using (var context = new DbEntities())
            {
                // Fetch the session
                Session session = context.Sessions.FirstOrDefault(s => s.Token == sessionToken);
                if (session == null)
                    throw new ObjectNotFoundException(String.Format("Session with token {0} not found.", sessionToken));

                // Update the absolute timeout
                session.IdleTimeout = newTimeout;
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Sets the claim status on the user auth key.
        /// </summary>
        /// <exception cref="NullReferenceException">
        /// Thrown if the user auth key could not be found.
        /// </exception>
        /// <param name="key">The key to set the status of</param>
        /// <param name="value">The value to set the claim status to.</param>
        public void SetUserKeyClaimStatus(Guid key, bool value)
        {
            using (var context = new DbEntities())
            {
                // Fetch the user key
                var userKey = context.UserKeys.FirstOrDefault(k => k.Id == key);
                if (userKey == null)
                    throw new NullReferenceException("Could not find user key with given guid.");

                // Claim it
                userKey.Claimed = value;
                context.SaveChanges();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Validates a given session based on the following criteria:
        /// 1) Both absolute and idle timeouts must be in the future.
        /// 2) The API key for the current request must be the same as the initial one
        /// </summary>
        /// <param name="session">The entity for the session to validate</param>
        /// <param name="apikey">The API key for the current request</param>
        /// <returns>True if the session is valid, false otherwise</returns>
        private bool ValidateSession(Session session, string apikey)
        {
            // Are the timeouts in the future?
            if (session.AbsoluteTimeout <= DateTime.Now || session.IdleTimeout <= DateTime.Now)
                return false;

            // Is the API key the same?
            if (session.ApiKey1.Key != apikey)
                return false;

            // We're good to go!
            return true;
        }

        #endregion

    }
}
