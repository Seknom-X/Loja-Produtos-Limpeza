using System.Net.Http.Json;
using LojaLimpeza.Mobile.Models;

namespace LojaLimpeza.Mobile.Services;

public class LojaApiService
{
    private readonly HttpClient _httpClient;
    
    // ATENÇÃO: Se testarmos como App Windows, o localhost funciona. 
    // Se usarmos o Emulador Android depois, teremos de mudar para "10.0.2.2"
    private const string BaseUrl = "http://localhost:5193";

    public LojaApiService()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
    }

    public async Task<List<Produto>> GetProdutosAsync()
    {
        try
        {
            var produtos = await _httpClient.GetFromJsonAsync<List<Produto>>("/api/Produtos");
            
            // REMOVEMOS A REGRA DO ESTOQUE > 0
            // Agora trazemos todos os que estão Ativos (para o dono poder desativar no site se quiser esconder de vez)
            return produtos?.Where(p => p.Ativo).ToList() ?? new List<Produto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao buscar produtos: {ex.Message}");
            return new List<Produto>();
        }
    }
    // 1. Agora o método exige receber a lista de produtos do carrinho!
    public async Task<string> EnviarPedidoAsync(decimal valorTotal, List<Produto> produtos)
    {
        try
        {
            // 2. Traduzimos os produtos soltos para "Itens de Pedido" agrupados
            var itensPedido = produtos
                .GroupBy(p => p.Id)
                .Select(g => new 
                {
                    ProdutoId = g.Key,
                    Quantidade = g.Count(), // Conta quantos do mesmo produto estão no cesto
                    PrecoUnitario = g.First().Preco
                }).ToList();

            var novoPedido = new 
            { 
                ValorTotal = valorTotal, 
                Status = 0, 
                ChaveIdempotencia = Guid.NewGuid(),
                Itens = itensPedido // <-- O SEGREDO ESTÁ AQUI: Agora enviamos os produtos!
            }; 
            
            var response = await _httpClient.PostAsJsonAsync("/api/Pedidos", novoPedido);
            
            if (response.IsSuccessStatusCode) return "Sucesso";
                
            return $"A API Recusou: {await response.Content.ReadAsStringAsync()}";
        }
        catch (Exception ex)
        {
            return $"Falha na App: {ex.Message}";
        }
    }
}