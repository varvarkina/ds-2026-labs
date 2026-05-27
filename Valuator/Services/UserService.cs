using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Valuator.Services;

public class UserService
{
    private readonly IDatabase _db;
    private readonly ILogger<UserService> _logger;
    private const string UsersHashKey = "users";

    public UserService( IConnectionMultiplexer redis, ILogger<UserService> logger )
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<bool> RegisterAsync( string username, string password )
    {
        bool exists = await _db.HashExistsAsync( UsersHashKey, username );
        if ( exists )
        {
            _logger.LogWarning( "Registration failed: user {Username} already exists", username );
            return false;
        }

        string hash = BCrypt.Net.BCrypt.HashPassword( password );
        await _db.HashSetAsync( UsersHashKey, username, hash );
        _logger.LogInformation( "User {Username} registered successfully", username );
        return true;
    }

    public async Task<bool> ValidateCredentialsAsync( string username, string password )
    {
        RedisValue storedHash = await _db.HashGetAsync( UsersHashKey, username );
        if ( !storedHash.HasValue )
            return false;

        return BCrypt.Net.BCrypt.Verify( password, storedHash! );
    }
}