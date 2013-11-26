using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DolomiteModel.EntityFramework;

namespace DolomiteModel
{
    internal static class TrackRuleProvider
    {

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
