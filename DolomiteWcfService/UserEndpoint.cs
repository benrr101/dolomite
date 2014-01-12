using System;
using System.Data;
using System.IO;
using System.Net;
using System.ServiceModel.Channels;
using System.Text;
using DolomiteWcfService.Exceptions;
using DolomiteWcfService.Requests;
using DolomiteWcfService.Responses;
using Newtonsoft.Json;

namespace DolomiteWcfService
{
    class UserEndpoint : IUserEndpoint
    {
        #region Properties

        /// <summary>
        /// Instance of the User Manager
        /// </summary>
        private UserManager UserManager { get; set; }

        #endregion

        public UserEndpoint()
        {
            // Initialize the track manager
            UserManager = UserManager.Instance;
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
                string bodyStr = Encoding.Default.GetString(body.ToByteArray());
                var request = JsonConvert.DeserializeObject<UserCreationRequest>(bodyStr);

                // Attempt to create a new user
                UserManager.CreateUser(username, request.Email, request.Password, request.AuthKey);
                return WebUtilities.GenerateResponse(new Response(Response.StatusValue.Success), HttpStatusCode.OK);
            }
            catch (JsonSerializationException)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse("The JSON for the user creation request is invalid."),
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
        /// Operation for logging in a user. This method, by itself, does very
        /// little: deserializing the request and retruning the necessary
        /// responses. A UserLoginRequest object is required from the client.
        /// Upon validation, a new session is created.
        /// </summary>
        /// <param name="body">The body of the request. Should contain a UserLoginRequest object.</param>
        /// <returns>A success message with a session token or failure with a 401 status.</returns>
        public Message Login(Stream body)
        {
            try
            {
                // Process the body of the request into a login request object
                string bodyStr = Encoding.Default.GetString(body.ToByteArray());
                var request = JsonConvert.DeserializeObject<UserLoginRequest>(bodyStr);

                // Attempt to login the user
                string token = UserManager.ValidateLogin(request.ApiKey, WebUtilities.GetRemoteIpAddress(),
                    request.Username,
                    request.Password);

                // Send a successful token back via a login success response
                LoginSuccessResponse response = new LoginSuccessResponse(token);
                return WebUtilities.GenerateResponse(response, HttpStatusCode.OK);
            }
            catch (JsonSerializationException)
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
        /// <returns>A success or error message</returns>
        public Message Logout()
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
    }
}
