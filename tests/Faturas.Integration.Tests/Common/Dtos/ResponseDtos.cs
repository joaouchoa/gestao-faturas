namespace Faturas.Integration.Tests.Common.Dtos;

internal record FaturaDto(
    Guid Id,
    string Numero,
    string NomeCliente,
    DateTime DataEmissao,
    string Status,
    decimal ValorTotal);

internal record FaturaDetailDto(
    Guid Id,
    string Numero,
    string NomeCliente,
    DateTime DataEmissao,
    string Status,
    decimal ValorTotal,
    List<ItemDetailDto> Itens);

internal record ItemDetailDto(
    Guid Id,
    string Descricao,
    int Quantidade,
    decimal ValorUnitario,
    decimal ValorTotalItem,
    string? Justificativa);

internal record ItemAddedDto(
    Guid ItemId,
    Guid FaturaId,
    string Descricao,
    int Quantidade,
    decimal ValorUnitario,
    decimal ValorTotalItem,
    string? Justificativa);

internal record FecharDto(Guid Id, string Status);

internal record PagedFaturasDto(
    List<FaturaDto> Itens,
    int TotalRegistros,
    int Pagina,
    int TamanhoPagina,
    int TotalPaginas);
