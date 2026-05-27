using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Valuator.Pages;

[Authorize]
public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly IDatabase _db;

    public SummaryModel(ILogger<SummaryModel> logger, IConnectionMultiplexer redis )
    {
        _logger = logger;
        _db = redis.GetDatabase();
    }

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

        RedisValue json = _db.StringGet(textKey);
        if (!json.HasValue)
        {
            return RedirectToPage("Index");
        }

        try
        {
            var textData = JsonSerializer.Deserialize<TextEntry>(json!);
            if (textData == null)
                return RedirectToPage("Index");

            string currentUser = User.Identity?.Name!;
            if (!string.Equals(textData.Author, currentUser, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("User {User} attempted to access text {Id} owned by {Owner}",
                    currentUser, id, textData.Author);
                return Forbid();  
            }
        }
        catch (JsonException)
        {
            return RedirectToPage("Index");
        }

        RedisValue rankValue = _db.StringGet("RANK-" + id);
        Rank = rankValue.HasValue ? (double)rankValue : null;

        RedisValue similarityValue = _db.StringGet( "SIMILARITY-" + id );
        Similarity = similarityValue.HasValue ? ( int )similarityValue : 0;

        return Page();
    }

    private class TextEntry
    {
        public string Text { get; set; } = "";
        public string Author { get; set; } = "";
    }
}
