using Faturas.Domain.Common;

namespace Faturas.Domain.Faturas.Events;

public record FaturaCriadaEvent(Guid FaturaId, string Numero) : IDomainEvent;
