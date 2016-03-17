using System;
using System.Data.Entity.Core.EntityClient;

namespace DolomiteModel.EntityFramework
{
    public partial class Entities
    {
        #region Connection String Generator

        /// <summary>
        /// Metadata for the EF model to connect against
        /// </summary>
        private const string Metadata = @"res://*/EntityFramework.DbEntities.csdl"
                                        + @"|res://*/EntityFramework.DbEntities.ssdl"
                                        + @"|res://*/EntityFramework.DbEntities.msl";

        /// <summary>
        /// The EF provider name
        /// </summary>
        private const string Provider = @"System.Data.SqlClient"; //@"System.Data.EntityClient";

        /// <summary>
        /// Generates an entity framework connection string for the database based on the
        /// connection string that was provided and well known metadata files.
        /// </summary>
        /// <param name="connectionString">The SQL connection string to use</param>
        /// <returns>A connection string for connecting to the database with EF</returns>
        internal static string GetConnectionString(string connectionString)
        {
            // Sanity check the connection string
            if (String.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(@"connectionString");
            }

            // Build the connection string
            EntityConnectionStringBuilder builder = new EntityConnectionStringBuilder
            {
                Metadata = Metadata,
                Provider = Provider,
                ProviderConnectionString = connectionString
            };

            return builder.ToString();
        }

        #endregion

        /// <summary>
        /// Overload for creating entities that supports providing the connection string.
        /// </summary>
        /// <param name="connectionString">A SQL connection string</param>
        public Entities(string connectionString) 
            : base(GetConnectionString(connectionString))
        {
        }
    }
}
