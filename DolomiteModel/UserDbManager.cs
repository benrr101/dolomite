using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using DolomiteModel.EntityFramework;

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
                return userKey != null && (userKey.Email == null || userKey.Email == email) && !userKey.Claimed;
            }
        }

        #endregion

        #region Update Methods

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
    }
}
