# PLAN.md — Desafio Técnico: Sistema de Gestão de Faturas

> Aplicação ASP.NET MVC + API REST para gestão de Faturas e Itens de Fatura, com Clean Architecture, DDD, CQRS, EF Core, PostgreSQL e DbUp.

---

## 1. Visão Geral

| Item | Definição |
|------|-----------|
| **Stack** | .NET 8, C# 12, ASP.NET Core MVC, EF Core 8, PostgreSQL, DbUp |
| **Arquitetura** | Clean Architecture + DDD + CQRS |
| **Testes** | xUnit, Bogus, FluentAssertions, padrão AAA |
| **Validação** | FluentValidation |
| **DTOs** | `record` (imutáveis) |
| **Persistência** | EF Core + PostgreSQL (Npgsql) |
| **Migrations** | DbUp com scripts SQL versionados |
| **Front** | ASP.NET MVC (Razor) consumindo a API |

---

## 2. Estrutura da Solution

```
FaturasSolution.sln
│
├── src/
│   ├── Faturas.Domain/                   # Camada de Domínio (Rich Domain + DDD)
│   ├── Faturas.Application/              # Casos de Uso (CQRS, Features)
│   ├── Faturas.Infrastructure/           # EF Core, Repositórios, DbContext
│   ├── Faturas.Infrastructure.Migrations/# DbUp + Scripts SQL
│   ├── Faturas.Api/                      # API REST (Controllers, Middlewares)
│   └── Faturas.Web/                      # Projeto MVC (View)
│
└── tests/
    ├── Faturas.Domain.Tests/             # Testes de unidade do domínio
    ├── Faturas.Application.Tests/        # Testes de Handlers/Validators
    └── Faturas.Integration.Tests/        # Testes de integração API + DB
```

---

## 3. Camada de Domínio (`Faturas.Domain`)

### 3.1 Princípios

Domínio **rico**, com regras de negócio dentro das próprias entidades, encapsulando estado e comportamento. Nada de modelos anêmicos. As propriedades têm `set` privado e a mutação ocorre apenas via métodos do agregado.

### 3.2 Estrutura de pastas

```
Faturas.Domain/
├── Common/
│   ├── Entity.cs                         # Base com Id (Guid)
│   ├── AggregateRoot.cs                  # Marca raízes de agregado
│   ├── ValueObject.cs                    # Base para Value Objects
│   ├── DomainException.cs                # Exceção base do domínio
│   └── IDomainEvent.cs                   # Interface de eventos (extensível)
│
├── Faturas/
│   ├── Fatura.cs                         # Aggregate Root
│   ├── ItemFatura.cs                     # Entity dentro do agregado
│   ├── StatusFatura.cs                   # Enum (Aberta, Fechada)
│   ├── ValueObjects/
│   │   ├── NumeroFatura.cs               # VO com validação de formato
│   │   └── Dinheiro.cs                   # VO Money (valor + invariantes)
│   ├── Events/
│   │   ├── FaturaCriadaEvent.cs
│   │   ├── ItemAdicionadoEvent.cs
│   │   └── FaturaFechadaEvent.cs
│   ├── Repositories/
│   │   └── IFaturaRepository.cs          # Contrato (implementado em Infra)
│   └── Errors/
│       └── FaturaErrors.cs               # Constantes de mensagens do domínio
```

### 3.3 Modelagem das Entidades

#### `Fatura` (Aggregate Root)

| Propriedade | Tipo | Observação |
|-------------|------|------------|
| `Id` | `Guid` | Identidade |
| `Numero` | `NumeroFatura` (VO) | Imutável após criação |
| `NomeCliente` | `string` | Obrigatório |
| `DataEmissao` | `DateTime` | UTC |
| `Status` | `StatusFatura` | Inicia como `Aberta` |
| `ValorTotal` | `decimal` | **Calculado** a partir dos itens |
| `Itens` | `IReadOnlyCollection<ItemFatura>` | Coleção encapsulada |

Comportamentos (métodos públicos do agregado):

- `static Criar(numero, nomeCliente, dataEmissao)` — fábrica que valida invariantes (RN-1, RN-2).
- `AdicionarItem(descricao, quantidade, valorUnitario, justificativa?)` — bloqueia se Fechada (RN-5/6), valida justificativa (RN-7), recalcula total (RN-4).
- `RemoverItem(itemId)` — bloqueia se Fechada.
- `AtualizarItem(...)` — bloqueia se Fechada.
- `Fechar()` — RN-9. Transiciona estado.
- `RecalcularTotal()` — privado, chamado após mutação de itens.

#### `ItemFatura` (Entity)

| Propriedade | Tipo | Observação |
|-------------|------|------------|
| `Id` | `Guid` | |
| `Descricao` | `string` | Obrigatório, mínimo 3 caracteres (RN-8) |
| `Quantidade` | `int` | > 0 |
| `ValorUnitario` | `decimal` | > 0 |
| `ValorTotalItem` | `decimal` | Calculado |
| `Justificativa` | `string?` | Obrigatória se ValorTotalItem > 1000 (RN-7) |

### 3.4 Mapeamento Regras de Negócio → Domínio

| RN | Regra | Onde é implementada |
|----|-------|---------------------|
| 1 | Status inicial Aberta | `Fatura.Criar` |
| 2 | Nome cliente obrigatório | `Fatura.Criar` (Guard) |
| 3 | Pode conter um ou mais itens | `Fatura.AdicionarItem` |
| 4 | Recalcular total | `Fatura.RecalcularTotal` |
| 5 | Fechada não pode ser alterada | Guard em todos os métodos mutáveis |
| 6 | Bloqueio adicionar/editar/remover em fatura fechada | Idem |
| 7 | Item > R$ 1.000 exige justificativa | `ItemFatura.Criar` |
| 8 | Descrição obrigatória + tamanho mínimo | `ItemFatura.Criar` |
| 9 | Possível fechar fatura | `Fatura.Fechar` |
| 10 | Consultar com filtros | Camada de Application (Queries) |

---

## 4. Camada de Application (`Faturas.Application`)

### 4.1 Estrutura de pastas

```
Faturas.Application/
├── Common/
│   ├── Behaviors/
│   │   ├── ValidationBehavior.cs         # Pipeline behavior do MediatR
│   │   └── LoggingBehavior.cs
│   ├── Mediator/
│   │   ├── ICommand.cs                   # Marker interface
│   │   ├── IQuery.cs
│   │   ├── ICommandHandler.cs
│   │   └── IQueryHandler.cs
│   ├── Results/
│   │   ├── Result.cs                     # Result pattern (sucesso/erro)
│   │   └── Error.cs
│   └── Errors/
│       └── ApplicationErrorMessages.cs   # ★ Arquivo central de mensagens
│
├── Features/
│   └── Faturas/
│       ├── Commands/
│       │   ├── CreateFatura/
│       │   │   ├── CreateFaturaRequest.cs    # record
│       │   │   ├── CreateFaturaResponse.cs   # record
│       │   │   ├── CreateFaturaHandler.cs
│       │   │   └── CreateFaturaValidator.cs  # FluentValidation
│       │   ├── AddItemFatura/
│       │   │   ├── AddItemFaturaRequest.cs
│       │   │   ├── AddItemFaturaResponse.cs
│       │   │   ├── AddItemFaturaHandler.cs
│       │   │   └── AddItemFaturaValidator.cs
│       │   ├── FecharFatura/
│       │   │   ├── FecharFaturaRequest.cs
│       │   │   ├── FecharFaturaResponse.cs
│       │   │   ├── FecharFaturaHandler.cs
│       │   │   └── FecharFaturaValidator.cs
│       │   ├── UpdateItemFatura/
│       │   │   └── ...
│       │   └── RemoveItemFatura/
│       │       └── ...
│       └── Queries/
│           ├── GetFaturaById/
│           │   ├── GetFaturaByIdRequest.cs
│           │   ├── GetFaturaByIdResponse.cs
│           │   └── GetFaturaByIdHandler.cs
│           └── ListFaturas/
│               ├── ListFaturasRequest.cs   # Filtros: cliente, dtIni, dtFim, status
│               ├── ListFaturasResponse.cs
│               ├── ListFaturasHandler.cs
│               └── ListFaturasValidator.cs
│
└── DependencyInjection/
    └── ServiceCollectionExtensions.cs    # ★ AddApplication()
```

### 4.2 DTOs com `record`

Todos os Request/Response são `record` imutáveis. Exemplo:

```csharp
public sealed record CreateFaturaRequest(
    string Numero,
    string NomeCliente,
    DateTime DataEmissao
) : ICommand<Result<CreateFaturaResponse>>;

public sealed record CreateFaturaResponse(
    Guid Id,
    string Numero,
    string NomeCliente,
    DateTime DataEmissao,
    string Status,
    decimal ValorTotal
);
```

### 4.3 Arquivo central de mensagens de erro

`Common/Errors/ApplicationErrorMessages.cs` — classe estática com **todas** as mensagens usadas pelos validators e handlers. Estrutura recomendada:

```csharp
public static class ApplicationErrorMessages
{
    public static class Fatura
    {
        public const string NumeroObrigatorio   = "O número da fatura é obrigatório.";
        public const string NomeClienteObrigatorio = "O nome do cliente é obrigatório.";
        public const string NomeClienteTamanhoMaximo = "O nome do cliente deve ter no máximo {0} caracteres.";
        public const string FaturaNaoEncontrada = "Fatura não encontrada.";
        public const string FaturaJaFechada     = "Não é possível alterar uma fatura fechada.";
        // ...
    }

    public static class ItemFatura
    {
        public const string DescricaoObrigatoria = "A descrição do item é obrigatória.";
        public const string DescricaoTamanhoMinimo = "A descrição deve ter no mínimo {0} caracteres.";
        public const string QuantidadeInvalida   = "A quantidade deve ser maior que zero.";
        public const string ValorUnitarioInvalido = "O valor unitário deve ser maior que zero.";
        public const string JustificativaObrigatoriaAcimaDe1000 = "Itens acima de R$ 1.000,00 exigem justificativa.";
        // ...
    }
}
```

### 4.4 Validators (FluentValidation)

Cada caso de uso tem um validator dedicado, que **referencia as constantes** do arquivo central:

```csharp
public sealed class CreateFaturaValidator : AbstractValidator<CreateFaturaRequest>
{
    public CreateFaturaValidator()
    {
        RuleFor(x => x.Numero)
            .NotEmpty().WithMessage(ApplicationErrorMessages.Fatura.NumeroObrigatorio);

        RuleFor(x => x.NomeCliente)
            .NotEmpty().WithMessage(ApplicationErrorMessages.Fatura.NomeClienteObrigatorio)
            .MaximumLength(150).WithMessage(string.Format(ApplicationErrorMessages.Fatura.NomeClienteTamanhoMaximo, 150));
    }
}
```

### 4.5 ServiceCollectionExtensions modular

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly));
        services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        return services;
    }
}
```

---

## 5. Camada de Infrastructure (`Faturas.Infrastructure`)

### 5.1 Estrutura

```
Faturas.Infrastructure/
├── Persistence/
│   ├── FaturasDbContext.cs
│   ├── Configurations/
│   │   ├── FaturaConfiguration.cs        # IEntityTypeConfiguration<Fatura>
│   │   └── ItemFaturaConfiguration.cs
│   └── Interceptors/
│       └── AuditableInterceptor.cs       # opcional (CreatedAt/UpdatedAt)
│
├── Repositories/
│   └── FaturaRepository.cs               # Implementa IFaturaRepository
│
└── DependencyInjection/
    └── ServiceCollectionExtensions.cs    # AddInfrastructure(IConfiguration)
```

### 5.2 Pacotes NuGet

| Pacote | Versão | Por quê |
|--------|--------|---------|
| `Microsoft.EntityFrameworkCore` | 8.0.x | EF Core compatível com .NET 8 |
| `Microsoft.EntityFrameworkCore.Relational` | 8.0.x | Suporte relacional |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 8.0.x | Provider PostgreSQL |

### 5.3 DbContext

`FaturasDbContext` mapeia o agregado `Fatura` com:

- Coleção `Itens` mapeada via `OwnsMany` ou `HasMany` (preferir `HasMany` por ter Id próprio).
- Value Objects mapeados com `OwnsOne` ou conversores.
- Concorrência otimista (`xmin` do Postgres) opcional.
- Snake_case naming (sufixos `_id`, tabela `faturas`, `itens_fatura`).

### 5.4 Repositórios

Repositório por agregado (`IFaturaRepository`), retornando o agregado completo (Eager loading dos itens). Métodos típicos: `GetByIdAsync`, `ListAsync(filters)`, `AddAsync`, `Update`, `SaveChangesAsync` (UoW via DbContext).

### 5.5 ServiceCollectionExtensions

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
{
    services.AddDbContext<FaturasDbContext>(opt =>
        opt.UseNpgsql(cfg.GetConnectionString("Postgres")));

    services.AddScoped<IFaturaRepository, FaturaRepository>();

    return services;
}
```

---

## 6. Camada de Migrations (`Faturas.Infrastructure.Migrations`)

> Projeto **separado** do EF Core. Aqui o EF Core **não** gera migrations — o DbUp aplica scripts SQL versionados.

### 6.1 Estrutura

```
Faturas.Infrastructure.Migrations/
├── Scripts/
│   ├── 0001_create_schema.sql
│   ├── 0002_create_table_faturas.sql
│   ├── 0003_create_table_itens_fatura.sql
│   ├── 0004_indexes_faturas.sql
│   └── 0005_seed_dados_iniciais.sql      (opcional)
├── MigrationRunner.cs                    # Programa console que roda DbUp
├── Faturas.Infrastructure.Migrations.csproj
└── README.md
```

### 6.2 Pacote NuGet

- `dbup-postgresql` — provider DbUp para PostgreSQL.

### 6.3 Convenções de scripts

- Numeração sequencial (`0001_`, `0002_` …).
- Marcados como **Embedded Resource** no `.csproj`.
- Idempotência sempre que possível (`CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`).
- Sem rollback automático: rollback manual via novo script.

### 6.4 Runner

```csharp
public static int Main(string[] args)
{
    var connectionString = args.FirstOrDefault() ?? Environment.GetEnvironmentVariable("POSTGRES_CONN");

    EnsureDatabase.For.PostgresqlDatabase(connectionString);

    var upgrader = DeployChanges.To
        .PostgresqlDatabase(connectionString)
        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
        .LogToConsole()
        .Build();

    var result = upgrader.PerformUpgrade();
    return result.Successful ? 0 : -1;
}
```

### 6.5 Execução

- Local: `dotnet run --project src/Faturas.Infrastructure.Migrations -- "Host=localhost;..."`.
- API: pode chamar o runner no startup em `Development` (opcional).

---

## 7. Camada de API (`Faturas.Api`)

### 7.1 Estrutura

```
Faturas.Api/
├── Controllers/
│   └── FaturasController.cs
├── Middlewares/
│   └── ExceptionHandlingMiddleware.cs    # Map domain/validation exceptions
├── Filters/
│   └── ValidationFilter.cs               # opcional
├── Program.cs                            # AddApplication + AddInfrastructure
└── appsettings.json
```

### 7.2 Endpoints (mínimos exigidos pelo desafio)

| Método | Rota | Caso de Uso |
|--------|------|-------------|
| `POST` | `/api/faturas` | `CreateFatura` |
| `POST` | `/api/faturas/{id}/itens` | `AddItemFatura` |
| `PUT`  | `/api/faturas/{id}/fechar` | `FecharFatura` |
| `GET`  | `/api/faturas` | `ListFaturas` (filtros: `cliente`, `dataInicial`, `dataFinal`, `status`) |
| `GET`  | `/api/faturas/{id}` | `GetFaturaById` |

### 7.3 Cross-cutting

- Swagger/OpenAPI ligado.
- `ExceptionHandlingMiddleware` retornando `ProblemDetails` (RFC 7807).
- HTTPS Redirection.
- Validação automática via `ValidationBehavior` (MediatR pipeline).
- Logging estruturado (Serilog opcional).

---

## 8. Camada View — MVC (`Faturas.Web`)

### 8.1 Estrutura

```
Faturas.Web/
├── Controllers/
│   ├── HomeController.cs
│   └── FaturasController.cs
├── Views/
│   ├── Shared/
│   │   ├── _Layout.cshtml
│   │   └── _ValidationScriptsPartial.cshtml
│   └── Faturas/
│       ├── Index.cshtml      # Lista com filtros
│       ├── Details.cshtml    # Detalhes + itens
│       ├── Create.cshtml     # Form criar fatura
│       └── _AddItem.cshtml   # Partial form adicionar item
├── Models/
│   └── ViewModels/
│       ├── FaturaListViewModel.cs
│       ├── FaturaDetailsViewModel.cs
│       ├── CreateFaturaViewModel.cs
│       └── AddItemViewModel.cs
├── Services/
│   └── FaturasApiClient.cs   # Typed HttpClient para a API
├── wwwroot/
└── Program.cs
```

### 8.2 Boas práticas

- **Typed HttpClient** para consumir a API (`AddHttpClient<IFaturasApiClient, FaturasApiClient>`).
- ViewModels separados das Requests/Responses da API.
- Tag Helpers para forms.
- Validação client-side via `jquery.validate.unobtrusive`.
- Antiforgery token nos forms.
- Tratamento de erros amigável (página de erro custom).

---

## 9. Camada de Testes

### 9.1 Pacotes comuns aos projetos de teste

| Pacote | Uso |
|--------|-----|
| `xunit` | Framework de testes |
| `xunit.runner.visualstudio` | Runner |
| `FluentAssertions` | Assertions legíveis |
| `Bogus` | Geração de dados fake |
| `Microsoft.NET.Test.Sdk` | SDK |

### 9.2 `Faturas.Domain.Tests`

Testes de unidade puros do domínio (sem mocks, sem infra).

```
Faturas.Domain.Tests/
├── Faturas/
│   ├── FaturaTests.cs
│   ├── ItemFaturaTests.cs
│   └── Builders/
│       ├── FaturaFaker.cs        # Bogus Faker<Fatura>
│       └── ItemFaturaFaker.cs
```

Cobertura mínima exigida pelo desafio:

| Cenário | Teste |
|---------|-------|
| Criação de fatura válida | `Criar_DeveCriarFatura_QuandoDadosValidos` |
| Criação inválida (sem nome cliente) | `Criar_DeveLancar_QuandoNomeClienteVazio` |
| Bloqueio em fatura fechada | `AdicionarItem_DeveLancar_QuandoFaturaFechada` |
| Item > R$ 1.000 sem justificativa | `AdicionarItem_DeveLancar_QuandoValorMaiorQue1000ESemJustificativa` |
| Item > R$ 1.000 com justificativa | `AdicionarItem_DeveAdicionar_QuandoValorMaiorQue1000ComJustificativa` |
| Recálculo do valor total | `AdicionarItem_DeveRecalcularValorTotal` |
| Fechar fatura | `Fechar_DeveAlterarStatusParaFechada` |
| Descrição com tamanho inválido | `Item_DeveLancar_QuandoDescricaoMenorQueMinimo` |

### 9.3 `Faturas.Application.Tests`

Testes de Handlers e Validators usando mocks (`NSubstitute` ou `Moq`).

```
Faturas.Application.Tests/
├── Features/Faturas/
│   ├── Commands/
│   │   ├── CreateFaturaHandlerTests.cs
│   │   ├── CreateFaturaValidatorTests.cs
│   │   ├── AddItemFaturaHandlerTests.cs
│   │   ├── AddItemFaturaValidatorTests.cs
│   │   └── FecharFaturaHandlerTests.cs
│   └── Queries/
│       ├── GetFaturaByIdHandlerTests.cs
│       └── ListFaturasHandlerTests.cs
└── Common/
    └── Fakers/                  # Fakers reutilizáveis com Bogus
```

### 9.4 Padrão AAA + FluentAssertions + Bogus

Exemplo canônico:

```csharp
[Fact]
public void AdicionarItem_DeveLancar_QuandoValorMaiorQue1000ESemJustificativa()
{
    // Arrange
    var fatura = new FaturaFaker().Generate();
    var faker = new Faker();
    var descricao = faker.Commerce.ProductName();
    var quantidade = 2;
    var valorUnitario = 600m; // total = 1200

    // Act
    Action act = () => fatura.AdicionarItem(descricao, quantidade, valorUnitario, justificativa: null);

    // Assert
    act.Should().Throw<DomainException>()
       .WithMessage(FaturaErrors.JustificativaObrigatoriaAcimaDe1000);
}
```

### 9.5 `Faturas.Integration.Tests` (opcional / bônus)

- `WebApplicationFactory<Program>` para subir a API em memória.
- `Testcontainers.PostgreSql` para subir Postgres real.
- DbUp aplicado antes da bateria.
- Cobre fluxo end-to-end: cria fatura → adiciona item → fecha → não permite adicionar.

---

## 10. Tratamento de erros

| Camada | Mecanismo |
|--------|-----------|
| Domínio | `DomainException` lançada nos guards |
| Application | Validators (FluentValidation) + Result pattern |
| API | `ExceptionHandlingMiddleware` mapeia para `ProblemDetails` HTTP adequado |

Mapeamentos:

| Exceção | HTTP |
|---------|------|
| `ValidationException` (FluentValidation) | 400 |
| `DomainException` | 422 |
| `NotFoundException` | 404 |
| Outras | 500 (logada, mensagem genérica ao cliente) |

---

## 11. Segurança e qualidade

- Validação de entrada em todas as Requests via FluentValidation.
- Sanitização implícita do EF Core (queries parametrizadas).
- Antiforgery nos forms MVC.
- HTTPS obrigatório.
- Headers de segurança (`X-Content-Type-Options`, `X-Frame-Options`).
- Logging sem dados sensíveis.
- `ConfigureAwait(false)` onde aplicável.
- Análise estática: `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` + nullable enabled.

---

## 12. Roadmap de execução (ordem sugerida)

1. **Setup da Solution**
   - Criar `.sln` e os 6 projetos `src/` + 3 projetos `tests/`.
   - Configurar referências entre projetos.
   - `Directory.Build.props` com `LangVersion=latest`, `Nullable=enable`, `TreatWarningsAsErrors`.

2. **Domínio**
   - Implementar `Entity`, `AggregateRoot`, `ValueObject`, `DomainException`.
   - Implementar `Fatura`, `ItemFatura`, `StatusFatura`, VOs.
   - Escrever **todos** os testes unitários (TDD friendly).

3. **Migrations (DbUp)**
   - Scripts SQL: schema, faturas, itens_fatura, índices.
   - Runner DbUp.
   - Testar localmente contra Postgres.

4. **Infrastructure**
   - `FaturasDbContext` + Configurations.
   - `FaturaRepository`.
   - `AddInfrastructure` extension.

5. **Application**
   - `ApplicationErrorMessages`.
   - Result/Error.
   - MediatR + ValidationBehavior.
   - Features: Commands (Create, AddItem, Fechar, Update, Remove) + Queries (GetById, List).
   - Validators e testes.

6. **API**
   - Controllers, Middleware, Swagger.
   - Testar endpoints com REST Client / Postman.

7. **Integration tests** (bônus).

8. **MVC (Web)**
   - Typed HttpClient.
   - ViewModels e Views.
   - Tela de listagem com filtros, criação, detalhes, adicionar item, fechar.

9. **Documentação**
   - `README.md` com:
     - Tecnologias usadas.
     - Como rodar Postgres (Docker compose recomendado).
     - Como rodar migrations (`dotnet run --project Faturas.Infrastructure.Migrations`).
     - Como rodar a API e o MVC.
     - Como rodar os testes (`dotnet test`).
     - Premissas e decisões técnicas.
     - Melhorias futuras.

10. **Empacotamento / Repositório**
    - `.gitignore` adequado.
    - `docker-compose.yml` para Postgres (e opcionalmente API).
    - Tag de versão final.

---

## 13. Política de documentação contínua (README.md no GitHub)

> **Regra obrigatória do projeto:** a cada desenvolvimento concluído (entrega de uma camada, feature, caso de uso, conjunto de testes ou qualquer item do roadmap da seção 12), o `README.md` do repositório no GitHub **deve ser atualizado** descrevendo o que foi entregue.

### 13.1 Quando documentar

Atualizar o `README.md` ao concluir, no mínimo:

- Setup inicial da Solution e estrutura de projetos
- Implementação de cada camada (Domain, Application, Infrastructure, Migrations, API, MVC)
- Cada caso de uso entregue (Create/AddItem/Fechar/List/GetById, etc.)
- Cada bateria de testes (Domain, Application, Integration)
- Cada script de migration novo
- Configurações de infraestrutura (Docker, Postgres, variáveis de ambiente)
- Decisões técnicas relevantes ou mudanças de premissa

### 13.2 O que registrar a cada atualização

Cada entrega documentada deve conter:

- **O que foi feito**: breve descrição da feature ou camada entregue
- **Como executar / testar**: comandos, endpoints, exemplos de payload
- **Dependências adicionadas**: novos pacotes NuGet, bibliotecas, ferramentas
- **Decisões técnicas**: justificativas relevantes da entrega
- **Status**: marcar no checklist da seção 13.3 como `[x]`

### 13.3 Estrutura mínima sugerida do `README.md`

```
# Faturas — Gestão de Faturas e Itens

## Tecnologias
## Arquitetura
## Como executar
   - Pré-requisitos
   - Banco de dados (Postgres + DbUp)
   - API
   - MVC
## Como rodar os testes
## Endpoints da API
## Estrutura da Solution
## Histórico de entregas      ← ★ atualizado a cada desenvolvimento concluído
   - [yyyy-mm-dd] Setup da Solution
   - [yyyy-mm-dd] Camada de Domínio (Fatura, ItemFatura, VOs)
   - [yyyy-mm-dd] Caso de uso CreateFatura
   - ...
## Premissas adotadas
## Decisões técnicas
## Melhorias futuras
```

### 13.4 Fluxo recomendado de commits

1. Implementar a entrega
2. Cobrir com testes
3. Atualizar `README.md` (seção *Histórico de entregas* + seções afetadas)
4. Commit único ou commit de docs separado: `docs(readme): adiciona <feature>`
5. Push para o GitHub

> **Importante:** entregas que não estiverem refletidas no `README.md` do GitHub são consideradas incompletas dentro deste projeto.

---

## 14. Checklist final de aderência ao desafio

- [ ] CRUD completo de Faturas e Itens
- [ ] Status inicial Aberta (RN-1)
- [ ] Nome cliente obrigatório (RN-2)
- [ ] Múltiplos itens por fatura (RN-3)
- [ ] Recálculo automático do total (RN-4)
- [ ] Bloqueio de alteração em fatura fechada (RN-5/6)
- [ ] Justificativa obrigatória > R$ 1.000 (RN-7)
- [ ] Descrição obrigatória com tamanho mínimo (RN-8)
- [ ] Endpoint para fechar fatura (RN-9)
- [ ] Filtros de consulta: cliente, período, status (RN-10)
- [ ] Endpoints exigidos implementados
- [ ] Testes automatizados cobrindo todos os cenários mínimos
- [ ] README completo
- [ ] Script de criação do banco (DbUp)
- [ ] Validação de entrada
- [ ] Tratamento de erros padronizado

---

> **Premissa principal:** banco PostgreSQL substituindo o SQL Server sugerido (permitido pelo enunciado: *"SQL Server ou outro banco relacional, desde que documentado"*), com EF Core 8 + Npgsql e migrations gerenciadas pelo DbUp via scripts SQL versionados.
