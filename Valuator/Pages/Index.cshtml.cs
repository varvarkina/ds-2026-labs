using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using StackExchange.Redis;
using System.Globalization;
using Valuator.Services;

namespace Valuator.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IDatabase _db;
    private readonly RankTaskPublisher _rankTaskPublisher;
    private readonly EventsPublisher _eventsPublisher;

    private const string TextsSetKey = "TEXTS_SET";

    public IndexModel( 
        ILogger<IndexModel> logger, 
        IConnectionMultiplexer redis, 
        RankTaskPublisher rankTaskPublisher,
        EventsPublisher eventsPublisher)
    {
        _logger = logger;
        _db = redis.GetDatabase();
        _rankTaskPublisher = rankTaskPublisher;
        _eventsPublisher = eventsPublisher;
    }

    public void OnGet()
    {

    }

    public IActionResult OnPost( string text )
    {
        _logger.LogDebug( text );

        if ( string.IsNullOrEmpty( text ) )
        {
            return RedirectToPage( "Index" );
        }

        string id = Guid.NewGuid().ToString();

        string textKey = "TEXT-" + id;
        var textData = new { Text = text, Author = User.Identity?.Name ?? "anonymous" };
        string json = System.Text.Json.JsonSerializer.Serialize(textData);
        _db.StringSet(textKey, json);

        string similarityKey = "SIMILARITY-" + id;
        bool isNewText = _db.SetAdd( TextsSetKey, text );
        int similarity = isNewText ? 0 : 1;
        _db.StringSet( similarityKey, similarity );

        _eventsPublisher.PublishSimilarityCalculated(
            id,
            similarity,
            HttpContext.RequestAborted
        );

        _rankTaskPublisher.Publish(id, HttpContext.RequestAborted);

        return Redirect( $"summary?id={id}" );
    }
}
