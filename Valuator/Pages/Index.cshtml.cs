using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using System.Globalization;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IDatabase _db;

    private const string TextsSetKey = "TEXTS_SET";

    public IndexModel( ILogger<IndexModel> logger, IConnectionMultiplexer redis )
    {
        _logger = logger;
        _db = redis.GetDatabase();
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

        string rankKey = "RANK-" + id;
        // TODO: (pa1) посчитать rank и сохранить в БД (Redis) по ключу rankKey
        int nonLetterCount = text.Count( c => !char.IsLetter( c ) );
        double rank = ( double )nonLetterCount / text.Length;
        _db.StringSet( rankKey, rank );

        string similarityKey = "SIMILARITY-" + id;
        // TODO: (pa1) посчитать similarity и сохранить в БД (Redis) по ключу similarityKey
        bool isNewText = _db.SetAdd( TextsSetKey, text );
        int similarity = isNewText ? 0 : 1;

        _db.StringSet( similarityKey, similarity );

        return Redirect( $"summary?id={id}" );
    }
}
