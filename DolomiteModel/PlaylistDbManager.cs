using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using DolomiteModel.EntityFramework;
using Pub = DolomiteModel.PublicRepresentations;

namespace DolomiteModel
{
    public class PlaylistDbManager
    {

        #region Constants

        private const string AutoPlaylist = "auto";

        private const string StandardPlaylist = "standard";

        #endregion

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
        /// <param name="name">The name to give to the playlist</param>
        /// <param name="matchAll">Whether or not tracks must match all rules of the playlist</param>
        /// <param name="limit">The optional limit of tracks to satisfy the playlist</param>
        /// <returns>The new guid id for the playlist</returns>
        public Guid CreateAutoPlaylist(string name, bool matchAll, int? limit = null)
        {
            using (var context = new DbEntities())
            {
                // Generate a new playlist guid
                Guid guid = Guid.NewGuid();

                // Create the playlist and add to db
                Autoplaylist playlist = new Autoplaylist
                {
                    Id = guid,
                    Limit = limit,
                    MatchAll = matchAll,
                    Name = name
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
                        throw new DuplicateNameException(name);
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
        /// <returns>The new guid id for the playlist</returns>
        public Guid CreateStandardPlaylist(string name)
        {
            using (var context = new DbEntities())
            {
                // Generate the guid of the playlist
                Guid guid = Guid.NewGuid();

                // Create a new playlist and send it to the db
                Playlist playlist = new Playlist
                {
                    Id = guid,
                    Name = name
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
        /// <returns>A public-ready list of auto playlists w/o rules</returns>
        public List<Pub.Playlist> GetAllAutoPlaylists()
        {
            using (var context = new DbEntities())
            {
                return (from p in context.Autoplaylists
                    select new Pub.Playlist
                    {
                        Id = p.Id,
                        Name = p.Name
                    }).ToList();
            }
        }

        /// <summary>
        /// Creates a list of all the static playlists in the database. Does 
        /// not return the tracks of the playlist
        /// </summary>
        /// <returns>The list of static playlists without tracks</returns>
        public List<Pub.Playlist> GetAllStaticPlaylists()
        {
            using (var context = new DbEntities())
            {
                return (from p in context.Playlists
                    select new Pub.Playlist
                    {
                        Id = p.Id,
                        Name = p.Name,
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
        /// <returns>A public-ready auto playlist object.</returns>
        public Pub.AutoPlaylist GetAutoPlaylist(Guid guid)
        {
            using (var context = new DbEntities())
            {
                // Try to retrieve the playlist as an auto playlist
                Autoplaylist autoPlaylist = context.Autoplaylists.FirstOrDefault(ap => ap.Id == guid);
                if (autoPlaylist == null)
                    throw new ObjectNotFoundException(String.Format("A playlist with id {0} could not be found", guid));
                
                // Optionally build the limiter
                Pub.AutoPlaylistLimiter limiter = null;
                if (autoPlaylist.Limit.HasValue)
                {
                    limiter = new Pub.AutoPlaylistLimiter
                    {
                        Limit = autoPlaylist.Limit.Value,
                        SortDescending = autoPlaylist.SortDesc,
                        SortField =
                            autoPlaylist.SortField.HasValue ? autoPlaylist.SortFieldMetadataField.TagName : null
                    };
                }

                // Build the list of rules
                List<Pub.AutoPlaylistRule> rules =
                    autoPlaylist.AutoplaylistRules.Select(
                        r => new Pub.AutoPlaylistRule
                             {
                                 Comparison = r.Rule1.Name,
                                 Field = r.MetadataField1.TagName,
                                 Value = r.Value
                             }).ToList();

                // Put it all together
                Pub.AutoPlaylist pubAutoPlaylist = new Pub.AutoPlaylist
                {
                    Id = autoPlaylist.Id,
                    Limit = limiter,
                    MatchAll = autoPlaylist.MatchAll,
                    Name = autoPlaylist.Name,
                    Rules = rules,
                    Tracks = TrackRuleProvider.GetAutoplaylistTracks(context, autoPlaylist)
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
            using (var context = new DbEntities())
            {
                // Try to retrieve the playlist as a standard playlist
                Playlist playlist = context.Playlists.FirstOrDefault(p => p.Id == playlistGuid);
                if (playlist == null)
                    throw new ObjectNotFoundException(String.Format("A playlist with id {0} could not be found", playlistGuid));

                Pub.Playlist pubPlaylist = new Pub.Playlist
                {
                    Id = playlist.Id,
                    Name = playlist.Name,
                    Tracks = playlist.PlaylistTracks.Select(spt => spt.Track).ToList()
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
        /// <param name="playlistGuid">The guid of the playlist to add the track to</param>
        /// <param name="trackGuid">The track to add to the playlist</param>
        /// <param name="position">
        /// The order of the track in the list. If not provided, then the position
        /// will be added to end of the playlist.
        /// </param>
        public void AddTrackToPlaylist(Guid playlistGuid, Guid trackGuid, int? position = null)
        {
            using (var context = new DbEntities())
            {
                // Determine what the position should be
                if (position.HasValue)
                {
                    // Increment all the existing playlist orders
                    context.IncrementPlaylistTrackOrder(playlistGuid, position);
                }
                else
                {
                    // Grab the maximum of the tracks in the playlist
                    int? maxPos = context.PlaylistTracks.Where(pt => pt.Playlist == playlistGuid).Max(pt => pt.Order);
                    position = maxPos.HasValue ? maxPos + 1 : 1;
                }

                // Create a new record for the track->playlist
                PlaylistTrack playlistTrack = new PlaylistTrack
                {
                    Order = position.Value,
                    Playlist = playlistGuid,
                    Track = trackGuid
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

        #endregion

        #region Deletion Methods

        /// <summary>
        /// Tries to delete the playlist from the database. It doesn't matter
        /// what type of playlist it is. Non-existing playlist errors are swallowed.
        /// </summary>
        /// <remarks>
        /// Since GUIDs are supposed to be unique, we don't have to worry about
        /// accidentally deleting the wrong playlist.
        /// </remarks>
        /// <param name="playlistGuid">GUID of the playlist to delete</param>
        public void DeletePlaylist(Guid playlistGuid)
        {
            if (playlistGuid == Guid.Empty)
                return;

            using (var context = new DbEntities())
            {
                // Try to delete the playlist from the autoplaylists
                Autoplaylist autoplaylist = context.Autoplaylists.FirstOrDefault(ap => ap.Id == playlistGuid);
                if (autoplaylist != null)
                    context.Autoplaylists.Remove(autoplaylist);

                // Try to delete the playlist from the playlists
                Playlist playlist = context.Playlists.FirstOrDefault(p => p.Id == playlistGuid);
                if (playlist != null)
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
