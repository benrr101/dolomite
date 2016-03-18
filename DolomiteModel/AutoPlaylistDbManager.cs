using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using DolomiteModel.EntityFramework;
using Pub = DolomiteModel.PublicRepresentations;

namespace DolomiteModel
{
    public sealed class AutoPlaylistDbManager
    {
        #region Properties

        /// <summary>
        /// A cache of the allowed rules organized by their datatype.
        /// </summary>
        private Dictionary<string, EntityFramework.Rule[]> _allowedMetadataRules;
        private Dictionary<string, EntityFramework.Rule[]> AllowedMetadataRules
        {
            get
            {
                // Perform the cache lookup if necessary
                if (_allowedMetadataRules == null)
                {
                    using (var context = new Entities(SqlConnectionString))
                    {
                        _allowedMetadataRules = (from r in context.Rules
                                                 group r by r.Type
                                                     into types
                                                     select new { types.Key, Value = types }).ToDictionary((t => t.Key), (t => t.Value.ToArray()));
                    }
                }

                // Returned the cached version
                return _allowedMetadataRules;
            }
        }

        /// <summary>
        /// The connection string to the database
        /// </summary>
        public static string SqlConnectionString { get; set; }

        #endregion

        #region Singleton Instance Code


        private static AutoPlaylistDbManager _instance;

        /// <summary>
        /// Singleton instance of the track database manager
        /// </summary>
        public static AutoPlaylistDbManager Instance
        {
            get { return _instance ?? (_instance = new AutoPlaylistDbManager()); }
        }

        /// <summary>
        /// Singleton constructor for the track database manager
        /// </summary>
        private AutoPlaylistDbManager() { }

        #endregion

        #region Public Methods

        #region Creation Methods

        /// <summary>
        /// Creates a new auto playlist
        /// </summary>
        /// <param name="input">The playlist to input into the database</param>
        /// <param name="owner">The username of the owner of the playlist</param>
        /// <returns>The new guid id for the playlist</returns>
        public Guid CreateAutoPlaylist(Pub.AutoPlaylist input, string owner)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Generate a new playlist guid
                // TODO: Remove, let the user provide the guid
                Guid guid = Guid.NewGuid();

                // Create the playlist and add to db
                int? sortField;
                if (input.Limit.SortField != null)
                    sortField = context.MetadataFields.First(f => f.TagName == input.Limit.SortField).Id;
                else
                    sortField = null;

                // Check to make sure that a descending bool is provided if a limiter is provided
                if (sortField.HasValue && !input.Limit.SortDescending.HasValue)
                    throw new InvalidExpressionException("A boolean value for SortDescending must be provided.");

                Autoplaylist playlist = new Autoplaylist
                {
                    GuidId = guid,
                    Limit = input.Limit.Limit,
                    MatchAll = input.MatchAll,
                    Owner = context.Users.First(u => u.Username == owner).Id,
                    Name = input.Name,
                    SortField = sortField,
                    SortDesc = input.Limit.SortDescending
                };
                context.Autoplaylists.Add(playlist);

                try
                {
                    context.SaveChanges();
                }
                catch (DbUpdateException ex)
                {
                    // Check for duplicate entry error
                    SqlException sex = ex.InnerException.InnerException as SqlException;
                    if (sex != null && sex.Number == 2601)
                    {
                        throw new DuplicateNameException(input.Name);
                    }

                    // Default to rethrowing
                    throw;
                }

                return guid;
            }
        }

        #endregion

        #region Retrieval Methods

        /// <summary>
        /// Creates a list of all the auto playlists in the database. Does not
        /// store the rules for the playlists.
        /// </summary>
        /// <param name="owner">The username of the owner playlists to select</param>
        /// <returns>A public-ready list of auto playlists w/o rules</returns>
        public List<Pub.Playlist> GetAllAutoPlaylists(string owner)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                return (from p in context.Autoplaylists
                        where p.User.Username == owner
                        select new Pub.Playlist
                        {
                            Id = p.GuidId,
                            Name = p.Name,
                            Owner = p.User.Username,
                            Type = Pub.Playlist.PlaylistType.Auto
                        }).ToList();
            }
        }

        /// <summary>
        /// Attempts to find a playlist with the given id. It checks for a standard
        /// playlist first, then an autoplaylist.
        /// </summary>
        /// <exception cref="ObjectNotFoundException">
        /// Thrown if a auto playlist with the given guid could not be found
        /// </exception>
        /// <param name="guid">The guid of the playlist to find</param>
        /// <param name="fetchTracks">
        /// Whether or not to fetch matching tracks. Setting to false can
        /// dramatically speed up playlist fetching times, but is only useful
        /// in very few situations.
        /// </param>
        /// <returns>A public-ready auto playlist object.</returns>
        public Pub.AutoPlaylist GetAutoPlaylist(Guid guid, bool fetchTracks = true)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Try to retrieve the playlist as an auto playlist
                Autoplaylist autoPlaylist = context.Autoplaylists.FirstOrDefault(ap => ap.GuidId == guid);
                if (autoPlaylist == null)
                    throw new ObjectNotFoundException(String.Format("A playlist with id {0} could not be found", guid));

                // Only build the list of rules and the list of rules if we need to
                // TODO: Create a new method that just determines if an autoplaylist exists using Any()
                Pub.AutoPlaylistLimiter limiter = null;
                List<Pub.AutoPlaylistRule> rules = null;
                IQueryable<Metadata> ownerMetadata = null;
                if (fetchTracks)
                {

                    // Optionally build the limiter
                    if (autoPlaylist.Limit.HasValue)
                    {
                        limiter = new Pub.AutoPlaylistLimiter
                        {
                            Limit = autoPlaylist.Limit.Value,
                            SortDescending = autoPlaylist.SortDesc,
                            SortField = autoPlaylist.SortField.HasValue
                                ? autoPlaylist.SortField1.TagName
                                : null
                        };
                    }

                    // Build the list of rules
                    rules = (from rule in context.AutoplaylistRules
                             select new Pub.AutoPlaylistRule
                             {
                                 Id = rule.Id,
                                 Comparison = rule.Rule1.Name,
                                 Field = rule.MetadataField1.TagName,
                                 Value = rule.Value
                             }).ToList();

                    // Build the list of metadata for the user's tracks
                    ownerMetadata = context.Metadatas.Where(m => m.Track1.Owner == autoPlaylist.Owner);
                }

                // Put it all together
                Pub.AutoPlaylist pubAutoPlaylist = new Pub.AutoPlaylist
                {
                    Id = autoPlaylist.GuidId,
                    InternalId = autoPlaylist.Id,
                    Limit = fetchTracks ? limiter : null,
                    MatchAll = autoPlaylist.MatchAll,
                    Name = autoPlaylist.Name,
                    Owner = autoPlaylist.User.Username,
                    Rules = rules,
                    Tracks = fetchTracks ? TrackRuleProvider.GetAutoplaylistTracks(ownerMetadata, autoPlaylist) : null,
                    Type = Pub.Playlist.PlaylistType.Auto
                };
                return pubAutoPlaylist;
            }
        }

        #endregion

        #region Update Methods

        /// <summary>
        /// Adds the given rule to the autoplaylist specified.
        /// </summary>
        /// <exception cref="InvalidExpressionException">Thrown when the rule is invalid</exception>
        /// <exception cref="ObjectNotFoundException">Thrown if the guid does not represent an autoplaylist</exception>
        /// <param name="playlistGuid">The guid of the autoplaylist to add the rule to</param>
        /// <param name="rule">The rule to add to the autoplaylist</param>
        public void AddRuleToAutoplaylist(Guid playlistGuid, Pub.AutoPlaylistRule rule)
        {
            // Make sure the rule is valid
            if (!IsValidRule(rule))
            {
                string message = String.Format("The rule {0} {1} {2} is invalid.", rule.Field, rule.Comparison, rule.Value);
                throw new InvalidExpressionException(message);
            }

            using (var context = new Entities(SqlConnectionString))
            {
                // Add the rule to the playlist
                Autoplaylist playlist = context.Autoplaylists.FirstOrDefault(p => p.GuidId == playlistGuid);
                if (playlist == null)
                {
                    string message =
                        String.Format("Autoplaylist with guid {0} does not exist or is not an auto playlist.",
                            playlistGuid);
                    throw new ObjectNotFoundException(message);
                }

                AutoplaylistRule newRule = new AutoplaylistRule
                {
                    Autoplaylist = playlist.Id,
                    MetadataField = context.MetadataFields.First(m => m.TagName == rule.Field).Id,
                    Rule = context.Rules.First(r => r.Name == rule.Comparison).Id,
                    Value = rule.Value
                };

                playlist.AutoplaylistRules.Add(newRule);
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Attempts to delete a rule from an autoplaylist. Despite the rule id
        /// being unique across all playlists, we make sure the playlist matches
        /// to help with avoiding cross-playlist deletions.
        /// </summary>
        /// <param name="playlist">The autoplaylist to remove the rule from</param>
        /// <param name="ruleId">The id of the rule to delete</param>
        public void DeleteRuleFromAutoplaylist(Pub.AutoPlaylist playlist, int ruleId)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Find the rule in the playlist
                var rule = context.AutoplaylistRules.FirstOrDefault(
                    r => r.Autoplaylist == playlist.InternalId && r.Id == ruleId);

                if (rule == null)
                    throw new ObjectNotFoundException("Failed to find a playlist with the given id " +
                                                      "or a rule with given id");

                context.AutoplaylistRules.Remove(rule);
                context.SaveChanges();
            }
        }

        #endregion

        #region Deletion Methods

        /// <summary>
        /// Tries to delete the playlist from the database.
        /// </summary>
        /// <param name="playlistGuid">GUID of the autoplaylist to delete</param>
        public void DeleteAutoPlaylist(Guid playlistGuid)
        {
            if (playlistGuid == Guid.Empty)
                return;

            using (var context = new Entities(SqlConnectionString))
            {
                // Try to delete the playlist from the autoplaylists
                Autoplaylist autoplaylist = context.Autoplaylists.FirstOrDefault(ap => ap.GuidId == playlistGuid);
                if (autoplaylist == null)
                    throw new ObjectNotFoundException("Autoplaylist with the given GUID not found.");

                context.Autoplaylists.Remove(autoplaylist);
                context.SaveChanges();
            }
        }

        #endregion

        #endregion

        #region Private Methods

        /// <summary>
        /// Determines if the rule is valid by comparing it to the list of valid
        /// comparison for the data type.
        /// </summary>
        /// <param name="rule">The public autoplaylist that was deserialized from the request</param>
        /// <returns>True if the rule is valid. False otherwise.</returns>
        private bool IsValidRule(Pub.AutoPlaylistRule rule)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Try to fetch the field that the rule uses
                MetadataField field = context.MetadataFields.FirstOrDefault(f => f.TagName == rule.Field);
                return field != null &&
                       AllowedMetadataRules[field.Type].Any(
                           t => t.Name.Equals(rule.Comparison, StringComparison.OrdinalIgnoreCase));
            }
        }

        #endregion
    }
}
