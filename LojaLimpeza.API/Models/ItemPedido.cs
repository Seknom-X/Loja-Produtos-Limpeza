namespace LojaLimpeza.API.Models;

public class ItemPedido
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public int ProdutoId { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }

    // Propriedades de navegação
    public Pedido? Pedido { get; set; }
    public Produto? Produto { get; set; }
}