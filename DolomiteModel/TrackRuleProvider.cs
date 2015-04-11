using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DolomiteModel.EntityFramework;

namespace DolomiteModel
{
    internal static class TrackRuleProvider
    {

        /// <summary>
        /// Retrieves a list of track guids that match the rules set forth in
        /// the autoplaylist. Utilizes combination of LINQ queries.
        /// </summary>
        /// <param name="metadatas">A collection of metadatas to compare</param>
        /// <param name="playlist">The playlist to find the tracks for</param>
        /// <returns>A list of guids for tracks that match the list of rules</returns>
        public static List<Guid> GetAutoplaylistTracks(IQueryable<Metadata> metadatas, Autoplaylist playlist)
        {
            // Iterate over the rules in the list
            List<IQueryable<Track>> trackProviders = new List<IQueryable<Track>>();
            foreach (AutoplaylistRule rule in playlist.AutoplaylistRules)
            {
                // Build a query for the rule
                switch (rule.MetadataField1.Type)
                {
                    case "string":
                        trackProviders.Add(GetStringTrackProvider(metadatas, rule));
                        break;
                    case "numeric":
                        trackProviders.Add(GetNumericTrackProvider(metadatas, rule));
                        break;
                    case "date":
                        trackProviders.Add(GetDateTrackProvider(metadatas, rule));
                        break;
                    default:
                        var message = String.Format("Metadata type '{0}' for field {1} is not supported. " +
                                                    "You may need to confirm that the metadatafields table " +
                                                    "is properly initialized.", rule.Rule1.Type,
                                                    rule.MetadataField1.DisplayName);
                        throw new InvalidDataException(message);
                }
            }

            // Concatenate together
            var tracksProvider = ConcatenateProviders(trackProviders, playlist.MatchAll);

            // Apply the limiter if necessary
            IQueryable<Track> tracks = playlist.Limit.HasValue
                ? ApplyLimiter(tracksProvider, playlist.Limit.Value, playlist.SortDesc, playlist.SortField1)
                : tracksProvider;

            // If the playlist is limited, then sort
            return tracks.Select(t => t.GuidId).ToList();
        }

        /// <summary>
        /// Performs the query building for rules that use a string field as
        /// as their comparison field.
        /// </summary>
        /// <param name="metadatas">A collection of metadatas to compare</param>
        /// <param name="rule">The rule to perform the comparison to</param>
        /// <returns>An queryable list of GUIDs for tracks that match the rule.</returns>
        private static IQueryable<Track> GetStringTrackProvider(IQueryable<Metadata> metadatas, AutoplaylistRule rule)
        {
            // Sanity check
            if (rule.Rule1.Type != "string")
            {
                var message = String.Format("String track provider cannot be used process rule with datetype {0}",
                    rule.Rule1.Type);
                throw new InvalidDataException(message);
            }

            // Basis of the query requires the fields to match
            IQueryable<Metadata> query = metadatas.Where(m => m.Field == rule.MetadataField);

            // Build the query
            switch (rule.Rule1.Name)
            {
                case "contains":
                    query = query.Where(m => m.Value.ToUpper().Contains(rule.Value.ToUpper()));
                    break;

                case "notcontains":
                    query = query.Where(m => !m.Value.ToUpper().Contains(rule.Value.ToUpper()));
                    break;

                case "sequals":
                    query = query.Where(m => m.Value.Equals(rule.Value, StringComparison.CurrentCultureIgnoreCase));
                    break;

                case "snotequal":
                    query = query.Where(m => !m.Value.Equals(rule.Value, StringComparison.CurrentCultureIgnoreCase));
                    break;

                case "startswith":
                    query = query.Where(m => m.Value.ToUpper().StartsWith(rule.Value.ToUpper()));
                    break;

                case "endswith":
                    query = query.Where(m => m.Value.ToUpper().EndsWith(rule.Value.ToUpper()));
                    break;
            }

            return query.Select(m => m.Track1);
        }

        /// <summary>
        /// Performs the query building for rules that use a numeric field as
        /// as their comparison field. Utilizes a cast to decimal, regardless of int vs. decimal.
        /// </summary>
        /// <param name="metadatas">A collection of metadatas to compare</param>
        /// <param name="rule">The rule to perform the comparison to</param>
        /// <returns>An queryable list of GUIDs for tracks that match the rule.</returns>
        private static IQueryable<Track> GetNumericTrackProvider(IQueryable<Metadata> metadatas , AutoplaylistRule rule)
        {
            // Sanity check
            if (rule.Rule1.Type != "numeric")
            {
                var message = String.Format("Numeric track provider cannot be used process rule with datetype {0}",
                    rule.Rule1.Type);
                throw new InvalidDataException(message);
            }

            // Basis of the query requires the fields to match
            IQueryable<Metadata> query = metadatas.Where(m => m.Field == rule.MetadataField);

            // Attempt to cast the rule's value to a decimal
            decimal ruleDec;
            if (!Decimal.TryParse(rule.Value, out ruleDec))
            {
                string message = String.Format("The value for numeric comparison {0} is not a valid number.", rule.Value);
                throw new InvalidDataException(message);
            }

            // Build the query
            switch (rule.Rule1.Name)
            {
                case "greaterthan":
                    query = query.Where(m => ConversionUtilities.ConvertToDecimal(m.Value) > ruleDec);
                    break;

                case "lessthan":
                    query = query.Where(m => ConversionUtilities.ConvertToDecimal(m.Value) < ruleDec);
                    break;

                case "greaterthanequal":
                    query = query.Where(m => ConversionUtilities.ConvertToDecimal(m.Value) >= ruleDec);
                    break;

                case "lessthanequal":
                    query = query.Where(m => ConversionUtilities.ConvertToDecimal(m.Value) <= ruleDec);
                    break;

                case "equal":
                    query = query.Where(m => ConversionUtilities.ConvertToDecimal(m.Value) == ruleDec);
                    break;

                case "notequal":
                    query = query.Where(m => ConversionUtilities.ConvertToDecimal(m.Value) != ruleDec);
                    break;
            }
            return query.Select(m => m.Track1);
        }

        /// <summary>
        /// Performs the query building for rules that use a string field as
        /// as their comparison field. Uses a cast to int since dates are stored
        /// as unix epoch times.
        /// </summary>
        /// <param name="metadatas">A collection of metadatas to compare</param>
        /// <param name="rule">The rule to perform the comparison to</param>
        /// <returns>An queryable list of GUIDs for tracks that match the rule.</returns>
        private static IQueryable<Track> GetDateTrackProvider(IQueryable<Metadata> metadatas, AutoplaylistRule rule)
        {
            // Sanity check
            if (rule.Rule1.Type != "date")
            {
                var message = String.Format("Date track provider cannot be used process rule with datetype {0}",
                    rule.Rule1.Type);
                throw new InvalidDataException(message);
            }

            // Base of the query requires the fields to match
            IQueryable<Metadata> query = metadatas.Where(m => m.Field == rule.MetadataField);

            // Dates are stored as UNIX timestamps, so cast to int
            // This is subject to the 2038 problem
            int ruleInt;
            if (!Int32.TryParse(rule.Value, out ruleInt))
            {
                string message = String.Format("The value for date comparison {0} is not a valid unix timestamp.", rule.Value);
                throw new InvalidDataException(message);
            }

            // Useful to calculate today in unix time
            int today = (int) Math.Round((DateTime.Today - new DateTime(1970, 1, 1).ToLocalTime()).TotalSeconds);
            int lastNDays = today - (ruleInt*24*60*60);

            // Build the query
            switch (rule.Rule1.Name)
            {
                case "dequal":
                    query = query.Where(m => ConversionUtilities.ConvertToInt32(m.Value) == ruleInt);
                    break;

                case "dnotequal":
                    query = query.Where(m => ConversionUtilities.ConvertToInt32(m.Value) != ruleInt);
                    break;

                case "isafter":
                    query = query.Where(m => ConversionUtilities.ConvertToInt32(m.Value) > ruleInt);
                    break;

                case "isbefore":
                    query = query.Where(m => ConversionUtilities.ConvertToInt32(m.Value) < ruleInt);
                    break;

                case "inlastdays":
                    query = query.Where(m => ConversionUtilities.ConvertToInt32(m.Value) >= lastNDays);
                    break;

                case "notinlastdays":
                    query = query.Where(m => ConversionUtilities.ConvertToInt32(m.Value) < lastNDays);
                    break;
            }
            return query.Select(m => m.Track1);
        } 

        /// <summary>
        /// Performs concatenation of the rules to generate a unique list of
        /// track guids that match the rules of the playlist. For "any" 
        /// playlists, this is performed using unions. For "all" playlists, 
        /// this is performed using intersects. 
        /// </summary>
        /// <param name="providers">The list of queries that generate matching tracks</param>
        /// <param name="matchAll">Whether to intersect the matches or union them</param>
        /// <returns>An enumerable list of unique guids of matching tracks</returns>
        private static IQueryable<Track> ConcatenateProviders(List<IQueryable<Track>> providers, bool matchAll)
        {
            var iterator = providers.GetEnumerator();
            if (!iterator.MoveNext() || iterator.Current == null)
            {
                return null;
            }

            var concatenatedProviders = iterator.Current;
            while (iterator.MoveNext() && iterator.Current != null)
            {
                concatenatedProviders = matchAll
                    ? concatenatedProviders.Intersect(iterator.Current)
                    : concatenatedProviders.Union(iterator.Current);
            }

            return concatenatedProviders;
        }

        /// <summary>
        /// Applies the rules of a limiter for a auto playlist. Will randomize
        /// the playlist if the sort field is null. Will generate a LINQ query to
        /// determine the sorting of the tracks based on the metadata field
        /// specified. In any case, the maximum number of tracks in the playlist
        /// will be equal to the limit.
        /// </summary>
        /// <remarks>
        /// For right now, only tracks that contain the metadata field used for
        /// sorting are returned. This is because the logic for appending tracks
        /// that don't have the sorting metadata field is clunky and generally
        /// requires enumerating the IQueryable, hence it is poor performance.
        /// TODO: Decide if we need to add this logic back in.
        /// </remarks>
        /// <param name="providers">The linq query that provide the tracks</param>
        /// <param name="limit">The number of tracks to limit to</param>
        /// <param name="sortDesc">
        /// Whether or not to sort the playlist in descending order. Should be
        /// null if sorting randomly. Must not be null if sorting by a field.
        /// </param>
        /// <param name="sortField">
        /// The metadata field to sort by. If null, the playlist will be 
        /// randomly sorted.
        /// </param>
        /// <returns>A LINQ query of sorted tracks</returns>
        private static IQueryable<Track> ApplyLimiter(IQueryable<Track> providers, int limit, bool? sortDesc,
            MetadataField sortField)
        {
            // Should it be randomized?
            if (sortField == null)
            {
                // Randomize the list and return it
                // Source of randomizing model: http://stackoverflow.com/a/3169165
                return providers.OrderBy(q => Guid.NewGuid()).Take(limit);
            }

            if (!sortDesc.HasValue)
            {
                throw new InvalidFilterCriteriaException(
                    "The sort descending field cannot be null if there is a non-random limiter applied.");
            }

            // Do not randomize it. Sort it based on the selected field
            //IQueryable<Track> sorted = providers.Where(t => t.Metadatas.Any(md => md.Field == sortField.Id));
            //var sorted = entities.Metadatas.Where(
            //    md => providers.Contains(md.Track) && md.MetadataField.Id == sortField.Id);
            var sortingMetadata = providers.SelectMany(p => p.Metadatas).Where(m => m.Field == sortField.Id);

            // Order by ascending or descending and corresponding tracks
            return sortDesc.Value
                ? sortingMetadata.OrderByDescending(m => m.Value).Select(m => m.Track1).Take(limit)
                : sortingMetadata.OrderBy(m => m.Value).Select(m => m.Track1).Take(limit);
        }
    }
}
