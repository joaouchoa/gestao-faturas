# Faturas — Gestão de Faturas e Itens

> Sistema completo de gestão de faturas com API REST e interface MVC, construído com Clean Architecture, DDD e CQRS em .NET 8.

---

## Tecnologias

| Camada | Tecnologia | Versão |
|--------|-----------|--------|
| Runtime | .NET / C# | 8.0 / 12 |
| API | ASP.NET Core (Controllers) | 8.0 |
| Front-end | ASP.NET Core MVC (Razor) | 8.0 |
| ORM | Entity Framework Core + Npgsql | 8.0.10 |
| Banco de dados | PostgreSQL | 16 |
| Migrations | DbUp | 5.0.37 |
| CQRS / Mediator | MediatR | 12.4.1 |
| Validação | FluentValidation | 11.11.0 |
| Documentação da API | Swashbuckle (Swagger/OpenAPI) | 6.9.0 |
| Testes unitários | xUnit + FluentAssertions + Bogus | — |
| Mocking | NSubstitute | 5.1.0 |
| Testes de integração | Testcontainers.PostgreSql + WebApplicationFactory | 3.10.0 / 8.0.0 |

---

## Arquitetura

O projeto segue **Clean Architecture** com separação de camadas, **Domain-Driven Design (DDD)** com domínio rico e **CQRS** via MediatR.

```
┌─────────────────────────────────────────────────────────────┐
│  Faturas.Web (MVC)          Faturas.Api (REST)              │
│        │  Typed HttpClient        │  Controllers            │
└────────┼─────────────────────────┼─────────────────────────┘
         │                         │
         └──────────┬──────────────┘
                    ▼
┌─────────────────────────────────────────────────────────────┐
│  Faturas.Application                                        │
│  Commands / Queries / Handlers / Validators / Behaviors     │
└───────────────────────┬─────────────────────────────────────┘
                        │
          ┌─────────────┼─────────────┐
          ▼             ▼             ▼
┌──────────────┐ ┌────────────────┐ ┌──────────────────────┐
│ Faturas      │ │ Faturas        │ │ Faturas.Infra        │
│ .Domain      │ │ .Infrastructure│ │ structure.Migrations │
│ (Aggregates, │ │ (EF Core,      │ │ (DbUp + Scripts SQL) │
│  VOs, Events)│ │  Repositórios) │ │                      │
└──────────────┘ └────────────────┘ └──────────────────────┘
```

**Fluxo de uma requisição:** `HTTP → Controller → MediatR → ValidationBehavior → Handler → Repository → EF Core → PostgreSQL`

---

## Como executar

### Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (para o PostgreSQL)

### 1. Banco de dados (PostgreSQL via Docker)

```bash
docker run -d \
  --name faturas-postgres \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=faturas \
  -p 5432:5432 \
  postgres:16-alpine
```

> **Alternativa:** use o `docker-compose.yml` na raiz do repositório — veja a seção **Docker Compose** abaixo.

### 2. Rodar as migrations (DbUp)

```bash
dotnet run --project src/Faturas.Infrastructure.Migrations \
  -- "Host=localhost;Port=5432;Database=faturas;Username=postgres;Password=postgres"
```

As migrations são scripts SQL versionados embarcados no assembly (`0001` → `0004`). O DbUp os aplica em ordem e é idempotente — pode ser executado múltiplas vezes com segurança.

### 3. Rodar a API

```bash
dotnet run --project src/Faturas.Api
```

| Protocolo | URL |
|-----------|-----|
| HTTPS | `https://localhost:7159` |
| HTTP | `http://localhost:5127` |
| Swagger UI | `https://localhost:7159/swagger` |

A string de conexão pode ser sobrescrita via variável de ambiente:

```bash
ConnectionStrings__Postgres="Host=...;..." dotnet run --project src/Faturas.Api
```

### 4. Rodar o MVC

A API deve estar em execução antes de iniciar o MVC.

```bash
dotnet run --project src/Faturas.Web
```

| Protocolo | URL |
|-----------|-----|
| HTTPS | `https://localhost:7238` |
| HTTP | `http://localhost:5194` |

A URL base da API é configurada em `src/Faturas.Web/appsettings.json`:

```json
{
  "ApiBaseUrl": "https://localhost:7159"
}
```

### Docker Compose

```bash
docker compose up -d
```

Sobe o PostgreSQL configurado e pronto para uso (ver `docker-compose.yml` na raiz).

---

## Como rodar os testes

### Pré-requisito para testes de integração

Os testes de integração requerem **Docker em execução** — o Testcontainers sobe um container PostgreSQL automaticamente durante a execução.

### Executar todas as suítes

```bash
dotnet test
```

### Por suíte

```bash
# Testes de domínio (sem infra, sem mocks)
dotnet test tests/Faturas.Domain.Tests

# Testes de Application (handlers e validators com mocks NSubstitute)
dotnet test tests/Faturas.Application.Tests

# Testes de integração E2E (API real + PostgreSQL real em container)
dotnet test tests/Faturas.Integration.Tests
```

### Resultado esperado

| Suíte | Testes | Tipo |
|-------|--------|------|
| `Faturas.Domain.Tests` | 17 | Unitários — sem mocks, sem banco |
| `Faturas.Application.Tests` | 30 | Unitários — mocks do repositório |
| `Faturas.Integration.Tests` | 15 | E2E — API real + PostgreSQL em container |
| **Total** | **62** | |

---

## Endpoints da API

Base URL: `https://localhost:7159/api`

| Método | Rota | Caso de Uso | Retornos possíveis |
|--------|------|-------------|-------------------|
| `POST` | `/faturas` | Criar fatura | `201` / `400` |
| `GET` | `/faturas` | Listar faturas (filtros + paginação) | `200` / `400` |
| `GET` | `/faturas/{id}` | Buscar fatura por ID | `200` / `404` |
| `POST` | `/faturas/{id}/itens` | Adicionar item à fatura | `201` / `400` / `404` / `422` |
| `PUT` | `/faturas/{id}/itens/{itemId}` | Atualizar item da fatura | `200` / `400` / `404` / `422` |
| `DELETE` | `/faturas/{id}/itens/{itemId}` | Remover item da fatura | `200` / `404` / `422` |
| `PUT` | `/faturas/{id}/fechar` | Fechar fatura | `200` / `404` / `422` |

### Filtros disponíveis em `GET /faturas`

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `cliente` | `string` | Filtra por nome do cliente (busca parcial) |
| `dataInicial` | `DateTime` | Início do período de emissão |
| `dataFinal` | `DateTime` | Fim do período de emissão |
| `status` | `string` | `Aberta` ou `Fechada` |
| `pagina` | `int` | Número da página (padrão: `1`) |
| `tamanhoPagina` | `int` | Itens por página (padrão: `10`) |

### Exemplos de payload

**Criar fatura:**
```json
POST /api/faturas
{
  "numero": "NF-2026-0001",
  "nomeCliente": "Empresa XPTO Ltda",
  "dataEmissao": "2026-05-06T00:00:00Z"
}
```

**Adicionar item:**
```json
POST /api/faturas/{id}/itens
{
  "descricao": "Notebook Dell XPS",
  "quantidade": 2,
  "valorUnitario": 4500.00,
  "justificativa": "Aprovado pelo gestor financeiro"
}
```

> Itens com `valorUnitario × quantidade > R$ 1.000,00` **exigem** o campo `justificativa`.

### Mapeamento de erros HTTP

| HTTP | Situação |
|------|---------|
| `400` | Falha de validação de entrada (FluentValidation) |
| `404` | Recurso não encontrado |
| `422` | Violação de regra de negócio do domínio |
| `500` | Erro interno inesperado |

Todas as respostas de erro seguem o padrão `ProblemDetails` (RFC 7807).

---

## Estrutura da Solution

```
FaturasSolution.sln
│
├── src/
│   ├── Faturas.Domain/                    # Domínio rico (Aggregates, VOs, Events, Errors)
│   ├── Faturas.Application/               # Casos de uso (CQRS, Handlers, Validators)
│   ├── Faturas.Infrastructure/            # EF Core, Repositórios, DbContext
│   ├── Faturas.Infrastructure.Migrations/ # DbUp + Scripts SQL versionados (0001–0004)
│   ├── Faturas.Api/                       # API REST (Controllers, Middleware, Swagger)
│   └── Faturas.Web/                       # MVC Razor (Views, ViewModels, Typed HttpClient)
│
├── tests/
│   ├── Faturas.Domain.Tests/              # 17 testes unitários do domínio
│   ├── Faturas.Application.Tests/         # 30 testes de handlers e validators
│   └── Faturas.Integration.Tests/         # 15 testes E2E (API + PostgreSQL real)
│
└── docs/
    ├── documentacao-tecnica-testes.md     # Documentação aprofundada da estratégia de testes
    └── ...                                # Documentação técnica das demais camadas
```

---

## Histórico de entregas

| Data | Entrega |
|------|---------|
| 2026-05-05 | Setup inicial da Solution — estrutura de projetos, referências entre camadas, `Directory.Build.props` |
| 2026-05-05 | **Camada de Domínio** — `Fatura` (Aggregate Root), `ItemFatura`, Value Objects (`NumeroFatura`, `Dinheiro`), eventos de domínio, `FaturaErrors`, 17 testes unitários |
| 2026-05-05 | **Camada de Migrations** — DbUp com scripts SQL versionados (`0001_create_schema` → `0004_indexes_faturas`), runner console |
| 2026-05-05 | **Camada de Infrastructure** — `FaturasDbContext`, `FaturaConfiguration`, `ItemFaturaConfiguration`, `FaturaRepository`, `AddInfrastructure` |
| 2026-05-05 | **Camada de Application** — Commands (CreateFatura, AddItemFatura, UpdateItemFatura, RemoveItemFatura, FecharFatura), Queries (GetFaturaById, ListFaturas), `ValidationBehavior`, `LoggingBehavior`, Result pattern |
| 2026-05-05 | **Camada de API** — `FaturasController` (7 endpoints), `ExceptionHandlingMiddleware`, Swagger/OpenAPI com XML comments |
| 2026-05-05 | **Camada MVC** — Views Razor (listagem com filtros e paginação, detalhes, criar, adicionar item), ViewModels, `FaturasApiClient` (Typed HttpClient) |
| 2026-05-06 | **`Faturas.Application.Tests`** — 30 testes de handlers e validators com NSubstitute + FluentAssertions + Bogus |
| 2026-05-06 | **`Faturas.Integration.Tests`** — 15 testes E2E com WebApplicationFactory + Testcontainers.PostgreSql + DbUp |
| 2026-05-06 | **Documentação técnica** — `docs/documentacao-tecnica-testes.md` (estratégia de testes, ferramentas, decisões) |
| 2026-05-06 | **Seed de dados** — `0005_seed_dados_iniciais.sql` com 21 faturas e 42 itens para demonstração |
| 2026-05-06 | **Docker** — `docker-compose.yml` (PostgreSQL + Migrations + API + MVC), Dockerfiles multi-stage, `.dockerignore` |

---

## Premissas adotadas

| Premissa | Justificativa |
|----------|---------------|
| **PostgreSQL** no lugar de SQL Server | O enunciado permite *"SQL Server ou outro banco relacional, desde que documentado"*. PostgreSQL é gratuito, tem suporte nativo no .NET via Npgsql e é amplamente usado em produção |
| **DbUp** para migrations, não EF Core Migrations | Scripts SQL são artefatos versionáveis, revisáveis em code review e auditáveis. EF Core Migrations geram SQL opaco que dificulta inspeção |
| **Domínio rico** (não anêmico) | Regras de negócio encapsuladas nas próprias entidades. Propriedades com `private set`. Mutação apenas via métodos do agregado |
| **CQRS sem event sourcing** | MediatR como despachante. Leitura e escrita separadas explicitamente em Queries e Commands, compartilhando o mesmo banco |
| **Result pattern** em vez de exceções para fluxo esperado | Exceções reservadas para erros imprevistos. Fluxos esperados (not found, regra de domínio) retornam `Result<T>` com `Error` tipado |
| **Paginação obrigatória** em `ListFaturas` | Proteção contra consultas sem limite retornando volumes arbitrários de dados |
| **`records` imutáveis** para todos os DTOs | Request/Response como `sealed record` — sem estado mutável nos contratos de comunicação entre camadas |

---

## Decisões técnicas

| Decisão | Alternativa considerada | Motivo da escolha |
|---------|------------------------|-------------------|
| MediatR como despachante CQRS | Dispatcher manual | Pipeline behaviors (ValidationBehavior, LoggingBehavior) acoplados de forma declarativa. Controllers desacoplados de handlers |
| FluentValidation separada do domínio | Validação no próprio handler | Validators são testáveis isoladamente. A pipeline do MediatR intercepta e retorna 400 antes do handler executar |
| `ExceptionHandlingMiddleware` centralizado | `try/catch` em cada controller | Um único ponto de mapeamento de exceções para respostas HTTP. Controllers ficam limpos de tratamento de erro |
| Testes de domínio sem mocks | Mocks de infraestrutura | O domínio não tem dependências externas. Testar sem mocks é mais rápido, mais simples e mais fiel ao comportamento real |
| NSubstitute para mocks em `Application.Tests` | Moq | Sintaxe mais fluente: `.Received(1)` vs `.Verify(..., Times.Once())` |
| Testcontainers em `Integration.Tests` | SQLite in-memory | PostgreSQL real evita falso-positivos causados por diferenças entre providers (tipos, constraints, comportamento de índices) |
| `ICollectionFixture` — container Docker compartilhado | Nova instância por classe de teste | Inicializar o container é caro (~3–5 s). Compartilhá-lo entre todas as classes da suíte reduz o tempo total de execução |
| Scripts SQL como `EmbeddedResource` em `Integration.Tests` | Referenciar o projeto de Migrations | O projeto de Migrations é `OutputType=Exe` (não referenciável). Os `.sql` são vinculados via `<EmbeddedResource Link="...">` e embarcados na DLL de teste |
| `public partial class Program {}` em `Program.cs` | `InternalsVisibleTo` no `.csproj` | Top-level statements geram `Program` como `internal`. O `partial` é a abordagem idiomática recomendada pela documentação do ASP.NET Core para testes |
| `ResponseDtos` internos no projeto de integração | Reutilizar DTOs da Application | Isola a suíte de integração de mudanças internas da camada de Application. Desserialização HTTP é um contrato próprio do teste |

---

## Melhorias futuras

| Melhoria | Prioridade | Descrição |
|----------|-----------|-----------|
| Autenticação e autorização | Alta | JWT Bearer com ASP.NET Core Identity ou Keycloak. Controle de acesso por papel (gestor, operador) |
| Soft delete | Média | Marcar registros como excluídos em vez de deletar fisicamente. Preserva histórico e permite auditoria |
| Auditoria (CreatedAt / UpdatedAt) | Média | Interceptor EF Core (`AuditableInterceptor`) para registrar automaticamente datas de criação e última modificação |
| Eventos de domínio via MediatR | Média | Publicar `FaturaCriadaEvent`, `ItemAdicionadoEvent`, `FaturaFechadaEvent` como `INotification` após a persistência |
| Cache de consultas | Baixa | Redis ou cache em memória para `ListFaturas`. Invalida ao criar/fechar fatura |
| Exportação em PDF | Baixa | Geração de PDF via QuestPDF. Endpoint dedicado `GET /faturas/{id}/pdf` |
| Observabilidade | Baixa | OpenTelemetry (traces + métricas) + Serilog com sink para Seq ou Elasticsearch |
| Cobertura de código | Baixa | Relatório com `dotnet-coverage` integrado ao CI/CD (GitHub Actions) |
| Concorrência otimista | Baixa | `xmin` do PostgreSQL como `RowVersion` no EF Core para evitar conflitos de escrita simultânea |
| Rate limiting | Baixa | `Microsoft.AspNetCore.RateLimiting` para proteger endpoints públicos contra abuso |

---

## Checklist de aderência ao desafio

- [x] CRUD completo de Faturas e Itens
- [x] Status inicial Aberta (RN-1)
- [x] Nome cliente obrigatório (RN-2)
- [x] Múltiplos itens por fatura (RN-3)
- [x] Recálculo automático do total (RN-4)
- [x] Bloqueio de alteração em fatura fechada (RN-5/6)
- [x] Justificativa obrigatória > R$ 1.000 (RN-7)
- [x] Descrição obrigatória com tamanho mínimo de 3 caracteres (RN-8)
- [x] Endpoint para fechar fatura (RN-9)
- [x] Filtros de consulta: cliente, período, status (RN-10)
- [x] Todos os endpoints exigidos implementados
- [x] Testes automatizados cobrindo todos os cenários mínimos (62 testes)
- [x] README completo
- [x] Scripts de criação do banco (DbUp, `0001`–`0004`)
- [x] Validação de entrada em todas as requests (FluentValidation)
- [x] Tratamento de erros padronizado (`ProblemDetails` RFC 7807)
