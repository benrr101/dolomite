using System;
using System.Text;

namespace DolomiteManagement.Exceptions
{
    public static class ExceptionUtilities
    {
        /// <summary>
        /// Gets all the exception messages for an aggregate exception
        /// </summary>
        /// <param name="ae">The exception to get all the messages from</param>
        /// <returns>A string with all the exception messages in it</returns>
        public static string GetAllExceptionMessages(this AggregateException ae)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("AggregateException:");
            foreach (Exception e in ae.InnerExceptions)
            {
                sb.Append(e.GetAllExceptionMessages(1));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Recursively generates a message for an exception.
        /// </summary>
        /// <param name="e">The exception to get the message from</param>
        /// <param name="indentLevel">The indentation level of the message</param>
        /// <returns>A single formatted string with all the exception messages in it.</returns>
        public static string GetAllExceptionMessages(this Exception e, int indentLevel = 0)
        {
            // Indent the message
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < indentLevel; i++)
            {
                sb.Append("  ");
            }

            // Append the message
            sb.AppendFormat("{0}: {1}", e.GetType().Name, e.Message);

            if (e.InnerException != null)
            {
                // Recurse with a deeper level of indentation
                sb.AppendLine();
                sb.AppendLine(e.GetAllExceptionMessages(indentLevel + 1));
            }

            return sb.ToString();
        }
    }
}
