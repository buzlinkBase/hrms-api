//namespace Hrms.Api.Extensions
//{
//    public static class MessageHandlerServiceRegistrations
//    {

//        public static void RegisterMessageHandlers(this WebApplicationBuilder builder)
//        {

//            builder.Services.AddScoped<IMessageHandlerFactory, MessageHandlerFactory>();
//            builder.Services.AddKeyedScoped<IMessageHandler, CreateDTRSummaryHandler>("trans.dtr.summary.created");
//            builder.Services.AddKeyedScoped<IMessageHandler, DeleteDTRSummaryHandler>("trans.dtr.summary.deleted");
//            builder.Services.AddKeyedScoped<IMessageHandler, EmployeeMessageHandler>("master.employee.sync");
//            builder.Services.AddKeyedScoped<IMessageHandler, DepartmentMessageHandler>("master.department.sync");
//            builder.Services.AddKeyedScoped<IMessageHandler, ClientMessageHandler>("master.client.sync");
//            builder.Services.AddKeyedScoped<IMessageHandler, HolidayMessageHandler>("master.holiday.sync");
//            builder.Services.AddKeyedScoped<IMessageHandler, ChangeHolidayMessageHandler>("master.change.holiday.sync");
//            builder.Services.AddKeyedScoped<IMessageHandler, AreaMessageHandler>("master.area.sync");
//            builder.Services.AddKeyedScoped<IMessageHandler, TimeShiftMessageHandler>("master.timeshift.sync");
//            builder.Services.AddKeyedScoped<IMessageHandler, LeaveMessageHandler>("master.leave.sync");
//            builder.Services.AddKeyedScoped<IMessageHandler, DeductionMessageHandler>("master.deduction.sync");
//            builder.Services.AddKeyedScoped<IMessageHandler, OtherIncomeMessageHandler>("master.otherincome.sync");
//            builder.Services.AddKeyedScoped<IMessageHandler, BranchMessageHandler>("master.branch.sync");
//            //builder.Services.AddKeyedScoped<IMessageHandler, TenantMessageHandler>("master.tenant.sync");

//        }
//    }
//}