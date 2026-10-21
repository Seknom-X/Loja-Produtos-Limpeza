namespace LojaLimpeza.Mobile.Models;

public class Produto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int QuantidadeEstoque { get; set; }
    public bool Ativo { get; set; }

    public string PrecoFormatado => $"R$ {Preco:F2}";
    public string ImagemUrl => "dotnet_bot.png"; 

    // --- NOVAS REGRAS DE INTERFACE ---
    // Devolve verdadeiro se tem stock
    public bool TemEstoque => QuantidadeEstoque > 0; 
    
    // Devolve verdadeiro se o stock for zero ou negativo
    public bool Esgotado => QuantidadeEstoque <= 0;  
    
    // Deixa o "cartão" meio transparente (0.5) se estiver esgotado
    public double OpacidadeVisual => TemEstoque ? 1.0 : 0.5; 
}