using Microsoft.Azure.Cosmos;

namespace CustomBlazorApp.Server.Services.DatabaseWrapper
{
    public interface IDatabaseConnection
    {
        public ITableService<T>? TryGetTable<T>(string name);
        public ITableService<T> GetTable<T>(string name)
        {
            return TryGetTable<T>(name) ?? throw new NullReferenceException();
        }
    }

    public static class DatabaseConstants
    {
        public const string LOGINSTABLE = "Logins";
        public const string USERSTABLE = "Users";
        public const string CHANNELSTABLE = "Channels";
        public const string MEMBERSHIPSTABLE = "Memberships";
        public const string MESSAGESTABLE = "Messages";
        public const string FORMSTABLE = "Forms";
        public const string FORMREQUESTSTABLE = "FormRequests";
        public const string FORMRESPONSESTABLE = "FormResponses";
    }
}
