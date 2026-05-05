using Faturas.Application.Common.Results;
using Faturas.Application.Features.Faturas.Commands.AddItemFatura;
using Faturas.Application.Features.Faturas.Commands.CreateFatura;
using Faturas.Application.Features.Faturas.Commands.FecharFatura;
using Faturas.Application.Features.Faturas.Commands.RemoveItemFatura;
using Faturas.Application.Features.Faturas.Commands.UpdateItemFatura;
using Faturas.Application.Features.Faturas.Queries.GetFaturaById;
using Faturas.Application.Features.Faturas.Queries.ListFaturas;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Faturas.Api.Controllers;

[ApiController]
[Route("api/faturas")]
[Produces("application/json")]
public class FaturasController : ControllerBase
{
    private readonly ISender _sender;

    public FaturasController(ISender sender) => _sender = sender;

    // ── POST /api/faturas ─────────────────────────────────────────────────────

    /// <summary>Cria uma nova fatura.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateFaturaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateFaturaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : MapFailure(result.Error);
    }

    // ── GET /api/faturas ──────────────────────────────────────────────────────

    /// <summary>Lista faturas com filtros opcionais.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ListFaturasResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? cliente,
        [FromQuery] DateTime? dataInicial,
        [FromQuery] DateTime? dataFinal,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var request = new ListFaturasRequest(cliente, dataInicial, dataFinal, status);
        var result  = await _sender.Send(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error);
    }

    // ── GET /api/faturas/{id} ─────────────────────────────────────────────────

    /// <summary>Retorna uma fatura com seus itens.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetFaturaByIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetFaturaByIdRequest(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error);
    }

    // ── POST /api/faturas/{id}/itens ─────────────────────────────────────────

    /// <summary>Adiciona um item à fatura.</summary>
    [HttpPost("{id:guid}/itens")]
    [ProducesResponseType(typeof(AddItemFaturaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddItem(
        Guid id,
        [FromBody] AddItemBody body,
        CancellationToken cancellationToken)
    {
        var request = new AddItemFaturaRequest(id, body.Descricao, body.Quantidade, body.ValorUnitario, body.Justificativa);
        var result  = await _sender.Send(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id }, result.Value)
            : MapFailure(result.Error);
    }

    // ── PUT /api/faturas/{id}/itens/{itemId} ─────────────────────────────────

    /// <summary>Atualiza um item da fatura.</summary>
    [HttpPut("{id:guid}/itens/{itemId:guid}")]
    [ProducesResponseType(typeof(UpdateItemFaturaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateItem(
        Guid id,
        Guid itemId,
        [FromBody] UpdateItemBody body,
        CancellationToken cancellationToken)
    {
        var request = new UpdateItemFaturaRequest(id, itemId, body.Descricao, body.Quantidade, body.ValorUnitario, body.Justificativa);
        var result  = await _sender.Send(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error);
    }

    // ── DELETE /api/faturas/{id}/itens/{itemId} ───────────────────────────────

    /// <summary>Remove um item da fatura.</summary>
    [HttpDelete("{id:guid}/itens/{itemId:guid}")]
    [ProducesResponseType(typeof(RemoveItemFaturaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RemoveItem(
        Guid id,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RemoveItemFaturaRequest(id, itemId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error);
    }

    // ── PUT /api/faturas/{id}/fechar ──────────────────────────────────────────

    /// <summary>Fecha uma fatura, impedindo novas alterações.</summary>
    [HttpPut("{id:guid}/fechar")]
    [ProducesResponseType(typeof(FecharFaturaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Fechar(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new FecharFaturaRequest(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IActionResult MapFailure(Error error) => error.Code switch
    {
        "NotFound"   => NotFound(new { error.Message }),
        "Conflict"   => Conflict(new { error.Message }),
        "Validation" => UnprocessableEntity(new { error.Message }),
        _            => BadRequest(new { error.Message })
    };
}

// ── Body DTOs (HTTP input — separados dos records da Application) ─────────────

public sealed record AddItemBody(
    string Descricao,
    int Quantidade,
    decimal ValorUnitario,
    string? Justificativa);

public sealed record UpdateItemBody(
    string Descricao,
    int Quantidade,
    decimal ValorUnitario,
    string? Justificativa);
