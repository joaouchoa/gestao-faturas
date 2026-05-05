# Documentação Técnica — Camada de API (`Faturas.Api`)

> **Objetivo deste documento:** explicar em profundidade cada decisão conceitual e técnica tomada na construção da camada de API do sistema de Gestão de Faturas. Esta camada é o ponto de entrada HTTP do sistema — ela recebe requisições do mundo externo, as direciona para a Application e devolve respostas padronizadas.

---

## 1. O Papel da API na Arquitetura

A camada de API é a **fronteira do sistema com o mundo externo**. Ela é responsável por:

1. Receber requisições HTTP
2. Deserializar e mapear para commands/queries da Application
3. Delegar ao MediatR (que aciona o pipeline de behaviors + handler)
4. Receber o resultado e traduzi-lo para um status HTTP adequado
5. Tratar exceções não capturadas e retornar respostas padronizadas

O que a API **não faz:**
- Regras de negócio (Domínio)
- Validação de campos (Application — ValidationBehavior)
- Acesso ao banco de dados (Infrastructure)

```
Cliente HTTP
     │
     ▼
ExceptionHandlingMiddleware   ← captura qualquer exceção não tratada
     │
     ▼
FaturasController             ← recebe, mapeia, delega, traduz
     │
     ▼
ISender.Send(command/query)   ← entra no pipeline MediatR
     │
     ▼
Application + Domain + Infrastructure
```

---

## 2. Estrutura do Projeto

```
Faturas.Api/
├── Controllers/
│   └── FaturasController.cs          ← Todos os endpoints de faturas
├── Middlewares/
│   └── ExceptionHandlingMiddleware.cs ← Tratamento centralizado de erros
├── Program.cs                         ← Composição e configuração da aplicação
├── appsettings.json                   ← Configurações base (connection string)
├── appsettings.Development.json       ← Overrides para ambiente de desenvolvimento
└── Properties/
    └── launchSettings.json            ← Perfis de execução local
```

**Por que um único controller?** O sistema tem apenas um agregado (`Fatura`) com seus itens. Um controller por agregado é a escolha natural — evita fragmentação desnecessária. Se o sistema crescer com novos agregados (ex: `Clientes`, `Produtos`), cada um ganha seu próprio controller.

---

## 3. `Program.cs` — Composição da Aplicação

```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(...);

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(...);
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
```

### 3.1 Ordem de Registro dos Serviços

**`AddApplication()` antes de `AddInfrastructure()`** — convenção de legibilidade: camadas internas antes de externas. Na prática, a ordem de `AddXxx` no DI container não afeta o funcionamento (exceto quando há decorators ou comportamentos que dependem da ordem de resolução).

### 3.2 Ordem dos Middlewares (Pipeline HTTP)

A ordem dos middlewares no ASP.NET Core **importa** — cada requisição percorre a lista de cima para baixo na entrada e de baixo para cima na saída.

```
Requisição entra
      │
      ▼
ExceptionHandlingMiddleware  ← 1°: envolve tudo abaixo num try/catch
      │
      ▼
UseHttpsRedirection          ← 2°: redireciona HTTP → HTTPS
      │
      ▼
MapControllers               ← 3°: roteia para o controller correto
      │
      ▼
Handler executa
      │
      ▼ (resposta sobe na ordem inversa)
ExceptionHandlingMiddleware  ← captura qualquer exceção que subiu
```

**Por que `ExceptionHandlingMiddleware` é o primeiro?** Para que qualquer exceção lançada em qualquer parte do pipeline seja capturada. Se fosse registrado depois do `UseHttpsRedirection`, exceções lançadas pelo redirecionamento não seriam capturadas.

### 3.3 Swagger apenas em Development

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(...);
}
```

O Swagger expõe a estrutura completa da API — endpoints, modelos, exemplos. Em produção, isso representa um risco de segurança (surface de ataque) e informação desnecessária. O `IsDevelopment()` garante que só esteja disponível localmente.

### 3.4 XML de Documentação para o Swagger

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<NoWarn>$(NoWarn);1591</NoWarn>
```

`GenerateDocumentationFile` instrui o compilador a gerar um arquivo `.xml` com os comentários `/// <summary>` de cada método público. O Swagger lê esse arquivo e exibe as descrições na UI.

`NoWarn 1591` suprime o aviso "Elemento XML não tem comentário de documentação" para membros públicos sem `///`. Sem isso, `TreatWarningsAsErrors` quebraria o build para todos os tipos públicos sem documentação (ex: os Body DTOs).

---

## 4. `ExceptionHandlingMiddleware` — Tratamento Centralizado de Erros

```csharp
public async Task InvokeAsync(HttpContext context)
{
    try
    {
        await _next(context);
    }
    catch (ValidationException ex) { await HandleValidationExceptionAsync(context, ex); }
    catch (DomainException ex)     { await HandleDomainExceptionAsync(context, ex); }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Erro inesperado: {Message}", ex.Message);
        await HandleUnexpectedExceptionAsync(context);
    }
}
```

### 4.1 Por que Middleware e não um `ExceptionFilter` ou `try/catch` nos controllers?

| Abordagem | Cobertura | Problema |
|-----------|-----------|---------|
| `try/catch` em cada controller | Apenas o controller | Duplicação; middleware e outros componentes não são cobertos |
| `IExceptionFilter` (MVC) | Controllers MVC | Não captura exceções fora do pipeline MVC (middleware, DI) |
| **`Middleware`** ✅ | **Todo o pipeline HTTP** | Única implementação cobre tudo |

O middleware envolve **toda** a requisição num `try/catch`. Qualquer exceção que suba de qualquer camada — handler, behavior, repositório — é capturada aqui.

### 4.2 Mapeamento de Exceções para HTTP

| Exceção | HTTP | Motivo |
|---------|------|--------|
| `ValidationException` (FluentValidation) | **400 Bad Request** | Dados de entrada inválidos — problema do cliente |
| `DomainException` | **422 Unprocessable Entity** | Dados válidos sintaticamente, mas violam regra de negócio |
| `Exception` genérica | **500 Internal Server Error** | Erro inesperado — problema do servidor |

**Por que 422 para `DomainException` e não 400?**

- `400 Bad Request` significa: "a requisição está mal formada — eu não consigo entendê-la"
- `422 Unprocessable Entity` significa: "entendi a requisição, o formato está correto, mas não posso processá-la por razões semânticas"

Tentar fechar uma fatura já fechada é semanticamente inválido, não sintaticamente. O JSON está correto, o ID existe — mas a regra de negócio impede a operação. RFC 4918 define o 422 exatamente para esse caso.

### 4.3 `ProblemDetails` — RFC 7807

```csharp
// Erro de validação (400)
var problem = new ValidationProblemDetails(errors)
{
    Status = 400,
    Title  = "Erro de validação",
    Type   = "https://tools.ietf.org/html/rfc7807"
};

// Regra de negócio violada (422)
var problem = new ProblemDetails
{
    Status = 422,
    Title  = "Regra de negócio violada",
    Detail = ex.Message,
    Type   = "https://tools.ietf.org/html/rfc4918#section-11.2"
};
```

`ProblemDetails` é um padrão RFC 7807 para respostas de erro em APIs HTTP. Em vez de inventar um formato próprio, seguimos o padrão de mercado. Clientes que consomem a API já conhecem esse formato.

**Estrutura de um 400 de validação:**
```json
{
  "title": "Erro de validação",
  "status": 400,
  "type": "https://tools.ietf.org/html/rfc7807",
  "errors": {
    "Descricao": ["A descrição do item é obrigatória."],
    "Quantidade": ["A quantidade deve ser maior que zero."]
  }
}
```

**Estrutura de um 422 de regra de negócio:**
```json
{
  "title": "Regra de negócio violada",
  "status": 422,
  "detail": "Não é possível alterar uma fatura fechada.",
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2"
}
```

**`ValidationProblemDetails`** — subclasse de `ProblemDetails` específica para erros de validação. Inclui um dicionário `errors` com campo → lista de mensagens, compatível com o formato que frameworks front-end (React Hook Form, Angular, etc.) já esperam.

### 4.4 Por que não logar `DomainException`?

```csharp
catch (DomainException ex)
{
    // SEM log aqui
    await HandleDomainExceptionAsync(context, ex);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Erro inesperado"); // Log apenas aqui
    await HandleUnexpectedExceptionAsync(context);
}
```

`DomainException` é um resultado **esperado e legítimo** do sistema (usuário tentou operação inválida). Logar cada tentativa de adicionar item em fatura fechada como erro poluiria os logs com ruído. O log de erro deve ser reservado para situações genuinamente inesperadas.

---

## 5. `FaturasController` — Os Endpoints

```csharp
[ApiController]
[Route("api/faturas")]
[Produces("application/json")]
public class FaturasController : ControllerBase
{
    private readonly ISender _sender;

    public FaturasController(ISender sender) => _sender = sender;
}
```

### 5.1 Por que `ISender` e não `IMediator`?

`ISender` é uma interface mais restrita do MediatR que expõe apenas `Send()` (para commands e queries). `IMediator` expõe também `Publish()` (para events). O controller não publica eventos — usar `ISender` segue o **Princípio de Segregação de Interface (ISP)**: dependa apenas do que você usa.

### 5.2 `[ApiController]`

Este atributo ativa automaticamente:
- **Validação automática de ModelState** — se o body JSON não puder ser desserializado, retorna 400 automaticamente, antes mesmo de chegar no controller
- **Inferência de binding** — `[FromBody]` e `[FromQuery]` são inferidos automaticamente
- **Respostas de erro padronizadas** para falhas de binding

### 5.3 Os 7 Endpoints

#### `POST /api/faturas` — Criar Fatura

```csharp
[HttpPost]
public async Task<IActionResult> Create(
    [FromBody] CreateFaturaRequest request,
    CancellationToken cancellationToken)
{
    var result = await _sender.Send(request, cancellationToken);
    return result.IsSuccess
        ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
        : MapFailure(result.Error);
}
```

**`[FromBody] CreateFaturaRequest`** — o `CreateFaturaRequest` é um `record` da Application usado diretamente como body DTO. Funciona porque o request só tem campos que vêm do body (sem IDs de rota). Isso evita criar um DTO extra apenas para o HTTP layer.

**`CreatedAtAction`** — retorna HTTP **201 Created** com o header `Location` apontando para o endpoint `GET /api/faturas/{id}` da fatura recém-criada. Isso é a semântica correta de REST para criação de recursos.

#### `GET /api/faturas` — Listar com Filtros

```csharp
[HttpGet]
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
```

**Por que `[FromQuery]` separado em vez de um objeto?** O ASP.NET Core consegue fazer o binding de um objeto como `[FromQuery] ListFaturasRequest request`, mas como `ListFaturasRequest` é um `record` imutável (sem setters públicos), o binding automático não funciona. A alternativa é receber os parâmetros individualmente e construir o record manualmente.

**`status` como `string?`** — o enum `StatusFatura` é convertido de string para enum no handler (`Enum.TryParse`). Isso torna a API tolerante a case (`"aberta"`, `"Aberta"`, `"ABERTA"` funcionam) e evita que um valor inválido retorne 400 automático — o handler simplesmente ignora filtros de status inválidos.

#### `POST /api/faturas/{id}/itens` — Adicionar Item

```csharp
[HttpPost("{id:guid}/itens")]
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
```

**Por que `AddItemBody` separado?** Porque `AddItemFaturaRequest` tem `FaturaId` (que vem da rota), mas o body HTTP não deve repetir o ID — ele já está na URL. Usar o request da Application diretamente forçaria o cliente a enviar `faturaId` tanto na rota quanto no body. O `AddItemBody` recebe apenas o que vem no body; o controller monta o request completo combinando rota + body.

**`{id:guid}`** — a constraint `:guid` no route template garante que o ASP.NET Core só roteia para este endpoint se o `{id}` for um GUID válido. Se for uma string qualquer, retorna 404 automaticamente.

#### `PUT /api/faturas/{id}/fechar` — Fechar Fatura

```csharp
[HttpPut("{id:guid}/fechar")]
public async Task<IActionResult> Fechar(
    Guid id,
    CancellationToken cancellationToken)
{
    var result = await _sender.Send(new FecharFaturaRequest(id), cancellationToken);
    return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error);
}
```

**Por que `PUT` e não `PATCH` ou `POST`?** O fechamento de uma fatura é uma transição de estado idempotente — chamar `PUT /fechar` duas vezes resulta no mesmo estado final (fechada). `PUT` é semânticamente correto para atualizações idempotentes. `PATCH` seria para atualizações parciais de campos. `POST` seria para operações não idempotentes.

**Sem body** — o endpoint de fechar não precisa de body. O ID da fatura está na rota e não há dados adicionais necessários.

### 5.4 `MapFailure` — Tradução de Erros do Result Pattern

```csharp
private IActionResult MapFailure(Error error) => error.Code switch
{
    "NotFound"   => NotFound(new { error.Message }),
    "Conflict"   => Conflict(new { error.Message }),
    "Validation" => UnprocessableEntity(new { error.Message }),
    _            => BadRequest(new { error.Message })
};
```

O `Result.Failure(Error.NotFound(...))` retornado pelo handler é convertido no status HTTP correspondente. Esse switch centraliza a tradução em um único lugar — se adicionarmos novos códigos de erro, mudamos só aqui.

**Por que não usar exceptions para tudo?** Porque o `Error.NotFound` é um resultado esperado, não uma condição excepcional. Com o Result pattern, o controller recebe o resultado como um valor normal e decide o status HTTP de forma explícita, sem depender de exceções para controle de fluxo.

### 5.5 Body DTOs — Separação entre HTTP e Application

```csharp
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
```

Esses records ficam no mesmo arquivo do controller por simplicidade — são pequenos e específicos da camada HTTP. São necessários quando o request da Application combina dados de rota + body, o que impede usar o record da Application diretamente como `[FromBody]`.

**Por que `sealed record`?** Mesmo padrão do resto do sistema — imutáveis por padrão, igualdade por valor, sem herança desnecessária.

---

## 6. `appsettings.json` e Configuração

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=faturas;Username=postgres;Password=postgres"
  }
}
```

### 6.1 `appsettings.json` vs `appsettings.Development.json`

O ASP.NET Core carrega configurações em camadas — cada camada sobrescreve a anterior:

```
appsettings.json             ← base (todos os ambientes)
     +
appsettings.{Environment}.json ← override por ambiente
     +
Variáveis de ambiente         ← override final (produção)
```

**`appsettings.json`** — configurações base que se aplicam a todos os ambientes.

**`appsettings.Development.json`** — adiciona `"Microsoft.EntityFrameworkCore": "Information"` ao log. Isso faz o EF Core logar cada SQL gerado no console durante o desenvolvimento — útil para debugar queries, mas verboso demais para produção.

### 6.2 Segurança da Connection String

A connection string com credenciais **nunca deve ir para produção via `appsettings.json`**. O fluxo correto para produção é:

```
appsettings.json (credenciais de dev)
        ↓ sobrescrito por
Variável de ambiente CONNECTIONSTRINGS__POSTGRES
        ↓ ou
Azure Key Vault / AWS Secrets Manager / Vault
```

O ASP.NET Core lê automaticamente variáveis de ambiente no formato `CONNECTIONSTRINGS__POSTGRES` como override da connection string `Postgres`.

---

## 7. Tabela Completa de Endpoints

| Método | Rota | Command/Query | Sucesso | Erro Negócio | Não Encontrado |
|--------|------|---------------|---------|--------------|----------------|
| `POST` | `/api/faturas` | `CreateFaturaRequest` | 201 | 400 (validação) | — |
| `GET` | `/api/faturas` | `ListFaturasRequest` | 200 | 400 (período inválido) | — |
| `GET` | `/api/faturas/{id}` | `GetFaturaByIdRequest` | 200 | — | 404 |
| `POST` | `/api/faturas/{id}/itens` | `AddItemFaturaRequest` | 201 | 400/422 | 404 |
| `PUT` | `/api/faturas/{id}/itens/{itemId}` | `UpdateItemFaturaRequest` | 200 | 400/422 | 404 |
| `DELETE` | `/api/faturas/{id}/itens/{itemId}` | `RemoveItemFaturaRequest` | 200 | 422 | 404 |
| `PUT` | `/api/faturas/{id}/fechar` | `FecharFaturaRequest` | 200 | 422 | 404 |

---

## 8. Fluxo Completo de uma Requisição

```
POST https://localhost:7159/api/faturas/abc/itens
     body: { "descricao": "Monitor", "quantidade": 2, "valorUnitario": 800 }
                          │
                          ▼
          ExceptionHandlingMiddleware
          try { await _next(context) }
                          │
                          ▼
          FaturasController.AddItem(id="abc", body)
          │
          ├─ id = "abc" → não é GUID válido
          │   → ASP.NET Core retorna 404 automaticamente (constraint :guid)
          │
          └─ id = Guid válido → monta AddItemFaturaRequest
                          │
                          ▼
          ISender.Send(AddItemFaturaRequest)
                          │
                          ▼ Pipeline MediatR
          LoggingBehavior  → loga "Executando AddItemFaturaRequest"
                          │
                          ▼
          ValidationBehavior
          → Descricao não vazia ✓
          → Quantidade > 0 ✓
          → ValorUnitario > 0 ✓
          → válido → chama next()
                          │
                          ▼
          AddItemFaturaHandler
          → GetByIdAsync(faturaId)
              null? → Result.Failure(NotFound) → controller retorna 404
          → fatura.AdicionarItem(...)
              DomainException? → sobe → middleware captura → 422
          → SaveChangesAsync()
          → Result.Success(response)
                          │
                          ▼
          Controller recebe Result.Success
          → CreatedAtAction → 201 Created
          {
            "itemId": "...",
            "faturaId": "...",
            "descricao": "Monitor",
            "quantidade": 2,
            "valorUnitario": 800.00,
            "valorTotalItem": 1600.00,
            "justificativa": null   ← null porque 1600 > 1000 → DomainException!
          }
          → Na prática: esse exemplo retornaria 422 pois total > 1000 sem justificativa
```

---

## 9. Diagrama de Dependências

```
┌─────────────────────────────────────────────────────────────────┐
│                        Faturas.Api                              │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Program.cs                                              │   │
│  │  ─────────────────────────────────────────────────────   │   │
│  │  AddApplication()      ← registra MediatR + Validators  │   │
│  │  AddInfrastructure()   ← registra DbContext + Repos     │   │
│  │  UseMiddleware<ExceptionHandlingMiddleware>()            │   │
│  │  UseSwagger() / UseSwaggerUI() [apenas Development]     │   │
│  │  MapControllers()                                        │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  ExceptionHandlingMiddleware                             │   │
│  │  ValidationException → 400 ProblemDetails               │   │
│  │  DomainException     → 422 ProblemDetails               │   │
│  │  Exception           → 500 ProblemDetails + LogError    │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  FaturasController                                       │   │
│  │  Injeta: ISender (MediatR)                              │   │
│  │  ─────────────────────────────────────────────────────   │   │
│  │  POST   /api/faturas              → CreateFatura        │   │
│  │  GET    /api/faturas              → ListFaturas         │   │
│  │  GET    /api/faturas/{id}         → GetFaturaById       │   │
│  │  POST   /api/faturas/{id}/itens   → AddItemFatura       │   │
│  │  PUT    /api/faturas/{id}/itens/{itemId} → UpdateItem   │   │
│  │  DELETE /api/faturas/{id}/itens/{itemId} → RemoveItem   │   │
│  │  PUT    /api/faturas/{id}/fechar  → FecharFatura        │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                 │
│  Depende de:                                                    │
│  ├── Faturas.Application (commands, queries, ISender)           │
│  ├── Faturas.Infrastructure (AddInfrastructure)                 │
│  └── Swashbuckle.AspNetCore 6.9.0                              │
└─────────────────────────────────────────────────────────────────┘
```

---

## 10. Resumo das Decisões Técnicas

| Decisão | Alternativa | Por que escolhemos assim |
|---------|-------------|--------------------------|
| `ISender` em vez de `IMediator` | `IMediator` completo | ISP — controller só precisa de Send, não de Publish |
| `ExceptionHandlingMiddleware` | `try/catch` em cada controller | Cobertura total do pipeline; DRY — uma implementação para todos |
| `422` para `DomainException` | `400` | Semanticamente correto: dado válido, mas violação de regra de negócio |
| `ProblemDetails` (RFC 7807) | JSON customizado | Padrão de mercado; clientes já conhecem o formato |
| `ValidationProblemDetails` para FluentValidation | Objeto customizado | Formato com `errors` por campo que frameworks front-end já entendem |
| `AddItemBody` separado do request da Application | Usar request diretamente | Combina dado de rota + body sem forçar o cliente a repetir o ID no body |
| Swagger apenas em `IsDevelopment` | Sempre habilitado | Segurança — não expõe estrutura da API em produção |
| `CreatedAtAction` em endpoints de criação | `Ok(201)` manual | Retorna header `Location` com URL do recurso criado — REST correto |
| Connection string no `appsettings.json` | Hardcoded no código | Configurável por ambiente via override de variáveis de ambiente |
| Log do EF Core apenas em Development | Sempre ligado | Verbosidade alta em produção; útil só para debug local |
| `GenerateDocumentationFile` + `NoWarn 1591` | Sem XML | Swagger exibe descriptions dos endpoints sem quebrar o build |
