# Documentação Técnica — Camada de Domínio (`Faturas.Domain`)

> **Objetivo deste documento:** explicar, em profundidade, cada decisão conceitual e técnica tomada na construção da camada de domínio do sistema de Gestão de Faturas. Serve como referência para qualquer desenvolvedor que precise entender o "porquê" por trás do código, não apenas o "o quê".

---

## 1. A Filosofia por trás do Domínio

### 1.1 O que é "Domínio"?

Em sistemas com **Clean Architecture + DDD (Domain-Driven Design)**, o **Domínio** é o coração da aplicação. Ele contém apenas as **regras de negócio puras**, sem saber nada sobre banco de dados, API, HTTP, ou qualquer tecnologia.

> Pense no domínio como as regras escritas no papel por um contador ou gestor financeiro — elas existem independentemente de qualquer sistema.

A camada de domínio **não tem dependência de nenhum outro projeto** do sistema. Ela é autossuficiente.

### 1.2 Domínio Rico vs. Domínio Anêmico

Existem duas abordagens para modelar entidades:

| Abordagem | Descrição | Problema |
|-----------|-----------|----------|
| **Anêmico** | Entidade é só um "saco de dados" com getters e setters públicos. A lógica fica espalhada em services. | Qualquer lugar do código pode alterar o estado da entidade de forma indevida. |
| **Rico** ✅ | Entidade encapsula seus dados e expõe apenas métodos que fazem sentido de negócio. | Nenhum — é a abordagem correta para DDD. |

**Este projeto usa Domínio Rico.** Isso significa que `Fatura` não tem `set` público para `Status`, `ValorTotal` ou `Itens`. Só os próprios métodos da `Fatura` (`AdicionarItem`, `Fechar`, etc.) podem mudar seu estado. Isso garante que as regras de negócio **nunca sejam violadas**, independentemente de quem chama o código.

---

## 2. Estrutura de Arquivos e o Papel de Cada Um

```
Faturas.Domain/
├── Common/                         ← Blocos de construção genéricos (reutilizáveis)
│   ├── Entity.cs
│   ├── AggregateRoot.cs
│   ├── ValueObject.cs
│   ├── DomainException.cs
│   └── IDomainEvent.cs
│
└── Faturas/                        ← O contexto de negócio "Faturas"
    ├── Fatura.cs                   ← A entidade principal
    ├── ItemFatura.cs               ← Entidade secundária
    ├── StatusFatura.cs             ← Enum de estados
    ├── Errors/
    │   └── FaturaErrors.cs         ← Mensagens de erro centralizadas
    ├── ValueObjects/
    │   ├── NumeroFatura.cs
    │   └── Dinheiro.cs
    ├── Events/
    │   ├── FaturaCriadaEvent.cs
    │   ├── ItemAdicionadoEvent.cs
    │   └── FaturaFechadaEvent.cs
    └── Repositories/
        ├── IFaturaRepository.cs
        └── FaturaFilter.cs
```

---

## 3. Os Blocos de Construção (`Common/`)

Estes são conceitos fundamentais do DDD, implementados como classes base que todos os objetos do domínio herdam.

---

### 3.1 `Entity.cs` — O que é uma Entidade?

```csharp
public abstract class Entity
{
    public Guid Id { get; private set; }

    protected Entity(Guid id) => Id = id;
    protected Entity() => Id = Guid.NewGuid();
}
```

**Conceito:** Uma **Entidade** é um objeto que tem **identidade única**. Dois objetos com os mesmos dados, mas IDs diferentes, são considerados objetos **diferentes**.

**Por que `Guid`?** Em sistemas distribuídos, usar `Guid` (identificador único universal) evita colisões de IDs entre diferentes instâncias ou bancos de dados, sem precisar consultar o banco para gerar o próximo número.

**Por que `private set`?** Para garantir que o `Id` nunca seja alterado após a criação. Uma entidade nasce com uma identidade e morre com ela.

**Por que dois construtores?**
- `Entity(Guid id)` — usado quando o EF Core (Entity Framework) carrega a entidade do banco de dados. O banco já tem o ID salvo.
- `Entity()` — usado quando criamos uma nova entidade do zero. O `Guid.NewGuid()` gera um ID único automaticamente.

---

### 3.2 `AggregateRoot.cs` — O que é uma Raiz de Agregado?

```csharp
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

**Conceito:** Um **Agregado** é um grupo de objetos relacionados que são tratados como uma única unidade de consistência. A **Raiz do Agregado** é o único objeto desse grupo que o mundo externo pode tocar diretamente.

**No nosso sistema:** `Fatura` é a raiz do agregado. `ItemFatura` só existe dentro de uma `Fatura`. Ninguém de fora cria um `ItemFatura` diretamente — você chama `fatura.AdicionarItem(...)` e é a `Fatura` quem cria o item internamente. Isso garante que as regras de negócio (ex: não adicionar item em fatura fechada) nunca sejam contornadas.

**O que são `_domainEvents`?** São **Eventos de Domínio** — notificações de que algo importante aconteceu. A raiz coleta esses eventos durante a operação, e depois da transação ser salva no banco, eles podem ser publicados para outras partes do sistema reagirem. Isso é a base de arquiteturas orientadas a eventos.

**Por que `IReadOnlyCollection`?** Para que ninguém de fora adicione eventos na lista diretamente. Somente os métodos da própria `Fatura` podem fazer isso via `RaiseDomainEvent`.

---

### 3.3 `ValueObject.cs` — O que é um Value Object?

```csharp
public abstract class ValueObject
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public override bool Equals(object? obj) { ... }
    public override int GetHashCode() { ... }
    public static bool operator == ...
    public static bool operator != ...
}
```

**Conceito:** Um **Value Object** é um objeto definido pelos seus **valores**, não pela sua identidade. Dois Value Objects com os mesmos valores são considerados **iguais**.

**Diferença entre Entidade e Value Object:**

| | Entidade | Value Object |
|---|---|---|
| Identidade | Tem `Id` único | Não tem `Id` |
| Igualdade | Comparada pelo `Id` | Comparada pelos valores |
| Mutabilidade | Pode mudar (com cuidado) | **Imutável** |
| Exemplo | `Fatura`, `ItemFatura` | `NumeroFatura`, `Dinheiro` |

**Por que sobrescrever `Equals` e `GetHashCode`?** Em C#, por padrão, dois objetos são iguais apenas se são o **mesmo objeto na memória** (mesma referência). Para Value Objects, queremos que `new NumeroFatura("FAT-000001") == new NumeroFatura("FAT-000001")` seja `true`. Por isso reimplementamos a igualdade baseada nos valores.

---

### 3.4 `DomainException.cs` — Exceções de Negócio

```csharp
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
```

**Conceito:** Quando uma **regra de negócio é violada**, o domínio lança uma `DomainException`. Ela representa uma situação de negócio inválida (ex: tentar fechar uma fatura já fechada).

**Por que criar uma exceção própria?** Para que as camadas externas (API, por exemplo) possam distinguir entre:
- `DomainException` → erro de negócio → retornar HTTP 422 (Unprocessable Entity)
- `Exception` genérica → erro inesperado → retornar HTTP 500

Isso é feito no `ExceptionHandlingMiddleware` da API (será implementado depois).

---

### 3.5 `IDomainEvent.cs` — Interface de Eventos

```csharp
public interface IDomainEvent { }
```

**Conceito:** É um **contrato (marcador)** que todos os eventos de domínio devem implementar. Não tem métodos — serve apenas para identificar que um objeto é um evento de domínio.

**Por que é uma interface e não uma classe?** Flexibilidade. Um evento pode herdar de outras classes ou records enquanto ainda implementa essa interface. Também facilita futuras integrações com sistemas de mensageria (ex: MassTransit, MediatR notifications).

---

## 4. Os Value Objects do Negócio

### 4.1 `NumeroFatura.cs` — Número com Formato Válido

```csharp
public sealed class NumeroFatura : ValueObject
{
    private static readonly Regex _formato = new(@"^FAT-\d{6}$", RegexOptions.Compiled);

    public string Valor { get; }

    private NumeroFatura(string valor) => Valor = valor;

    public static NumeroFatura Criar(string valor) { ... }
}
```

**Por que é um Value Object e não uma `string` simples?**

Se o número da fatura fosse apenas uma `string`, qualquer valor seria aceito: `""`, `"abc"`, `"123"`. O `NumeroFatura` garante que **apenas strings no formato `FAT-000001`** existam no sistema. Essa validação acontece **no momento da criação**, não em algum validator externo.

**Por que o construtor é `private`?** Para forçar que a única forma de criar um `NumeroFatura` válido seja pelo método estático `Criar(...)`, que executa a validação. Ninguém pode criar um `NumeroFatura` inválido — é impossível pelo design.

**Por que `sealed`?** `NumeroFatura` não deve ser herdado. É um conceito fechado e específico.

**Por que `Regex.Compiled`?** A `Regex` é criada uma única vez (`static readonly`) e compilada para máquina nativa. Isso é mais eficiente do que compilar a expressão a cada chamada.

**O formato `FAT-XXXXXX`:** Exemplos válidos: `FAT-000001`, `FAT-000999`. Exatamente 6 dígitos após o prefixo `FAT-`.

---

### 4.2 `Dinheiro.cs` — Valor Monetário

```csharp
public sealed class Dinheiro : ValueObject
{
    public decimal Valor { get; }

    public static Dinheiro Zero => new(0m);
    public Dinheiro Somar(Dinheiro outro) => new(Valor + outro.Valor);
}
```

**Por que `decimal` e não `double` ou `float`?** `double` e `float` têm erro de precisão em aritmética de ponto flutuante. Para dinheiro, `0.1 + 0.2` em `double` pode resultar em `0.30000000000000004`. O tipo `decimal` foi criado exatamente para cálculos monetários precisos.

**Por que um VO para dinheiro?** Encapsula as invariantes monetárias (não pode ser negativo) e comportamentos (`Somar`). Também garante que operações monetárias sejam feitas de forma semântica e segura.

> **Nota:** Neste projeto, `Fatura` e `ItemFatura` usam `decimal` diretamente nos campos de valor (para simplificar o mapeamento com o EF Core). O `Dinheiro` existe como VO disponível para uso em cálculos ou extensões futuras.

---

## 5. O Enum `StatusFatura`

```csharp
public enum StatusFatura
{
    Aberta,
    Fechada
}
```

**Por que enum?** O status de uma fatura é um conjunto fixo e conhecido de valores. Um `enum` garante que nenhum valor inválido seja atribuído e melhora a legibilidade do código.

**Regra de negócio (RN-1):** Toda fatura nasce com status `Aberta`. Só transita para `Fechada` pelo método `Fechar()`. Não existe transição de volta — é **unidirecional**.

---

## 6. `FaturaErrors.cs` — Mensagens Centralizadas

```csharp
public static class FaturaErrors
{
    public const string NomeClienteObrigatorio = "O nome do cliente é obrigatório.";
    public const string FaturaJaFechada = "Não é possível alterar uma fatura fechada.";
    // ...
}
```

**Por que centralizar as mensagens?** Evita **strings duplicadas** espalhadas pelo código. Se a mensagem de erro precisar mudar, muda em um único lugar. Os testes unitários também referenciam essas constantes, então se a mensagem mudar o teste ainda funciona corretamente.

**Por que `const` e não `static readonly string`?** `const` é resolvido em tempo de compilação, é mais performático e não ocupa espaço em heap. Para strings simples e imutáveis, `const` é a escolha certa.

---

## 7. As Entidades de Negócio

### 7.1 `ItemFatura.cs` — Entidade Secundária

```csharp
public class ItemFatura : Entity
{
    public string Descricao { get; private set; }
    public int Quantidade { get; private set; }
    public decimal ValorUnitario { get; private set; }
    public decimal ValorTotalItem { get; private set; }
    public string? Justificativa { get; private set; }

    internal static ItemFatura Criar(...) { ... }
    internal void Atualizar(...) { ... }
}
```

**Por que `internal` nos métodos `Criar` e `Atualizar`?** `internal` significa que esses métodos só são visíveis **dentro do mesmo projeto** (`Faturas.Domain`). Assim, nem a camada de Application nem a API podem criar ou atualizar um `ItemFatura` diretamente. A única forma é chamar `fatura.AdicionarItem(...)` ou `fatura.AtualizarItem(...)`, garantindo que os guards da `Fatura` (verificar se está fechada) sempre sejam executados.

**Regras implementadas no `ItemFatura`:**

| Regra | Como é validada |
|-------|----------------|
| RN-7: item > R$ 1.000 exige justificativa | `quantidade * valorUnitario > 1000m && string.IsNullOrWhiteSpace(justificativa)` |
| RN-8: descrição obrigatória, mínimo 3 chars | `string.IsNullOrWhiteSpace(descricao)` e `descricao.Trim().Length < 3` |
| Quantidade > 0 | `quantidade <= 0` |
| Valor Unitário > 0 | `valorUnitario <= 0` |

**Por que `ValorTotalItem` é calculado na criação?** Para que o valor total do item seja sempre consistente com quantidade × valor unitário. Não confiamos que quem chama o método vai fazer essa conta certa.

**O construtor `private ItemFatura() { }`** existe para o Entity Framework Core, que precisa de um construtor sem parâmetros para materializar objetos vindos do banco de dados.

---

### 7.2 `Fatura.cs` — A Raiz do Agregado (o objeto mais importante)

```csharp
public class Fatura : AggregateRoot
{
    private readonly List<ItemFatura> _itens = [];

    public NumeroFatura Numero { get; private set; }
    public string NomeCliente { get; private set; }
    public DateTime DataEmissao { get; private set; }
    public StatusFatura Status { get; private set; }
    public decimal ValorTotal { get; private set; }
    public IReadOnlyCollection<ItemFatura> Itens => _itens.AsReadOnly();
    ...
}
```

**Por que `_itens` é `private` e `Itens` é `IReadOnlyCollection`?**

A lista interna `_itens` é completamente privada. O que o mundo externo enxerga é apenas uma **coleção somente leitura**. Isso impede que alguém faça `fatura.Itens.Add(item)` diretamente, bypassando as regras de negócio. A única forma de adicionar é `fatura.AdicionarItem(...)`.

**O método `Criar` (Factory Method):**

```csharp
public static Fatura Criar(string numero, string nomeCliente, DateTime dataEmissao)
```

É um **método estático de fábrica** (Factory Method pattern). Em vez de usar `new Fatura(...)` diretamente, usamos `Fatura.Criar(...)`. Isso permite que a fábrica execute validações **antes** de criar o objeto. Se as validações falharem, lança `DomainException` e o objeto nunca é criado. Um objeto `Fatura` que existe em memória **sempre é válido**.

**Todas as regras de negócio mapeadas:**

| RN | Regra | Método | Como funciona |
|----|-------|--------|---------------|
| RN-1 | Status inicial = Aberta | `Criar` | `Status = StatusFatura.Aberta` no construtor privado |
| RN-2 | Nome cliente obrigatório | `Criar` | Guard com `string.IsNullOrWhiteSpace` |
| RN-3 | Pode ter múltiplos itens | `AdicionarItem` | Adiciona na lista `_itens` |
| RN-4 | Total recalculado após mutação | `RecalcularTotal` | `_itens.Sum(i => i.ValorTotalItem)` |
| RN-5/6 | Fechada não pode ser alterada | `GuardarFechada` | Guard chamado em todo método mutável |
| RN-7 | Item > R$1.000 exige justificativa | `ItemFatura.Criar` | Validação no próprio item |
| RN-8 | Descrição obrigatória ≥ 3 chars | `ItemFatura.Criar` | Validação no próprio item |
| RN-9 | Pode fechar fatura | `Fechar` | Muda `Status` para `Fechada` |
| RN-10 | Consultar com filtros | `IFaturaRepository` | Implementado na camada de Application |

**O método `GuardarFechada` (Guard Clause):**

```csharp
private void GuardarFechada()
{
    if (Status == StatusFatura.Fechada)
        throw new DomainException(FaturaErrors.FaturaJaFechada);
}
```

É um **guard clause** — uma verificação que "guarda" os métodos mutáveis. Está presente em `AdicionarItem`, `RemoverItem`, `AtualizarItem` e `Fechar`. Em vez de repetir a mesma verificação em 4 lugares, extraímos para um método privado. Se a regra mudar, muda em um único ponto.

**O método `RecalcularTotal` (privado):**

```csharp
private void RecalcularTotal() =>
    ValorTotal = _itens.Sum(i => i.ValorTotalItem);
```

É chamado **após qualquer mutação de itens** (adicionar, remover, atualizar). Nunca é exposto publicamente porque recalcular o total é uma consequência interna de outras operações, não uma ação que faz sentido chamar externamente.

---

## 8. Os Eventos de Domínio

```csharp
public record FaturaCriadaEvent(Guid FaturaId, string Numero) : IDomainEvent;
public record ItemAdicionadoEvent(Guid FaturaId, Guid ItemId) : IDomainEvent;
public record FaturaFechadaEvent(Guid FaturaId) : IDomainEvent;
```

**O que são?** São notificações imutáveis de que **algo aconteceu** no domínio. São criados dentro dos métodos da `Fatura` e acumulados em `AggregateRoot._domainEvents`.

**Por que `record`?** `record` em C# é imutável por padrão e implementa igualdade por valor automaticamente. É perfeito para eventos, que não devem mudar após serem criados.

**Para que servem na prática?** Permitem que outras partes do sistema reajam a eventos sem acoplamento direto. Exemplos de uso futuro:
- Enviar e-mail quando uma fatura é fechada (`FaturaFechadaEvent`)
- Auditar quem criou cada fatura (`FaturaCriadaEvent`)
- Integrar com sistemas externos quando itens são adicionados (`ItemAdicionadoEvent`)

**Quando são publicados?** Os eventos ficam acumulados na `Fatura` durante a operação. Após salvar no banco de dados (via `SaveChangesAsync`), a Infrastructure os publica para os handlers registrados no MediatR. Esse padrão é chamado de **Outbox Pattern** simplificado.

---

## 9. O Repositório (`IFaturaRepository`)

```csharp
public interface IFaturaRepository
{
    Task<Fatura?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Fatura>> ListAsync(FaturaFilter filter, CancellationToken cancellationToken = default);
    Task AddAsync(Fatura fatura, CancellationToken cancellationToken = default);
    void Update(Fatura fatura);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

**O que é um repositório?** É uma **abstração sobre o acesso a dados**. O domínio não sabe se os dados vêm de um banco PostgreSQL, SQL Server, arquivo JSON, ou memória. Ele apenas chama `repository.GetByIdAsync(id)` e recebe uma `Fatura`.

**Por que a interface fica no Domínio e não na Infrastructure?** Porque o Domínio **define o contrato** do que precisa. A Infrastructure **implementa** esse contrato. Isso é o **Princípio de Inversão de Dependência (DIP)** — detalhes dependem de abstrações, não o contrário.

**Por que repositório por agregado?** Seguindo DDD, o repositório corresponde ao **Aggregate Root**, não a cada entidade. Não existe `IItemFaturaRepository` — `ItemFatura` sempre é acessado através de `Fatura`.

**`CancellationToken`:** Permite cancelar operações assíncronas (ex: se o usuário fechar a requisição HTTP antes de terminar). É uma boa prática em todos os métodos `async`.

**`FaturaFilter`:**

```csharp
public record FaturaFilter(
    string? NomeCliente = null,
    DateTime? DataInicial = null,
    DateTime? DataFinal = null,
    StatusFatura? Status = null
);
```

Encapsula os **critérios de busca** (RN-10). Usando um `record` com parâmetros opcionais (nullable), podemos passar qualquer combinação de filtros. A implementação concreta do repositório (na Infrastructure) traduzirá esses filtros em queries SQL.

---

## 10. Os Testes Unitários

Os testes ficam em `tests/Faturas.Domain.Tests/` e usam três bibliotecas:

| Biblioteca | Papel |
|-----------|-------|
| **xUnit** | Framework de testes (.NET padrão de mercado) |
| **FluentAssertions** | Assertions legíveis em inglês natural (`should.Be`, `should.Throw`) |
| **Bogus** | Geração de dados falsos realistas para os testes |

**Padrão AAA (Arrange, Act, Assert):**

```csharp
[Fact]
public void AdicionarItem_DeveLancar_QuandoFaturaFechada()
{
    // Arrange — prepara o cenário
    var fatura = new FaturaFaker().Generate();
    fatura.Fechar();

    // Act — executa a ação a ser testada
    Action act = () => fatura.AdicionarItem("Produto", 1, 10m);

    // Assert — verifica o resultado
    act.Should().Throw<DomainException>()
       .WithMessage(FaturaErrors.FaturaJaFechada);
}
```

**`FaturaFaker`:** É um builder de `Fatura` usando o Bogus. Gera faturas com dados realistas (nome de cliente real, datas recentes) sem precisar repetir código de criação em cada teste. O contador `_counter` garante que cada fatura gerada tenha um número único no formato correto.

**Por que os testes não usam mocks nem banco de dados?** Porque estão testando apenas o domínio. O domínio é puro — sem dependências externas. Não há nada para "mockar". Esse é o benefício da arquitetura limpa: o domínio pode ser testado de forma extremamente rápida e simples.

**18 cenários cobertos:**

| Cenário | O que valida |
|---------|-------------|
| Criação válida | Fatura criada com dados corretos, status Aberta, total zero |
| Nome cliente vazio | RN-2: lança `DomainException` |
| Número formato inválido | VO `NumeroFatura` rejeita formatos incorretos |
| Adicionar item válido | Item adicionado e total recalculado |
| Recálculo de total | Soma correta de múltiplos itens |
| Item em fatura fechada | RN-5/6: lança `DomainException` |
| Item > R$1.000 sem justificativa | RN-7: lança `DomainException` |
| Item > R$1.000 com justificativa | RN-7: aceita com justificativa |
| Fechar fatura | RN-9: status muda para Fechada |
| Fechar fatura já fechada | Guard clause funciona corretamente |
| Remover item em fatura fechada | RN-5/6 para remoção |
| Atualizar item em fatura fechada | RN-5/6 para atualização |
| Descrição vazia | RN-8: lança `DomainException` |
| Descrição < 3 chars | RN-8: lança `DomainException` |
| Quantidade zero | Invariante: lança `DomainException` |
| Valor unitário zero | Invariante: lança `DomainException` |
| Cálculo `ValorTotalItem` | Quantidade × Valor unitário correto |

---

## 11. Diagrama Conceitual

```
┌─────────────────────────────────────────────────────────────────┐
│                        FATURAS.DOMAIN                           │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              Agregado: Fatura                            │   │
│  │                                                          │   │
│  │   ┌──────────────────────────────────┐                  │   │
│  │   │  Fatura (AggregateRoot)          │                  │   │
│  │   │  ─────────────────────────────   │                  │   │
│  │   │  Id: Guid                        │                  │   │
│  │   │  Numero: NumeroFatura (VO)       │                  │   │
│  │   │  NomeCliente: string             │                  │   │
│  │   │  DataEmissao: DateTime           │                  │   │
│  │   │  Status: StatusFatura (Enum)     │                  │   │
│  │   │  ValorTotal: decimal             │                  │   │
│  │   │  Itens: [ ItemFatura ]           │                  │   │
│  │   │                                  │                  │   │
│  │   │  + Criar(...)        ← Factory   │                  │   │
│  │   │  + AdicionarItem(...)            │◄── Mundo Externo │   │
│  │   │  + RemoverItem(...)              │    só acessa     │   │
│  │   │  + AtualizarItem(...)            │    a Fatura      │   │
│  │   │  + Fechar()                      │                  │   │
│  │   │  - GuardarFechada()  ← Guard     │                  │   │
│  │   │  - RecalcularTotal() ← Privado   │                  │   │
│  │   └─────────────┬────────────────────┘                  │   │
│  │                 │ contém (1..*)                         │   │
│  │   ┌─────────────▼────────────────────┐                  │   │
│  │   │  ItemFatura (Entity)             │                  │   │
│  │   │  ─────────────────────────────   │                  │   │
│  │   │  Id: Guid                        │                  │   │
│  │   │  Descricao: string               │                  │   │
│  │   │  Quantidade: int                 │                  │   │
│  │   │  ValorUnitario: decimal          │                  │   │
│  │   │  ValorTotalItem: decimal         │                  │   │
│  │   │  Justificativa: string?          │                  │   │
│  │   │                                  │                  │   │
│  │   │  internal Criar(...)  ← só para  │                  │   │
│  │   │  internal Atualizar() ← Domain   │                  │   │
│  │   └──────────────────────────────────┘                  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                 │
│  Value Objects: NumeroFatura, Dinheiro                          │
│  Enum:          StatusFatura (Aberta, Fechada)                  │
│  Eventos:       FaturaCriada, ItemAdicionado, FaturaFechada     │
│  Contrato:      IFaturaRepository (implementado na Infra)       │
│  Erros:         FaturaErrors (constantes de mensagens)          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 12. Resumo das Decisões Técnicas

| Decisão | Alternativa | Por que escolhemos assim |
|---------|-------------|--------------------------|
| Domínio Rico | Domínio Anêmico + Services | Garante que regras de negócio sejam sempre respeitadas, sem depender da "boa vontade" de quem chama |
| `private set` em todas as propriedades | `public set` | Impede modificações não autorizadas; o estado muda só por métodos com semântica de negócio |
| `internal` em `ItemFatura.Criar` | `public` | Força que `ItemFatura` só seja criado pela `Fatura`, garantindo o guard de fatura fechada |
| Factory Method `Fatura.Criar` | `new Fatura(...)` público | Validação antes da criação; objeto inexistente se dados forem inválidos |
| Guard Clause `GuardarFechada` | Verificar em cada método | DRY (Don't Repeat Yourself); regra muda em um ponto |
| `IFaturaRepository` no Domínio | Interface na Infrastructure | Inversão de dependência; Domínio dita o contrato |
| `Guid` como identidade | `int` auto-increment | Distribuído, sem dependência do banco para gerar ID |
| `decimal` para valores monetários | `double` / `float` | Precisão exata em cálculos financeiros |
| `record` para eventos | `class` | Imutabilidade nativa e igualdade por valor |
| Constantes em `FaturaErrors` | Strings inline | Manutenção centralizada; testes referenciam mesma constante |
