namespace DTR.Core;

public interface IDailyRecordBuilder
{
    DTRDetailModel Build(
        TimeContext context,
        PipeLineResult pipeline,
        EvaluatedColumnResult evaluated,
        NightDiffEvaluationResult nightDiff,
        WorkType workType);
}
