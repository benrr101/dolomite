namespace DolomiteWcfService.Requests
{
    public struct UserLoginRequest
    {
        /// <summary>
        /// The API key that is being used to initialize the session. Should
        /// be a 64-character sha256 hash
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// The password for the login attempt
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// The username for the login attempt
        /// </summary>
        public string Username { get; set; }
    }
}
