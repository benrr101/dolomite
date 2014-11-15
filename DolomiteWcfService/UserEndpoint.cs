using System;
using System.Data;
using System.IO;
using System.Net;
using System.ServiceModel.Channels;
using DolomiteManagement;
using DolomiteManagement.Exceptions;
using DolomiteModel.PublicRepresentations;
using DolomiteWcfService.Requests;
using DolomiteWcfService.Responses;
using Newtonsoft.Json;

namespace DolomiteWcfService
{
    class UserEndpoint : IUserEndpoint
    {
        #region Properties

        /// <summary>
        /// Instance of the track manager
        /// </summary>
        private static TrackManager TrackManager { get; set; }

        /// <summary>
        /// Instance of the User Manager
        /// </summary>
        private static UserManager UserManager { get; set; }

        #endregion

        static UserEndpoint()
        {
            // Initialize the track manager
            UserManager = UserManager.Instance;
            TrackManager = TrackManager.Instance;
        }

        /// <summary>
        /// Operation for creating a new user account. This requires passing in
        /// a user creation request object. The processing is handed off to the
        /// user manager which does all the validation and creation work. The
        /// method is PUT, so the username is extracted from the URI.
        /// </summary>
        /// <param name="username">
        /// Name of the user to create, extracted from the URI
        /// </param>
        /// <param name="body">
        /// The body of the request. Should be a JSON representation of a user
        /// creation request object.
        /// </param>
        /// <returns>Success or failure message</returns>
        public Message CreateUser(string username, Stream body)
        {
            try
            {
                // Deserialize the body of the request for the user details
                string bodyStr = WebUtilities.GetUtf8String(body);
                var request = JsonConvert.DeserializeObject<UserCreationRequest>(bodyStr);

                // Attempt to create a new user
                UserManager.CreateUser(username, request.Email, request.Password, request.AuthKey);
                return WebUtilities.GenerateResponse(new Response(Response.StatusValue.Success), HttpStatusCode.OK);
            }
            catch (JsonSerializationException)
            {
                return WebUtilities.GenerateResponse(
                        new ErrorResponse("The JSON for the user creation request is invalid."),
                        HttpStatusCode.BadRequest);
            }
            catch (JsonReaderException)
            {
                return WebUtilities.GenerateResponse(
                        new ErrorResponse("The JSON for the user creation request is invalid."),
                        HttpStatusCode.BadRequest);
            }
            catch (ArgumentNullException ane)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(ane.Message), HttpStatusCode.BadRequest);
            }
            catch (DuplicateNameException dne)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(dne.Message), HttpStatusCode.BadRequest);
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Retrieves the specified user's settings
        /// </summary>
        /// <param name="username">The username of the user to get the settings of</param>
        /// <returns>The settings of the user on success, an error message on failure</returns>
        public Message GetUserSettings(string username)
        {
            try
            {
                // Make sure we have a valid session
                string apiKey;
                string token = WebUtilities.GetDolomiteSessionToken(out apiKey);
                string seshUsername = UserManager.GetUsernameFromSession(token, apiKey);
                UserManager.ExtendIdleTimeout(token);

                // Make sure the owners are correct
                if (username != seshUsername)
                    throw new InvalidSessionException("You may not request settings for this user.");

                return WebUtilities.GenerateResponse(UserManager.GetUserSettings(username), HttpStatusCode.OK);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Fetches the general statistics of the user
        /// </summary>
        /// <param name="username">The username of the user to fetch statistics for</param>
        /// <returns>A success or failure message</returns>
        public Message GetUserStatistics(string username)
        {
            try
            {
                // Make sure we have a valid session
                string apiKey;
                string token = WebUtilities.GetDolomiteSessionToken(out apiKey);
                string seshUsername = UserManager.GetUsernameFromSession(token, apiKey);
                UserManager.ExtendIdleTimeout(token);

                // Make sure the owners are correct
                if (username != seshUsername)
                    throw new InvalidSessionException("You may not request information about this user.");

                return WebUtilities.GenerateResponse(TrackManager.GetTotalTrackInfo(username), HttpStatusCode.OK);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Operation for logging in a user. This method, by itself, does very
        /// little: deserializing the request and retruning the necessary
        /// responses. A UserLoginRequest object is required from the client.
        /// Upon validation, a new session is created.
        /// </summary>
        /// <param name="username">The username that wishes to login</param>
        /// <param name="body">The body of the request. Should contain a UserLoginRequest object.</param>
        /// <returns>A success message with a session token or failure with a 401 status.</returns>
        public Message Login(string username, Stream body)
        {
            try
            {
                // If there was a cookie sent with a prior session token, invalidate it
                try
                {
                    string apiKey;
                    string sessionToken = WebUtilities.GetDolomiteSessionToken(out apiKey);
                    UserManager.InvalidateSession(sessionToken);
                }
                catch (InvalidSessionException) // Ideally, this should always happen
                {
                }

                // Process the body of the request into a login request object
                string bodyStr = WebUtilities.GetUtf8String(body);
                var request = JsonConvert.DeserializeObject<UserLoginRequest>(bodyStr);

                // Attempt to login the user
                string token = UserManager.ValidateLogin(request.ApiKey, WebUtilities.GetRemoteIpAddress(),
                    username,
                    request.Password);

                // Send a successful token back via a login success response
                LoginSuccessResponse response = new LoginSuccessResponse(token);

                // Add a header to handle sending the session cookie
                string seshToke = String.Format("sesh={0}-{1}; Path=/; Secure", token, request.ApiKey);
                WebUtilities.SetHeader(HttpResponseHeader.SetCookie, seshToke);

                return WebUtilities.GenerateResponse(response, HttpStatusCode.OK);
            }
            catch (JsonSerializationException)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse("The JSON for the login request is invalid."),
                    HttpStatusCode.BadRequest);
            }
            catch (JsonReaderException)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse("The JSON for the login request is invalid."),
                    HttpStatusCode.BadRequest);
            }
            catch (ApplicationException)
            {
                WebUtilities.SetHeader(HttpResponseHeader.WwwAuthenticate, "DOLOMITE");
                return WebUtilities.GenerateResponse(new ErrorResponse("Invalid API key"), HttpStatusCode.Unauthorized);
            }
            catch (ObjectNotFoundException)
            {
                string message = "Username or password was incorrect. Please try again.";
                WebUtilities.SetHeader(HttpResponseHeader.WwwAuthenticate, "DOLOMITE");
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.Unauthorized);
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Attempts to log out the user by invalidating their session. Error
        /// messages are only given when the session token is missing or an unknown
        /// error occurred. If the session is already invalid or does not exist,
        /// then nothing happens.
        /// </summary>
        /// <param name="username">The username of the user wishing to logout. Not used.</param>
        /// <returns>A success or error message</returns>
        public Message Logout(string username)
        {
            try
            {
                // Read the session token from the headers
                string ignore;
                string token = WebUtilities.GetDolomiteSessionToken(out ignore);

                // Invalidate the session
                UserManager.InvalidateSession(token);
            }
            catch (InvalidSessionException)
            {
                string message = "The session token was missing or not formatted correctly. Please see the API guide for more info.";
                WebUtilities.SetHeader(HttpResponseHeader.WwwAuthenticate, "DOLOMITE href=\"/users/login\"");
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.Unauthorized);
            }
            catch(ObjectNotFoundException) {}   // Ignore this one.
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
            }
            
            // Tell the client everything went "ok"
            return WebUtilities.GenerateResponse(new Response(Response.StatusValue.Success), HttpStatusCode.OK);
        }

        /// <summary>
        /// Stores the settings of the user
        /// </summary>
        /// <param name="username">The username of the user who wanted to store the settings</param>
        /// <param name="body">The settings the user wishes to store, Json serialized</param>
        /// <returns>Success or failure message</returns>
        public Message StoreUserSettings(string username, Stream body)
        {
            try
            {
                // Make sure the user is successfully logged in
                string apiKey;
                string token = WebUtilities.GetDolomiteSessionToken(out apiKey);
                string seshUsername = UserManager.GetUsernameFromSession(token, apiKey);

                // Deserialize the body of the request for the user details
                string bodyStr = WebUtilities.GetUtf8String(body);
                var settings = JsonConvert.DeserializeObject<UserSettings>(bodyStr);

                // Make sure the owners are correct
                if (username != seshUsername)
                    throw new InvalidSessionException("You may not store settings for this user.");

                // Store the settings into the database
                UserManager.StoreSettings(username, settings);

                return WebUtilities.GenerateResponse(new Response(Response.StatusValue.Success), HttpStatusCode.OK);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Returns true just to allow the CORS preflight request via OPTIONS
        /// HTTP method to go through
        /// </summary>
        /// <returns>True</returns>
        public bool PreflyRequest()
        {
            return true;
        }
    }
}
