using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DolomiteModel.EntityFramework;

namespace DolomiteModel
{
    internal static class TrackRuleProvider
    {

        /// <summary>
        /// Retrieves a list of track guids that match the rules set forth in
        /// the autoplaylist. Utilizes combination of LINQ queries.
        /// </summary>
        /// <param name="entities">Instance of the database to perform the actions</param>
        /// <param name="playlist">The playlist to find the tracks for</param>
        /// <returns>A list of guids for tracks that match the list of rules</returns>
        public static List<Guid> GetAutoplaylistTracks(DbEntities entities, Autoplaylist playlist)
        {
            // Iterate over the rules in the list
            List<IEnumerable<Guid>> trackProviders = new List<IEnumerable<Guid>>();
            foreach (AutoplaylistRule rule in playlist.AutoplaylistRules)
            {
                // Build a query for the rule
                switch (rule.MetadataField1.Type)
                {
                    case "string":
                        trackProviders.Add(GetStringTrackProvider(entities, rule));
                        break;
                    case "numeric":
                        trackProviders.Add(GetNumericTrackProvider(entities, rule));
                        break;
                    case "date":
                        trackProviders.Add(GetDateTrackProvider(entities, rule));
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
            return ConcatenateProviders(trackProviders).ToList();
        }

        /// <summary>
        /// Performs the query building for rules that use a string field as
        /// as their comparison field.
        /// </summary>
        /// <param name="entities">A reference to the database entities.</param>
        /// <param name="rule">The rule to perform the comparison to</param>
        /// <returns>An enumerable list of GUIDs for tracks that match the rule.</returns>
        private static IEnumerable<Guid> GetStringTrackProvider(DbEntities entities, AutoplaylistRule rule)
        {
            // Sanity check
            if (rule.Rule1.Type != "string")
            {
                var message = String.Format("String track provider cannot be used process rule with datetype {0}",
                    rule.Rule1.Type);
                throw new InvalidDataException(message);
            }

            // Basis of the query requires the fields to match
            IQueryable<Metadata> query = entities.Metadatas.Where(m => m.Field == rule.MetadataField);

            // Build the query
            switch (rule.Rule1.Name)
            {
                case "contains":
                    query = query.Where(m => m.Value.IndexOf(rule.Value, StringComparison.CurrentCultureIgnoreCase) >= 0);
                    break;

                case "notcontains":
                    query = query.Where(m => m.Value.IndexOf(rule.Value, StringComparison.CurrentCultureIgnoreCase) < 0);
                    break;

                case "sequals":
                    query = query.Where(m => m.Value.Equals(rule.Value, StringComparison.CurrentCultureIgnoreCase));
                    break;

                case "snotequal":
                    query = query.Where(m => !m.Value.Equals(rule.Value, StringComparison.CurrentCultureIgnoreCase));
                    break;

                case "startswith":
                    query = query.Where(m => m.Value.StartsWith(rule.Value, StringComparison.CurrentCultureIgnoreCase));
                    break;

                case "endswith":
                    query = query.Where(m => m.Value.EndsWith(rule.Value, StringComparison.CurrentCultureIgnoreCase));
                    break;
            }

            return query.Select(m => m.Track);
        }

        /// <summary>
        /// Performs the query building for rules that use a numeric field as
        /// as their comparison field. Utilizes a cast to decimal, regardless of int vs. decimal.
        /// </summary>
        /// <param name="entities">A reference to the database entities.</param>
        /// <param name="rule">The rule to perform the comparison to</param>
        /// <returns>An enumerable list of GUIDs for tracks that match the rule.</returns>
        private static IEnumerable<Guid> GetNumericTrackProvider(DbEntities entities, AutoplaylistRule rule)
        {
            // Sanity check
            if (rule.Rule1.Type != "numeric")
            {
                var message = String.Format("Numeric track provider cannot be used process rule with datetype {0}",
                    rule.Rule1.Type);
                throw new InvalidDataException(message);
            }

            // Basis of the query requires the fields to match
            IQueryable<Metadata> query = entities.Metadatas.Where(m => m.Field == rule.MetadataField);

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
            return query.Select(m => m.Track);
        }

        /// <summary>
        /// Performs the query building for rules that use a string field as
        /// as their comparison field. Uses a cast to int since dates are stored
        /// as unix epoch times.
        /// </summary>
        /// <param name="entities">A reference to the database entities.</param>
        /// <param name="rule">The rule to perform the comparison to</param>
        /// <returns>An enumerable list of GUIDs for tracks that match the rule.</returns>
        private static IEnumerable<Guid> GetDateTrackProvider(DbEntities entities, AutoplaylistRule rule)
        {
            // Sanity check
            if (rule.Rule1.Type != "date")
            {
                var message = String.Format("Date track provider cannot be used process rule with datetype {0}",
                    rule.Rule1.Type);
                throw new InvalidDataException(message);
            }

            // Base of the query requires the fields to match
            IQueryable<Metadata> query = entities.Metadatas.Where(m => m.Field == rule.MetadataField);

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
            return query.Select(m => m.Track);
        } 

        /// <summary>
        /// Performs concatenation of the rules to generate a unique list of
        /// track guids that match the rules of the playlist. For "any" 
        /// playlists, this is performed using unions. For "all" playlists, 
        /// this is performed using intersects. 
        /// </summary>
        /// <param name="providers">The list of queries that generate matching tracks</param>
        /// <returns>An enumerable list of unique guids of matching tracks</returns>
        private static IEnumerable<Guid> ConcatenateProviders(List<IEnumerable<Guid>> providers)
        {
            var iterator = providers.GetEnumerator();
            if (!iterator.MoveNext() || iterator.Current == null)
            {
                return null;
            }

            IEnumerable<Guid> concatenatedProviders = iterator.Current;
            while (iterator.MoveNext() && iterator.Current != null)
            {
                concatenatedProviders = concatenatedProviders.Intersect(iterator.Current);
            }

            return concatenatedProviders;
        } 

    }
}
