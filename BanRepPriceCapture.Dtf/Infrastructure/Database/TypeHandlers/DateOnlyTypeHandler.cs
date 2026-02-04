using System.Data;
using Dapper;

namespace BanRepPriceCapture.Dtf.Infrastructure.Database.TypeHandlers;

public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
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
