﻿namespace BlazorChat.Server
{
    public static class EnvironmentVarKeys
    {
        /// <summary>
        /// Connection string used to connect to and authenticate with azure cosmosdb
        /// </summary>
        public const string AZURECOSMOSCONNECTIONSTRING = "AzureCosmosConnectionString";
        /// <summary>
        /// Connection string used to connect to and authenticate with azure blob storage
        /// </summary>
        public const string AZUREBLOBCONNECTIONSTRING = "AzureBlobConnectionString";
        /// <summary>
        /// Bearer token string used to authenticate users of the admin api
        /// </summary>
        public const string ADMINAPIBEARERTOKEN = "AdminApiBearerToken";
        /// <summary>
        /// Json encoded ice configurations provided to clients when initiating calls
        /// </summary>
        public const string ICECONFIGURATIONS = "IceConfigurations";
        /// <summary>
        /// Key used to authenticate with azure translator service
        /// </summary>
        public const string AZURETRANSLATORKEY = "AzureTranslatorKey";
        /// <summary>
        /// Location string used to configure http requests to azure translator service
        /// </summary>
        public const string AZURETRANSLATORLOCATION = "AzureTranslatorLocation";
        /// <summary>
        /// Token key string used to configure the jwt Securitykey (UTF8 representation)
        /// </summary>
        public const string JWTSECRET = "JwtSecret";

        /// <summary>
        /// Checks for required environment variables.
        /// </summary>
        /// <param name="enableBlob"></param>
        /// <param name="enableTranslation"></param>
        /// <returns>Returns true, if all mandatory environment variables are set, false otherwise.</returns>
        public static bool CheckEnvironment(out bool enableBlob, out bool enableTranslation)
        {
            bool hasCosmosDbConnectionString = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURECOSMOSCONNECTIONSTRING));
            enableBlob = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZUREBLOBCONNECTIONSTRING));
            enableTranslation = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURETRANSLATORKEY)) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURETRANSLATORLOCATION));
            bool hasTokenSecret = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(JWTSECRET));

            if (!hasCosmosDbConnectionString)
            {
                Console.Error.WriteLine($"REQUIRED environment variable \"{AZURECOSMOSCONNECTIONSTRING}\" was not set!");
            }

            if (!enableBlob)
            {
                Console.WriteLine($"Optional Environment Variable \"{AZUREBLOBCONNECTIONSTRING}\" was not set. BlobStorage support disabled!");
            }

            if (!enableTranslation)
            {
                Console.WriteLine($"Optional Environment Variables \"{AZURETRANSLATORKEY}\" and \"{AZURETRANSLATORLOCATION}\" were not set. Translator support disabled!");
            }

            if (!hasTokenSecret)
            {
                Console.WriteLine($"Optional Environment Variable \"{JWTSECRET}\" not set. Jwt tokens generated for token sign-in will be signed with a random key generated at startup!");
            }
            return hasCosmosDbConnectionString;
        }
    }
}
