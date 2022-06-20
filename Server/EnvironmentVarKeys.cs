namespace BlazorChat.Server
{
    public static class EnvironmentVarKeys
    {
        public const string AZURECOSMOSCONNECTIONSTRING = "AzureCosmosConnectionString";
        public const string AZUREBLOBCONNECTIONSTRING = "AzureBlobConnectionString";
        public const string ADMINAPIBEARERTOKEN = "AdminApiBearerToken";
        public const string ICECONFIGURATIONS = "IceConfigurations";
        public const string AZURETRANSLATORKEY = "AzureTranslatorKey";
        public const string AZURETRANSLATORLOCATION = "AzureTranslatorLocation";


        public static void CheckEnvironment(out bool enableBlob, out bool enableTranslation)
        {
            bool hasCosmosDbConnectionString = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURECOSMOSCONNECTIONSTRING));
            enableBlob = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZUREBLOBCONNECTIONSTRING));
            enableTranslation = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURETRANSLATORKEY)) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURETRANSLATORLOCATION));

            if (!hasCosmosDbConnectionString)
            {
                Console.Error.WriteLine($"Required environment variable \"{AZURECOSMOSCONNECTIONSTRING}\" was not set!");
            }

            if (!enableBlob)
            {
                Console.WriteLine($"Environment Variable \"{AZUREBLOBCONNECTIONSTRING}\" was not set. BlobStorage support disabled!");
            }

            if (!enableTranslation)
            {
                Console.WriteLine($"Environment Variables \"{AZURETRANSLATORKEY}\" and \"{AZURETRANSLATORLOCATION}\" were not set. Translator support disabled!");
            }
        }
    }
}
