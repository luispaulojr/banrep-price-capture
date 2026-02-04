# HELP

Este documento descreve como configurar, executar e implantar o BanRep Price Capture.

## Configuração

A aplicação lê configurações em `appsettings.json`, arquivos de ambiente
(`appsettings.dev.json`, `appsettings.uat.json`, etc.) e variáveis de ambiente.
Em .NET, variáveis podem sobrescrever chaves usando o padrão `Section__Key`.

### Principais chaves de configuração

- **DtfDailyCapture**
  - `OutboundUrl`: URL para envio do payload diário.
  - `QueueName`: fila RabbitMQ que dispara a captura diária.
- **Database**
  - `DatabaseName`: nome do banco PostgreSQL.
  - `Port`: porta do PostgreSQL (padrão 5432).
  - `EnableSsl`: habilita SSL nas conexões.
- **DatabaseSecrets**
  - `SecretId`: segredo no AWS Secrets Manager (opcional).
  - `HostEnvVar`, `HostReadOnlyEnvVar`, `UsernameEnvVar`, `PasswordEnvVar`:
    nomes das variáveis de ambiente usadas como fallback.
- **BearerToken**
  - `TokenEnvVar`: variável de ambiente com o token Bearer do outbound.
- **RabbitMq**
  - `HostName`, `Port`, `VirtualHost`: endpoint do RabbitMQ.
  - `UserNameEnvVar`, `PasswordEnvVar`: variáveis para credenciais.
- **AWS / AWSLogging**
  - `AWS:Region`: região AWS.
  - `AWSLogging:LogGroup` e `AWSLogging:LogStreamNamePrefix`: destino de logs.

### Variáveis de ambiente relevantes

- `BANREP_DB_HOST`
- `BANREP_DB_HOST_RO`
- `BANREP_DB_USERNAME`
- `BANREP_DB_PASSWORD`
- `BANREP_BEARER_TOKEN`
- `BANREP_RABBITMQ_USERNAME`
- `BANREP_RABBITMQ_PASSWORD`

---

## AWS setup

### Secrets Manager (banco de dados)

- Se `DatabaseSecrets:SecretId` estiver configurado, a aplicação busca o segredo
  via AWS Secrets Manager.
- O segredo deve conter as chaves: `host`, `host_ro`, `username`, `password`.
- Se o segredo estiver ausente ou incompleto, a aplicação faz fallback para as
  variáveis de ambiente listadas acima.

### CloudWatch Logs

- O logging usa `AWS.Logger.AspNetCore` e envia logs ao CloudWatch.
- Garanta permissões IAM para criar/editar o log group configurado.
- Credenciais podem vir de IAM Role (ECS/EKS/EC2) ou variáveis padrão do SDK.

---

## RabbitMQ setup

- Crie a fila definida em `DtfDailyCapture:QueueName` (padrão `dtf-daily`).
- A aplicação consome mensagens com `autoAck=false` e faz `ACK/NACK` manual.
- Configure:
  - `RabbitMq:HostName`, `RabbitMq:Port`, `RabbitMq:VirtualHost`
  - `BANREP_RABBITMQ_USERNAME` / `BANREP_RABBITMQ_PASSWORD`

---

## Database setup (PostgreSQL)

A aplicação grava os registros diários na tabela `dtf_daily_prices`.
Exemplo de estrutura recomendada:

```sql
create table if not exists dtf_daily_prices (
  flow_id uuid not null,
  data_capture timestamptz not null,
  data_price date not null,
  payload jsonb not null
);

create index if not exists idx_dtf_daily_prices_flow_price
  on dtf_daily_prices (flow_id, data_price);
```

A inserção é idempotente por `(flow_id, data_price)` e usa JSONB para o payload.

---

## Local run

### Pré-requisitos

- .NET SDK 10.x
- PostgreSQL acessível localmente
- RabbitMQ em execução

### Passo a passo

1. Configure as variáveis de ambiente do banco, RabbitMQ e token Bearer.
2. Atualize `DtfDailyCapture:OutboundUrl` para o destino de envio do payload.
3. Execute o serviço:

```bash
dotnet run --project BanRepPriceCapture.ServiceLayer
```

Para habilitar Swagger localmente, use `ASPNETCORE_ENVIRONMENT=Development`.

---

## Deployment

### Docker

```bash
docker build -t banrep-price-capture:local .
docker run --rm -p 8080:8080 \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e BANREP_DB_HOST=... \
  -e BANREP_DB_HOST_RO=... \
  -e BANREP_DB_USERNAME=... \
  -e BANREP_DB_PASSWORD=... \
  -e BANREP_BEARER_TOKEN=... \
  -e BANREP_RABBITMQ_USERNAME=... \
  -e BANREP_RABBITMQ_PASSWORD=... \
  banrep-price-capture:local
```

### Kubernetes

Os manifests `kubernetes.dev.yaml`, `kubernetes.uat.yaml` e `kubernetes.prod.yaml`
contêm o deployment e o service. Ajuste a imagem e crie o Secret com as chaves:

- `db-host`
- `db-host-ro`
- `db-username`
- `db-password`
- `bearer-token`
- `rabbitmq-username`
- `rabbitmq-password`

Depois aplique:

```bash
kubectl apply -f kubernetes.dev.yaml
```

O health check está disponível em `/health`.
