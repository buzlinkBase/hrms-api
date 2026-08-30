namespace hrms.test.ResolverTests;

public class StatutoryCreditDateResolverTests
{
    private static readonly DateOnly FromDate = new(2025, 3, 26);
    private static readonly DateOnly ToDate = new(2025, 4, 10);

    [Fact]
    public void CutoffStartMonth_CreditsToTheFromDate()
    {
        StatutoryCreditDateResolver.Resolve(FromDate, ToDate, CrossMonthStatutoryCreditPolicy.CutoffStartMonth)
            .Should().Be(FromDate);
    }

    [Fact]
    public void CutoffEndMonth_CreditsToTheToDate()
    {
        StatutoryCreditDateResolver.Resolve(FromDate, ToDate, CrossMonthStatutoryCreditPolicy.CutoffEndMonth)
            .Should().Be(ToDate);
    }

    [Fact]
    public void PayDate_CreditsToTheSuppliedPayDate_WhenProvided()
    {
        var payDate = new DateOnly(2025, 4, 15);
        StatutoryCreditDateResolver.Resolve(FromDate, ToDate, CrossMonthStatutoryCreditPolicy.PayDate, payDate)
            .Should().Be(payDate);
    }

    [Fact]
    public void PayDate_FallsBackToToDate_WhenNoPayDateSupplied()
    {
        // Defense-in-depth fallback only — PayrollProcessorService.CalculateAsync's own
        // up-front guard is the primary enforcement that PayDate is supplied when required.
        StatutoryCreditDateResolver.Resolve(FromDate, ToDate, CrossMonthStatutoryCreditPolicy.PayDate)
            .Should().Be(ToDate);
    }

    [Fact]
    public void SameMonthPeriod_PolicyHasNoObservableEffect()
    {
        var from = new DateOnly(2025, 3, 1);
        var to = new DateOnly(2025, 3, 15);
        StatutoryCreditDateResolver.Resolve(from, to, CrossMonthStatutoryCreditPolicy.CutoffStartMonth).Should().Be(from);
        StatutoryCreditDateResolver.Resolve(from, to, CrossMonthStatutoryCreditPolicy.CutoffEndMonth).Should().Be(to);
    }
}
