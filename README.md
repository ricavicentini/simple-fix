# Simple FIX OMS (.NET)

Implementacao minima dos requisitos:

- `Oms.Client`: recebe `NewOrderSingle` e `OrderCancelRequest` via FIX do usuario externo, encaminha para `Oms.Server`, aguarda resposta e devolve a resposta FIX.
- `Oms.Server`: processa pedidos de varios clients, valida e mantem ordens vivas em memoria.
- Snapshot global em protocolo livre (HTTP):
  - `GET http://localhost:8080/snapshot` (Client)
  - Client consulta `Server` e devolve snapshot global.

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

## Rodar dockerizado

`docker compose up --build`

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
