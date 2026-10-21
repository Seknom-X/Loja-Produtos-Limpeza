using LojaLimpeza.API.Models.Enums;

namespace LojaLimpeza.API.Models;

public class Pedido
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public StatusPedido Status { get; set; } = StatusPedido.Pendente;
    public decimal ValorTotal { get; set; }
    
    // Regra de Negócio (D04 e RN11): Previne duplicação de pedidos por falha de rede
    public Guid ChaveIdempotencia { get; set; }

    // Propriedades de navegação
    public Usuario? Usuario { get; set; }
    public ICollection<ItemPedido> Itens { get; set; } = new List<ItemPedido>();
}