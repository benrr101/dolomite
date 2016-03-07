using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Text;
using System.Text.RegularExpressions;
using DolomiteManagement.Exceptions;
using DolomiteWcfService.Requests;
using DolomiteWcfService.Responses;
using Newtonsoft.Json;

namespace DolomiteWcfService
{
    internal class WebUtilities
    {
        /// <summary>
        /// Common message for internal server errors
        /// </summary>
        public const string InternalServerMessage = "An internal server error occurred";

        private WebOperationContext CurrentWebContext { get; set; }

        private OperationContext CurrentOperationContext { get; set; }

        public WebUtilities()
        {
            // Make sure there is a current web operation context to use and store it
            if (WebOperationContext.Current == null)
                throw new CommunicationException("The current web operation context is null.");

            CurrentWebContext = WebOperationContext.Current;

            // Make sure there is a current operation context and store it
            if(OperationContext.Current == null)
                throw new CommunicationException("The current operation context is null.");

            CurrentOperationContext = OperationContext.Current;
        }

        #region Header Management

        /// <summary>
        /// Fetches a header from the current incoming request. Performs a
        /// check to see that the current web operation context exists.
        /// </summary>
        /// <param name="header">The string name of the header to get</param>
        /// <returns>The string value of the header, if it exists. Null otherwise</returns>
        public string GetHeader(string header)
        {
            return CurrentWebContext.IncomingRequest.Headers[header];
        }

        /// <summary>
        /// Fetches a header from the current incoming request. Performs a
        /// check to see that the current web operation context exists.
        /// </summary>
        /// <param name="header">The enum value of the header to get</param>
        /// <returns>The string value of the header, if it exists. Null otherwise</returns>
        public string GetHeader(HttpRequestHeader header)
        {
            return CurrentWebContext.IncomingRequest.Headers[header];
        }

        /// <summary>
        /// Sets a header on the outgoing response. Performs a check to make
        /// sure there's a web operation context, first.
        /// </summary>
        /// <param name="headerName">The string name of the header to perform add to the response</param>
        /// <param name="content">The value to set the header to</param>
        public void SetHeader(string headerName, string content)
        {
            CurrentWebContext.OutgoingResponse.Headers.Add(headerName, content);
        }

        /// <summary>
        /// Sets a header on the outgoing response. Performs a check to make
        /// sure there's a web operation context, first.
        /// </summary>
        /// <param name="header">The enum value of the header to perform add to the response</param>
        /// <param name="value">The value to set the header to</param>
        public void SetHeader(HttpResponseHeader header, string value)
        {
            CurrentWebContext.OutgoingResponse.Headers.Add(header, value);
        }

        #endregion

        #region Request Getters

        /// <summary>
        /// Fetches the session token from the headers of the incoming request.
        /// This uses regular expressions to get the token from the header. It
        /// also is able to validate the header at the same time!
        /// </summary>
        /// <exception cref="InvalidSessionException">Thrown if the authorization header is formatted incorrectly or is missing.</exception>
        /// <returns>The session token from the authorization header</returns>
        public UserSession GetDolomiteSessionToken()
        {
            // Fetch the header
            string authHeader = GetHeader(HttpRequestHeader.Cookie);
            if (String.IsNullOrWhiteSpace(authHeader))
                throw new InvalidSessionException("The cookie header is missing.");

            // Parse the header to get at the token
            Regex regex = new Regex(@"sesh=([0-9a-fA-F]{64})-([0-9a-fA-F]{64})", RegexOptions.Compiled);
            if (!regex.IsMatch(authHeader))
                throw new InvalidSessionException("Cookie header does not match regular expression for the session header");

            var groups = regex.Match(authHeader).Groups;

            return new UserSession
            {
                ApiKey = groups[2].Value,
                Token = groups[1].Value
            };
        }

        /// <summary>
        /// Retrieves the remote IP address from the current incoming message
        /// </summary>
        /// <returns>The IP address of the client that made the request</returns>
        public string GetRemoteIpAddress()
        {
            // The ip address must be fetched from the regular operation context, not the web one
            MessageProperties properties = CurrentOperationContext.IncomingMessageProperties;
            var endpointProperty = properties[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;

            if (endpointProperty == null)
                throw new CommunicationException("The current endpoint property is null.");

            return endpointProperty.Address;
        }

        /// <summary>
        /// Fetches the contests of the given stream as a UTF8 stream
        /// </summary>
        /// <param name="stream">The stream to retreive the contents of</param>
        /// <returns></returns>
        /// TODO: Do we need to support any other encoding in here?
        public static string GetUtf8String(Stream stream)
        {
            // Get the string as per the usual
            string s = Encoding.UTF8.GetString(StreamToByteArray(stream));
            string preamble = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());
            if (s.StartsWith(preamble, StringComparison.Ordinal))
                s = s.Remove(0, preamble.Length);

            return s;
        }

        /// <summary>
        /// Fetches the query parameters from the latest request
        /// </summary>
        /// <returns>A dictionary of variable name => value</returns>
        public Dictionary<string, string> GetQueryParameters()
        {
            // Convert to a sensible type
            NameValueCollection source = CurrentWebContext.IncomingRequest.UriTemplateMatch.QueryParameters;
            return source.Cast<string>()
                .Select(s => new {Key = s, Value = source[s]})
                .ToDictionary(p => p.Key, p => p.Value);
        }

        #endregion

        public Message GenerateUnauthorizedResponse()
        {
            const string message = "Invalid session information provided";
            SetHeader(HttpResponseHeader.WwwAuthenticate, "DOLOMITE href=\"/users/login\"");
            return GenerateResponse(new ErrorResponse(message), HttpStatusCode.Unauthorized);
        }

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
        public Message GenerateResponse(object response, HttpStatusCode statusCode)
        {
            // Serialize the response
            string responseJson = JsonConvert.SerializeObject(response);

            // Set the status code and let the response fly
            CurrentWebContext.OutgoingResponse.StatusCode = statusCode;
            return CurrentWebContext.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
        }

        /// <summary>
        /// Explicitly sets the HTTP status code to return when the response is sent
        /// </summary>
        /// <param name="code">The HTTP status code to set</param>
        public void SetStatusCode(HttpStatusCode code)
        {
            CurrentWebContext.OutgoingResponse.StatusCode = code;
        }

        /// <summary>
        /// Converts a stream to a byte array by copying the stream to a MemoryStream and then
        /// converting that stream to a byte array.
        /// </summary>
        /// <param name="stream">The stream to convert to a byte array</param>
        /// <returns>A byte array with the convents of the stream</returns>
        public static byte[] StreamToByteArray(Stream stream)
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
