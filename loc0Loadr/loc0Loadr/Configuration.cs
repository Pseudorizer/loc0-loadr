using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace loc0Loadr
{
    internal static class Configuration
    {
        private static JObject _configFile;
        
        public static bool GetConfig()
        {
            string configFilePath = Path.Join(AppContext.BaseDirectory, "config.json");

            if (!File.Exists(configFilePath))
            {
                Helpers.RedMessage("Failed to find config.json file");
                return false;
            }

            string configFileContent = File.ReadAllText(configFilePath);

            try
            {
                _configFile = JObject.Parse(configFileContent);
            }
            catch (JsonReaderException ex)
            {
                Helpers.RedMessage(ex.Message);
                return false;
            }

            return true;
        }

        public static bool UpdateConfig(string keyToUpdate, string newValue)
        {
            if (_configFile.ContainsKey(keyToUpdate))
            {
                _configFile[keyToUpdate] = newValue;
            }
            else
            {
                Helpers.RedMessage("Key not found");

                return false;
            }

            string configFilePath = Path.Join(AppContext.BaseDirectory, "config.json");

            string configFileSerialized = JsonConvert.SerializeObject(_configFile);
            
            File.WriteAllText(configFilePath, configFileSerialized);

            return true;
        }

        public static T GetValue<T>(string key)
        {
            try
            {
                return _configFile[key].Value<T>();
            }
            catch (Exception ex)
            {
                if (ex is KeyNotFoundException || ex is InvalidCastException)
                {
                    Helpers.RedMessage(ex.Message);
                    return default;
                }

                throw;
            }
        }
    }
}