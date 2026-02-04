-- name: InsertDtfDailyPrice
insert into "dtf_daily_prices" (flow_id, data_capture, data_price, payload)
select @FlowId, @DataCapture, @DataPrice, @Payload::jsonb
where not exists (
    select 1
    from "dtf_daily_prices"
    where flow_id = @FlowId
      and data_price = @DataPrice
);
