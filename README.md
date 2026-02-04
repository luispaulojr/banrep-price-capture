# BanRep Price Capture

## Visão geral

O **BanRep Price Capture** captura, processa e expõe a taxa DTF publicada pelo
Banco de la República da Colômbia por meio do SDMX. A aplicação consome o endpoint
oficial, interpreta o XML retornado e disponibiliza os dados em dois formatos:

- Série diária da DTF
- Série semanal da DTF (última observação de cada semana)

Além da API HTTP, o serviço possui um fluxo de captura diária acionado por fila
RabbitMQ que persiste os dados em banco e envia o payload para um endpoint externo.

---

## Arquitetura (visão geral)

O projeto segue uma arquitetura em camadas, com responsabilidades bem separadas:

- **Service Layer**: API HTTP, health checks, middleware e composição de dependências.
- **Application Layer**: workflows e jobs que orquestram as operações de negócio.
- **Domain Layer**: modelos de domínio compartilhados.
- **Infrastructure Layer**: integrações externas (SDMX, RabbitMQ, PostgreSQL,
  outbound HTTP, AWS Secrets Manager e logging).

Integrações principais:

- **SDMX (BanRep)**: fonte oficial dos dados da DTF.
- **PostgreSQL**: persistência dos registros diários.
- **RabbitMQ**: dispara a captura diária por mensagem.
- **Outbound HTTP**: entrega do payload diário a um endpoint configurado.
- **AWS**: Secrets Manager para credenciais de banco e CloudWatch Logs para logging.

---

## Fluxos de execução

### 1) API HTTP (/dtf-daily e /dtf-weekly)

1. Requisição chega ao endpoint HTTP.
2. O workflow `DtfSeriesWorkflow` executa o job correspondente:
   - `DtfDailyJob` para série diária
   - `DtfWeeklyJob` para série semanal
3. O job consulta o SDMX via `BanRepSdmxClient`.
4. A resposta é transformada em JSON e devolvida ao cliente.

### 2) Captura diária via RabbitMQ

1. Uma mensagem é publicada na fila configurada (`DtfDailyCapture.QueueName`).
2. O consumidor `DtfDailyRabbitConsumer` inicia o fluxo de captura.
3. O workflow `DtfDailyCaptureWorkflow`:
   - Consulta o SDMX para obter a série diária.
   - Persiste cada registro no PostgreSQL.
   - Envia o payload para o endpoint externo configurado.
4. Em falha crítica, uma notificação é disparada e a mensagem é reencaminhada.

---

## Fonte dos dados

Os dados são obtidos via SDMX do Banco de la República. Cada `<generic:Obs>`
representa uma observação diária, que é convertida para JSON sem alterar a
granularidade original.

---

## Endpoints disponíveis

### GET /dtf-daily

Retorna a série diária da taxa DTF. Cada dia presente no SDMX é representado
individualmente no JSON, sem agregações adicionais.

#### Exemplo de resposta

```json
{
  "series": "DTF 90 dias (diária)",
  "data": [
    {
      "date": "2026-02-02",
      "value": 9.15
    },
    {
      "date": "2026-02-03",
      "value": 9.15
    }
  ]
}
```

---

### GET /dtf-weekly

Retorna a série semanal da taxa DTF, utilizando apenas a última observação de
cada semana.

#### Exemplo de resposta

```json
{
  "series": "DTF 90 dias (semanal)",
  "data": [
    {
      "date": "2026-02-08",
      "value": 9.15
    }
  ]
}
```

---

## Observações importantes

- Os endpoints `/dtf-daily` e `/dtf-weekly` possuem fluxos separados.
- Nenhuma regra de negócio altera a granularidade original dos dados.
- A aplicação não realiza cálculos de vigência, apenas representação fiel.

---

## Objetivo do projeto

Fornecer uma interface simples, confiável e fiel aos dados oficiais do BanRep,
permitindo o consumo da taxa DTF tanto em formato diário quanto semanal.
