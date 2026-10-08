namespace Hrms.Core.Services.Schedules;

// Template Method for an application's payment schedule (Other Income schedule, loan
// amortization): applies what the form submitted to the application's stored rows.
//  - Rows are matched by Id only. A submitted row with no Id, or with an Id that isn't one of this
//    application's rows, becomes a new row -- a client-sent Id is never INSERTed again.
//  - Stored rows the form no longer sends are handed to `remove`.
//  - A locked row (already used by payroll) can't change or go: it must come back unchanged.
// Subclasses supply only what differs per schedule type.
public abstract class ScheduleReconciler<TParent, TRow, TInput>
    where TRow : IEntity
{
    public void Reconcile(TParent parent, ICollection<TRow> rows, IEnumerable<TInput> submitted,
        Action<TRow> remove)
    {
        var existing = rows.ToDictionary(x => x.Id);
        var kept = new HashSet<Guid>();
        var position = 0;

        foreach (var item in submitted)
        {
            var id = IdOf(item);
            if (id != Guid.Empty && existing.TryGetValue(id, out var row) && kept.Add(id))
            {
                if (IsLocked(row))
                    Guard.EnsureTrue(IsUnchanged(row, item), LockedMessage(row));
                else
                    Apply(parent, row, item, position);
            }
            else
            {
                rows.Add(Create(parent, item, position));
            }
            position++;
        }

        foreach (var row in existing.Values.Where(x => !kept.Contains(x.Id)))
        {
            EnsureNotLocked(row);
            remove(row);
        }
    }

    public void EnsureNotLocked(TRow row) => Guard.EnsureFalse(IsLocked(row), LockedMessage(row));

    public bool AnyLocked(IEnumerable<TRow> rows) => rows.Any(IsLocked);

    protected abstract Guid IdOf(TInput input);
    protected abstract bool IsLocked(TRow row);
    protected abstract bool IsUnchanged(TRow row, TInput input);
    protected abstract void Apply(TParent parent, TRow row, TInput input, int position);
    protected abstract TRow Create(TParent parent, TInput input, int position);
    protected abstract DateOnly DateOf(TRow row);

    private string LockedMessage(TRow row) =>
        $"The schedule row dated {DateOf(row):yyyy-MM-dd} was already paid in a saved payroll run and can't be changed or removed.";
}
