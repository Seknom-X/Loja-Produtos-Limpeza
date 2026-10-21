# Executar na raiz da solucao LojaLimpeza, com o usuario restrito ja criado no MySQL.
# Nao salva senhas em arquivos, nao modifica appsettings.json e restaura a variavel anterior.
$usuario = 'loja_testes'
$senhaSegura = Read-Host 'Senha do MySQL para loja_testes' -AsSecureString
$senha = [System.Net.NetworkCredential]::new('', $senhaSegura).Password
$conexaoAnterior = $env:LOJALIMPEZA_TESTES_CONNECTION

try {
    $config = [System.Data.Common.DbConnectionStringBuilder]::new()
    $config['Server'] = 'localhost'
    $config['Port'] = 3306
    $config['Database'] = 'LojaLimpeza_Testes'
    $config['User ID'] = $usuario
    $config['Password'] = $senha
    $env:LOJALIMPEZA_TESTES_CONNECTION = $config.ConnectionString

    Write-Host 'Destino: LojaLimpeza_Testes. Usuario: loja_testes.'
    Write-Host 'Executando apenas os testes de integracao MySQL...'
    dotnet test LojaLimpeza.Tests/LojaLimpeza.Tests.csproj `
        --filter 'FullyQualifiedName~PedidosMySqlIntegracaoTests' `
        --logger 'console;verbosity=normal'
    if ($LASTEXITCODE -ne 0) {
        throw 'Os testes nao passaram. Envie a mensagem de erro (sem senhas).'
    }
    Write-Host 'Testes de integracao finalizados sem falhas.'
}
finally {
    if ($null -eq $conexaoAnterior) {
        Remove-Item Env:LOJALIMPEZA_TESTES_CONNECTION -ErrorAction SilentlyContinue
    }
    else {
        $env:LOJALIMPEZA_TESTES_CONNECTION = $conexaoAnterior
    }
    Remove-Variable senha, senhaSegura, config -ErrorAction SilentlyContinue
}
