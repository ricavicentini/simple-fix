# Simple FIX OMS (.NET)

Implementacao minima de um OMS com comunicacao FIX e snapshot HTTP.
>  This is a challenge by [Coodesh](https://coodesh.com/)

## Visao geral

- `Oms.Client`: recebe `NewOrderSingle` e `OrderCancelRequest` via FIX do usuario externo, encaminha para `Oms.Server` e devolve a resposta FIX.
- `Oms.Server`: valida e processa ordens de multiplos clients, mantendo ordens vivas em memoria.
- Snapshot global (protocolo livre/HTTP):
  - `GET http://localhost:8080/snapshot` (exposto pelo Client)
  - o Client consulta o Server e retorna o snapshot global.

## Stack tecnica

- .NET: `10.0` (`net10.0`)
- C#: `12+` (SDK-style project com `ImplicitUsings` e `Nullable`)

Principais bibliotecas:

- `QuickFIXn.Core` (`1.14.0`): engine FIX (sessao, transporte, parser/protocolo)
- `QuickFIXn.FIX44` (`1.14.0`): mensagens/fields tipados FIX 4.4
- `Microsoft.AspNetCore` (via SDK Web): Minimal APIs, DI, hosting e logging
- `xUnit` (projeto `Oms.Tests`): testes unitarios

## Arquitetura (resumo)

- `Oms.Shared`: regras de dominio e contratos de aplicacao.
- `Oms.Server`: API HTTP + gateway FIX do lado servidor.
- `Oms.Client`: bridge FIX entre externo e servidor OMS.
- `Oms.Tests`: testes unitarios de regra de negocio.
- `Oms.Benchmark`: medicao de latencia round-trip.

### Decisoes de design

- Uma classe por arquivo.
  Pattern sustentado: Single Responsibility Principle (SRP) / separacao por responsabilidade.
- Dependencia por interfaces (`IOrderBook`, `IOrderValidator`) para reduzir acoplamento.
  Pattern sustentado: Dependency Inversion Principle (DIP) e Strategy (troca de implementacao sem afetar consumidores).
- `Program.cs` como composition root (bootstrap/DI/endpoints).
  Pattern sustentado: Composition Root / Dependency Injection.
- Infraestrutura FIX isolada em `Infrastructure/Fix`.
  Pattern sustentado: Ports and Adapters (Hexagonal) / separacao de camadas.
- Thread-safety no order book com `ConcurrentDictionary` + `Interlocked.Increment`.
  Pattern sustentado: Shared-nothing parcial + sincronizacao lock-free para estado concorrente.

### Trade-offs

- Estado do order book em memoria (foco em simplicidade e baixa latencia).
- Sem persistencia nesta versao.

### Evolucao futura

- Event Store para durabilidade + replay de estado.
- Persistencia assincrona append-only para minimizar impacto no caminho critico.

## Regras implementadas

- Simbolo: `PETR4` ou `VALE3`
- Lado: `1=Buy` ou `2=Sell`
- Quantidade: inteiro positivo `< 100000`
- Preco: decimal positivo `< 1000` com 2 casas
- Cancelamento: remove por `OrigClOrdID` (ou `ClOrdID` quando `OrigClOrdID` nao vier)
- Snapshot ordenado por simbolo/lado, preco crescente e prioridade temporal

## Como rodar

### Local

1. `dotnet build SimpleFixOms.slnx`
2. Terminal 1: `dotnet run --project Oms.Server`
3. Terminal 2: `dotnet run --project Oms.Client`
4. Snapshot: `curl http://localhost:8080/snapshot`

### Docker (modo padrao)

Use para subir ambiente normal da aplicacao:

- `docker compose up --build`

### Docker (perfil de benchmark)

Use quando o objetivo for medir latencia com menos ruido de ambiente.

Racional do perfil:

- reduz overhead de I/O de log FIX (`FIX_DISABLE_FILE_LOG=1` -> `NullLogFactory`)
- usa `tmpfs` para `/app/log`
- ativa ajustes de runtime (`DOTNET_TieredPGO=1`, `DOTNET_gcServer=1`)
- roda benchmark em servico one-shot na mesma rede Docker (evita NAT host -> container)

Subir stack otimizada:

- `docker compose -f docker-compose.yml -f docker-compose.benchmark.yml up --build -d`

Executar benchmark one-shot:

Linux/macOS:

- `COMPOSE_PROFILES=benchmark docker compose -f docker-compose.yml -f docker-compose.benchmark.yml run --rm benchmark`

PowerShell (Windows):

- `$env:COMPOSE_PROFILES="benchmark"; docker compose -f docker-compose.yml -f docker-compose.benchmark.yml run --rm benchmark`

## Logging

- `Oms.Server` usa `ILogger` com output em console.
- Logs de sessao FIX podem ser em arquivo (modo padrao) ou desabilitados via `FIX_DISABLE_FILE_LOG=1`.

## Testes unitarios

- Projeto: `Oms.Tests`
- Executar: `dotnet test Oms.Tests/Oms.Tests.csproj`

Cobertura principal:

- validacao dos campos de nova ordem
- rejeicao de `ClOrdID` duplicado
- cancelamento (sucesso e ordem inexistente)
- snapshot apenas com ordens vivas
- ordenacao do snapshot

## Benchmark de latencia (100k sequencial)

Projeto: `Oms.Benchmark`

Execucao local:

1. subir `Oms.Server` e `Oms.Client`
2. executar `dotnet run --project Oms.Benchmark`

### Resultados medidos

- Local (host): `Avg round-trip: 0,3791 ms`
- Dockerizado (benchmark local via `docker compose`): `Avg round-trip: 1,2688 ms`
- Dockerizado (one-shot em rede interna Docker): `Avg round-trip: 0.8204 ms`
