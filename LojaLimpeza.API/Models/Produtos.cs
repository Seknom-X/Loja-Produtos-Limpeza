namespace LojaLimpeza.API.Models;

public class Produto
{
    public int Id { get; set; }
    public int CategoriaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    
    // Regra de Negócio: Quantidade real no armazém
    public int QuantidadeEstoque { get; set; }
    
    // Permite a exclusão lógica
    public bool Ativo { get; set; } = true;

    // Propriedade de navegação (Relacionamento): Um produto pertence a uma Categoria
    public Categoria? Categoria { get; set; }
}
