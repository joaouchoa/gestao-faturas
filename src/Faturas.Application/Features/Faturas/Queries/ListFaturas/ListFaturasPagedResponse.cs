namespace Faturas.Application.Features.Faturas.Queries.ListFaturas;

public sealed record ListFaturasPagedResponse(
    IReadOnlyList<ListFaturasResponse> Itens,
    int TotalRegistros,
    int Pagina,
    int TamanhoPagina,
    int TotalPaginas
);
