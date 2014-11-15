using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DolomiteModel.PublicRepresentations
{
    [DataContract]
    public class UserSettings
    {
        #region Internal Enum Types

        public enum ShuffleModes
        {
            Random,     // True random selection of tracks.
            Order       // Random ordering, ensuring all tracks are played once.
        }

        #endregion

        #region Properties

        /// <summary>
        /// The type of shuffling the user has selected
        /// </summary>
        [DataMember]
        [JsonConverter(typeof(StringEnumConverter))]
        public ShuffleModes ShuffleMode { get; set; }

        [DataMember]
        public string SampleString { get; set; }

        #endregion

        /// <summary>
        /// Converts the string to a properly formatted JSON string
        /// </summary>
        /// <returns>String serialization of the settings object</returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        /// <summary>
        /// Creates a user settings object from a JSON serialization of them
        /// </summary>
        /// <param name="serializedSettings">The JSON serialized settings</param>
        /// <returns>A proper settings object</returns>
        public static UserSettings FromSerializedString(string serializedSettings)
        {
            return JsonConvert.DeserializeObject<UserSettings>(serializedSettings);
        }
    }
}
