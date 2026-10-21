using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;
using LojaLimpeza.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace LojaLimpeza.Web.Controllers;

public class AuthController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        var client = _httpClientFactory.CreateClient("LojaAPI");
        var jsonContent = new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/Usuarios/login", jsonContent);

        if (response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenDoc = JsonDocument.Parse(responseBody);
            var token = tokenDoc.RootElement.GetProperty("token").GetString();

            // 1. Criar a "Identidade" para o sistema MVC saber quem acabou de entrar
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, model.Email)
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            // 2. Fazer o REGISTO OFICIAL de entrada (SignIn) no painel Web
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, 
                new ClaimsPrincipal(claimsIdentity)
            );

            // 3. Guardar o Token JWT para podermos conversar com a API posteriormente
            Response.Cookies.Append("JwtCookie", token!, new CookieOptions { HttpOnly = true });

            return RedirectToAction("Index", "Home");
        }

        ViewBag.Erro = "Email ou palavra-passe incorretos.";
        return View(model);
    }

    // BÓNUS: Já aproveitamos para deixar o método de Sair (Logout) pronto!
    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete("JwtCookie");
        return RedirectToAction("Login");
    }
}