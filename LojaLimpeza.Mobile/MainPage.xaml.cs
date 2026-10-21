using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using LojaLimpeza.Mobile.Services;
using LojaLimpeza.Mobile.Models;

namespace LojaLimpeza.Mobile;

public partial class MainPage : ContentPage
{
    private readonly LojaApiService _apiService;
    private readonly CarrinhoService _carrinhoService;

    // Agora recebemos a API e o Carrinho!
    public MainPage(LojaApiService apiService, CarrinhoService carrinhoService)
    {
        InitializeComponent();
        _apiService = apiService;
        _carrinhoService = carrinhoService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CarregarProdutos();
    }

    private async Task CarregarProdutos()
    {
        var produtos = await _apiService.GetProdutosAsync();
        ProdutosCollectionView.ItemsSource = produtos;
    }

    private async void OnAdicionarCarrinhoClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        var produto = button?.BindingContext as Produto;

        if (produto != null)
        {
            // NOVA BARREIRA DE ESTOQUE
            if (produto.QuantidadeEstoque <= 0)
            {
                await DisplayAlert("Esgotado", "Infelizmente este produto acabou de ficar sem stock.", "OK");
                return; // Pára tudo, não adiciona ao carrinho
            }

            _carrinhoService.AdicionarProduto(produto);
            
            await DisplayAlert("Adicionado!", 
                $"{produto.Nome} colocado no cesto.\nItens no carrinho: {_carrinhoService.Itens.Count}\nTotal: R$ {_carrinhoService.CalcularTotal():F2}", 
                "OK");
        }
    }
	private async void OnVerCarrinhoClicked(object sender, EventArgs e)
    {
        // Pede ao MAUI para nos dar a página do Carrinho pronta a usar
        var carrinhoPage = Handler.MauiContext.Services.GetService<CarrinhoPage>();
        
        // Viaja para a página do carrinho!
        await Navigation.PushAsync(carrinhoPage);
    }
    
}