using System;
using System.ComponentModel;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace DolomiteCommon
{
    public static class RoleUtilities
    {
        /// <summary>
        /// Returns the configuration value for the role.
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="configurationKey">The key to use to look up the config value</param>
        /// <returns>The converted configuration value</returns>
        public static T GetConfigurationValue<T>(string configurationKey)
        {
            // Get the value from the role config. If the output type is a string, then just return
            // it as is, no conversions necessary.
            string configValue = RoleEnvironment.GetConfigurationSettingValue(configurationKey);
            if (typeof(T) == typeof(string))
            {
                return (T)(object)configValue;
            }

            // Get a converter for the output type and if we can convert with it, use it
            var converter = TypeDescriptor.GetConverter(typeof (T));
            if(converter.CanConvertFrom(typeof(string)))
            {
                return (T) converter.ConvertFrom(configValue);
            }

            // If we can't convert with it, we never will be able to.
            throw new InvalidCastException(String.Format("Cannot convert from string to {0}", typeof (T)));
        }
    }
}
