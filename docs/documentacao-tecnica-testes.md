# Documentação Técnica — Camada de Testes (`Faturas.Domain.Tests` e `Faturas.Application.Tests`)

> **Objetivo deste documento:** explicar em profundidade a estratégia de testes adotada no projeto, detalhando cada decisão de design, ferramenta e padrão utilizado. O leitor entenderá não apenas *o que* os testes fazem, mas *por que* foram estruturados desta forma — e como essa estrutura se conecta à arquitetura geral do sistema.

---

## 1. A Filosofia de Testes deste Projeto

### 1.1 Por que testar?

Testes automatizados são a única forma de garantir que regras de negócio críticas — como "não é possível alterar uma fatura fechada" ou "itens acima de R$ 1.000 exigem justificativa" — continuam funcionando à medida que o código evolui. Sem testes, cada mudança no código é uma aposta.

Neste projeto, os testes cumprem três funções:

| Função | Descrição |
|--------|-----------|
| **Verificação de regras de negócio** | Garantem que o domínio se comporta como o enunciado determina |
| **Documentação viva** | Um teste bem nomeado descreve um comportamento do sistema sem ambiguidade |
| **Rede de segurança para refatoração** | Permitem alterar a implementação com confiança de que o contrato de negócio não foi quebrado |

### 1.2 A Pirâmide de Testes e como este projeto a aplica

A pirâmide de testes é um guia clássico que define a proporção ideal entre tipos de teste:

```
           ╱▔▔▔▔▔▔▔▔╲          ← Integração / E2E
          ╱▔▔▔▔▔▔▔▔▔▔▔▔╲       (poucos, lentos, caros)
         ╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔╲    ← Serviços / Handlers
        ╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔╲ ← Unidade / Domínio
       ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔ (muitos, rápidos, baratos)
```

Este projeto implementa dois níveis:

- **`Faturas.Domain.Tests`** — base da pirâmide. Testes unitários puros do domínio. Sem mocks, sem banco de dados, sem infraestrutura. Apenas regras de negócio.
- **`Faturas.Application.Tests`** — camada intermediária. Testa os Handlers e Validators da Application. Usa mocks do repositório para isolar o banco de dados.

> **Importante:** os testes de domínio NÃO precisam de mocks porque o domínio não tem dependências externas. Essa é uma das grandes vantagens da Clean Architecture — a camada mais crítica do sistema é também a mais fácil de testar.

---

## 2. Estrutura de Projetos

```
tests/
├── Faturas.Domain.Tests/                  ← Testes unitários do domínio (sem mocks)
│   ├── Faturas/
│   │   ├── Builders/
│   │   │   ├── FaturaFaker.cs             ← Gerador de Faturas realistas (Bogus)
│   │   │   └── ItemFaturaFaker.cs         ← Gerador de dados de ItemFatura
│   │   ├── FaturaTests.cs                 ← Testes do Aggregate Root Fatura
│   │   └── ItemFaturaTests.cs             ← Testes das invariantes de ItemFatura
│   └── Faturas.Domain.Tests.csproj
│
└── Faturas.Application.Tests/             ← Testes de Handlers e Validators (com mocks)
    ├── Common/
    │   └── Fakers/
    │       └── FaturaFaker.cs             ← Faker reutilizável para Application tests
    ├── Features/
    │   └── Faturas/
    │       ├── Commands/
    │       │   ├── CreateFaturaHandlerTests.cs
    │       │   ├── CreateFaturaValidatorTests.cs
    │       │   ├── AddItemFaturaHandlerTests.cs
    │       │   ├── AddItemFaturaValidatorTests.cs
    │       │   └── FecharFaturaHandlerTests.cs
    │       └── Queries/
    │           ├── GetFaturaByIdHandlerTests.cs
    │           ├── ListFaturasHandlerTests.cs
    │           └── ListFaturasValidatorTests.cs
    └── Faturas.Application.Tests.csproj
```

---

## 3. Pacotes NuGet e seus Papéis

### 3.1 Pacotes do `Faturas.Domain.Tests`

| Pacote | Versão | Papel |
|--------|--------|-------|
| `xUnit` | 2.5.3 | Framework de testes. Descobre e executa os métodos marcados com `[Fact]` e `[Theory]` |
| `xunit.runner.visualstudio` | 2.5.3 | Integração do xUnit com o Test Explorer do Visual Studio e a CLI `dotnet test` |
| `Microsoft.NET.Test.Sdk` | 17.8.0 | SDK base que permite o `dotnet test` descobrir os testes |
| `FluentAssertions` | 6.12.2 | Assertions expressivas em inglês natural: `result.Should().Be(...)` |
| `Bogus` | 35.6.1 | Geração de dados fake realistas: nomes, datas, valores |
| `coverlet.collector` | 6.0.0 | Coleta de cobertura de código durante a execução dos testes |

### 3.2 Pacotes adicionais do `Faturas.Application.Tests`

| Pacote | Versão | Papel |
|--------|--------|-------|
| `NSubstitute` | 5.1.0 | Biblioteca de mocking — cria implementações falsas de interfaces (ex: `IFaturaRepository`) |

> **Por que NSubstitute e não Moq?** NSubstitute tem sintaxe mais fluente e menos verbosa. `Substitute.For<IFaturaRepository>()` vs `new Mock<IFaturaRepository>()`. Para verificação de chamadas: `repository.Received(1).AddAsync(...)` vs `mock.Verify(r => r.AddAsync(...), Times.Once())`. O resultado é o mesmo, o código fica mais legível.

---

## 4. Padrões e Ferramentas em Profundidade

Antes de entrar nos testes específicos, é essencial entender os padrões utilizados em toda a suíte.

### 4.1 O Padrão AAA (Arrange, Act, Assert)

Todos os testes seguem rigorosamente o padrão **AAA**:

```csharp
[Fact]
public void AdicionarItem_DeveLancar_QuandoFaturaFechada()
{
    // Arrange — prepara o cenário de teste
    var fatura = new FaturaFaker().Generate();
    fatura.Fechar();

    // Act — executa a ação que queremos testar
    Action act = () => fatura.AdicionarItem("Produto", 1, 10m);

    // Assert — verifica se o resultado é o esperado
    act.Should().Throw<DomainException>()
       .WithMessage(FaturaErrors.FaturaJaFechada);
}
```

**Por que separar em três fases?** Força clareza estrutural. Quem lê o teste sabe imediatamente: o que o cenário exige (Arrange), o que está sendo testado (Act) e qual o resultado esperado (Assert). Um teste mal estruturado muitas vezes falha não porque o código está errado, mas porque o cenário não foi preparado corretamente.

**Por que capturar a ação em `Action act`?** Para testar exceções com FluentAssertions de forma elegante. `act.Should().Throw<DomainException>()` é muito mais expressivo do que envolver em `try/catch` ou usar `Assert.Throws`.

### 4.2 `[Fact]` vs `[Theory]`

xUnit oferece dois tipos de marcadores de teste:

| Marcador | Uso | Exemplo |
|----------|-----|---------|
| `[Fact]` | Um cenário fixo, sem variação de dados | `Criar_DeveCriarFatura_QuandoDadosValidos` |
| `[Theory]` + `[InlineData]` | Mesmo teste com múltiplos conjuntos de dados | Validação de strings vazias e com espaços |

```csharp
// Testa duas variações com um único método
[Theory]
[InlineData("")]
[InlineData("   ")]
public async Task Validar_DeveFalhar_QuandoNumeroVazio(string numero)
{
    var request = new CreateFaturaRequest(numero, "João Silva", DateTime.UtcNow);
    var result = await _validator.ValidateAsync(request);
    result.IsValid.Should().BeFalse();
}
```

**Por que `[Theory]`?** Evita código duplicado para cenários que diferem apenas nos dados de entrada. Uma string vazia `""` e uma string de espaços `"   "` devem ter o mesmo comportamento — ambas são "vazias" para fins de validação. Com `[Theory]`, definimos a regra uma vez e a executamos com todas as variações.

### 4.3 FluentAssertions — Assertions Legíveis

FluentAssertions transforma assertions técnicas em frases próximas do inglês natural:

```csharp
// Sem FluentAssertions (MSTest/NUnit padrão)
Assert.Equal("Aberta", result.Value.Status);
Assert.True(result.IsSuccess);
Assert.Throws<DomainException>(() => fatura.Fechar());

// Com FluentAssertions
result.Value!.Status.Should().Be("Aberta");
result.IsSuccess.Should().BeTrue();
act.Should().Throw<DomainException>().WithMessage(FaturaErrors.FaturaJaFechada);
```

**Vantagem real:** quando um teste falha, a mensagem de erro do FluentAssertions é muito mais descritiva. Em vez de `"Expected: true. Actual: false"`, você recebe `"Expected result.IsSuccess to be true, but found false"`.

**`.WithMessage(...)`:** Verifica não só o tipo da exceção, mas também a mensagem específica. Isso garante que a exceção correta está sendo lançada — não qualquer `DomainException`, mas a com a mensagem exata da regra violada.

### 4.4 Bogus — Geração de Dados Realistas

Bogus é uma biblioteca de geração de dados fake. Em vez de hardcodar `"João Silva"` em cada teste, geramos nomes reais e aleatórios:

```csharp
public class FaturaFaker : Faker<Fatura>
{
    private static int _counter = 1;

    public FaturaFaker()
    {
        CustomInstantiator(f =>
        {
            var numero = $"NF-{_counter++:D4}-{f.Random.AlphaNumeric(4).ToUpper()}";
            var nomeCliente = f.Person.FullName;   // "Carlos Eduardo Souza"
            var dataEmissao = f.Date.Recent(30).ToUniversalTime();
            return Fatura.Criar(numero, nomeCliente, dataEmissao);
        });
    }
}
```

**Por que Bogus e não dados fixos?**

| Abordagem | Problema |
|-----------|----------|
| Dados fixos hardcodados | Tests acidentalmente acoplados aos dados. Ex: um teste que passa em `"João Silva"` pode falhar em `"María García"` por encoding |
| Bogus | Cada execução usa dados diferentes. Revela bugs que dados fixos não revelariam |

**`static int _counter`:** O número da fatura precisa ser único e seguir o formato `NF-0001-XXXX`. Um contador estático garante que cada `FaturaFaker().Generate()` produz um número diferente, mesmo em paralelo dentro da mesma sessão de testes.

**`CustomInstantiator`:** É o método do Bogus que define como criar a instância do objeto. Como `Fatura` não tem construtor público (só o factory method `Fatura.Criar(...)`), usamos o `CustomInstantiator` para chamar o método correto.

### 4.5 NSubstitute — Mocking de Interfaces

NSubstitute cria implementações falsas ("mocks") de interfaces em tempo de execução. No contexto da Application, o objeto que precisamos mockar é o `IFaturaRepository`:

```csharp
// Cria uma implementação falsa de IFaturaRepository
var repository = Substitute.For<IFaturaRepository>();

// Configura o comportamento: quando GetByIdAsync for chamado com esse ID, retorna essa fatura
repository.GetByIdAsync(fatura.Id, Arg.Any<CancellationToken>()).Returns(fatura);

// Verifica que o método foi chamado exatamente uma vez
await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
```

**Por que mockar o repositório?** Os testes de Application testam a **lógica de orquestração** dos Handlers — não o banco de dados. Se usássemos um repositório real, precisaríamos de um PostgreSQL disponível, migrações aplicadas, dados de seed. Os testes ficariam lentos, frágeis e dependentes de infraestrutura. Com o mock, o Handler recebe uma `Fatura` conforme configurado e os testes rodam em milissegundos.

**`Arg.Any<T>()`:** Diz ao NSubstitute "aceite qualquer valor deste tipo para este parâmetro". É útil quando o valor exato do argumento não é relevante para o cenário. Para o `CancellationToken`, por exemplo, nunca precisamos verificar o token específico — só que o método foi chamado.

**`.Returns(valor)`:** Configura o retorno do método mockado. NSubstitute lida com o `Task<T>` automaticamente — se o método retorna `Task<Fatura?>`, basta passar a `Fatura?` diretamente em `Returns`.

---

## 5. `Faturas.Domain.Tests` em Profundidade

### 5.1 Os Builders (`Faturas/Builders/`)

Os Builders são a fundação da suíte de testes. Eles encapsulam a criação de objetos válidos para uso nos testes.

#### `FaturaFaker.cs`

```csharp
public class FaturaFaker : Faker<Fatura>
{
    private static int _counter = 1;

    public FaturaFaker()
    {
        CustomInstantiator(f =>
        {
            var numero = $"NF-{_counter++:D4}-{f.Random.AlphaNumeric(4).ToUpper()}";
            var nomeCliente = f.Person.FullName;
            var dataEmissao = f.Date.Recent(30).ToUniversalTime();
            return Fatura.Criar(numero, nomeCliente, dataEmissao);
        });
    }
}
```

**Papel:** Gera instâncias de `Fatura` válidas (com todos os invariantes satisfeitos). Evita repetição de código de criação em cada teste. Um teste que precisa de uma `Fatura` em estado válido para então testá-la em um cenário específico (ex: fechada, com itens) começa sempre com `new FaturaFaker().Generate()`.

**Por que herdar de `Faker<Fatura>` e não de `Faker`?** `Faker<T>` é fortemente tipado — o `Generate()` retorna diretamente um `Fatura`, sem necessidade de cast.

#### `ItemFaturaFaker.cs`

```csharp
public class ItemFaturaFaker : Faker
{
    public string Descricao => Commerce.ProductName();
    public int Quantidade => Random.Int(1, 10);
    public decimal ValorUnitario => Random.Decimal(1m, 100m);
}
```

**Papel:** Gera dados de item (não o item em si, pois `ItemFatura` só é criado via `fatura.AdicionarItem(...)`). O `ItemFaturaFaker` provê os dados de entrada para esse método. O `ValorUnitario` é limitado a no máximo `100m` para garantir que o total do item fique abaixo de R$ 1.000 (evitando a necessidade de justificativa por padrão).

---

### 5.2 `FaturaTests.cs` — Todos os Cenários

Este arquivo testa o **Aggregate Root `Fatura`** — o objeto mais importante do domínio.

#### Grupo: Criação

**`Criar_DeveCriarFatura_QuandoDadosValidos`**

Verifica que, ao chamar `Fatura.Criar(...)` com dados válidos, o objeto retornado possui todas as propriedades corretas: número, nome do cliente, status inicial `Aberta` e valor total zero (sem itens ainda).

```csharp
var fatura = Fatura.Criar("FAT-000001", "João da Silva", DateTime.UtcNow);

fatura.Numero.Valor.Should().Be("FAT-000001");
fatura.Status.Should().Be(StatusFatura.Aberta);  // RN-1
fatura.ValorTotal.Should().Be(0m);
fatura.Itens.Should().BeEmpty();
```

**Por que verificar `Status` e `ValorTotal` na criação?** São invariantes iniciais — regras que devem ser verdadeiras no momento em que a `Fatura` é criada. Se esquecermos de inicializar `Status = Aberta` no construtor, este teste falha imediatamente.

---

**`Criar_DeveLancar_QuandoNomeClienteVazio`** (Theory: `""` e `"   "`)

Verifica que o factory method `Fatura.Criar(...)` recusa nomes de cliente vazios ou contendo apenas espaços. Implementa a **RN-2** do enunciado.

```csharp
Action act = () => Fatura.Criar("FAT-000001", nomeCliente, DateTime.UtcNow);

act.Should().Throw<DomainException>()
   .WithMessage(FaturaErrors.NomeClienteObrigatorio);
```

**Por que `[Theory]` com duas variações?** Uma string vazia `""` e uma string de espaços `"   "` podem ter tratamentos diferentes dependendo da implementação. O guard usa `string.IsNullOrWhiteSpace(nomeCliente)`, então `"   "` também deve falhar — e o `[Theory]` garante que ambos os casos são cobertos.

---

#### Grupo: Adição de Itens

**`AdicionarItem_DeveAdicionarItem_QuandoDadosValidos`**

Caminho feliz: adiciona um item e verifica que a coleção de itens foi atualizada e o total recalculado.

```csharp
fatura.AdicionarItem("Notebook", 1, 500m);

fatura.Itens.Should().HaveCount(1);
fatura.ValorTotal.Should().Be(500m);
```

**`AdicionarItem_DeveRecalcularValorTotal`**

Adiciona dois itens e verifica que o `ValorTotal` é a soma de ambos. Cobre a **RN-4**.

```csharp
fatura.AdicionarItem("Monitor", 2, 300m);   // total parcial = 600
fatura.AdicionarItem("Teclado", 1, 150m);   // total parcial = 150

fatura.ValorTotal.Should().Be(750m);        // 600 + 150
```

**Por que testar com dois itens?** Um único item poderia fazer o `ValorTotal = ValorTotalItem` por coincidência, sem que o `RecalcularTotal` realmente some a coleção. Dois itens provam que o somatório está correto.

---

**`AdicionarItem_DeveLancar_QuandoFaturaFechada`**

Garante que a **RN-5/6** está implementada: fatura fechada não pode receber novos itens.

```csharp
fatura.Fechar();
Action act = () => fatura.AdicionarItem("Produto", 1, 10m);

act.Should().Throw<DomainException>()
   .WithMessage(FaturaErrors.FaturaJaFechada);
```

**Por que verificar a mensagem específica?** Um `DomainException` genérico poderia ser lançado por outro guard por engano. Verificar `.WithMessage(FaturaErrors.FaturaJaFechada)` garante que é *exatamente esse* guard que está sendo ativado.

---

**`AdicionarItem_DeveLancar_QuandoValorMaiorQue1000ESemJustificativa`**

Cobre a **RN-7**: itens com `quantidade × valorUnitario > 1000` exigem justificativa.

```csharp
// 2 unidades × R$ 600,00 = R$ 1.200,00 > R$ 1.000,00 → exige justificativa
Action act = () => fatura.AdicionarItem("Servidor", 2, 600m, justificativa: null);

act.Should().Throw<DomainException>()
   .WithMessage(FaturaErrors.JustificativaObrigatoriaAcimaDe1000);
```

**Por que usar 2 × 600 e não 1 × 1100?** Para validar que o critério é o *total do item* (`quantidade × valorUnitario`), não apenas o `valorUnitario`. Um valor unitário de R$ 600 individual não ultrapassaria o limite, mas dois deles ultrapassam.

---

**`AdicionarItem_DeveAdicionar_QuandoValorMaiorQue1000ComJustificativa`**

O cenário complementar da RN-7: o mesmo item de R$ 1.200 deve ser aceito quando a justificativa é fornecida.

```csharp
fatura.AdicionarItem("Servidor", 2, 600m, justificativa: "Aprovado pelo gestor");

fatura.Itens.Should().HaveCount(1);
fatura.ValorTotal.Should().Be(1200m);
```

---

#### Grupo: Fechar Fatura

**`Fechar_DeveAlterarStatusParaFechada`**

Caminho feliz da **RN-9**: fechar uma fatura aberta muda o status para `Fechada`.

```csharp
fatura.Fechar();
fatura.Status.Should().Be(StatusFatura.Fechada);
```

**`Fechar_DeveLancar_QuandoJaFechada`**

Fatura já fechada não pode ser fechada novamente. Garante idempotência negativa do estado.

```csharp
fatura.Fechar();
Action act = () => fatura.Fechar();

act.Should().Throw<DomainException>()
   .WithMessage(FaturaErrors.FaturaJaFechada);
```

---

#### Grupo: Remover e Atualizar Item

**`RemoverItem_DeveLancar_QuandoFaturaFechada`** e **`AtualizarItem_DeveLancar_QuandoFaturaFechada`**

Verificam que o guard `GuardarFechada` está presente em **todos** os métodos mutáveis — não só em `AdicionarItem`. Uma fatura fechada não pode ser alterada de nenhuma forma (RN-5/6).

```csharp
var item = fatura.AdicionarItem("Produto", 1, 10m);
fatura.Fechar();

Action remover  = () => fatura.RemoverItem(item.Id);
Action atualizar = () => fatura.AtualizarItem(item.Id, "Novo", 2, 20m);

remover.Should().Throw<DomainException>().WithMessage(FaturaErrors.FaturaJaFechada);
atualizar.Should().Throw<DomainException>().WithMessage(FaturaErrors.FaturaJaFechada);
```

---

### 5.3 `ItemFaturaTests.cs` — Invariantes do Item

Este arquivo testa as invariantes que pertencem ao `ItemFatura` em si, independentemente da fatura que o contém.

**`Item_DeveLancar_QuandoDescricaoVazia`** e **`Item_DeveLancar_QuandoDescricaoMenorQueMinimo`**

Validam a **RN-8**: descrição obrigatória com no mínimo 3 caracteres. `"AB"` tem 2 caracteres e deve ser rejeitado.

**`Item_DeveLancar_QuandoQuantidadeZero`**

Quantidade deve ser estritamente maior que zero. `0` é inválido.

**`Item_DeveLancar_QuandoValorUnitarioZero`**

Valor unitário deve ser estritamente maior que zero. `0m` é inválido.

**`Item_DeveCalcularValorTotalItem_Corretamente`**

Verifica que `ValorTotalItem = Quantidade × ValorUnitario` é calculado corretamente:

```csharp
var item = fatura.AdicionarItem("Produto", 3, 150m);
item.ValorTotalItem.Should().Be(450m);  // 3 × 150 = 450
```

---

## 6. `Faturas.Application.Tests` em Profundidade

### 6.1 A Estratégia de Isolamento

Os testes de Application têm um desafio diferente dos testes de Domínio: os Handlers têm dependências — especificamente, `IFaturaRepository`. Para testar um Handler de forma isolada, precisamos controlar o comportamento do repositório.

A estratégia é:

```
Teste → Handler → IFaturaRepository (mock controlado pelo teste)
                        ↑
                  NSubstitute cria uma
                  implementação falsa
                  que retorna o que
                  o teste define
```

Cada teste de Handler:
1. Cria um mock de `IFaturaRepository` com NSubstitute
2. Configura o comportamento esperado do repositório
3. Instancia o Handler passando o mock como dependência
4. Executa o Handler com um Request específico
5. Verifica o resultado e se os métodos corretos foram chamados no repositório

### 6.2 O `FaturaFaker` da Application

```csharp
// tests/Faturas.Application.Tests/Common/Fakers/FaturaFaker.cs
public class FaturaFaker : Faker<Fatura>
{
    private static int _counter = 1;

    public FaturaFaker()
    {
        CustomInstantiator(f =>
        {
            var numero = $"NF-{_counter++:D4}-{f.Random.AlphaNumeric(4).ToUpper()}";
            var nomeCliente = f.Person.FullName;
            var dataEmissao = f.Date.Recent(30).ToUniversalTime();
            return Fatura.Criar(numero, nomeCliente, dataEmissao);
        });
    }
}
```

**Por que duplicar o Faker do Domain.Tests?** `Faturas.Application.Tests` não tem (e não deve ter) dependência de `Faturas.Domain.Tests`. Projetos de teste são independentes entre si — um não deve referenciar o outro. Por isso, o Faker é replicado. A lógica é idêntica; o namespace é diferente (`Faturas.Application.Tests.Common.Fakers` vs `Faturas.Domain.Tests.Faturas.Builders`).

---

### 6.3 Testes de Handlers

Os Handlers implementam a lógica de orquestração de cada caso de uso. Os testes de Handler verificam:
- O **caminho feliz** (sucesso): o Handler executa a operação e retorna um `Result` de sucesso
- O **caminho de falha** (não encontrado): o Handler retorna um `Result` de falha quando o recurso não existe

#### `CreateFaturaHandlerTests`

O `CreateFaturaHandler` é o caso mais simples — não precisa buscar nada no repositório antes de criar.

```csharp
public CreateFaturaHandlerTests()
{
    _repository = Substitute.For<IFaturaRepository>();
    _handler = new CreateFaturaHandler(_repository);
}
```

**Padrão de construtor de teste:** O repositório mock e o handler são criados no construtor da classe de teste. Isso segue o padrão **"Shared context via constructor"** do xUnit — cada método de teste recebe uma instância limpa da classe, portanto um mock limpo sem configurações residuais de outros testes.

**`Handle_DeveRetornarSucesso_QuandoDadosValidos`:**

```csharp
var request = new CreateFaturaRequest("FAT-0001", "João Silva", DateTime.UtcNow);

var result = await _handler.Handle(request, CancellationToken.None);

result.IsSuccess.Should().BeTrue();
result.Value!.Numero.Should().Be(request.Numero);
result.Value.Status.Should().Be("Aberta");
result.Value.ValorTotal.Should().Be(0m);
await _repository.Received(1).AddAsync(Arg.Any<Fatura>(), Arg.Any<CancellationToken>());
await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
```

**Por que verificar `Received(1)` para `AddAsync` e `SaveChangesAsync`?** Para garantir que o Handler realmente persistiu a fatura. Um Handler que retorna sucesso mas não chama `AddAsync` passaria na assertion de retorno, mas a fatura não seria salva. Verificar as chamadas ao repositório protege contra esse tipo de bug.

---

#### `AddItemFaturaHandlerTests`

O `AddItemFaturaHandler` primeiro busca a fatura pelo ID e depois adiciona o item. Isso exige dois cenários:

**`Handle_DeveRetornarSucesso_QuandoFaturaExiste`:**

```csharp
var fatura = new FaturaFaker().Generate();
_repository.GetByIdAsync(fatura.Id, Arg.Any<CancellationToken>()).Returns(fatura);
var request = new AddItemFaturaRequest(fatura.Id, "Monitor", 2, 300m, null);

var result = await _handler.Handle(request, CancellationToken.None);

result.IsSuccess.Should().BeTrue();
result.Value!.Descricao.Should().Be("Monitor");
result.Value.Quantidade.Should().Be(2);
result.Value.ValorTotalItem.Should().Be(600m);       // 2 × 300
_repository.Received(1).AddItem(Arg.Any<ItemFatura>());
```

**Por que verificar `ValorTotalItem = 600m`?** Além de verificar que o Handler funcionou, verificamos que o cálculo do item foi correto. Uma resposta com `ValorTotalItem = 0` indicaria que o item foi criado mas o cálculo não foi executado.

**`Handle_DeveRetornarFalha_QuandoFaturaNaoEncontrada`:**

```csharp
_repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Fatura?)null);
var request = new AddItemFaturaRequest(Guid.NewGuid(), "Monitor", 1, 300m, null);

var result = await _handler.Handle(request, CancellationToken.None);

result.IsFailure.Should().BeTrue();
result.Error.Code.Should().Be("NotFound");
```

**Por que `.Returns((Fatura?)null)`?** O tipo de retorno do `GetByIdAsync` é `Task<Fatura?>` — nullable. Para simular o cenário de "fatura não encontrada", o repositório mock deve retornar `null`. O cast explícito `(Fatura?)null` é necessário para que o NSubstitute resolva o tipo corretamente.

---

#### `FecharFaturaHandlerTests`

Além de verificar sucesso e falha, este teste confirma que após `Fechar()`, o status da fatura refletido na resposta é `"Fechada"`:

```csharp
var result = await _handler.Handle(request, CancellationToken.None);

result.Value!.Status.Should().Be("Fechada");
_repository.Received(1).Update(fatura);
```

**Por que verificar `Update(fatura)` e não `AddAsync`?** `Fechar` é uma atualização de estado — a fatura já existe no banco. O Handler correto deve chamar `Update`, não `Add`. Testar isso previne uma regressão onde alguém substitui `Update` por `Add` por engano.

---

#### `GetFaturaByIdHandlerTests`

Este teste adiciona um item à fatura antes de buscar, para verificar que a resposta inclui os itens corretamente:

```csharp
var fatura = new FaturaFaker().Generate();
fatura.AdicionarItem("Notebook", 1, 500m);
_repository.GetByIdAsync(fatura.Id, Arg.Any<CancellationToken>()).Returns(fatura);

var result = await _handler.Handle(request, CancellationToken.None);

result.Value!.Itens.Should().HaveCount(1);
result.Value.ValorTotal.Should().Be(500m);
```

**Por que adicionar um item antes?** Para validar o mapeamento dos itens na resposta. Se o Handler mapeia apenas `Fatura` mas ignora `fatura.Itens`, o teste falharia com `Itens.Count = 0`.

---

#### `ListFaturasHandlerTests`

Este Handler tem comportamento de paginação que merece atenção especial.

**`Handle_DeveRetornarPaginaVazia_QuandoNaoHaFaturas`:**

Configura o repositório para retornar lista vazia e `Count = 0`. Verifica que a resposta reflete esse estado corretamente.

**`Handle_DeveRetornarFaturas_QuandoExistemRegistros`:**

```csharp
var faturas = new FaturaFaker().Generate(3);
_repository.ListAsync(...).Returns((IReadOnlyList<Fatura>)faturas);
_repository.CountAsync(...).Returns(3);

var result = await _handler.Handle(request, CancellationToken.None);

result.Value!.TotalRegistros.Should().Be(3);
result.Value.Itens.Should().HaveCount(3);
result.Value.TotalPaginas.Should().Be(1);  // ceil(3 / 10) = 1 página
```

**Por que verificar `TotalPaginas`?** O cálculo de `TotalPaginas = ceil(total / tamanhoPagina)` é uma lógica no Handler que pode ter bugs. Verificar `1` página para 3 registros com tamanho padrão de 10 confirma que o cálculo está correto.

**`Handle_DeveUsarValoresPadrao_QuandoPaginacaoInvalida`:**

```csharp
// Valores inválidos: Pagina = 0, TamanhoPagina = 0
var request = new ListFaturasRequest(null, null, null, null, Pagina: 0, TamanhoPagina: 0);

var result = await _handler.Handle(request, CancellationToken.None);

result.Value!.Pagina.Should().Be(1);          // 0 é corrigido para 1
result.Value.TamanhoPagina.Should().Be(10);   // 0 é corrigido para 10
```

**Por que testar valores inválidos de paginação?** O Handler tem lógica de normalização: `var pagina = request.Pagina < 1 ? 1 : request.Pagina`. Se essa lógica fosse removida, a query ao banco retornaria a primeira página mas a resposta indicaria página 0 — uma inconsistência confusa para o cliente da API.

---

### 6.4 Testes de Validators

Os Validators são testados independentemente dos Handlers. Isso porque o Validator é executado *antes* do Handler (via `ValidationBehavior` do MediatR), mas no teste do Handler não passa pelo pipeline do MediatR — o Handler é instanciado e chamado diretamente. Portanto, testar os Validators separadamente é obrigatório.

#### `CreateFaturaValidatorTests`

O padrão de todos os testes de Validator é o mesmo:

```csharp
private readonly CreateFaturaValidator _validator = new();

[Fact]
public async Task Validar_DevePassar_QuandoDadosValidos()
{
    var request = new CreateFaturaRequest("FAT-0001", "João Silva", DateTime.UtcNow);
    var result = await _validator.ValidateAsync(request);
    result.IsValid.Should().BeTrue();
}
```

**Por que `new CreateFaturaValidator()` no campo (não no construtor)?** O Validator não tem estado mutável — ele pode ser reutilizado entre testes sem risco de contaminação. Inicializar no campo (`= new()`) é mais conciso que criar no construtor.

**Por que `async` em testes de Validator?** O FluentValidation tem `ValidateAsync` para suportar validações assíncronas (ex: verificar unicidade no banco). Embora este projeto não use validações async, usar `ValidateAsync` uniformemente é uma boa prática — se uma validação async for adicionada futuramente, os testes já estão preparados.

**`Validar_DeveFalhar_QuandoNomeClienteExcedeTamanhoMaximo`:**

```csharp
var nomeGrande = new string('A', 151);  // 151 chars > limite de 150
var result = await _validator.ValidateAsync(request);
result.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrorMessages.Fatura.NomeClienteTamanhoMaximo);
```

**Por que `result.Errors.Should().Contain(...)` e não `result.Errors.First().ErrorMessage.Should().Be(...)`?** Uma request inválida pode ter múltiplos erros de validação. `Contain` verifica que *ao menos um* dos erros tem a mensagem esperada, sem assumir a posição ou o número total de erros.

**Por que referenciar `ApplicationErrorMessages.Fatura.NomeClienteTamanhoMaximo` em vez de hardcodar a string?** Porque o test e o Validator usam a mesma constante. Se a mensagem for alterada em `ApplicationErrorMessages`, ambos refletem a mudança automaticamente — o teste não quebra por discrepância de string.

---

#### `AddItemFaturaValidatorTests`

```csharp
[Theory]
[InlineData(0)]
[InlineData(-1)]
public async Task Validar_DeveFalhar_QuandoQuantidadeInvalida(int quantidade)
```

**Por que testar `0` e `-1` para quantidade?** A regra é `quantidade > 0`. Testar exatamente o valor do limite (`0`) e um valor abaixo (`-1`) garante que o boundary está correto. Testar apenas `-1` deixaria dúvida sobre se `0` seria aceito.

---

#### `ListFaturasValidatorTests`

```csharp
[Fact]
public async Task Validar_DeveFalhar_QuandoDataInicialMaiorQueDataFinal()
{
    // DataInicial = hoje, DataFinal = 7 dias atrás → período invertido
    var request = new ListFaturasRequest(null, DateTime.UtcNow, DateTime.UtcNow.AddDays(-7), null);
    var result = await _validator.ValidateAsync(request);
    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrorMessages.ListFaturas.PeriodoInvalido);
}
```

**Por que testar o período invertido?** O Validator usa `When(ambas as datas preenchidas, ...)` para validar a ordem. Se apenas uma das datas for fornecida, não há validação de período — o que faz sentido (filtro parcial é válido). Este teste confirma que quando **ambas** são fornecidas e a inicial é maior, a validação recusa.

---

## 7. Relação entre os Tipos de Teste

A tabela abaixo mostra o que cada tipo de teste cobre e o que ele *não* cobre:

| O que é testado | Domain.Tests | Application.Tests | Integration.Tests* |
|----------------|:---:|:---:|:---:|
| Regras de negócio (guards, invariantes) | ✅ | — | — |
| Orquestração do Handler (fluxo) | — | ✅ | — |
| Validação de entrada (Validators) | — | ✅ | — |
| Mapeamento de resposta (Response) | — | ✅ | — |
| Repositório com banco real | — | — | ✅ |
| API HTTP (status codes, headers) | — | — | ✅ |
| Fluxo end-to-end completo | — | — | ✅ |

*Integration.Tests está previsto como bônus no PLAN, ainda não implementado.

---

## 8. Convenções de Nomenclatura dos Testes

Todos os testes seguem o padrão:

```
[Método]_[Resultado esperado]_[Condição]
```

Exemplos:
- `Criar_DeveCriarFatura_QuandoDadosValidos` — método `Criar`, deve criar fatura, quando dados são válidos
- `AdicionarItem_DeveLancar_QuandoFaturaFechada` — método `AdicionarItem`, deve lançar exceção, quando a fatura está fechada
- `Handle_DeveRetornarFalha_QuandoFaturaNaoEncontrada` — método `Handle`, deve retornar falha, quando a fatura não é encontrada
- `Validar_DeveFalhar_QuandoNumeroVazio` — método `Validar`, deve falhar, quando o número está vazio

**Por que esta convenção?** O nome do teste é sua documentação. Ao ler a lista de testes no Test Explorer ou na saída do `dotnet test`, fica imediatamente claro o que cada teste valida — sem precisar abrir o arquivo. Quando um teste falha em CI, a mensagem `Criar_DeveLancar_QuandoNomeClienteVazio FALHOU` é auto-explicativa.

---

## 9. Como Executar os Testes

### Executar todos os testes da Solution

```bash
dotnet test
```

### Executar apenas os testes de Domínio

```bash
dotnet test tests/Faturas.Domain.Tests/
```

### Executar apenas os testes de Application

```bash
dotnet test tests/Faturas.Application.Tests/
```

### Executar com relatório de cobertura (coverlet)

```bash
dotnet test --collect:"XPlat Code Coverage"
```

O relatório será gerado em `TestResults/*/coverage.cobertura.xml`. Use o `reportgenerator` para converter em HTML:

```bash
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:Html
```

### Resultado esperado ao rodar `dotnet test`

```
Domain.Tests:
  Aprovados: 17 de 17
  Tempo: ~50ms

Application.Tests:
  Aprovados: 30 de 30
  Tempo: ~900ms

Total: 47 testes aprovados, 0 falhas
```

---

## 10. Diagrama da Estratégia de Testes

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                          ESTRATÉGIA DE TESTES                                │
│                                                                              │
│  ┌─────────────────────────────────────────────────────┐                     │
│  │              Faturas.Domain.Tests                   │                     │
│  │                                                     │                     │
│  │  ┌─────────────┐    ┌──────────────────────────┐   │                     │
│  │  │ FaturaFaker │───▶│ FaturaTests              │   │                     │
│  │  │ (Bogus)     │    │ ─────────────────────    │   │                     │
│  │  └─────────────┘    │ Criar (válido/inválido)  │   │                     │
│  │                     │ AdicionarItem (7 casos)  │   │◀── Sem mocks        │
│  │  ┌─────────────┐    │ Fechar (2 casos)         │   │    Sem infraestrutura│
│  │  │ItemFatura   │───▶│ Remover/Atualizar        │   │                     │
│  │  │Faker        │    └──────────────────────────┘   │                     │
│  │  └─────────────┘    ┌──────────────────────────┐   │                     │
│  │                     │ ItemFaturaTests           │   │                     │
│  │                     │ ─────────────────────    │   │                     │
│  │                     │ Descrição (2 casos)      │   │                     │
│  │                     │ Quantidade/Valor (2)     │   │                     │
│  │                     │ Cálculo ValorTotalItem   │   │                     │
│  │                     └──────────────────────────┘   │                     │
│  └─────────────────────────────────────────────────────┘                     │
│                                                                              │
│  ┌─────────────────────────────────────────────────────┐                     │
│  │           Faturas.Application.Tests                 │                     │
│  │                                                     │                     │
│  │  ┌─────────────┐    ┌─────────────────────────┐    │                     │
│  │  │ FaturaFaker │───▶│ Handlers Tests          │    │◀── IFaturaRepository │
│  │  │ (Bogus)     │    │ ──────────────────────  │    │    mockado com       │
│  │  └─────────────┘    │ CreateFatura (1 caso)   │    │    NSubstitute       │
│  │                     │ AddItemFatura (2 casos)  │    │                     │
│  │  ┌─────────────┐    │ FecharFatura (2 casos)  │    │                     │
│  │  │NSubstitute  │───▶│ GetFaturaById (2 casos) │    │                     │
│  │  │IFatura      │    │ ListFaturas (3 casos)   │    │                     │
│  │  │Repository   │    └─────────────────────────┘    │                     │
│  │  └─────────────┘    ┌─────────────────────────┐    │                     │
│  │                     │ Validators Tests         │    │                     │
│  │                     │ ──────────────────────   │    │                     │
│  │                     │ CreateFatura (5 casos)   │    │                     │
│  │                     │ AddItemFatura (7 casos)  │    │                     │
│  │                     │ ListFaturas (3 casos)    │    │                     │
│  │                     └─────────────────────────┘    │                     │
│  └─────────────────────────────────────────────────────┘                     │
│                                                                              │
│  Total: 47 testes  ·  0 falhas  ·  Execução < 1 segundo                    │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 11. Resumo das Decisões Técnicas

| Decisão | Alternativa | Por que escolhemos assim |
|---------|-------------|--------------------------|
| xUnit como framework | MSTest, NUnit | Padrão de mercado para .NET moderno. Melhor integração com `dotnet test` e ecossistema open source |
| FluentAssertions | `Assert.Equal(...)` nativo | Mensagens de erro mais descritivas em falhas. Sintaxe fluente mais legível e próxima de linguagem natural |
| Bogus para dados fake | Strings hardcodadas | Dados aleatórios revelam bugs que dados fixos não revelariam. Evita acoplamento acidental com valores específicos |
| NSubstitute para mocking | Moq | Sintaxe mais fluente e menos verbosa. `Received(1)` vs `Verify(..., Times.Once())` |
| AAA em todos os testes | Sem separação de fases | Força clareza estrutural. Facilita diagnóstico de falhas — se o erro está no Arrange, Act ou Assert |
| `[Theory]` para variações | Métodos duplicados | DRY (Don't Repeat Yourself). Um método, múltiplos conjuntos de dados. Fácil adicionar novas variações |
| Constantes do domínio nos asserts | Strings inline nos testes | Se a mensagem de erro mudar, os testes refletem automaticamente. Sem string "mágica" duplicada |
| Verificar chamadas ao repositório | Só verificar retorno | O retorno correto não garante que a persistência foi chamada. Um Handler pode retornar sucesso sem salvar nada |
| `FaturaFaker` replicado em App.Tests | Referência ao Domain.Tests | Projetos de teste não se referenciam entre si. Independência total das suítes de teste |
| Construtor de teste para mock + handler | Método `Setup` (MSTest) | xUnit cria nova instância por método de teste — mocks sempre limpos, sem estado residual entre testes |
