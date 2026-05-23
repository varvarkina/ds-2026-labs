using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Valuator.Services;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ShardConnectionManager _shardConnections;
    private readonly RankTaskPublisher _rankTaskPublisher;
    private readonly EventsPublisher _eventsPublisher;

    private const string TextsSetKey = "TEXTS_SET";

    private static readonly Dictionary<string, string> CountryToRegion = new()
    {
        { "Russia", "RU" },
        { "France", "EU" },
        { "Germany", "EU" },
        { "UAE", "ASIA" },
        { "India", "ASIA" }
    };

    public IndexModel( 
        ILogger<IndexModel> logger,
        ShardConnectionManager shardConnections,
        RankTaskPublisher rankTaskPublisher,
        EventsPublisher eventsPublisher)
    {
        _logger = logger;
        _shardConnections = shardConnections;
        _rankTaskPublisher = rankTaskPublisher;
        _eventsPublisher = eventsPublisher;
    }

    public void OnGet()
    {

    }

    public IActionResult OnPost( string text, string country )
    {
        _logger.LogDebug( text, country );

        if ( string.IsNullOrEmpty( text ) || string.IsNullOrEmpty( country ) )
        {
            return RedirectToPage( "Index" );
        }

        if ( !CountryToRegion.TryGetValue( country, out var region ) )
        {
            _logger.LogError( $"Unknown country: {country}" );
            return RedirectToPage( "Index" );
        }

        string id = Guid.NewGuid().ToString();
        var mainDb = _shardConnections.GetMainDatabase();
        var shardDb = _shardConnections.GetShardDatabase( region );

        mainDb.StringSet( $"SHARD-{id}", region );

        shardDb.StringSet( $"TEXT-{id}", text );

        bool isNewText = shardDb.SetAdd( TextsSetKey, text );
        int similarity = isNewText ? 0 : 1;
        shardDb.StringSet( $"SIMILARITY-{id}", similarity );
        _eventsPublisher.PublishSimilarityCalculated(
            id,
            similarity,
            HttpContext.RequestAborted
        );

        _rankTaskPublisher.Publish(id, HttpContext.RequestAborted);

        return Redirect( $"summary?id={id}" );
    }
}
