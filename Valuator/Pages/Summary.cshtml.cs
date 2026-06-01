using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Valuator.Pages;

[Authorize]
public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly IDatabase _db;

    public SummaryModel( ILogger<SummaryModel> logger, IConnectionMultiplexer redis )
    {
        _logger = logger;
        _db = redis.GetDatabase();
    }

    public double? Rank { get; private set; }
    public int Similarity { get; private set; }
    public bool IsRankReady => Rank.HasValue;

    public IActionResult OnGet( string id )
    {
        _logger.LogDebug( id );

        string textKey = "TEXT-" + id;
        if ( string.IsNullOrWhiteSpace( id ) || !_db.KeyExists( textKey ) )
        {
            return RedirectToPage( "Index" );
        }

        RedisValue authorValue = _db.StringGet( "AUTHOR-" + id );
        string? author = authorValue.HasValue ? ( string? )authorValue : null;

        string currentUser = User.Identity?.Name!;

        if ( !string.Equals( author, currentUser, StringComparison.OrdinalIgnoreCase ) )
        {
            _logger.LogWarning( "User {User} attempted to access text {Id} owned by {Owner}",
                currentUser, id, author );
            return Forbid();
        }

        RedisValue rankValue = _db.StringGet( "RANK-" + id );
        Rank = rankValue.HasValue ? ( double )rankValue : null;

        RedisValue similarityValue = _db.StringGet( "SIMILARITY-" + id );
        Similarity = similarityValue.HasValue ? ( int )similarityValue : 0;

        return Page();
    }
}
