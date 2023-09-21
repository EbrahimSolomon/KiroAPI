using Kiron_Interactive.CachingLayer;
using Kiron_Interactive.Data_Layer.Helpers;
using KironAPI.Models;
using Microsoft.Extensions.Options;

public class UserRepository
{
    private readonly CacheManager _cacheManager;
    private readonly string _connectionString;

    public UserRepository(CacheManager cacheManager, IOptions<DatabaseSettings> dbSettings)
    {
         _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
        _connectionString = dbSettings.Value.DefaultConnection;
    }

    public async Task<User> GetUserByUsername(string username)
{
    // Try to get the user from cache first
    var cachedUser = _cacheManager.Get<User>(username);
    if (cachedUser != null)
    {
        return cachedUser;
    }

    using var commandExecutor = new CommandExecutor(_connectionString);
    var user = await commandExecutor.ExecuteStoredProcedureAsync<User>("GetUserByUsername", new { Username = username });

    // Store the retrieved user in cache for future calls with an example expiration duration of 1 hour
    _cacheManager.Add(username, user, TimeSpan.FromHours(1));

    return user;
    }

    public async Task AddUser(User user)
{
    using var commandExecutor = new CommandExecutor(_connectionString);
    var parameters = new 
    {
        Username = user.Username,
        PasswordHash = user.PasswordHash,
        Salt = user.Salt
    };
    await commandExecutor.ExecuteStoredProcedureAsync<User>("AddUser", parameters);
}

}
