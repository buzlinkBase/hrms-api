namespace Hrms.Core.Calculators.DtrPay;

// One day's DTR pay for one employee, under one OT/ND calculation method (Setup > Company Policy
// > OT/ND Calculation Method). Pick one through IDtrPayCalculatorFactory.
public interface IDtrPayCalculator
{
    OtNdCalculationMethod Method { get; }
    DTRPayModel Calculate(PayrollContext context);
}

// Template Method: the flow every pay method follows, in one place --
//   each day type's pay (one class per day type, see Additive/ and Compounded/),
//   then the day's attendance amounts, then the DTRPayModel.
// A pay method is just its own set of 8 day-type calculators; attendance and the model mapping
// are shared, so the two methods can't drift apart on those.
internal abstract class DtrPayCalculatorBase : IDtrPayCalculator
{
    private readonly IReadOnlyList<IDayTypePayCalculator> _dayTypes;
    private readonly IAttendancePayCalculator _attendance;
    private readonly IDtrPayModelBuilder _builder;

    protected DtrPayCalculatorBase(
        IEnumerable<IDayTypePayCalculator> dayTypes,
        IAttendancePayCalculator attendance,
        IDtrPayModelBuilder builder)
    {
        _dayTypes = dayTypes.ToList();
        var kinds = _dayTypes.Select(d => d.Kind).ToList();
        if (kinds.Count != kinds.Distinct().Count() || !Enum.GetValues<PayCategoryKind>().All(kinds.Contains))
            throw new InvalidOperationException(
                $"{GetType().Name} needs exactly one day-type calculator per day type; got [{string.Join(", ", kinds)}].");
        _attendance = attendance;
        _builder = builder;
    }

    public abstract OtNdCalculationMethod Method { get; }

    public DTRPayModel Calculate(PayrollContext context)
    {
        var pay = _dayTypes.ToDictionary(d => d.Kind, d => d.Calculate(context));
        return _builder.Build(context, pay, _attendance.Calculate(context));
    }
}

// Compounded (DOLE-standard): the saved DTR hours exactly as stored, each bucket at its fully
// compounded rate -- one class per day type under Compounded/ (CompoundedDayTypePayBase).
internal sealed class CompoundedDtrPayCalculator : DtrPayCalculatorBase
{
    public CompoundedDtrPayCalculator(
        IEnumerable<ICompoundedDayTypePay> dayTypes,
        IAttendancePayCalculator attendance,
        IDtrPayModelBuilder builder)
        : base(dayTypes, attendance, builder) { }

    public override OtNdCalculationMethod Method => OtNdCalculationMethod.Compounded;
}

// Additive: the DTR display hours, each bucket at one rate, added up -- one class per day type
// under Additive/ (AdditiveDayTypePayBase). Pays exactly what the old Additive branches did;
// only the bucket each peso lands in changed.
internal sealed class AdditiveDtrPayCalculator : DtrPayCalculatorBase
{
    public AdditiveDtrPayCalculator(
        IEnumerable<IAdditiveDayTypePay> dayTypes,
        IAttendancePayCalculator attendance,
        IDtrPayModelBuilder builder)
        : base(dayTypes, attendance, builder) { }

    public override OtNdCalculationMethod Method => OtNdCalculationMethod.Additive;
}

// Factory: the one place that maps a company's OT/ND method to its calculator. Registry-based --
// every IDtrPayCalculator registered in DI is picked up, so a new method is a new class plus one
// registration, with no change here or in any caller.
public interface IDtrPayCalculatorFactory
{
    IDtrPayCalculator For(OtNdCalculationMethod method);
}

internal sealed class DtrPayCalculatorFactory : IDtrPayCalculatorFactory
{
    private readonly IReadOnlyDictionary<OtNdCalculationMethod, IDtrPayCalculator> _byMethod;

    public DtrPayCalculatorFactory(IEnumerable<IDtrPayCalculator> calculators)
    {
        _byMethod = calculators.ToDictionary(c => c.Method);
    }

    public IDtrPayCalculator For(OtNdCalculationMethod method) =>
        _byMethod.TryGetValue(method, out var calculator)
            ? calculator
            : throw new InvalidOperationException($"No DTR pay calculator is registered for OT/ND calculation method '{method}'.");
}
