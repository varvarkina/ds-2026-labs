using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Valuator.Services;

namespace Valuator.Pages;

public class LoginModel : PageModel
{
    private readonly UserService _userService;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel( UserService userService, ILogger<LoginModel> logger )
    {
        _userService = userService;
        _logger = logger;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync( string username, string password )
    {
        if ( string.IsNullOrWhiteSpace( username ) || string.IsNullOrWhiteSpace( password ) )
        {
            ModelState.AddModelError( "", "Логин и пароль обязательны" );
            return Page();
        }

        bool isValid = await _userService.ValidateCredentialsAsync( username, password );
        if ( !isValid )
        {
            ModelState.AddModelError( "", "Неверный логин или пароль" );
            return Page();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username)
        };
        var identity = new ClaimsIdentity( claims, CookieAuthenticationDefaults.AuthenticationScheme );
        var principal = new ClaimsPrincipal( identity ); //главный объект безопасности

        await HttpContext.SignInAsync( CookieAuthenticationDefaults.AuthenticationScheme, principal );

        _logger.LogInformation( "User {Username} logged in", username );
        return RedirectToPage( "/Index" );
    }
}