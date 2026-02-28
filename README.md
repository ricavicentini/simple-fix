# Simple FIX OMS (.NET)

Implementacao minima dos requisitos:

- `Oms.Client`: recebe `NewOrderSingle` e `OrderCancelRequest` via FIX do usuario externo, encaminha para `Oms.Server`, aguarda resposta e devolve a resposta FIX.
- `Oms.Server`: processa pedidos de varios clients, valida e mantem ordens vivas em memoria.
- Snapshot global em protocolo livre (HTTP):
  - `GET http://localhost:8080/snapshot` (Client)
  - Client consulta `Server` e devolve snapshot global.

## Arquitetura e racional

### Visao geral

O projeto foi estruturado para separar responsabilidades por camada e reduzir acoplamento:

- `Oms.Shared`: regras de dominio e contratos de aplicacao reutilizaveis.
- `Oms.Server`: API HTTP + gateway FIX para processamento de ordens.
- `Oms.Client`: bridge FIX entre usuario externo e servidor OMS.
- `Oms.Tests`: testes de regras de negocio.
- `Oms.Benchmark`: medicao simples de latencia round-trip.

### Estrutura por camadas

No `Oms.Shared`:

- `Domain`:
  - `Enums/OrderSide.cs`
  - `Entities/LiveOrder.cs`
- `Application`:
  - `Contracts/IOrderBook.cs`
  - `Contracts/IOrderValidator.cs`
  - `Services/OrderBook.cs`
  - `Services/DefaultOrderValidator.cs`
  - `Models/SnapshotOrder.cs`

No `Oms.Server`:

- `Api`:
  - `DependencyInjection/ServiceCollectionExtensions.cs`
  - `Endpoints/OrderEndpoints.cs`
- `Infrastructure`:
  - `Fix/FixServerGateway.cs`
- `Program.cs`:
  - apenas composition root (bootstrap, logging, DI, endpoints).

### Decisoes de design

- Uma classe por arquivo:
  - facilita manutencao, navegacao e revisao de codigo.
- Dependencia por interface:
  - `IOrderBook` e `IOrderValidator` desacoplam consumidores das implementacoes concretas.
  - simplifica troca de estrategia (ex.: outro validador, persistencia futura) sem quebrar chamadas.
- Composition root explicito:
  - regras de injecao em um unico ponto (`AddOmsServer`), evitando configuracao espalhada.
- Infraestrutura isolada:
  - classes QuickFIXn ficaram em `Infrastructure/Fix`, separadas de regras de negocio.
- Thread-safety no book em memoria:
  - `ConcurrentDictionary` para estado compartilhado.
  - `Interlocked.Increment` para sequenciamento atomico e prioridade temporal consistente.
- Snapshot deterministico:
  - ordenacao por simbolo/lado, preco e sequencia de entrada.

### Trade-offs assumidos

- Estado em memoria (`OrderBook`) para simplicidade e baixa latencia local.
- Sem persistencia em banco nesta versao.
- Validacao e matching simplificados para atender os requisitos funcionais atuais.

### Evolucao futura

- Pode ser adotado Event Store para garantir durabilidade e reconstruir o estado do order book via replay de eventos.
- Para minimizar impacto de performance, a persistencia pode ser feita de forma assincrona (append-only), mantendo o caminho critico de processamento em memoria.

## Regras implementadas

- Simbolo: `PETR4` ou `VALE3`
- Lado: `1=Buy` ou `2=Sell`
- Quantidade: inteiro positivo `< 100000`
- Preco: decimal positivo `< 1000` com 2 casas
- Cancelamento: remove ordem existente por `OrigClOrdID` (ou `ClOrdID` quando `OrigClOrdID` nao vier)
- Snapshot ordenado:
  - agrupamento por simbolo e lado
  - ordem crescente de preco
  - empate por prioridade temporal

## Rodar local

1. `dotnet build SimpleFixOms.slnx`
2. Terminal 1: `dotnet run --project Oms.Server`
3. Terminal 2: `dotnet run --project Oms.Client`
4. Snapshot: `curl http://localhost:8080/snapshot`

## Logging

- `Oms.Server` usa `ILogger` com provider de console habilitado.
- Eventos de sessao FIX continuam em arquivo via `FileLogFactory` (pasta `log` configurada no cfg).

## Rodar dockerizado

`docker compose up --build`

### Perfil de benchmark (docker)

Para reduzir overhead durante benchmark, existe um override:

- `docker-compose.benchmark.yml`

Ajustes aplicados nesse perfil:

- CPU por container: `2.0`
- `DOTNET_TieredPGO=1`
- `DOTNET_gcServer=1`
- pasta de log FIX em `tmpfs` (`/app/log`) para reduzir I/O em disco

Subir com perfil de benchmark:

`docker compose -f docker-compose.yml -f docker-compose.benchmark.yml up --build -d`

## Testes unitarios

Projeto: `Oms.Tests`

Cobertura dos requisitos:

- validacao dos campos de nova ordem (simbolo, lado, quantidade, preco)
- rejeicao por `ClOrdID` duplicado
- cancelamento com sucesso e cancelamento de ordem inexistente
- snapshot apenas com ordens vivas
- ordenacao do snapshot por simbolo/lado, preco crescente e prioridade temporal

Executar:

`dotnet test Oms.Tests/Oms.Tests.csproj`

## Benchmark de latencia (100k sequencial)

Projeto: `Oms.Benchmark`

Passos:

1. subir `Oms.Server` e `Oms.Client`
2. executar `dotnet run --project Oms.Benchmark`

Saida esperada: total e media de round-trip em ms.

### Resultados medidos

- Local (host): `Avg round-trip: 0,3791 ms`
- Dockerizado (`docker compose`): `Avg round-trip: 1,2688 ms`
