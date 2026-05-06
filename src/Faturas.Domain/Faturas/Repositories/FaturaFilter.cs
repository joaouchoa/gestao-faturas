namespace Faturas.Domain.Faturas.Repositories;

public record FaturaFilter(
    string? NomeCliente = null,
    DateTime? DataInicial = null,
    DateTime? DataFinal = null,
    StatusFatura? Status = null,
    int Pagina = 1,
    int TamanhoPagina = 10
);
