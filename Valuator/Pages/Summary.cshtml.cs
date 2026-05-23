using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Valuator.Services;

namespace Valuator.Pages;
public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly ShardConnectionManager _shardConnections;

    public SummaryModel( ILogger<SummaryModel> logger, ShardConnectionManager shardConnections )
    {
        _logger = logger;
        _shardConnections = shardConnections;
    }

    public double? Rank { get; private set; }
    public int Similarity { get; private set; }
    public bool IsRankReady => Rank.HasValue;

    public IActionResult OnGet( string id )
    {
        _logger.LogDebug( id );

        if ( string.IsNullOrWhiteSpace( id ) )
        {
            return RedirectToPage( "Index" );
        }

        var mainDb = _shardConnections.GetMainDatabase();

        var regionValue = mainDb.StringGet( $"SHARD-{id}" );

        if ( !regionValue.HasValue )
        {
            return RedirectToPage( "Index" );
        }

        string region = regionValue.ToString();
        _logger.LogInformation( $"LOOKUP: {id}, {region}" );

        var shardDb = _shardConnections.GetShardDatabase( region );

        if ( !shardDb.KeyExists( $"TEXT-{id}" ) )
        {
            return RedirectToPage( "Index" );
        }

        var rankValue = shardDb.StringGet( "RANK-" + id );
        Rank = rankValue.HasValue ? ( double )rankValue : null;

        var similarityValue = shardDb.StringGet( "SIMILARITY-" + id );
        Similarity = similarityValue.HasValue ? ( int )similarityValue : 0;

        return Page();
    }
}
