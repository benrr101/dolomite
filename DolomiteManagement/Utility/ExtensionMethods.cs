using System;
using System.Linq;
using System.Text;

namespace DolomiteManagement.Utility
{
    internal static class ExtensionMethods
    {
        /// <summary>
        /// Aggregates the inner exceptions from an aggregate exception into a single message. Both
        /// the message and the strack traces will be collected into a single string, with blank
        /// lines inbetween.
        /// </summary>
        /// <param name="ex">The aggregate excpetion to collect the mess</param>
        /// <returns>The aggregated exception messages and stack traces</returns>
        public static string AggregateExceptionMessages(this AggregateException ex)
        {
            return ex.InnerExceptions.Aggregate(new StringBuilder(), (a, b) =>
            {
                if (a.Length > 0)
                {
                    a.AppendLine();
                }

                a.AppendLine(b.Message);
                a.AppendLine(b.StackTrace);
                return a;
            }).ToString();
        }
    }
}
