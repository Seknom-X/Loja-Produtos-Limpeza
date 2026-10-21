using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// 1. Adiciona o suporte para as páginas MVC (Views)
builder.Services.AddControllersWithViews();

// 2. Configura o "Telefone" (HttpClient) para falar com a API
builder.Services.AddHttpClient("LojaAPI", client =>
{
    // Lê o endereço da API que colocámos no appsettings.json
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:BaseUrl"]!);
});

// 3. Configura a Autenticação por Cookies no Navegador
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login"; // Se alguém tentar entrar sem login, é atirado para esta página
        options.AccessDeniedPath = "/Auth/AcessoNegado";
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// 4. Ordem crítica: Primeiro Autenticação, depois Autorização
app.UseAuthentication();
app.UseAuthorization();

// 5. Roteamento padrão do MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();