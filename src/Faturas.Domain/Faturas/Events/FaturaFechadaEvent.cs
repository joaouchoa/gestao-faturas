using Faturas.Domain.Common;

namespace Faturas.Domain.Faturas.Events;

public record FaturaFechadaEvent(Guid FaturaId) : IDomainEvent;
