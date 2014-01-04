using System;
using System.Data;
using System.IO;
using System.Net;
using System.ServiceModel.Channels;
using System.Text;
using DolomiteModel.PublicRepresentations;
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
                return WebUtilities.GenerateResponse(new WebResponse(WebResponse.StatusValue.Success), HttpStatusCode.OK);
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

        public Message Login()
        {
            throw new NotImplementedException();
        }

        public Message Logout()
        {
            throw new NotImplementedException();
        }
    }
}
