using System.Data;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Database.TypeHandlers;

public sealed class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
{
    public override void SetValue(IDbDataParameter parameter, TimeOnly value)
    {
        if (parameter is NpgsqlParameter npgsqlParameter)
        {
            npgsqlParameter.NpgsqlDbType = NpgsqlDbType.Time;
            npgsqlParameter.Value = value;
            return;
        }

        parameter.DbType = DbType.Time;
        parameter.Value = value.ToTimeSpan();
    }

    public override TimeOnly Parse(object value)
    {
        return value switch
        {
            TimeSpan ts => TimeOnly.FromTimeSpan(ts),
            TimeOnly timeOnly => timeOnly,
            DateTime dt => TimeOnly.FromDateTime(dt),
            _ => TimeOnly.FromDateTime(Convert.ToDateTime(value))
        };
    }
}
