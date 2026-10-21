using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;
using LojaLimpeza.Web.Models;
using Microsoft.AspNetCore.Authorization;

namespace LojaLimpeza.Web.Controllers;

[Authorize] // 1. O "Segurança" do HTML: Ninguém entra nesta página sem ter feito login!
public class ProdutosController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ProdutosController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> Index()
    {
        // 2. Vai ao Cookie do navegador e pega na pulseira VIP
        var token = Request.Cookies["JwtCookie"];
        var client = _httpClientFactory.CreateClient("LojaAPI");

        // 3. Coloca a pulseira no cabeçalho do "telefone"
        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // 4. Pede a lista de produtos à API (que agora nos vai deixar entrar)
        var response = await client.GetAsync("/api/Produtos");

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            // 5. Transforma o JSON recebido numa lista de ProdutoViewModel
            var produtos = JsonSerializer.Deserialize<List<ProdutoViewModel>>(json, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
            return View(produtos); // Envia os produtos para a página HTML
        }

        // Se a API rejeitar (ex: token expirou), redireciona o utilizador de volta para o Login
        return RedirectToAction("Login", "Auth");
    }
}