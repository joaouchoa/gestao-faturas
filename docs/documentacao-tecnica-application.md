# Documentação Técnica — Camada de Application (`Faturas.Application`)

> **Objetivo deste documento:** explicar em profundidade a arquitetura da camada de Application, seus padrões e decisões técnicas. Para não repetir os mesmos conceitos em cada caso de uso, usaremos o **`AddItemFatura`** como caso de estudo completo — ele é o mais representativo por envolver busca no repositório, risco de "não encontrado", disparo de regras de negócio no domínio e resposta mapeada. Os demais casos de uso seguem exatamente o mesmo padrão.

---

## 1. O Papel da Application na Arquitetura

A camada de Application é o **orquestrador** do sistema. Ela não contém regras de negócio (isso é responsabilidade do Domínio) e não sabe nada sobre banco de dados (isso é responsabilidade da Infrastructure). Ela apenas coordena o fluxo:

```
Receber intenção → Validar entrada → Chamar o Domínio → Persistir → Retornar resposta
```

Em termos práticos, cada **caso de uso** do sistema (adicionar item, criar fatura, fechar fatura) é representado por um **Handler** que orquestra essas etapas.

---

## 2. Estrutura do Projeto

```
Faturas.Application/
├── Common/
│   ├── Behaviors/
│   │   ├── ValidationBehavior.cs      ← Intercepta e valida antes do handler
│   │   └── LoggingBehavior.cs         ← Loga início e fim de cada request
│   ├── Mediator/
│   │   ├── ICommand.cs                ← Marker: operação que muda estado
│   │   ├── IQuery.cs                  ← Marker: operação que apenas lê
│   │   ├── ICommandHandler.cs         ← Contrato de handler de comando
│   │   └── IQueryHandler.cs           ← Contrato de handler de query
│   ├── Results/
│   │   ├── Result.cs                  ← Encapsula sucesso ou falha
│   │   └── Error.cs                   ← Representa um erro com código e mensagem
│   └── Errors/
│       └── ApplicationErrorMessages.cs ← Todas as mensagens centralizadas
│
├── Features/
│   └── Faturas/
│       ├── Commands/
│       │   ├── CreateFatura/          ← Criar fatura
│       │   ├── AddItemFatura/         ← ★ Caso de estudo deste documento
│       │   ├── FecharFatura/          ← Fechar fatura
│       │   ├── UpdateItemFatura/      ← Atualizar item
│       │   └── RemoveItemFatura/      ← Remover item
│       └── Queries/
│           ├── GetFaturaById/         ← Buscar fatura por ID
│           └── ListFaturas/           ← Listar com filtros
│
└── DependencyInjection/
    └── ServiceCollectionExtensions.cs ← AddApplication()
```

**Por que separar em `Commands` e `Queries`?** Esse é o padrão **CQRS** (Command Query Responsibility Segregation). Commands **mudam** o estado do sistema. Queries **leem** o estado. Separá-los torna cada operação mais focada, mais fácil de testar e abre espaço para otimizações futuras (ex: queries em read replica, commands no banco principal).

---

## 3. Pacotes NuGet

| Pacote | Versão | Papel |
|--------|--------|-------|
| `MediatR` | 12.4.1 | Barramento de mediação — desacopla quem envia o request de quem o processa |
| `FluentValidation` | 11.11.0 | Validação declarativa com regras encadeadas |
| `FluentValidation.DependencyInjectionExtensions` | 11.11.0 | Registra todos os validators no container DI automaticamente |
| `Microsoft.Extensions.Logging.Abstractions` | 8.0.3 | Interface `ILogger<T>` para o `LoggingBehavior` |

---

## 4. Os Blocos de Construção do `Common/`

Antes de entrar no caso de uso, precisamos entender as peças que compõem a infraestrutura interna da Application.

### 4.1 MediatR e o Padrão Mediator

**O que é o MediatR?** É uma biblioteca que implementa o padrão **Mediator**. Em vez de o controller chamar o handler diretamente (acoplamento direto), o controller envia um *request* ao MediatR, que encontra e executa o handler correto.

```
Controller ──→ ISender.Send(request) ──→ MediatR ──→ Handler correto
```

**Por que isso é bom?** O controller não precisa saber qual handler existe ou como ele foi construído. Você pode adicionar, remover ou trocar handlers sem mexer no controller.

### 4.2 `ICommand` e `IQuery` — Interfaces Marcadoras

```csharp
public interface ICommand<TResponse> : IRequest<TResponse> { }
public interface IQuery<TResponse>   : IRequest<TResponse> { }
```

Ambos herdam de `IRequest<TResponse>` do MediatR. A diferença é apenas semântica — `ICommand` sinaliza que a operação **altera** o sistema, `IQuery` sinaliza que apenas **lê**. Isso melhora a leitura do código e abre espaço para restrições futuras (ex: queries em modo read-only).

### 4.3 `ICommandHandler` e `IQueryHandler`

```csharp
public interface ICommandHandler<TCommand, TResponse>
    : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse> { }
```

Herdam de `IRequestHandler<,>` do MediatR. A constraint `where TCommand : ICommand<TResponse>` garante que só requests que são commands possam ser processados por command handlers — o compilador rejeita usos incorretos.

### 4.4 `Result<T>` e `Error` — O Result Pattern

```csharp
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T?    Value    { get; }
    public Error Error    { get; }

    public static Result<T> Success(T value)    => new(value);
    public static Result<T> Failure(Error error) => new(error);
}

public record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static Error NotFound(string message)  => new("NotFound", message);
    public static Error Conflict(string message)  => new("Conflict", message);
}
```

**O problema que o Result pattern resolve:** em C#, a forma tradicional de indicar falha é lançar uma exceção. Mas exceções têm custo e são para situações *excepcionais*. "Fatura não encontrada" não é excepcional — é um cenário esperado de negócio.

**A alternativa:** o handler retorna `Result<T>`. O controller verifica `result.IsFailure` e decide o status HTTP sem precisar capturar exceção.

**Divisão de responsabilidades entre Result e Exceptions:**

| Situação | Mecanismo | Quem trata | HTTP |
|----------|-----------|-----------|------|
| "Fatura não encontrada" | `Result.Failure(Error.NotFound(...))` | Controller verifica `IsFailure` | 404 |
| "Fatura fechada — não pode alterar" | `DomainException` (domínio lança) | `ExceptionHandlingMiddleware` | 422 |
| "Campo obrigatório vazio" | `ValidationException` (FluentValidation lança) | `ExceptionHandlingMiddleware` | 400 |
| Erro inesperado (bug) | `Exception` genérica | `ExceptionHandlingMiddleware` | 500 |

### 4.5 `ApplicationErrorMessages` — Central de Mensagens

```csharp
public static class ApplicationErrorMessages
{
    public static class Fatura
    {
        public const string FaturaNaoEncontrada    = "Fatura não encontrada.";
        public const string NumeroFormatoInvalido  = "O número da fatura deve ter o formato FAT-XXXXXX.";
        // ...
    }
    public static class ItemFatura
    {
        public const string DescricaoTamanhoMinimo = "A descrição deve ter no mínimo 3 caracteres.";
        // ...
    }
}
```

Todas as mensagens de erro da camada de Application residem aqui. Validators e handlers referenciam as constantes — nunca strings literais. Se uma mensagem mudar, muda em um único lugar.

---

## 5. O Pipeline do MediatR

Antes de qualquer handler ser executado, o request percorre um **pipeline de behaviors**. Pense como middlewares, mas para handlers.

```
ISender.Send(request)
        │
        ▼
┌───────────────────────┐
│   LoggingBehavior     │  ← 1° executa: loga "Iniciando AddItemFaturaRequest"
└──────────┬────────────┘
           │
           ▼
┌───────────────────────┐
│  ValidationBehavior   │  ← 2° executa: valida o request com FluentValidation
└──────────┬────────────┘
           │ se inválido → lança ValidationException → para aqui
           │ se válido   → passa adiante
           ▼
┌───────────────────────┐
│  AddItemFaturaHandler │  ← 3° executa: a lógica real
└──────────┬────────────┘
           │
           ▼
        resultado
        (voltando pelo pipeline ao contrário)
           │
           ▼
┌───────────────────────┐
│   LoggingBehavior     │  ← loga "Concluído AddItemFaturaRequest"
└───────────────────────┘
```

### 5.1 `ValidationBehavior`

```csharp
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context  = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
```

**Como funciona:**
1. Recebe pelo DI uma lista de todos os `IValidator<TRequest>` registrados para aquele tipo de request
2. Se não há nenhum validator registrado para esse request, passa direto para o próximo behavior
3. Roda todos os validators
4. Coleta todos os erros de validação
5. Se houver qualquer erro, lança `ValidationException` com todos os erros de uma vez
6. Se tudo válido, chama `next()` — passa para o próximo behavior (ou handler)

**Por que `IEnumerable<IValidator<TRequest>>` em vez de um único validator?** Para suportar múltiplos validators para o mesmo request. Útil quando validações são separadas por responsabilidade (ex: validator de formato + validator de regra de negócio de input).

**`RequestHandlerDelegate<TResponse> next`** é o "próximo passo" no pipeline — pode ser outro behavior ou o handler final. Chamar `next()` é como chamar `await next.Invoke()` num middleware ASP.NET.

### 5.2 `LoggingBehavior`

```csharp
public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executando {Request}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Concluído {Request}", typeof(TRequest).Name);
        return response;
    }
}
```

Simples mas essencial para observabilidade. Todo request que passa pelo sistema gera duas linhas de log. Em produção, isso vai para qualquer sink configurado (Application Insights, Seq, CloudWatch, etc.).

**Por que `LoggingBehavior` é registrado antes do `ValidationBehavior`?** Para que o log de "Executando" apareça mesmo quando a validação falha — você sabe que o request chegou, mesmo que tenha sido rejeitado por validação.

---

## 6. Caso de Estudo: `AddItemFatura`

Este caso de uso é o mais representativo porque envolve todos os cenários possíveis:
- Validação de input (FluentValidation)
- Busca no repositório com tratamento de "não encontrado" (Result pattern)
- Chamada ao domínio que pode lançar `DomainException` (RN-5/6/7/8)
- Persistência e mapeamento da resposta

### 6.1 O Fluxo Completo

```
POST /api/faturas/{id}/itens
    body: { descricao, quantidade, valorUnitario, justificativa? }
                │
                ▼
    FaturasController.AddItem(faturaId, request)
    → monta AddItemFaturaRequest
    → sender.Send(request)
                │
                ▼
    ┌─────────────────────────────────────────────────────┐
    │  PIPELINE MEDIATR                                   │
    │                                                     │
    │  1. LoggingBehavior       → loga "Executando..."   │
    │                                                     │
    │  2. ValidationBehavior    → roda AddItemFatura      │
    │     Validator:                                      │
    │       FaturaId not empty?  ✓                        │
    │       Descricao not empty? ✓                        │
    │       Descricao >= 3 chars? ✓                       │
    │       Quantidade > 0?      ✓                        │
    │       ValorUnitario > 0?   ✓                        │
    │     → se falhar: lança ValidationException (400)    │
    │     → se passar: chama next()                       │
    │                                                     │
    │  3. AddItemFaturaHandler  → lógica real (ver 6.3)  │
    │                                                     │
    │  4. LoggingBehavior       → loga "Concluído..."    │
    └─────────────────────────────────────────────────────┘
                │
                ▼
    Controller recebe Result<AddItemFaturaResponse>
    → IsSuccess? → 200 OK com o item criado
    → IsFailure? → 404 Not Found
    → DomainException? → capturada pelo Middleware → 422
```

### 6.2 `AddItemFaturaRequest` — O DTO de Entrada

```csharp
public sealed record AddItemFaturaRequest(
    Guid FaturaId,
    string Descricao,
    int Quantidade,
    decimal ValorUnitario,
    string? Justificativa
) : ICommand<Result<AddItemFaturaResponse>>;
```

**Por que `record`?** Records são imutáveis por padrão em C#. Um request não deve ser modificado depois de criado — ele representa uma intenção imutável do usuário. Records também geram `Equals` e `ToString` automaticamente, o que facilita testes e logging.

**Por que `: ICommand<Result<AddItemFaturaResponse>>`?** O request **é** o command. Ao implementar `ICommand<TResponse>`, ele informa ao MediatR qual é o tipo de retorno esperado. Isso torna tudo type-safe — o compilador garante que o handler retorne exatamente `Result<AddItemFaturaResponse>`.

**`string? Justificativa`** — o `?` indica que é nullable. A justificativa é opcional na entrada. O domínio decide se ela é obrigatória (RN-7: só quando o total > R$1.000). O validator de Application não valida isso — seria duplicação. Quem valida a regra de negócio é o Domínio.

### 6.3 `AddItemFaturaValidator` — Validação de Input

```csharp
public sealed class AddItemFaturaValidator : AbstractValidator<AddItemFaturaRequest>
{
    public AddItemFaturaValidator()
    {
        RuleFor(x => x.FaturaId)
            .NotEmpty().WithMessage(ApplicationErrorMessages.ItemFatura.FaturaIdObrigatorio);

        RuleFor(x => x.Descricao)
            .NotEmpty().WithMessage(ApplicationErrorMessages.ItemFatura.DescricaoObrigatoria)
            .MinimumLength(3).WithMessage(ApplicationErrorMessages.ItemFatura.DescricaoTamanhoMinimo);

        RuleFor(x => x.Quantidade)
            .GreaterThan(0).WithMessage(ApplicationErrorMessages.ItemFatura.QuantidadeInvalida);

        RuleFor(x => x.ValorUnitario)
            .GreaterThan(0).WithMessage(ApplicationErrorMessages.ItemFatura.ValorUnitarioInvalido);
    }
}
```

**Divisão de responsabilidade entre Validator e Domínio:**

| Regra | Onde fica | Por quê |
|-------|-----------|---------|
| `FaturaId` não vazio | Validator | Validação de formato de input — sem isso, até chegaria ao banco com Guid vazio |
| `Descricao` não vazia, mín. 3 chars | **Ambos** | O validator dá mensagem HTTP 400 formatada. O domínio garante invariante mesmo se chamado fora da API |
| `Quantidade > 0`, `ValorUnitario > 0` | **Ambos** | Mesma razão acima |
| Item > R$1.000 exige justificativa | **Só o Domínio** | É uma regra de negócio que depende do cálculo `quantidade × valorUnitario` — complexidade que pertence ao domínio |
| Fatura fechada não pode receber item | **Só o Domínio** | Depende do estado atual da Fatura no banco — o validator não acessa repositório |

**`AbstractValidator<T>`** — classe base do FluentValidation. As regras são declaradas no construtor com o padrão fluent `RuleFor(x => x.Campo).Regra().WithMessage(...)`.

**`.WithMessage(ApplicationErrorMessages...)`** — ao referenciar a constante, o validator e o teste de validator usam o mesmo valor. Se a mensagem mudar, muda em um lugar só.

### 6.4 `AddItemFaturaHandler` — O Orquestrador

```csharp
public sealed class AddItemFaturaHandler
    : ICommandHandler<AddItemFaturaRequest, Result<AddItemFaturaResponse>>
{
    private readonly IFaturaRepository _repository;

    public AddItemFaturaHandler(IFaturaRepository repository) => _repository = repository;

    public async Task<Result<AddItemFaturaResponse>> Handle(
        AddItemFaturaRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Busca a Fatura no banco
        var fatura = await _repository.GetByIdAsync(request.FaturaId, cancellationToken);

        // 2. Trata "não encontrado" com Result pattern
        if (fatura is null)
            return Result<AddItemFaturaResponse>.Failure(
                Error.NotFound(ApplicationErrorMessages.ItemFatura.FaturaIdObrigatorio));

        // 3. Delega ao Domínio — pode lançar DomainException
        var item = fatura.AdicionarItem(
            request.Descricao,
            request.Quantidade,
            request.ValorUnitario,
            request.Justificativa);

        // 4. Persiste
        await _repository.SaveChangesAsync(cancellationToken);

        // 5. Mapeia e retorna
        return Result<AddItemFaturaResponse>.Success(new AddItemFaturaResponse(
            item.Id,
            fatura.Id,
            item.Descricao,
            item.Quantidade,
            item.ValorUnitario,
            item.ValorTotalItem,
            item.Justificativa));
    }
}
```

**Passo a passo:**

**① `GetByIdAsync`** — busca a fatura com seus itens (eager loading). Neste ponto, os behaviors já rodaram — a validação de input passou.

**② `if (fatura is null) → Result.Failure`** — fatura não encontrada é um cenário esperado, não excepcional. Retornamos `Result.Failure` com `Error.NotFound`. O controller receberá isso e retornará HTTP 404. Não lançamos exceção porque o custo de exceções é alto e esse é um caso previsível.

**③ `fatura.AdicionarItem(...)`** — aqui o handler delega ao domínio. Não há lógica de negócio no handler. Se a fatura estiver fechada, o domínio lança `DomainException("Não é possível alterar uma fatura fechada")`. Essa exceção **não é capturada aqui** — ela sobe e é tratada pelo `ExceptionHandlingMiddleware` da API, que a converte em HTTP 422.

**Por que não fazer `try/catch` da `DomainException` no handler?** Porque seria ruído. Todo método do domínio pode lançar `DomainException`. Se tratássemos em cada handler, teríamos o mesmo `catch` repetido dezenas de vezes. O middleware centraliza esse tratamento uma única vez.

**④ `SaveChangesAsync`** — persiste o novo item e o valor total recalculado da fatura. Note: `AddAsync` não é chamado aqui porque o item já foi adicionado à coleção em memória da `Fatura`. O EF Core rastreia a mudança e gera o `INSERT` no `itens_fatura` automaticamente.

**⑤ Mapear a resposta manualmente** — sem AutoMapper, sem bibliotecas de mapeamento. Mapeamento explícito: você sabe exatamente o que está enviando ao cliente, o compilador valida os tipos, e não há "magia" que falha em runtime.

### 6.5 `AddItemFaturaResponse` — O DTO de Saída

```csharp
public sealed record AddItemFaturaResponse(
    Guid ItemId,
    Guid FaturaId,
    string Descricao,
    int Quantidade,
    decimal ValorUnitario,
    decimal ValorTotalItem,
    string? Justificativa
);
```

**Por que `record` imutável?** A resposta é uma fotografia do estado após a operação. Não faz sentido modificá-la depois de criada. Records também implementam igualdade por valor — útil em testes (`response.Should().Be(expected)`).

**Por que `string Status` em vez de `StatusFatura` (enum)?** Ao serializar para JSON, um enum vira um número (`0`, `1`). Converter para string (`"Aberta"`, `"Fechada"`) antes de enviar ao cliente torna a API auto-documentada e resistente a reordenação do enum no futuro.

---

## 7. Como os Outros Casos de Uso Seguem o Mesmo Padrão

Todos os demais handlers seguem exatamente a mesma estrutura de `AddItemFatura`:

| Caso de Uso | Diferencial |
|-------------|-------------|
| `CreateFatura` | Sem busca prévia — só cria e salva. Nunca retorna `Failure` (validação pega tudo antes) |
| `FecharFatura` | Busca → verifica null → chama `fatura.Fechar()` → salva. DomainException se já fechada |
| `UpdateItemFatura` | Busca → verifica null → `fatura.AtualizarItem(...)` → salva → relê o item da coleção para montar a resposta |
| `RemoveItemFatura` | Busca → verifica null → `fatura.RemoverItem(...)` → salva → retorna novo ValorTotal |
| `GetFaturaById` | Busca → verifica null → mapeia fatura + lista de itens para response |
| `ListFaturas` | Converte `string? Status` para `StatusFatura?` via `Enum.TryParse` → monta `FaturaFilter` → chama `ListAsync` |

---

## 8. `AddApplication()` — Registro de Dependências

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly));

    services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

    return services;
}
```

**`RegisterServicesFromAssembly`** — o MediatR varre o assembly da Application e registra automaticamente todos os handlers que implementam `IRequestHandler<,>`. Adicionar um novo handler não exige nenhuma linha a mais aqui.

**`AddValidatorsFromAssembly`** — o FluentValidation varre o assembly e registra automaticamente todos os `AbstractValidator<T>`. O `ValidationBehavior` recebe pelo DI `IEnumerable<IValidator<TRequest>>` — se nenhum validator existe para um dado request, a lista fica vazia e o behavior passa direto.

**Ordem dos behaviors importa:**
```csharp
services.AddTransient(..., typeof(LoggingBehavior<,>));     // 1° registrado = 1° executado
services.AddTransient(..., typeof(ValidationBehavior<,>));  // 2° registrado = 2° executado
```
O `LoggingBehavior` fica por fora para capturar tanto requests válidos quanto inválidos. O `ValidationBehavior` fica por dentro para validar antes do handler, mas depois do log.

**`Transient` para behaviors** — behaviors são stateless (sem estado). `Transient` é o lifetime mais leve: uma nova instância a cada uso, descartada imediatamente. Correto para comportamentos que não guardam dados entre chamadas.

---

## 9. Diagrama do Fluxo Completo

```
┌─────────────────────────────────────────────────────────────────┐
│                     FATURAS.APPLICATION                         │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  AddItemFaturaRequest (ICommand)                        │   │
│  │  ─────────────────────────────────────────────────────  │   │
│  │  FaturaId, Descricao, Quantidade, ValorUnitario,        │   │
│  │  Justificativa?                                         │   │
│  └────────────────────────┬────────────────────────────────┘   │
│                           │ ISender.Send(request)               │
│                           ▼                                     │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  Pipeline MediatR                                       │   │
│  │  ┌──────────────────────────────────────────────────┐  │   │
│  │  │ LoggingBehavior → loga início                    │  │   │
│  │  │  ┌───────────────────────────────────────────┐   │  │   │
│  │  │  │ ValidationBehavior                        │   │  │   │
│  │  │  │  AddItemFaturaValidator:                  │   │  │   │
│  │  │  │  • FaturaId not empty                     │   │  │   │
│  │  │  │  • Descricao not empty, min 3 chars       │   │  │   │
│  │  │  │  • Quantidade > 0                         │   │  │   │
│  │  │  │  • ValorUnitario > 0                      │   │  │   │
│  │  │  │  → falha: ValidationException (400)       │   │  │   │
│  │  │  │  ┌────────────────────────────────────┐   │   │  │   │
│  │  │  │  │ AddItemFaturaHandler                │   │   │  │   │
│  │  │  │  │  1. GetByIdAsync(faturaId)          │   │   │  │   │
│  │  │  │  │     null → Result.Failure (404)     │   │   │  │   │
│  │  │  │  │  2. fatura.AdicionarItem(...)       │   │   │  │   │
│  │  │  │  │     DomainException → (422)         │   │   │  │   │
│  │  │  │  │  3. SaveChangesAsync()              │   │   │  │   │
│  │  │  │  │  4. Result.Success(response)        │   │   │  │   │
│  │  │  │  └────────────────────────────────────┘   │   │  │   │
│  │  │  └───────────────────────────────────────────┘   │  │   │
│  │  │ LoggingBehavior → loga fim                       │  │   │
│  │  └──────────────────────────────────────────────────┘  │   │
│  └─────────────────────────────────────────────────────────┘   │
│                           │                                     │
│                           ▼                                     │
│           Result<AddItemFaturaResponse>                        │
│           IsSuccess → 201 Created                              │
│           IsFailure (NotFound) → 404                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## 10. Resumo das Decisões Técnicas

| Decisão | Alternativa | Por que escolhemos assim |
|---------|-------------|--------------------------|
| MediatR como barramento | Injetar handlers diretamente | Desacoplamento total; controller não conhece handlers; behaviors são transparentes |
| CQRS (Commands / Queries separados) | Tudo num só "Service" | Cada operação tem responsabilidade única; queries podem ser otimizadas independentemente |
| `record` para Request e Response | `class` mutável | Imutabilidade por padrão; igualdade por valor; perfeito para DTOs que não devem mudar |
| Result pattern para "não encontrado" | Lançar `NotFoundException` | Cenário esperado, não excepcional; custo de exceção evitado; controller faz decisão explícita |
| `DomainException` sobe sem catch | Try/catch em cada handler | DRY — o middleware captura uma única vez; handlers ficam limpos |
| `ValidationBehavior` no pipeline | Validar dentro de cada handler | Zero código de validação nos handlers; adicionar novo validator não exige mudar o handler |
| `LoggingBehavior` antes de `ValidationBehavior` | Após validação | Loga mesmo requests inválidos — visibilidade total do que chegou no sistema |
| Mapeamento manual (sem AutoMapper) | AutoMapper | Explícito, compile-safe, sem convenções implícitas que falham em runtime |
| `ApplicationErrorMessages` centralizados | Strings inline | Uma mudança de mensagem não quebra testes; validators e testes referenciam a mesma constante |
| `AddValidatorsFromAssembly` | Registrar cada validator manualmente | Adicionar validator novo é criar o arquivo — sem tocar no registro |
