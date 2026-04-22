using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using System.Globalization;
using Valuator.Services;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IDatabase _db;
    private readonly RankTaskPublisher _publisher;

    private const string TextsSetKey = "TEXTS_SET";

    public IndexModel( 
        ILogger<IndexModel> logger, 
        IConnectionMultiplexer redis, 
        RankTaskPublisher publisher)
    {
        _logger = logger;
        _db = redis.GetDatabase();
        _publisher = publisher;
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
        // TODO: (pa1) сохранить в БД (Redis) text по ключу textKey
        _db.StringSet( textKey, text );

        string similarityKey = "SIMILARITY-" + id;
        // TODO: (pa1) посчитать similarity и сохранить в БД (Redis) по ключу similarityKey
        bool isNewText = _db.SetAdd( TextsSetKey, text );
        int similarity = isNewText ? 0 : 1;
        _db.StringSet( similarityKey, similarity );

        _publisher.Publish(id, HttpContext.RequestAborted);

        return Redirect( $"summary?id={id}" );
    }
}
