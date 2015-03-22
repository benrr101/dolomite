namespace DolomiteWcfService.Requests
{
    internal struct UserSession
    {
        /// <summary>
        /// The API Key that was provided in the session token
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// The username that corresponds to the session token
        /// </summary>
        public string Token { get; set; }
    }
}
