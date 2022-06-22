using BlazorChat.Server.Models;
using BlazorChat.Server.Services.DatabaseWrapper;
using BlazorChat.Shared;

namespace BlazorChat.Server.Services
{
    public class LoginDataService : ILoginDataService
    {
        //private readonly IDatabaseArchService _dataArchService;
        private readonly ITableService<LoginModel> _loginsTable;
        private readonly ITableService<UserModel> _usersTable;
        private readonly IIdGeneratorService _idGenService;

        public LoginDataService(IDatabaseConnection db, IIdGeneratorService idgen)
        {
            _loginsTable = db.GetTable<LoginModel>(DatabaseConstants.LOGINSTABLE);
            _usersTable = db.GetTable<UserModel>(DatabaseConstants.USERSTABLE);
            _idGenService = idgen;
        }
        /// <inheritdoc/>
        public async Task<bool> TestLogin(string login)
        {
            //var item = await this._dataArchService.Database.GetItem<LoginModel>(_dataArchService.LOGINS,login, _dataArchService.MakePartKeyHashed(_dataArchService.LOGINS, login));
            var response = await _loginsTable.GetItemAsync(login);
            return response.IsSuccess && response.ResultAsserted.CheckWellFormed();
        }

        /// <inheritdoc/>
        public async Task<ItemId?> TestLoginPassword(string login, ByteArray passwordHash)
        {
            //var item = await this._dataArchService.Database.GetItem<LoginModel>(_dataArchService.LOGINS,login, _dataArchService.MakePartKeyHashed(_dataArchService.LOGINS, login));
            var response = await _loginsTable.GetItemAsync(login);
            if (!response.IsSuccess)
            {
                return null;
            }
            var item = response.ResultAsserted;
            if (item.CheckWellFormed() && item.Passwordhash == passwordHash)
            {
                return item.UserId;
            }
            else
            {
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<ItemId?> CreateLoginPassword(string login, ByteArray passwordHash)
        {
            ItemId userId = _idGenService.Generate();
            var item = new LoginModel() // Partition is set automatically
            {
                Login = login,
                Passwordhash = passwordHash,
                UserId = userId,
            };
            var result = await _loginsTable.CreateItemAsync(item);
            return result.IsSuccess ? userId : default;
        }
        /// <inheritdoc/>
        public async Task<Shared.User?> CreateUserAndLogin(string name, string login, ByteArray passwordHash)
        {
            // Generate user Id in advance
            ItemId userId = _idGenService.Generate();

            // Upload login information
            var item = new LoginModel()
            {
                Login = login,
                Passwordhash = passwordHash,
                UserId = userId,
            };
            var result = await _loginsTable.CreateItemAsync(item);
            if (!result.IsSuccess)
            {
                return null;
            }

            // Fill user object
            UserModel user = new UserModel
            {
                Id = userId,
                Created = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                Name = name,
                //Partition = _dataArchService.GetPartValueHashed(userId, _dataArchService.USERS.PartitionKeyModulo)
            };

            // upload user object
            result = await _usersTable.CreateItemAsync(user);
            return result.IsSuccess ? user.ToApiType() : null;
        }

        public async Task<bool> UpdatePassword(string login, ByteArray newPasswordHash)
        {
            //var item = await this._dataArchService.Database.GetItem<LoginModel>(_dataArchService.LOGINS,login, _dataArchService.MakePartKeyHashed(_dataArchService.LOGINS, login));
            var tableResult = await _loginsTable.GetItemAsync(login);
            if (!tableResult.IsSuccess)
            {
                return false;
            }
            var model = tableResult.ResultAsserted;
            model.Passwordhash = newPasswordHash;
            if (!model.CheckWellFormed())
            {
                return false;
            }
            return (await _loginsTable.ReplaceItemAsync(model)).IsSuccess;

        }
    }
}
