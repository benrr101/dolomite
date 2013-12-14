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
                    if (sex != null && sex.Number == 2601)
                    {
                        throw new DuplicateNameException(username);
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
    }
}
