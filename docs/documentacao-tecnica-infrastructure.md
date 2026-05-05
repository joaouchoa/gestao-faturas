# Documentação Técnica — Camada de Infrastructure (`Faturas.Infrastructure`)

> **Objetivo deste documento:** explicar em profundidade cada decisão conceitual e técnica tomada na construção da camada de Infrastructure do sistema de Gestão de Faturas. Esta camada é a ponte entre o mundo puro do Domínio e o mundo concreto do banco de dados.

---

## 1. O Papel da Infrastructure na Arquitetura

Na Clean Architecture, a regra fundamental é: **as camadas internas não conhecem as camadas externas**. O Domínio não sabe que existe um banco de dados. A Application não sabe que existe o PostgreSQL. Elas definem contratos (interfaces) e a Infrastructure os implementa.

```
┌─────────────────────────────────────────────┐
│              Faturas.Api / Web              │  ← Entrada (HTTP)
├─────────────────────────────────────────────┤
│           Faturas.Application               │  ← Casos de uso
├─────────────────────────────────────────────┤
│             Faturas.Domain                  │  ← Regras de negócio
├─────────────────────────────────────────────┤
│          Faturas.Infrastructure             │  ← Detalhes técnicos ★
│   (EF Core, PostgreSQL, Repositórios)       │
└─────────────────────────────────────────────┘
```

**A Infrastructure é o único lugar do sistema que:**
- Sabe que o banco de dados é PostgreSQL
- Usa o Entity Framework Core
- Implementa os repositórios definidos no Domínio
- Registra os serviços no container de injeção de dependência

Se amanhã precisarmos trocar de PostgreSQL para SQL Server, apenas a Infrastructure muda. O Domínio e a Application ficam intactos.

---

## 2. Estrutura do Projeto

```
Faturas.Infrastructure/
├── Persistence/
│   ├── FaturasDbContext.cs                    ← Contexto do EF Core
│   └── Configurations/
│       ├── FaturaConfiguration.cs             ← Mapeamento da Fatura
│       └── ItemFaturaConfiguration.cs         ← Mapeamento do ItemFatura
│
├── Repositories/
│   └── FaturaRepository.cs                    ← Implementa IFaturaRepository
│
└── DependencyInjection/
    └── ServiceCollectionExtensions.cs         ← AddInfrastructure()
```

**Por que essa organização de pastas?**

- `Persistence/` — tudo relacionado ao EF Core e banco de dados fica isolado aqui. Se trocarmos de ORM, mexemos apenas nesta pasta.
- `Repositories/` — as implementações concretas dos contratos definidos no Domínio.
- `DependencyInjection/` — ponto único de registro de serviços. A API só precisa chamar `AddInfrastructure()` e tudo é registrado corretamente.

---

## 3. Pacote NuGet: `Npgsql.EntityFrameworkCore.PostgreSQL`

```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.10" />
```

Este é o **único pacote** que a Infrastructure precisa declarar explicitamente. Ele traz consigo como dependências:

| Pacote | Trazido automaticamente |
|--------|------------------------|
| `Microsoft.EntityFrameworkCore` | ✅ |
| `Microsoft.EntityFrameworkCore.Relational` | ✅ |
| `Npgsql` (driver ADO.NET do PostgreSQL) | ✅ |

**Por que versão `8.0.x`?** O Npgsql segue o versionamento do EF Core. Para projetos em `.NET 8` com `EF Core 8`, usamos `Npgsql.EntityFrameworkCore.PostgreSQL 8.x`. Isso garante compatibilidade total entre o driver e o ORM.

---

## 4. `FaturasDbContext` — O Centro do EF Core

```csharp
public class FaturasDbContext : DbContext
{
    public FaturasDbContext(DbContextOptions<FaturasDbContext> options) : base(options) { }

    public DbSet<Fatura> Faturas => Set<Fatura>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FaturasDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

### O que é o `DbContext`?

O `DbContext` é o componente central do Entity Framework Core. Ele é responsável por:

1. **Rastrear mudanças** — quando você carrega uma `Fatura` e muda seu estado, o EF Core sabe o que mudou e gera o SQL de UPDATE correto
2. **Unidade de Trabalho (Unit of Work)** — acumula todas as mudanças e as persiste de uma vez com `SaveChangesAsync()`
3. **Traduzir LINQ para SQL** — `_context.Faturas.Where(f => f.Status == Aberta)` vira `SELECT * FROM faturas WHERE status = 0`
4. **Gerenciar conexões** — abre e fecha conexões com o banco automaticamente

### `DbSet<Fatura> Faturas => Set<Fatura>()`

O `DbSet` representa a tabela `faturas` no banco. É por ele que fazemos queries:
```csharp
_context.Faturas.Where(...).Include(...).ToListAsync()
```

**Por que `=> Set<Fatura>()` em vez de `{ get; set; }`?** É uma forma mais moderna e concisa. `Set<Fatura>()` e `DbSet<Fatura>` são equivalentes em comportamento, mas `Set<T>()` é a API preferida nas versões mais recentes do EF Core.

### `ApplyConfigurationsFromAssembly`

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(FaturasDbContext).Assembly);
```

Em vez de configurar o mapeamento diretamente no `OnModelCreating` (o que deixaria o método enorme), usamos **classes de configuração separadas** que implementam `IEntityTypeConfiguration<T>`. Este método varre o assembly inteiro, encontra todas essas classes automaticamente e as aplica.

**Benefício:** cada entidade tem seu próprio arquivo de configuração. Adicionamos uma nova entidade criando uma nova classe — sem tocar no `DbContext`.

---

## 5. `FaturaConfiguration` — Mapeando o Aggregate Root

```csharp
public class FaturaConfiguration : IEntityTypeConfiguration<Fatura>
{
    public void Configure(EntityTypeBuilder<Fatura> builder) { ... }
}
```

Esta classe ensina o EF Core **exatamente como** mapear cada propriedade da `Fatura` para colunas do banco. Sem ela, o EF Core tentaria adivinhar os nomes das colunas e os tipos — o que não funcionaria com nossa convenção snake_case do PostgreSQL.

### 5.1 Tabela e Chave Primária

```csharp
builder.ToTable("faturas");
builder.HasKey(f => f.Id);
builder.Property(f => f.Id).HasColumnName("id");
```

Mapeamos explicitamente o nome da tabela (`faturas`) e da coluna (`id`). Sem isso, o EF Core usaria os nomes em PascalCase do C# (`Faturas`, `Id`), que não corresponderiam ao que o DbUp criou em snake_case.

### 5.2 Value Object `NumeroFatura` — Conversão de Valor

```csharp
builder.Property(f => f.Numero)
    .HasColumnName("numero")
    .HasMaxLength(20)
    .IsRequired()
    .HasConversion(
        numero => numero.Valor,          // C# → Banco: extrai a string do VO
        valor  => NumeroFatura.Criar(valor)); // Banco → C#: reconstrói o VO
```

Este é um dos pontos mais importantes da configuração. O `NumeroFatura` é um **Value Object** — uma classe C#. O banco, porém, não sabe o que é um Value Object; ele só conhece tipos primitivos como `VARCHAR`.

O `HasConversion` define dois conversores:
- **Escrita (C# → Banco):** `numero.Valor` — extrai a string `"FAT-000001"` do VO e a salva no banco
- **Leitura (Banco → C#):** `NumeroFatura.Criar(valor)` — lê a string `"FAT-000001"` do banco e reconstrói o VO completo

**Por que chamar `NumeroFatura.Criar()` na leitura?** Porque `Criar()` valida o formato e é a única forma de construir um `NumeroFatura` válido (construtor privado). Mesmo dados vindos do banco passam pela validação do domínio.

### 5.3 Enum `StatusFatura` — Armazenado como Inteiro

```csharp
builder.Property(f => f.Status)
    .HasColumnName("status")
    .IsRequired();
```

O EF Core, por padrão, serializa enums como inteiros. Não precisamos de nenhum conversor adicional. O mapeamento é:

| C# (`StatusFatura`) | Banco (`status INTEGER`) |
|---------------------|--------------------------|
| `StatusFatura.Aberta` | `0` |
| `StatusFatura.Fechada` | `1` |

Esse comportamento é consistente com o `DEFAULT 0` que definimos no script SQL.

### 5.4 Precisão Monetária

```csharp
builder.Property(f => f.ValorTotal)
    .HasColumnName("valor_total")
    .HasPrecision(18, 2)
    .IsRequired();
```

`HasPrecision(18, 2)` instrui o EF Core a usar `NUMERIC(18, 2)` ao criar a coluna (caso usasse migrations do EF) e a deserializar o valor com a precisão correta. Isso espelha o tipo definido no script SQL do DbUp.

### 5.5 Coleção Privada `_itens` — O Ponto Mais Delicado

```csharp
builder.HasMany(f => f.Itens)
    .WithOne()
    .HasForeignKey("fatura_id")
    .OnDelete(DeleteBehavior.Cascade);

builder.Navigation(f => f.Itens)
    .UsePropertyAccessMode(PropertyAccessMode.Field);
```

Este é o mapeamento mais complexo e merece atenção especial.

**O problema:** no domínio, `_itens` é um campo privado (`private readonly List<ItemFatura> _itens`). A propriedade pública `Itens` retorna `IReadOnlyCollection<ItemFatura>` — somente leitura. O EF Core precisa **escrever** nos itens ao carregar do banco, mas a propriedade não permite isso.

**A solução:** `UsePropertyAccessMode(PropertyAccessMode.Field)` instrui o EF Core a ignorar a propriedade pública `Itens` e acessar **diretamente o campo privado** `_itens`. O EF Core encontra o campo por convenção de nome: propriedade `Itens` → campo `_itens`.

**`HasForeignKey("fatura_id")`** — define `fatura_id` como uma **shadow property** (propriedade sombra). Shadow properties existem no modelo do EF Core mas não têm propriedade C# correspondente na entidade. O EF Core gerencia o valor internamente, garantindo que o relacionamento seja persistido sem expor `FaturaId` no `ItemFatura`.

**`WithOne()` sem argumento** — `ItemFatura` não tem propriedade de navegação de volta para `Fatura` (o que é correto no DDD — entidade subordinada não referencia a raiz diretamente). O `WithOne()` sem argumento informa ao EF Core que existe a relação, mas sem navegação bidirecional exposta.

**`OnDelete(DeleteBehavior.Cascade)`** — quando uma `Fatura` é deletada, todos os seus `ItemFatura` são deletados automaticamente. Espelha o `ON DELETE CASCADE` do script SQL.

---

## 6. `ItemFaturaConfiguration` — Mapeando a Entidade Subordinada

```csharp
public class ItemFaturaConfiguration : IEntityTypeConfiguration<ItemFatura>
{
    public void Configure(EntityTypeBuilder<ItemFatura> builder)
    {
        builder.ToTable("itens_fatura");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");

        builder.Property<Guid>("fatura_id")
            .HasColumnName("fatura_id")
            .IsRequired();

        // ... demais propriedades
    }
}
```

### Shadow Property `fatura_id`

```csharp
builder.Property<Guid>("fatura_id")
    .HasColumnName("fatura_id")
    .IsRequired();
```

A shadow property `fatura_id` é declarada aqui explicitamente. Ela existe no modelo do EF Core, tem uma coluna no banco (`fatura_id UUID NOT NULL`), mas não aparece como propriedade na classe `ItemFatura`.

**Por que essa abordagem?** No DDD, `ItemFatura` é uma entidade interna do agregado `Fatura`. Ela não precisa saber qual é seu `FaturaId` — quem cuida dessa associação é a `Fatura` e o EF Core. Expor `FaturaId` em `ItemFatura` criaria acoplamento desnecessário e abriria a possibilidade de alguém "mover" um item de uma fatura para outra diretamente, violando as regras do domínio.

### Propriedades com `HasPrecision`

```csharp
builder.Property(i => i.ValorUnitario)
    .HasColumnName("valor_unitario")
    .HasPrecision(18, 2)
    .IsRequired();

builder.Property(i => i.ValorTotalItem)
    .HasColumnName("valor_total_item")
    .HasPrecision(18, 2)
    .IsRequired();
```

Ambos os campos monetários usam `NUMERIC(18, 2)` para precisão exata, consistente com o script SQL.

### `Justificativa` — Campo Nullable

```csharp
builder.Property(i => i.Justificativa)
    .HasColumnName("justificativa");
```

Sem `.IsRequired()` → o EF Core trata como nullable (`TEXT NULL` no banco). O tipo `string?` no C# também sinaliza nullabilidade. A regra de quando a justificativa é obrigatória é enforced no domínio (RN-7), não no banco.

---

## 7. `FaturaRepository` — Implementando o Contrato do Domínio

```csharp
public class FaturaRepository : IFaturaRepository
{
    private readonly FaturasDbContext _context;

    public FaturaRepository(FaturasDbContext context) => _context = context;
}
```

O repositório recebe o `FaturasDbContext` via **injeção de dependência**. Ele não cria nem gerencia o contexto — apenas o usa. Isso é fundamental para que a mesma instância do `DbContext` (e portanto a mesma transação) seja compartilhada durante toda a requisição HTTP.

### 7.1 `GetByIdAsync` — Busca com Eager Loading

```csharp
public async Task<Fatura?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
    await _context.Faturas
        .Include(f => f.Itens)
        .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
```

**`Include(f => f.Itens)`** — carrega os itens da fatura na mesma query (Eager Loading). Sem o `Include`, `fatura.Itens` estaria vazio — o EF Core não carrega relacionamentos automaticamente (Lazy Loading está desativado por padrão no Npgsql).

O SQL gerado é aproximadamente:
```sql
SELECT f.*, i.*
FROM faturas f
LEFT JOIN itens_fatura i ON i.fatura_id = f.id
WHERE f.id = @id
LIMIT 1
```

**`FirstOrDefaultAsync`** — retorna `null` se não encontrar, em vez de lançar exceção. A camada de Application decide o que fazer com o `null` (geralmente retorna um erro 404).

**`CancellationToken`** — propaga o token de cancelamento da requisição HTTP até o banco de dados. Se o cliente fechar a conexão antes da query terminar, a query é cancelada, liberando recursos do banco.

### 7.2 `ListAsync` — Filtros Dinâmicos

```csharp
public async Task<IReadOnlyList<Fatura>> ListAsync(FaturaFilter filter, CancellationToken cancellationToken = default)
{
    var query = _context.Faturas.AsQueryable();

    if (!string.IsNullOrWhiteSpace(filter.NomeCliente))
        query = query.Where(f => f.NomeCliente.Contains(filter.NomeCliente));

    if (filter.DataInicial.HasValue)
        query = query.Where(f => f.DataEmissao >= filter.DataInicial.Value);

    if (filter.DataFinal.HasValue)
        query = query.Where(f => f.DataEmissao <= filter.DataFinal.Value);

    if (filter.Status.HasValue)
        query = query.Where(f => f.Status == filter.Status.Value);

    return await query
        .Include(f => f.Itens)
        .AsNoTracking()
        .ToListAsync(cancellationToken);
}
```

**`AsQueryable()`** — cria uma query que ainda não foi enviada ao banco. Cada `.Where()` adiciona uma cláusula SQL. A query só é executada quando `ToListAsync()` é chamado. Isso é chamado de **composição de queries** — você monta o SQL peça por peça em C#.

**Filtros condicionais** — cada filtro só é aplicado se o valor foi informado. Se `filter.NomeCliente` for nulo, o `WHERE nome_cliente LIKE ...` não entra na query. O SQL gerado é sempre o mais enxuto possível.

**`NomeCliente.Contains()`** gera `LIKE '%valor%'` no SQL — busca por substring. Para buscas de alta performance em produção, futuramente poderia ser substituído por busca full-text do PostgreSQL.

**`AsNoTracking()`** — para listagens, não precisamos rastrear mudanças. O EF Core economiza memória e CPU ao não criar snapshots dos objetos carregados. **Nunca use tracking em queries de leitura pura.**

### 7.3 `AddAsync` e `Update`

```csharp
public async Task AddAsync(Fatura fatura, CancellationToken cancellationToken = default) =>
    await _context.Faturas.AddAsync(fatura, cancellationToken);

public void Update(Fatura fatura) =>
    _context.Faturas.Update(fatura);
```

Estes métodos apenas **marcam** a entidade no Change Tracker do EF Core. Nenhum SQL é executado aqui. O SQL (`INSERT` ou `UPDATE`) só é gerado quando `SaveChangesAsync()` é chamado.

**`AddAsync` vs `Add`:** a versão `Async` só faz diferença quando o banco precisa gerar o ID (sequências). Como usamos `UUID` gerado pela aplicação, `Add` e `AddAsync` são equivalentes. Usamos `AddAsync` por consistência.

**`Update`** — marca a entidade inteira como modificada. O EF Core gera um `UPDATE` com todas as colunas. Para entidades com muitas colunas, seria mais eficiente usar o Change Tracker (que detecta apenas o que mudou), mas `Update` é mais explícito e previsível.

### 7.4 `SaveChangesAsync` — Unidade de Trabalho

```csharp
public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
    await _context.SaveChangesAsync(cancellationToken);
```

Este é o momento em que o EF Core:
1. Examina o Change Tracker (o que foi adicionado, modificado, deletado)
2. Gera o SQL correspondente (`INSERT`, `UPDATE`, `DELETE`)
3. Executa tudo dentro de uma **transação implícita**
4. Se qualquer operação falhar, a transação é revertida automaticamente

**Por que expor `SaveChangesAsync` no repositório?** Para que a camada de Application controle **quando** persistir. Um handler de command faz várias operações no domínio e chama `SaveChangesAsync` apenas uma vez ao final. Isso garante que todas as mudanças sejam persistidas atomicamente.

---

## 8. `ServiceCollectionExtensions` — Registro de Dependências

```csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddDbContext<FaturasDbContext>(options =>
        options.UseNpgsql(configuration.GetConnectionString("Postgres")));

    services.AddScoped<IFaturaRepository, FaturaRepository>();

    return services;
}
```

### Extension Method Pattern

`AddInfrastructure` é um **extension method** de `IServiceCollection`. Isso permite que a API o chame de forma fluente no `Program.cs`:

```csharp
builder.Services.AddInfrastructure(builder.Configuration);
```

A API não precisa saber nada sobre `FaturasDbContext`, `FaturaRepository` ou Npgsql — só chama `AddInfrastructure()`.

### `AddDbContext<FaturasDbContext>`

Registra o `FaturasDbContext` no container de DI com **Scoped lifetime** (padrão do `AddDbContext`). Isso significa que uma única instância do contexto é criada por requisição HTTP e descartada no final. Todas as operações de uma requisição compartilham a mesma instância — e portanto a mesma transação.

**`UseNpgsql(...)`** — configura o EF Core para usar o PostgreSQL via Npgsql. Aqui é onde a connection string entra.

### `configuration.GetConnectionString("Postgres")`

Lê a connection string chamada `"Postgres"` do `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=faturas;Username=postgres;Password=postgres"
  }
}
```

A connection string nunca é hardcoded no código — vem da configuração, que pode ser sobrescrita por variáveis de ambiente em produção.

### `AddScoped<IFaturaRepository, FaturaRepository>`

Registra o repositório com **Scoped lifetime**:

| Lifetime | Criado | Descartado | Uso |
|----------|--------|-----------|-----|
| `Transient` | A cada injeção | Imediatamente | Serviços leves sem estado |
| **`Scoped`** ✅ | Uma vez por requisição | No fim da requisição | Repositórios, DbContext |
| `Singleton` | Uma vez na vida da app | No encerramento | Caches, configurações |

`Scoped` é correto para repositórios porque eles usam o `DbContext`, que também é `Scoped`. Se o repositório fosse `Singleton`, tentaria usar um `DbContext` que já foi descartado.

---

## 9. Fluxo Completo: do Controller ao Banco

Para solidificar o entendimento, veja como uma requisição percorre a Infrastructure:

```
POST /api/faturas
        │
        ▼
FaturasController.Create(request)
        │
        ▼
MediatR → CreateFaturaHandler           ← Application (próxima seção)
        │
        ├─ Fatura.Criar(...)             ← Domain (cria o agregado em memória)
        │
        ├─ repository.AddAsync(fatura)   ← Infrastructure
        │         │
        │         └─ _context.Faturas.AddAsync(fatura)
        │              (apenas marca no Change Tracker — sem SQL ainda)
        │
        └─ repository.SaveChangesAsync() ← Infrastructure
                  │
                  └─ _context.SaveChangesAsync()
                       │
                       ├─ EF Core gera o SQL:
                       │    INSERT INTO faturas (id, numero, ...) VALUES (@p1, @p2, ...)
                       │    INSERT INTO itens_fatura (...) VALUES (...)
                       │
                       └─ Executa no PostgreSQL via Npgsql
```

---

## 10. Diagrama de Dependências

```
┌─────────────────────────────────────────────────────────────────┐
│                   Faturas.Infrastructure                        │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  DependencyInjection/ServiceCollectionExtensions         │   │
│  │  AddInfrastructure() ──────────────────────────────────  │   │
│  │       │                                                  │   │
│  │       ├── AddDbContext<FaturasDbContext>                  │   │
│  │       └── AddScoped<IFaturaRepository, FaturaRepository> │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Persistence/FaturasDbContext                            │   │
│  │       │                                                  │   │
│  │       └── ApplyConfigurationsFromAssembly                │   │
│  │               ├── FaturaConfiguration                    │   │
│  │               └── ItemFaturaConfiguration                │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Repositories/FaturaRepository                          │   │
│  │  Implementa → IFaturaRepository (definida no Domain)    │   │
│  │       │                                                  │   │
│  │       ├── GetByIdAsync  → SELECT + JOIN (with Include)  │   │
│  │       ├── ListAsync     → SELECT + filtros dinâmicos    │   │
│  │       ├── AddAsync      → marca INSERT no tracker        │   │
│  │       ├── Update        → marca UPDATE no tracker        │   │
│  │       └── SaveChangesAsync → executa SQL no PostgreSQL  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                 │
│  Depende de:                                                    │
│  ├── Faturas.Domain (IFaturaRepository, Fatura, ItemFatura)     │
│  └── Npgsql.EntityFrameworkCore.PostgreSQL 8.0.10              │
└─────────────────────────────────────────────────────────────────┘
```

---

## 11. Resumo das Decisões Técnicas

| Decisão | Alternativa | Por que escolhemos assim |
|---------|-------------|--------------------------|
| `IEntityTypeConfiguration<T>` por arquivo | Tudo no `OnModelCreating` | Separação de responsabilidades; cada entidade tem seu arquivo de configuração |
| `ApplyConfigurationsFromAssembly` | Registrar manualmente cada config | Auto-descoberta; adicionar nova entidade não exige tocar no `DbContext` |
| `HasConversion` para `NumeroFatura` | `OwnsOne` ou string direta | VO mapeado para coluna única sem criar tabela extra nem expor propriedade separada |
| Shadow property `fatura_id` | Propriedade `FaturaId` em `ItemFatura` | Mantém `ItemFatura` sem conhecimento de sua FK; não expõe ID que poderia ser manipulado externamente |
| `UsePropertyAccessMode(Field)` | Lazy Loading ou `public set` | Permite coleção privada no domínio sem abrir `set` público; EF Core acessa o campo diretamente |
| `AsNoTracking()` em listagens | Tracking padrão | Performance — leituras puras não precisam de snapshot para Change Tracker |
| `Scoped` para repositório e DbContext | `Transient` ou `Singleton` | Uma instância por requisição; compartilha transação; sem uso de contexto descartado |
| Extension method `AddInfrastructure` | Registrar diretamente no `Program.cs` | Encapsulamento; a API não precisa conhecer os detalhes internos da Infrastructure |
| Connection string via `appsettings.json` | Hardcoded ou env var direta | Configurável por ambiente sem alterar código; sobrescrita fácil via variáveis de ambiente em produção |
