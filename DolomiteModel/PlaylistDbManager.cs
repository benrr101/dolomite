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
    public class PlaylistDbManager
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
                // Peform the cache lookup if necessary
                if (_allowedMetadataRules == null)
                {
                    using (var context = new DbEntities())
                    {
                        _allowedMetadataRules = (from r in context.Rules
                            group r by r.Type
                            into types
                            select new {types.Key, Value = types}).ToDictionary((t=> t.Key), (t => t.Value.ToArray()));
                    }
                }

                // Returned the cached version
                return _allowedMetadataRules;
            }
        }

        #endregion

        #region Singleton Instance Code

        private static PlaylistDbManager _instance;

        /// <summary>
        /// Singleton instance of the track database manager
        /// </summary>
        public static PlaylistDbManager Instance
        {
            get { return _instance ?? (_instance = new PlaylistDbManager()); }
        }

        /// <summary>
        /// Singleton constructor for the track database manager
        /// </summary>
        private PlaylistDbManager() { }

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
            using (var context = new DbEntities())
            {
                // Generate a new playlist guid
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
                    Id = guid,
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

        /// <summary>
        /// Creates a new standard playlist
        /// </summary>
        /// <param name="name">The name to give to the playlist</param>
        /// <param name="owner">The username of the owner of the playlist</param>
        /// <returns>The new guid id for the playlist</returns>
        public Guid CreateStandardPlaylist(string name, string owner)
        {
            using (var context = new DbEntities())
            {
                // Generate the guid of the playlist
                Guid guid = Guid.NewGuid();

                // Create a new playlist and send it to the db
                Playlist playlist = new Playlist
                {
                    Id = guid,
                    Name = name,
                    User = context.Users.First(u => u.Username == owner)
                };
                context.Playlists.Add(playlist);
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
                        throw new DuplicateNameException(name);
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
            using (var context = new Entities())
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
        /// Creates a list of all the static playlists in the database. Does 
        /// not return the tracks of the playlist
        /// </summary>
        /// <param name="owner">The username of the owner playlists to select</param>
        /// <returns>The list of static playlists without tracks</returns>
        public List<Pub.Playlist> GetAllStaticPlaylists(string owner)
        {
            using (var context = new Entities())
            {
                return (from p in context.Playlists
                        where p.User.Username == owner
                        select new Pub.Playlist
                        {
                            Id = p.GuidId,
                            Name = p.Name,
                            Owner = p.User.Username,
                            Type = Pub.Playlist.PlaylistType.Static
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
            using (var context = new Entities())
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

        /// <summary>
        /// Attempts to find a static playlist with the given id.
        /// <exception cref="ObjectNotFoundException">
        /// Thrown if a static playlist with the given guid could not be found
        /// </exception>
        /// </summary>
        /// <param name="playlistGuid">The guid of the playlist to find</param>
        /// <returns>A public-ready static playlist object</returns>
        public Pub.Playlist GetStaticPlaylist(Guid playlistGuid)
        {
            using (var context = new Entities())
            {
                // Try to retrieve the playlist as a standard playlist
                Playlist playlist = context.Playlists.FirstOrDefault(p => p.GuidId == playlistGuid);
                if (playlist == null)
                    throw new ObjectNotFoundException(String.Format("A playlist with id {0} could not be found", playlistGuid));

                Pub.Playlist pubPlaylist = new Pub.Playlist
                {
                    Id = playlist.GuidId,
                    InternalId = playlist.Id,
                    Name = playlist.Name,
                    Owner = playlist.User.Username,
                    Tracks = playlist.PlaylistTracks.OrderBy(spt => spt.Order).Select(spt => spt.Track1.GuidId).ToList(),
                    Type = Pub.Playlist.PlaylistType.Static
                };
                return pubPlaylist;
            }
        }

        #endregion

        #region Update Methods

        /// <summary>
        /// Adds the track to the playlist. If necessary the order of the
        /// tracks will be updated.
        /// </summary>
        /// <param name="playlist">The playlist to add the track to</param>
        /// <param name="track">The track to add to the playlist</param>
        /// <param name="position">
        /// The order of the track in the list. If not provided, then the position
        /// will be added to end of the playlist.
        /// </param>
        public void AddTrackToPlaylist(Pub.Playlist playlist, Pub.Track track, int? position = null)
        {
            using (var context = new Entities())
            {
                // Determine what the position should be
                if (position.HasValue)
                {
                    // Increment all the existing playlist orders
                    context.IncrementPlaylistTrackOrder(playlist.InternalId, position);
                }
                else
                {
                    // Grab the maximum of the tracks in the playlist
                    int? maxPos = context.PlaylistTracks.Where(pt => pt.Playlist == playlist.InternalId).Max(pt => pt.Order);
                    position = maxPos.HasValue ? maxPos + 1 : 1;
                }

                // Create a new record for the track->playlist
                PlaylistTrack playlistTrack = new PlaylistTrack
                {
                    Order = position.Value,
                    Playlist = playlist.InternalId,
                    Track = track.InternalId
                };
                context.PlaylistTracks.Add(playlistTrack);
                context.SaveChanges();
            }
        }

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

            using (var context = new DbEntities())
            {
                // Add the rule to the playlist
                Autoplaylist playlist = context.Autoplaylists.FirstOrDefault(p => p.Id == playlistGuid);
                if (playlist == null)
                {
                    string message =
                        String.Format("Autoplaylist with guid {0} does not exist or is not an auto playlist.",
                            playlistGuid);
                    throw new ObjectNotFoundException(message);
                }

                AutoplaylistRule newRule = new AutoplaylistRule
                {
                    Autoplaylist = playlistGuid,
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
        /// <param name="playlistGuid">The guid of the autoplaylist</param>
        /// <param name="ruleId">The id of the rule to delete</param>
        public void DeleteRuleFromAutoplaylist(Guid playlistGuid, int ruleId)
        {
            using (var context = new DbEntities())
            {
                // Find the rule in the playlist
                var rule = context.AutoplaylistRules.FirstOrDefault(
                    r => r.Autoplaylist == playlistGuid && r.Id == ruleId);

                if(rule == null)
                    throw new ObjectNotFoundException("Failed to find a playlist with the given id " +
                                                      "or a rule with given id");

                context.AutoplaylistRules.Remove(rule);
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Deletes a track from a static playlist. Also decrements the order 
        /// of the tracks in the playist to keep the indices correct.
        /// </summary>
        /// <param name="playlistGuid">GUID of the playlist to remove the track from</param>
        /// <param name="trackId">ID of the track to remove from the playlist</param>
        public void DeleteTrackFromPlaylist(Guid playlistGuid, int trackId)
        {
            using (var context = new DbEntities())
            {
                // Find the playlist<->track object
                var playlistTrack =
                    context.PlaylistTracks.FirstOrDefault(t => t.Playlist == playlistGuid && t.Order == trackId);
                if (playlistTrack == null)
                    throw new ObjectNotFoundException("Failed to find track in playlist.");

                // Temp store the order for future processes
                var order = playlistTrack.Order;

                // Delete the track from the playlist
                context.PlaylistTracks.Remove(playlistTrack);
                context.SaveChanges();

                context.DecrementPlaylistTrackOrder(playlistGuid, order);
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

            using (var context = new DbEntities())
            {
                // Try to delete the playlist from the autoplaylists
                Autoplaylist autoplaylist = context.Autoplaylists.FirstOrDefault(ap => ap.Id == playlistGuid);
                if (autoplaylist == null)
                    throw new ObjectNotFoundException("Autoplaylist with the given GUID not found.");

                context.Autoplaylists.Remove(autoplaylist);
                context.SaveChanges();
            } 
        }

        /// <summary>
        /// Tries to delete a static playlist from the database.
        /// </summary>
        /// <param name="playlistGuid">GUID of static playlist to delete</param>
        public void DeleteStaticPlaylist(Guid playlistGuid)
        {
            if (playlistGuid == Guid.Empty)
                return;

            using (var context = new DbEntities())
            {
                // Try to delete the static playlist from the playlists
                Playlist playlist = context.Playlists.FirstOrDefault(p => p.Id == playlistGuid);
                if (playlist == null)
                    throw new ObjectNotFoundException("Static playlist with the given GUID not found.");
                    
                context.Playlists.Remove(playlist);
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
            using (var context = new DbEntities())
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
