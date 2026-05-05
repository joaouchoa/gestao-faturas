using Faturas.Domain.Common;

namespace Faturas.Domain.Faturas.Events;

public record ItemAdicionadoEvent(Guid FaturaId, Guid ItemId) : IDomainEvent;
