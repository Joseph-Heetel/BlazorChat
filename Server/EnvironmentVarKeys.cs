namespace BlazorChat.Server
{
    public static class EnvironmentVarKeys
    {
        public const string AZURECOSMOSCONNECTIONSTRING = "AzureCosmosConnectionString";
        public const string AZUREBLOBCONNECTIONSTRING = "AzureBlobConnectionString";
        public const string ADMINAPIBEARERTOKEN = "AdminApiBearerToken";
        public const string ICECONFIGURATIONS = "IceConfigurations";

        public static void CheckEnvironment(out bool enableBlob)
        {
            bool hasCosmosDbConnectionString = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURECOSMOSCONNECTIONSTRING));
            enableBlob = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZUREBLOBCONNECTIONSTRING));

            if (!hasCosmosDbConnectionString)
            {
                Console.Error.WriteLine($"Required environment variable \"{AZURECOSMOSCONNECTIONSTRING}\" was not set!");
            }
            
            if (!enableBlob)
            {
                Console.WriteLine($"Environment Variable \"{AZUREBLOBCONNECTIONSTRING}\" was not set. BlobStorage support disabled!");
            }
        }
    }
}
