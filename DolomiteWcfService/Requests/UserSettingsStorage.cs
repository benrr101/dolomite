using System.Collections.Generic;

namespace DolomiteWcfService.Requests
{
    public struct UserSettingsStorage
    {
        public string ApiKey { get; set; }

        public Dictionary<string, string> Settings { get; set; } 
    }
}
