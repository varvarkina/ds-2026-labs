using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;
public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly IDatabase _db;

    public SummaryModel(ILogger<SummaryModel> logger, IConnectionMultiplexer redis )
    {
        _logger = logger;
        _db = redis.GetDatabase();
    }

    public string TextId { get; private set; } = string.Empty;
    public double? Rank { get; private set; }
    public int Similarity { get; private set; }
    public bool IsRankReady => Rank.HasValue;

    public IActionResult OnGet(string id)
    {
        _logger.LogDebug(id);

        string textKey = "TEXT-" + id;
        if ( string.IsNullOrWhiteSpace( id ) || !_db.KeyExists( textKey ) )
        {
            return RedirectToPage( "Index" );
        }

        TextId = id;

        RedisValue rankValue = _db.StringGet("RANK-" + id);
        Rank = rankValue.HasValue ? (double)rankValue : null;

        RedisValue similarityValue = _db.StringGet( "SIMILARITY-" + id );
        Similarity = similarityValue.HasValue ? ( int )similarityValue : 0;

        return Page();
    }
}
