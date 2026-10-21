
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LojaLimpeza.Web.Models;

namespace LojaLimpeza.Web.Controllers;

[Authorize]
public class PedidosController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PedidosController> _logger;

    public PedidosController(
        IHttpClientFactory httpClientFactory,
        ILogger<PedidosController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private HttpClient CriarClienteAutenticado(string token)
    {
        var client = _httpClientFactory.CreateClient("LojaAPI");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    // Lista todos os pedidos consultando a API
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var token = Request.Cookies["JwtCookie"];
        var client = _httpClientFactory.CreateClient("LojaAPI");

        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.GetAsync("/api/Pedidos");

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var pedidos = JsonSerializer.Deserialize<List<PedidoViewModel>>(json, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
            return View(pedidos);
        }

        // SE O ERRO FOR 401 (NÃO AUTORIZADO), MANDA PARA O LOGIN
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return RedirectToAction("Login", "Auth");
        }

        // SE FOR QUALQUER OUTRO ERRO (500, 404, etc), MOSTRA NA TELA!
        var erroDaApi = await response.Content.ReadAsStringAsync();
        return Content($"A API DEVOLVEU UM ERRO: {response.StatusCode} | Detalhes: {erroDaApi}");
    }

    // Confirma um pedido através da API
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar(int id)
    {
        if (id <= 0)
        {
            return BadRequest("Identificador do pedido inválido.");
        }

        var token = Request.Cookies["JwtCookie"];

        if (string.IsNullOrWhiteSpace(token))
        {
            return Challenge();
        }

        var client = CriarClienteAutenticado(token);

        client.DefaultRequestHeaders.Add(
            "X-Idempotency-Key",
            Guid.NewGuid().ToString());

        try
        {
            var response = await client.PutAsync(
                $"/api/Pedidos/{id}/confirmar",
                null);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return Challenge();
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return Forbid();
            }

            if (response.IsSuccessStatusCode)
            {
                TempData["Sucesso"] =
                    $"Pedido #{id} confirmado com sucesso!";
            }
            else if (response.StatusCode == HttpStatusCode.Conflict)
            {
                TempData["Erro"] =
                    $"Não foi possível confirmar o pedido #{id}. " +
                    "Verifique o estoque ou o status atual.";
            }
            else
            {
                TempData["Erro"] =
                    $"Erro ao confirmar o pedido #{id}. " +
                    $"Código: {(int)response.StatusCode}";
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Erro de conexão ao confirmar pedido {PedidoId}.",
                id);

            TempData["Erro"] =
                "Falha de conexão. Verifique se o pedido foi confirmado " +
                "antes de tentar novamente.";
        }

        return RedirectToAction(nameof(Index));
    }
    // ETAPA 10: O painel web envia a solicitação de cancelamento para a API.
    // A API verifica perfil, status e devolução do estoque, quando cabível.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id)
    {
        if (id <= 0)
        {
            return BadRequest("Identificador do pedido inválido.");
        }

        var token = Request.Cookies["JwtCookie"];
        if (string.IsNullOrWhiteSpace(token))
        {
            return Challenge();
        }

        var client = CriarClienteAutenticado(token);

        try
        {
            using var response = await client.PutAsync(
                $"/api/Pedidos/{id}/cancelar", null);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return Challenge();
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return Forbid();
            }

            if (response.IsSuccessStatusCode)
            {
                TempData["Sucesso"] =
                    $"Pedido #{id} cancelado com sucesso! " +
                    "Se estava confirmado, o estoque foi devolvido pela API.";
            }
            else if (response.StatusCode == HttpStatusCode.Conflict)
            {
                TempData["Erro"] =
                    $"Não foi possível cancelar o pedido #{id}. " +
                    "Ele pode ter mudado de status ou já ter sido cancelado.";
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                TempData["Erro"] = $"Pedido #{id} não encontrado.";
            }
            else
            {
                _logger.LogWarning(
                    "API recusou cancelamento do pedido {PedidoId}: HTTP {Status}.",
                    id, (int)response.StatusCode);

                TempData["Erro"] =
                    $"Erro ao cancelar o pedido #{id}. " +
                    $"Código: {(int)response.StatusCode}.";
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "Erro de conexão ao cancelar pedido {PedidoId}.", id);

            TempData["Erro"] =
                "Falha de conexão. Consulte o status do pedido " +
                "antes de tentar novamente.";
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex,
                "Tempo esgotado ao cancelar pedido {PedidoId}.", id);

            TempData["Erro"] =
                "A solicitação demorou demais. Consulte o status do pedido " +
                "antes de tentar novamente.";
        }

        return RedirectToAction(nameof(Index));
    }

}
