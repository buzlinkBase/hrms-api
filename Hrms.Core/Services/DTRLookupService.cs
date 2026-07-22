//using Hrms.Domain.Entities;

//namespace DTR.Core;

//public class DTRLookupService(IUnitOfWorkService uow) : BaseService<DailyRecord>(uow)
//{
//    //public async Task AddRange(List<DailyRecord> models)
//    //{
//    //    //delete by employee and Date
//    //    var dtrsIds = models.Select(x => x.EmployeeId).Distinct().ToList();
//    //    var dtrsDate = models.Select(x => x.WorkDate).Distinct().ToList();

//    //    _uow.Repository.Remove<DailyRecord>(x =>
//    //        dtrsIds.Any(xx => xx == x.EmployeeId) &&
//    //        dtrsDate.Any(xx => xx == x.WorkDate));

//    //    //reset Id
//    //    foreach (var dtr in models)
//    //    {
//    //        dtr.Id = Guid.Empty;
//    //    }
//    //    await CreateRangeAsync(models);
//    //}

//    //public List<DailyRecordModel> LoadDTRRawList(DTRRequestPayload payload)
//    //{
//    //    return FindDTR(payload)
//    //            .Select(x => new DailyRecordModel()
//    //            {
//    //                FullName = x.FullName,
//    //                EmployeeId = x.EmployeeId,
//    //                LateHours = x.Sum(x => x.LateHours),
//    //                UTHours = x.Sum(x => x.UTHours),
//    //                OverBreakHours = x.Sum(x => x.OverBreakHours),
//    //                LateForOTHours = x.Sum(x => x.LateForOTHours),

//    //                RegularNetHours = x.Sum(x => x.RegularNetHours),
//    //                RegularOTHours = x.Sum(x => x.RegularOTHours),
//    //                RegularNDHours = x.Sum(x => x.RegularNDHours),
//    //                RegularNDOTHours = x.Sum(x => x.RegularNDOTHours),

//    //                RestDayHours = x.Sum(x => x.RestDayHours),
//    //                RestDayOTHours = x.Sum(x => x.RestDayOTHours),
//    //                RestDayNDHours = x.Sum(x => x.RestDayNDHours),
//    //                RestDayNDOTHours = x.Sum(x => x.RestDayNDOTHours),

//    //                LegalHolHours = x.Sum(x => x.LegalHolHours),
//    //                LegalHolOTHours = x.Sum(x => x.LegalHolOTHours),
//    //                LegalHolNightDiffHours = x.Sum(x => x.LegalHolNightDiffHours),
//    //                LegalHolNightDiffOTHours = x.Sum(x => x.LegalHolNightDiffOTHours),

//    //                SpecialHolHours = x.Sum(x => x.SpecialHolHours),
//    //                SpecialHolOTHours = x.Sum(x => x.SpecialHolOTHours),
//    //                SpecialHolNightDiffHours = x.Sum(x => x.SpecialHolNightDiffHours),
//    //                SpecialHolNightDiffOTHours = x.Sum(x => x.SpecialHolNightDiffOTHours),

//    //                RestLegalDayHours = x.Sum(xx => xx.RestLegalDayHours),
//    //                RestLegalDayNDHours = x.Sum(xx => xx.RestLegalDayNDHours),
//    //                RestLegalDayNDOTHours = x.Sum(xx => xx.RestLegalDayNDOTHours),
//    //                RestLegalDayOTHours = x.Sum(xx => xx.RestLegalDayOTHours),

//    //                RestSpecialDayHours = x.Sum(xx => xx.RestSpecialDayHours),
//    //                RestSpecialDayNDHours = x.Sum(xx => xx.RestSpecialDayNDHours),
//    //                RestSpecialDayNDOTHours = x.Sum(xx => xx.RestSpecialDayNDOTHours),
//    //                RestSpecialDayOTHours = x.Sum(xx => xx.RestSpecialDayOTHours),
//    //                ShiftWorkingHour = x.Sum(xx => xx.ShiftWorkingHour),

//    //                BranchId = x.First().BranchId,
//    //                DepartmentId = x.First().DepartmentId,
//    //                AreaId = x.First().AreaId,
//    //                PayrollGroupId = x.First().PayrollGroupId,
//    //                ClientId = x.First().ClientId,
//    //                HolCount = x.Sum(xx => xx.HolCount),
//    //                SPCount = x.Sum(xx => xx.SPCount),
//    //                LeaveHours = x.Sum(xx => xx.LeaveHours),
//    //                OB = x.Sum(x => x.OB),
//    //                Absent = x.Sum(x => x.Absent),
//    //            })
//    //        .OrderBy(x => x.FullName)
//    //        .ToList();
//    //}
//    public List<DailyRecordModel> LoadDTRSummary(DTRRequestPayload payload)
//    {
//        return DTRSummaryQuery(payload);
//    }

//    public List<DailyRecordModel> FindTardiness(DTRRequestPayload payload)
//    {
//        var data = DTRSummaryQuery(payload)
//           .Where(x => x.LateHours > 0 || x.LateHours > 0 || x.LeaveHours > 0 || x.OB > 0)
//           .ToList();
//        return data;
//    }
//    public async Task RemoveRange(List<DailyRecord> models, CancellationToken token)
//    {
//        await RemoveRangeAsync(models.ToList(), token);
//    }
  
//}
