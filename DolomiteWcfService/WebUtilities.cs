using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Text;
using Newtonsoft.Json;

namespace DolomiteWcfService
{
    internal static class WebUtilities
    {
        /// <summary>
        /// Common message for internal server errors
        /// </summary>
        public const string InternalServerMessage = "An internal server error occurred";

        #region Request Getters

        public static string GetContentType()
        {
            // Make sure there is a current operation context to use
            if (WebOperationContext.Current == null)
            {
                throw new CommunicationException(
                    "The current web operation context is null. Are you sure you're running this as a web service?");
            }

            return WebOperationContext.Current.IncomingRequest.ContentType;
        }

        public static long GetContentLength()
        {
            // Make sure there is a current operation context to use
            if (WebOperationContext.Current == null)
            {
                throw new CommunicationException(
                    "The current web operation context is null. Are you sure you're running this as a web service?");
            }

            return WebOperationContext.Current.IncomingRequest.ContentLength;
        }

        public static string GetRemoteIpAddress()
        {
            // The ip address must be fetched from the regular operation context, not the web one
            MessageProperties properties = OperationContext.Current.IncomingMessageProperties;
            var endpointProperty = properties[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;

            if (endpointProperty == null)
            {
                throw new CommunicationException(
                    "The current endpoint property is null. Are you sure you're running this as a web service?");
            }

            return endpointProperty.Address;
        }

        public static NameValueCollection GetQueryParameters()
        {
            // Make sure there is a current operation context to use
            if (WebOperationContext.Current == null)
            {
                throw new CommunicationException(
                    "The current web operation context is null. Are you sure you're running this as a web service?");
            }

            return WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters;
        }

        #endregion

        /// <summary>
        /// Generates a Message object suitable for returning after a request to the server.
        /// A JSON serialization of the response object is performed. The status code
        /// is set on the outgoing request.
        /// </summary>
        /// <exception cref="CommunicationException">
        /// Thrown if the current web operation context is null.
        /// </exception>
        /// <param name="response">The response object to send back to the client</param>
        /// <param name="statusCode">The HTTP status code to send back to the client</param>
        /// <returns>A Message that can be send back to the user</returns>
        public static Message GenerateResponse(object response, HttpStatusCode statusCode)
        {
            // Make sure there is a current operation context to use
            if (WebOperationContext.Current == null)
            {
                throw new CommunicationException(
                    "The current web operation context is null. Are you sure you're running this as a web service?");
            }

            // Serialize the response
            string responseJson = JsonConvert.SerializeObject(response);

            // Set the status code and let the response fly
            WebOperationContext.Current.OutgoingResponse.StatusCode = statusCode;
            return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
        }

        public static void SetStatusCode(HttpStatusCode code)
        {
            // Make sure there is a current operation context to use
            if (WebOperationContext.Current == null)
            {
                throw new CommunicationException(
                    "The current web operation context is null. Are you sure you're running this as a web service?");
            }

            WebOperationContext.Current.OutgoingResponse.StatusCode = code;
        }

        public static void SetHeader(string headerName, string content)
        {
            // Make sure there is a current operation context to use
            if (WebOperationContext.Current == null)
            {
                throw new CommunicationException(
                    "The current web operation context is null. Are you sure you're running this as a web service?");
            }

            WebOperationContext.Current.OutgoingResponse.Headers.Add(headerName, content);
        }

        public static void SetHeader(HttpResponseHeader header, string value)
        {
            // Make sure there is a current operation context to use
            if (WebOperationContext.Current == null)
            {
                throw new CommunicationException(
                    "The current web operation context is null. Are you sure you're running this as a web service?");
            }

            WebOperationContext.Current.OutgoingResponse.Headers.Add(header, value);
        }

        /// <summary>
        /// Extension method to handle converting streams to a byte array.
        /// </summary>
        /// <param name="stream">The stream to convert to a byte array</param>
        /// <returns>A byte array with the convents of the stream</returns>
        public static byte[] ToByteArray(this Stream stream)
        {
            byte[] buffer = new byte[32768];
            using (MemoryStream ms = new MemoryStream())
            {
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        return ms.ToArray();
                    ms.Write(buffer, 0, read);
                }
            }
        }

    }
}
