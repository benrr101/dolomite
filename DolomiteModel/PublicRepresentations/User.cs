namespace DolomiteModel.PublicRepresentations
{
    public class User
    {
        /// <summary>
        /// The user's email
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// The user's hashed password
        /// </summary>
        public string PasswordHash { get; set; }

        /// <summary>
        /// The user's username
        /// </summary>
        public string Username { get; set; }
    }
}
