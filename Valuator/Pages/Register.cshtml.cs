using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Valuator.Services;

namespace Valuator.Pages;

public class RegisterModel : PageModel
{
    private readonly UserService _userService;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel( UserService userService, ILogger<RegisterModel> logger )
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
            ModelState.AddModelError( "", "Ћогин и пароль об€зательны" );
            return Page();
        }

        bool registered = await _userService.RegisterAsync( username, password );
        if ( !registered )
        {
            ModelState.AddModelError( "", "ѕользователь с таким логином уже существует" );
            return Page();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username) //значение доступно через User.Identity.Name
        };
        var identity = new ClaimsIdentity( claims, CookieAuthenticationDefaults.AuthenticationScheme ); //учетные данные
        var principal = new ClaimsPrincipal( identity ); //главный объект безопасности
        await HttpContext.SignInAsync( CookieAuthenticationDefaults.AuthenticationScheme, principal ); //устанавливает аутентификационную cookie в браузере

        _logger.LogInformation( "User {Username} registered and logged in", username );
        return RedirectToPage( "/Index" );
    }
}