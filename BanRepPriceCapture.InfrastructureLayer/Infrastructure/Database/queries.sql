-- name: InsertDtfDailyPrice
insert into "dtf_daily_prices" (flow_id, data_capture, data_price, payload)
select @FlowId, @DataCapture, @DataPrice, @Payload::jsonb
where not exists (
    select 1
    from "dtf_daily_prices"
    where flow_id = @FlowId
      and data_price = @DataPrice
);

-- name: GetDtfDailyPricePayloadsByFlowId
select payload as "Payload"
from "dtf_daily_prices"
where flow_id = @FlowId
order by data_price;

-- name: InsertProcessingState
insert into "dtf_processing_states" (
    capture_date,
    flow_id,
    status,
    last_updated_at
)
values (
    @CaptureDate,
    @FlowId,
    @Status,
    @LastUpdatedAt
)
on conflict (flow_id) do nothing;

-- name: UpdateProcessingStateStatus
update "dtf_processing_states"
set status = @Status,
    error_message = @ErrorMessage,
    last_updated_at = @LastUpdatedAt
where flow_id = @FlowId;

-- name: RecordProcessingStateSend
update "dtf_processing_states"
set downstream_send_id = @DownstreamSendId,
    status = @Status,
    error_message = @ErrorMessage,
    last_updated_at = @LastUpdatedAt
where flow_id = @FlowId;

-- name: GetProcessingStateByFlowId
select
    capture_date as "CaptureDate",
    flow_id as "FlowId",
    status as "Status",
    last_updated_at as "LastUpdatedAt",
    error_message as "ErrorMessage",
    downstream_send_id as "DownstreamSendId"
from "dtf_processing_states"
where flow_id = @FlowId;

-- name: GetLastProcessingStateByCaptureDate
select
    capture_date as "CaptureDate",
    flow_id as "FlowId",
    status as "Status",
    last_updated_at as "LastUpdatedAt",
    error_message as "ErrorMessage",
    downstream_send_id as "DownstreamSendId"
from "dtf_processing_states"
where capture_date = @CaptureDate
order by last_updated_at desc
limit 1;

-- name: ListFailedOrIncompleteExecutions
select
    capture_date as "CaptureDate",
    flow_id as "FlowId",
    status as "Status",
    last_updated_at as "LastUpdatedAt",
    error_message as "ErrorMessage",
    downstream_send_id as "DownstreamSendId"
from "dtf_processing_states"
where status in ('Received', 'Processing', 'Persisted', 'Failed')
order by last_updated_at desc;
