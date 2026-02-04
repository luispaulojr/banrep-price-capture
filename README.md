# BanRep DTF Price Capture

## Visão geral

Esta aplicação tem como objetivo capturar, processar e expor os dados da taxa DTF
publicados pelo Banco de la República da Colômbia, utilizando a integração via SDMX.

A aplicação consome o endpoint SDMX oficial do BanRep, interpreta o XML retornado
e disponibiliza os dados por meio de endpoints HTTP em dois formatos distintos:

- Série diária da DTF
- Série semanal da DTF, considerando apenas a última observação de cada semana

Toda a lógica de negócio foi construída para preservar fielmente a granularidade
e os valores divulgados pelo BanRep, sem cálculos adicionais de vigência ou ajustes manuais.

---

## Fonte dos dados

Os dados são obtidos a partir do serviço SDMX do Banco de la República.
O XML retornado já contém observações diárias, onde cada `<generic:Obs>`
representa um dia específico com seu respectivo valor da taxa DTF.

A aplicação apenas transforma esses dados para JSON, respeitando a granularidade
original da fonte.

---

## Endpoints disponíveis

### GET /dtf-daily

#### Descrição

Retorna a série diária da taxa DTF.
Cada dia presente no XML SDMX é representado individualmente no JSON de saída.

Não há agregação, colapso ou cálculo de períodos.
A saída reflete exatamente as observações diárias divulgadas pelo BanRep.

#### Comportamento

- Um registro por dia
- Datas consecutivas sem lacunas, conforme o SDMX
- Valor diário da DTF

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
````

---

### GET /dtf-weekly

#### Descrição

Retorna a série semanal da taxa DTF.
Para cada semana, é retornado apenas o valor correspondente à última observação
disponível daquela semana.

Este endpoint existe para manter compatibilidade com consumidores que esperam
uma série semanal resumida.

#### Comportamento

* Um registro por semana
* Sempre utiliza a última observação semanal
* Não altera a lógica de cálculo já existente

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

* O endpoint `/dtf-daily` e o endpoint `/dtf-weekly` possuem fluxos de execução separados.
* Cada endpoint chama explicitamente seu método correspondente.
* Nenhuma regra de negócio existente foi alterada durante a separação dos fluxos.
* A aplicação não realiza cálculos de vigência, apenas representação dos dados.

---

## Objetivo do projeto

Fornecer uma interface simples, confiável e fiel aos dados oficiais do BanRep,
permitindo o consumo da taxa DTF tanto em formato diário quanto semanal,
de acordo com a necessidade de cada consumidor.
