using System.Data;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace BanRepPriceCapture.InfrastructureLayer.Database.TypeHandlers;

public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        if (parameter is NpgsqlParameter npgsqlParameter)
        {
            npgsqlParameter.NpgsqlDbType = NpgsqlDbType.Date;
            npgsqlParameter.Value = value;
            return;
        }

        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value)
    {
        return value switch
        {
            DateTime dt => DateOnly.FromDateTime(dt),
            DateOnly dateOnly => dateOnly,
            _ => DateOnly.FromDateTime(Convert.ToDateTime(value))
        };
    }
}
