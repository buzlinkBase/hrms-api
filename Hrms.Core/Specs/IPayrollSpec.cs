
using Hrms.Domain.Entities;
using System.Linq.Expressions;

namespace Hrms.Core.Specs;

public interface IPayrollSpec<Context>
{
    bool IsSatisfiedBy(Context context);
}
public class OrPayrollSpec<Context> : IPayrollSpec<Context>
{
    private readonly IPayrollSpec<Context> _left;
    private readonly IPayrollSpec<Context> _right;
    public OrPayrollSpec(IPayrollSpec<Context> left, IPayrollSpec<Context> right)
    {
        _left = left;
        _right = right;
    }

    public bool IsSatisfiedBy(Context context)
    {
        return _left.IsSatisfiedBy(context)
           || _right.IsSatisfiedBy(context);
    }
}
public class AndPayrollSpec<Context> : IPayrollSpec<Context>
{
    private readonly IPayrollSpec<Context> _left;
    private readonly IPayrollSpec<Context> _right;
    public AndPayrollSpec(IPayrollSpec<Context> left, IPayrollSpec<Context> right)
    {
        _left = left;
        _right = right;
    }

    public bool IsSatisfiedBy(Context context)
    {
        return _left.IsSatisfiedBy(context)
           && _right.IsSatisfiedBy(context);
    }
}
public class NotPayrollSpec<Context> : IPayrollSpec<Context>
{
    private readonly IPayrollSpec<Context> _spec;

    public NotPayrollSpec(IPayrollSpec<Context> spec)
    {
        _spec = spec;
    }

    public bool IsSatisfiedBy(Context context)
    {
        return !_spec.IsSatisfiedBy(context);
    }
}
public class AndNotPayrollSpec<Context> : IPayrollSpec<Context>
{
    private readonly IPayrollSpec<Context> _left;
    private readonly IPayrollSpec<Context> _right;

    public AndNotPayrollSpec(IPayrollSpec<Context> left, IPayrollSpec<Context> right)
    {
        _left = left;
        _right = new NotPayrollSpec<Context>(right);
    }

    public bool IsSatisfiedBy(Context context)
    {
        return _left.IsSatisfiedBy(context)
            && _right.IsSatisfiedBy(context);
    }
}
public class OrNotPayrollSpec<Context> : IPayrollSpec<Context>
{
    private readonly IPayrollSpec<Context> _left;
    private readonly IPayrollSpec<Context> _right;

    public OrNotPayrollSpec(IPayrollSpec<Context> left, IPayrollSpec<Context> right)
    {
        _left = left;
        _right = new NotPayrollSpec<Context>(right);
    }

    public bool IsSatisfiedBy(Context context)
    {
        return _left.IsSatisfiedBy(context)
            || _right.IsSatisfiedBy(context);
    }
}

public class IsPostedSpec<T> : Specification<T> where T : class, IEntity, IPostedFilter, new()
{
    private readonly bool _isPosted;
    public IsPostedSpec(bool IsPosted)
    {
        _isPosted = IsPosted;
    }
    public override Expression<Func<T, bool>> Criteria => x => x.IsPosted == _isPosted;
}

//Example
//- Range: Jan 1 – Jan 31
//- Record: Jan 5 – Jan 10 → ✅ included
//- Record: Dec 28 – Jan 10 → ❌ excluded
//- Record: Jan 20 – Feb 5 → ❌ excluded
public class IsContainedDateRangeSpec<T> : Specification<T>
    where T : class, IEntity, IDateRangeFilter, new()
{
    private readonly DateOnly _fromDate;
    private readonly DateOnly _toDate;

    public IsContainedDateRangeSpec(DateOnly fromDate, DateOnly toDate)
    {
        _fromDate = fromDate;
        _toDate = toDate;
    }

    public override Expression<Func<T, bool>> Criteria =>
        x => x.FromDate >= _fromDate && x.ToDate <= _toDate;
}

//Example
//- Range: Jan 1 – Jan 31
//- Record: Dec 28 – Jan 10 → ✅ included
//- Record: Jan 20 – Feb 5 → ✅ included
//- Record: Feb 10 – Feb 20 → ❌ excluded
public class IsOverlappingDateRangeSpec<T> : Specification<T>
    where T : class, IEntity, IDateRangeFilter, new()
{
    private readonly DateOnly _fromDate;
    private readonly DateOnly _toDate;

    public IsOverlappingDateRangeSpec(DateOnly fromDate, DateOnly toDate)
    {
        _fromDate = fromDate;
        _toDate = toDate;
    }

    public override Expression<Func<T, bool>> Criteria =>
        x => x.FromDate <= _toDate && x.ToDate >= _fromDate;
}

//Example
//- Range: Jan 1 – Jan 31
//- Record with FromDate = Jan 5 → ✅ included
//- Record with FromDate = Jan 31 → ✅ included
//- Record with FromDate = Feb 1 → ❌ excluded
public class IsDateWithinRangeSpec<T> : Specification<T>
    where T : class, IEntity, IDateFilter, new()
{
    private readonly DateOnly _fromDate;
    private readonly DateOnly _toDate;

    public IsDateWithinRangeSpec(DateOnly fromDate, DateOnly toDate)
    {
        _fromDate = fromDate;
        _toDate = toDate;
    }

    public override Expression<Func<T, bool>> Criteria =>
        x => x.PayrollDate >= _fromDate && x.PayrollDate <= _toDate;

}

public class IsDateByMonthYearSpec<T> : Specification<T>
    where T : class, IEntity, IDateFilter, new()
{
    private readonly DateOnly _date;
    public IsDateByMonthYearSpec(DateOnly date)
    {
        _date = date;
    }

    public override Expression<Func<T, bool>> Criteria =>
        x => x.PayrollDate.Month == _date.Month && x.PayrollDate.Year == _date.Year;
}