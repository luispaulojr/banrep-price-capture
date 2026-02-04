using System.Data;
using Dapper;

namespace BanRepPriceCapture.Dtf.Infrastructure.Database.TypeHandlers;

public sealed class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
{
    public override void SetValue(IDbDataParameter parameter, TimeOnly value)
    {
        parameter.DbType = DbType.Time;
        parameter.Value = value.ToTimeSpan();
    }

    public override TimeOnly Parse(object value)
    {
        return value switch
        {
            TimeSpan ts => TimeOnly.FromTimeSpan(ts),
            TimeOnly timeOnly => timeOnly,
            _ => TimeOnly.FromTimeSpan((TimeSpan)value)
        };
    }
}
