# Documentação Técnica — Camada de Migrations (`Faturas.Infrastructure.Migrations`)

> **Objetivo deste documento:** explicar em profundidade cada decisão conceitual e técnica tomada na construção da camada de migrations do sistema de Gestão de Faturas. Serve como referência para qualquer desenvolvedor que precise entender como o banco de dados é criado, versionado e evoluído ao longo do tempo.

---

## 1. O Problema que as Migrations Resolvem

Imagine que você tem um sistema rodando em produção com dados reais. Surge a necessidade de adicionar uma coluna nova, criar um índice, ou renomear uma tabela. Como você faz isso **de forma controlada, rastreável e segura**?

Sem migrations, as opções são ruins:
- Rodar SQL manualmente no servidor → sem histórico, sem repetibilidade, propenso a erro humano
- Recriar o banco do zero → perde todos os dados existentes
- Cada desenvolvedor criando o banco "na mão" → ambientes inconsistentes entre dev, homologação e produção

**Migrations resolvem isso:** são scripts SQL versionados e numerados que são aplicados **sequencialmente e uma única vez** em cada ambiente. O sistema rastreia o que já foi aplicado e aplica apenas o que está faltando.

---

## 2. Por que DbUp e não EF Core Migrations?

O Entity Framework Core tem seu próprio sistema de migrations. Então por que escolhemos o **DbUp**?

| Critério | EF Core Migrations | DbUp ✅ |
|----------|--------------------|---------|
| **Formato dos scripts** | C# gerado automaticamente | SQL puro |
| **Controle do SQL** | Indireto (EF gera o SQL) | Total (você escreve o SQL) |
| **Legibilidade** | Código C# abstrato | SQL que qualquer DBA lê |
| **Revisão de código** | Difícil de revisar | Fácil de revisar no PR |
| **Portabilidade** | Acoplado ao EF Core | Independente de ORM |
| **Idempotência** | Controlada pelo EF | Controlada por você (`IF NOT EXISTS`) |
| **DBA-friendly** | Não | Sim |

**Decisão:** Em sistemas com arquitetura Clean Architecture + DDD, o banco de dados é um detalhe de infraestrutura. Usar SQL puro nos dá controle total sobre o que é executado, facilita revisões de código e permite que um DBA audite as mudanças sem precisar entender C#.

> **Importante:** O EF Core ainda é usado para **leitura e escrita** (queries e saves). O DbUp cuida apenas da **estrutura** (criação de tabelas, índices, etc.). São ferramentas complementares, não concorrentes.

---

## 3. Estrutura do Projeto

```
Faturas.Infrastructure.Migrations/
├── Scripts/
│   ├── 0001_create_schema.sql          ← Garante existência do schema
│   ├── 0002_create_table_faturas.sql   ← Cria a tabela principal
│   ├── 0003_create_table_itens_fatura.sql ← Cria a tabela de itens
│   └── 0004_indexes_faturas.sql        ← Cria os índices de performance
├── Program.cs                          ← Runner que executa o DbUp
└── Faturas.Infrastructure.Migrations.csproj
```

**Por que projeto separado?** Separar as migrations em um projeto console próprio permite:
- Rodar migrations independentemente da API (sem precisar subir a aplicação inteira)
- CI/CD pode executar migrations como etapa separada do deploy
- A API não precisa saber nada sobre DbUp — responsabilidade única

---

## 4. A Convenção de Nomenclatura dos Scripts

```
0001_create_schema.sql
0002_create_table_faturas.sql
0003_create_table_itens_fatura.sql
0004_indexes_faturas.sql
```

**O prefixo numérico (`0001_`, `0002_`...)** é o que define a **ordem de execução**. O DbUp ordena os scripts alfabeticamente pelo nome do recurso embutido. Com zeros à esquerda (`0001` em vez de `1`), a ordem se mantém correta mesmo com centenas de scripts.

**A parte descritiva** (`create_table_faturas`) deixa claro **o que o script faz** sem precisar abri-lo. Um histórico de scripts bem nomeados conta a história da evolução do banco de dados.

**Regra de ouro:** uma vez que um script é executado em produção, ele **nunca é modificado**. Se precisar alterar algo, cria-se um novo script com o próximo número. Isso garante que o histórico seja imutável e auditável.

---

## 5. Os Scripts SQL em Detalhe

### 5.1 `0001_create_schema.sql` — Garantindo o Schema

```sql
CREATE SCHEMA IF NOT EXISTS public;
```

**O que é um schema no PostgreSQL?** Um schema é um namespace dentro do banco de dados. O `public` é o schema padrão do PostgreSQL — todas as tabelas criadas sem especificar schema vão para `public`.

**Por que esse script existe se o schema já existe por padrão?** Por **idempotência** e **documentação**. Idempotente significa que você pode executar o script quantas vezes quiser e o resultado será sempre o mesmo. `IF NOT EXISTS` garante que não haverá erro se o schema já existir.

Além disso, documenta explicitamente que o projeto usa o schema `public`. Se no futuro quisermos migrar para um schema personalizado (ex: `faturas_app`), a mudança seria feita aqui.

---

### 5.2 `0002_create_table_faturas.sql` — A Tabela Principal

```sql
CREATE TABLE IF NOT EXISTS faturas (
    id              UUID            NOT NULL DEFAULT gen_random_uuid(),
    numero          VARCHAR(20)     NOT NULL,
    nome_cliente    VARCHAR(150)    NOT NULL,
    data_emissao    TIMESTAMPTZ     NOT NULL,
    status          INTEGER         NOT NULL DEFAULT 0,
    valor_total     NUMERIC(18, 2)  NOT NULL DEFAULT 0,

    CONSTRAINT pk_faturas PRIMARY KEY (id),
    CONSTRAINT uq_faturas_numero UNIQUE (numero)
);
```

**Decisão por coluna:**

| Coluna | Tipo | Por que esse tipo |
|--------|------|-------------------|
| `id` | `UUID` | Identificador único universal; sem dependência de sequência do banco; distribuído |
| `numero` | `VARCHAR(20)` | Formato `FAT-000001` tem 10 chars; 20 dá margem para variações futuras |
| `nome_cliente` | `VARCHAR(150)` | Limite definido no domínio (`MaximumLength(150)` no validator) |
| `data_emissao` | `TIMESTAMPTZ` | Armazena data **com fuso horário** — correto para sistemas que podem ter usuários em regiões diferentes; o domínio salva em UTC |
| `status` | `INTEGER` | O enum `StatusFatura` do C# é serializado como inteiro: `0 = Aberta`, `1 = Fechada` |
| `valor_total` | `NUMERIC(18, 2)` | Precisão decimal exata para valores monetários; 18 dígitos com 2 casas decimais |

**Por que `UUID` e não `SERIAL` (auto-increment)?**
- `SERIAL` exige consulta ao banco para gerar o ID → acoplamento
- `UUID` é gerado pela aplicação (via `Guid.NewGuid()`) ou pelo banco (`gen_random_uuid()`) → sem acoplamento
- `UUID` facilita replicação e merge de dados entre ambientes

**Por que `gen_random_uuid()` como DEFAULT?** Para o caso de inserções feitas diretamente no banco (scripts de seed, imports), o banco gera o UUID automaticamente.

**`CONSTRAINT pk_faturas`** — nome explícito na constraint facilita mensagens de erro e gerenciamento futuro.

**`CONSTRAINT uq_faturas_numero`** — garante que dois registros nunca terão o mesmo número de fatura. Essa é a RN implícita de unicidade de número.

---

### 5.3 `0003_create_table_itens_fatura.sql` — A Tabela de Itens

```sql
CREATE TABLE IF NOT EXISTS itens_fatura (
    id                  UUID            NOT NULL DEFAULT gen_random_uuid(),
    fatura_id           UUID            NOT NULL,
    descricao           VARCHAR(500)    NOT NULL,
    quantidade          INTEGER         NOT NULL,
    valor_unitario      NUMERIC(18, 2)  NOT NULL,
    valor_total_item    NUMERIC(18, 2)  NOT NULL,
    justificativa       TEXT            NULL,

    CONSTRAINT pk_itens_fatura PRIMARY KEY (id),
    CONSTRAINT fk_itens_fatura_fatura
        FOREIGN KEY (fatura_id)
        REFERENCES faturas (id)
        ON DELETE CASCADE
);
```

**Decisão por coluna:**

| Coluna | Tipo | Por que esse tipo |
|--------|------|-------------------|
| `fatura_id` | `UUID NOT NULL` | Toda item pertence a exatamente uma fatura — nunca nulo |
| `descricao` | `VARCHAR(500)` | Limite generoso para descrição de produto/serviço |
| `quantidade` | `INTEGER` | Sem casas decimais — unidades inteiras |
| `valor_unitario` | `NUMERIC(18, 2)` | Valor monetário preciso |
| `valor_total_item` | `NUMERIC(18, 2)` | **Calculado e armazenado** (quantidade × valor_unitario) — desnormalização intencional para performance |
| `justificativa` | `TEXT NULL` | Nullable — só obrigatória quando total > R$1.000 (RN-7); `TEXT` sem limite pois é um campo descritivo livre |

**Por que armazenar `valor_total_item` se é calculado?** Evita recalcular a cada query. Também garante consistência histórica — se a fórmula de cálculo mudar no futuro, registros antigos não são afetados.

**`ON DELETE CASCADE`** — se uma `Fatura` for deletada do banco, todos os seus `itens_fatura` são deletados automaticamente. Isso mantém a integridade referencial sem precisar deletar itens manualmente antes da fatura.

> **Nota de design:** no modelo de domínio, `ItemFatura` é sempre acessado **através** de `Fatura` (regra do Aggregate). O `ON DELETE CASCADE` reflete essa relação no banco — item não existe sem fatura.

---

### 5.4 `0004_indexes_faturas.sql` — Índices de Performance

```sql
CREATE INDEX IF NOT EXISTS ix_faturas_nome_cliente  ON faturas (nome_cliente);
CREATE INDEX IF NOT EXISTS ix_faturas_data_emissao  ON faturas (data_emissao);
CREATE INDEX IF NOT EXISTS ix_faturas_status        ON faturas (status);
CREATE INDEX IF NOT EXISTS ix_itens_fatura_fatura_id ON itens_fatura (fatura_id);
```

**O que é um índice?** É uma estrutura de dados auxiliar que o banco mantém para acelerar buscas. Sem índice, uma query de filtro por `nome_cliente` precisa varrer **todas as linhas** da tabela (full table scan). Com índice, o banco vai diretamente às linhas que interessam.

**Por que índice separado no script `0004`?** Porque índices têm custo: aceleram leituras mas desaceleram escritas (o banco precisa atualizar o índice a cada INSERT/UPDATE). Mantê-los em script separado facilita análise e eventual remoção se o custo não compensar.

**Mapeamento das RNs:**

| Índice | Coluna | Filtro da RN-10 que acelera |
|--------|--------|-----------------------------|
| `ix_faturas_nome_cliente` | `nome_cliente` | `?cliente=João` |
| `ix_faturas_data_emissao` | `data_emissao` | `?dataInicial=...&dataFinal=...` |
| `ix_faturas_status` | `status` | `?status=Aberta` |
| `ix_itens_fatura_fatura_id` | `fatura_id` | JOINs entre faturas e itens |

**Por que o índice em `fatura_id` nos itens?** Toda vez que o EF Core carrega uma fatura com seus itens (`Include(f => f.Itens)`), o banco executa um JOIN ou subquery usando `fatura_id`. Sem índice, esse JOIN é lento em tabelas com milhares de itens.

---

## 6. O Runner (`Program.cs`)

```csharp
var connectionString =
    args.FirstOrDefault()
    ?? Environment.GetEnvironmentVariable("POSTGRES_CONN")
    ?? throw new InvalidOperationException("Connection string não informada.");

EnsureDatabase.For.PostgresqlDatabase(connectionString);

var upgrader = DeployChanges.To
    .PostgresqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
    .LogToConsole()
    .Build();

var result = upgrader.PerformUpgrade();
```

**Linha por linha:**

**`args.FirstOrDefault() ?? Environment.GetEnvironmentVariable("POSTGRES_CONN") ?? throw`**

A connection string pode ser passada de duas formas:
1. Como argumento de linha de comando: `dotnet run -- "Host=localhost;..."`
2. Como variável de ambiente: `$env:POSTGRES_CONN = "Host=localhost;..."`

Se nenhuma das duas for fornecida, o programa falha imediatamente com mensagem clara. Nunca silencia o erro nem usa um valor padrão hardcoded — connection string com credenciais não pertence ao código.

**`EnsureDatabase.For.PostgresqlDatabase(connectionString)`**

Verifica se o banco de dados existe. Se não existir, **cria automaticamente**. Isso elimina a necessidade de criar o banco manualmente antes de rodar as migrations — útil especialmente para ambientes de CI/CD ou onboarding de novos devs.

**`WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())`**

Instrui o DbUp a buscar os scripts SQL **dentro do próprio assembly compilado** (o `.dll` gerado). Não depende de arquivos no disco — os scripts viajam embutidos no binário. Isso é possível porque marcamos os scripts como `EmbeddedResource` no `.csproj`.

**`.LogToConsole()`**

Exibe no terminal cada script que está sendo executado, em tempo real. Essencial para diagnóstico e confirmação visual do que foi aplicado.

**`upgrader.PerformUpgrade()`**

Aqui é onde a magia acontece:
1. O DbUp consulta a tabela `schemaversions` (criada automaticamente na primeira execução)
2. Compara com a lista de scripts embutidos no assembly
3. Executa apenas os scripts que ainda não constam em `schemaversions`
4. Registra cada script executado em `schemaversions` com timestamp

---

## 7. O Conceito de `EmbeddedResource`

```xml
<ItemGroup>
    <EmbeddedResource Include="Scripts\*.sql" />
</ItemGroup>
```

Por padrão, arquivos `.sql` são ignorados pelo compilador — apenas código C# é compilado. Ao marcar como `EmbeddedResource`, o MSBuild **embute o conteúdo do arquivo dentro do `.dll`** gerado.

**Por que embutir em vez de copiar como arquivo?**

| Arquivo no disco | EmbeddedResource ✅ |
|------------------|---------------------|
| Pode ser alterado/deletado acidentalmente | Imutável dentro do binário |
| Precisa gerenciar caminhos de arquivo | Acessado pelo nome do recurso |
| Pode ficar fora de sincronia com o código | Sempre sincronizado |
| Não funciona em ambientes containerizados sem montar volumes | Funciona em qualquer ambiente |

O nome do recurso embutido segue o padrão:
```
{Namespace}.{Pasta}.{NomeDoArquivo}
Faturas.Infrastructure.Migrations.Scripts.0001_create_schema.sql
```

O DbUp usa esse nome para ordenar e executar os scripts.

---

## 8. A Tabela `schemaversions` (Controle de Versão)

Ao rodar pela primeira vez, o DbUp cria automaticamente uma tabela de controle:

```sql
-- Criada automaticamente pelo DbUp
CREATE TABLE schemaversions (
    schemaversionid  SERIAL       PRIMARY KEY,
    scriptname       VARCHAR(255) NOT NULL,
    applied          TIMESTAMP    NOT NULL
);
```

Cada script executado gera um registro:

| schemaversionid | scriptname | applied |
|-----------------|------------|---------|
| 1 | `Faturas.Infrastructure.Migrations.Scripts.0001_create_schema.sql` | 2026-05-05 14:00:00 |
| 2 | `Faturas.Infrastructure.Migrations.Scripts.0002_create_table_faturas.sql` | 2026-05-05 14:00:01 |
| 3 | `Faturas.Infrastructure.Migrations.Scripts.0003_create_table_itens_fatura.sql` | 2026-05-05 14:00:01 |
| 4 | `Faturas.Infrastructure.Migrations.Scripts.0004_indexes_faturas.sql` | 2026-05-05 14:00:02 |

**Na segunda execução**, o DbUp vê que todos os scripts já estão em `schemaversions` e não executa nada. Isso torna as migrations **idempotentes**: seguras para executar múltiplas vezes.

**Em um novo ambiente** (novo dev, CI/CD, homologação), os 4 scripts são executados do zero na ordem correta.

---

## 9. Como Adicionar uma Nova Migration

Quando precisar evoluir o banco (nova coluna, nova tabela, novo índice):

1. Crie um novo arquivo em `Scripts/` com o próximo número sequencial:
```
Scripts/0005_add_coluna_observacoes_faturas.sql
```

2. Escreva o SQL idempotente:
```sql
ALTER TABLE faturas ADD COLUMN IF NOT EXISTS observacoes TEXT NULL;
```

3. O arquivo é automaticamente incluído como `EmbeddedResource` pelo glob `Scripts\*.sql`

4. Na próxima execução do runner, apenas esse novo script será aplicado nos ambientes que ainda não o têm

**Nunca modifique um script já executado.** Se precisar corrigir algo, crie um novo script que desfaz e refaz. Isso mantém o histórico intacto e garante que todos os ambientes passem pelas mesmas transformações na mesma ordem.

---

## 10. Fluxo de Execução Completo

```
dotnet run --project src/Faturas.Infrastructure.Migrations -- "Host=..."
                │
                ▼
    Lê connection string (args ou env var)
                │
                ▼
    EnsureDatabase: banco existe? Não → cria. Sim → continua.
                │
                ▼
    DbUp carrega scripts do assembly (ordem alfabética pelo nome do recurso)
                │
                ▼
    Consulta tabela schemaversions: quais scripts já foram aplicados?
                │
                ▼
    Para cada script pendente (em ordem):
    ┌─────────────────────────────────────────┐
    │  Abre transação                         │
    │  Executa o SQL do script                │
    │  Registra em schemaversions             │
    │  Commit                                 │
    └─────────────────────────────────────────┘
                │
                ▼
    Todos aplicados → "Migrações aplicadas com sucesso." → exit 0
    Qualquer erro   → "Migração falhou: [detalhe]"      → exit -1
```

**Cada script roda dentro de uma transação.** Se o script falhar no meio, a transação é revertida (rollback automático) e o erro é reportado. O banco fica no estado anterior ao script com falha — sem dados corrompidos.

---

## 11. Comandos de Referência

### Subir o PostgreSQL no Docker
```powershell
docker run --name postgres-faturas `
  -e POSTGRES_USER=postgres `
  -e POSTGRES_PASSWORD=postgres `
  -e POSTGRES_DB=faturas `
  -p 5432:5432 `
  -d postgres:16
```

### Rodar as migrations
```powershell
dotnet run --project src/Faturas.Infrastructure.Migrations `
  -- "Host=localhost;Port=5432;Database=faturas;Username=postgres;Password=postgres"
```

### Via variável de ambiente
```powershell
$env:POSTGRES_CONN = "Host=localhost;Port=5432;Database=faturas;Username=postgres;Password=postgres"
dotnet run --project src/Faturas.Infrastructure.Migrations
```

### Verificar tabelas criadas
```powershell
docker exec -it postgres-faturas psql -U postgres -d faturas -c "\dt"
```

### Verificar scripts aplicados
```powershell
docker exec -it postgres-faturas psql -U postgres -d faturas `
  -c "SELECT scriptname, applied FROM schemaversions ORDER BY schemaversionid;"
```

### URL de conexão para ferramentas visuais (DBeaver, pgAdmin, TablePlus)
```
postgresql://postgres:postgres@localhost:5432/faturas
```

---

## 12. Diagrama da Estrutura no Banco

```
┌─────────────────────────────────────────────────────────────────┐
│                     BANCO: faturas                              │
│                                                                 │
│  ┌──────────────────────────────────────┐                       │
│  │            faturas                   │                       │
│  │  ────────────────────────────────    │                       │
│  │  id            UUID (PK)             │                       │
│  │  numero        VARCHAR(20) (UNIQUE)  │                       │
│  │  nome_cliente  VARCHAR(150)          │                       │
│  │  data_emissao  TIMESTAMPTZ           │                       │
│  │  status        INTEGER (0/1)         │                       │
│  │  valor_total   NUMERIC(18,2)         │                       │
│  └───────────────────┬──────────────────┘                       │
│                      │ 1                                        │
│                      │                                          │
│                      │ *                                        │
│  ┌───────────────────▼──────────────────┐                       │
│  │            itens_fatura              │                       │
│  │  ────────────────────────────────    │                       │
│  │  id               UUID (PK)          │                       │
│  │  fatura_id        UUID (FK)          │                       │
│  │  descricao        VARCHAR(500)       │                       │
│  │  quantidade       INTEGER            │                       │
│  │  valor_unitario   NUMERIC(18,2)      │                       │
│  │  valor_total_item NUMERIC(18,2)      │                       │
│  │  justificativa    TEXT (nullable)    │                       │
│  └──────────────────────────────────────┘                       │
│                                                                 │
│  ┌──────────────────────────────────────┐                       │
│  │          schemaversions              │  ← Controle DbUp      │
│  │  ────────────────────────────────    │                       │
│  │  schemaversionid  SERIAL (PK)        │                       │
│  │  scriptname       VARCHAR(255)       │                       │
│  │  applied          TIMESTAMP          │                       │
│  └──────────────────────────────────────┘                       │
│                                                                 │
│  Índices:                                                       │
│  ix_faturas_nome_cliente   → faturas(nome_cliente)             │
│  ix_faturas_data_emissao   → faturas(data_emissao)             │
│  ix_faturas_status         → faturas(status)                   │
│  ix_itens_fatura_fatura_id → itens_fatura(fatura_id)           │
└─────────────────────────────────────────────────────────────────┘
```

---

## 13. Resumo das Decisões Técnicas

| Decisão | Alternativa | Por que escolhemos assim |
|---------|-------------|--------------------------|
| DbUp com SQL puro | EF Core Migrations | Controle total do SQL, legível por DBA, independente de ORM |
| Scripts como `EmbeddedResource` | Arquivos no disco | Binário autossuficiente, sem dependência de path, funciona em containers |
| Numeração com zeros à esquerda (`0001_`) | Sem prefixo numérico | Garante ordem de execução correta com centenas de scripts |
| `IF NOT EXISTS` em todos os DDLs | DDL simples | Idempotência — seguro de executar múltiplas vezes |
| `UUID` como PK | `SERIAL` (auto-increment) | Sem dependência do banco para gerar ID, distribuído |
| `TIMESTAMPTZ` para datas | `TIMESTAMP` | Armazena fuso horário — correto para sistemas com UTC |
| `NUMERIC(18,2)` para valores | `FLOAT` / `REAL` | Precisão exata para cálculos financeiros |
| `ON DELETE CASCADE` nos itens | Sem cascade | Integridade referencial automática, reflete relação do agregado |
| Projeto console separado | Migration na API | Responsabilidade única; CI/CD pode rodar independentemente |
| Connection string via arg ou env var | Hardcoded | Segurança — credenciais nunca no código-fonte |
