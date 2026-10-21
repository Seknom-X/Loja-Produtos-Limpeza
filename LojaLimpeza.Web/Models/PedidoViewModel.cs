using System.Text.Json.Serialization;

namespace LojaLimpeza.Web.Models;

public class PedidoViewModel
{
    public int Id { get; set; }

    // A API retorna 'dataCriacao', conforme GetTodosPedidos().
    [JsonPropertyName("dataCriacao")]
    public DateTimeOffset DataPedido { get; set; }

    public decimal ValorTotal { get; set; }

    // Mesmo enum definido na API: 1 Pendente, 2 Confirmado,
    // 3 EmPreparacao, 4 Concluido, 5 Cancelado.
    public int Status { get; set; }
}
