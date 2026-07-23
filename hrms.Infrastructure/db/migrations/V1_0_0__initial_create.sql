CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;


ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `AllowanceTypes` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_AllowanceTypes` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Areas` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Code` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Address` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Boundary` geometry NULL /*!80003 SRID 4326 */,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Areas` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Branches` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Code` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ShortName` longtext CHARACTER SET utf8mb4 NULL,
    `Address` longtext CHARACTER SET utf8mb4 NULL,
    `Contact` longtext CHARACTER SET utf8mb4 NULL,
    `ManagerName` longtext CHARACTER SET utf8mb4 NULL,
    `Email` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Boundary` geometry NULL /*!80003 SRID 4326 */,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Branches` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Clients` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Code` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Clients` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Companies` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Code` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ShortName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Address` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Contact` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Email` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TotalWorkingDays` int NOT NULL,
    `TakehomePercentage` int NOT NULL,
    `ApplyStatutoryOnActualMonth` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Companies` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `DeductionApplications` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `DeductionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `EncodeDate` date NOT NULL,
    `StartDate` date NOT NULL,
    `EndDate` date NOT NULL,
    `FrequencyOfPayment` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Terms` int NOT NULL,
    `TotalPrincipal` decimal(65,30) NOT NULL,
    `InterestRate` decimal(65,30) NOT NULL,
    `TotalAmount` decimal(65,30) NOT NULL,
    `ProcessBy` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Note` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Remarks` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_DeductionApplications` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `DeductionPayments` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `DeductionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `PayrollDate` date NOT NULL,
    `Amount` decimal(65,30) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_DeductionPayments` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `DeductionTypes` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_DeductionTypes` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `GeneralSettings` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `IdentityType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `IdentityTypeId` longtext CHARACTER SET utf8mb4 NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Value` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Metadata` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_GeneralSettings` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `GovHDMFs` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EffectiveDate` date NOT NULL,
    `MinSalaryBase` decimal(65,30) NOT NULL,
    `MaxSalaryBase` decimal(65,30) NOT NULL,
    `EmployeeRate` decimal(65,30) NOT NULL,
    `EmployerRate` decimal(65,30) NOT NULL,
    `EmployeeShare` decimal(65,30) NOT NULL,
    `EmployerShare` decimal(65,30) NOT NULL,
    `TotalContribution` decimal(65,30) NOT NULL,
    `Remarks` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_GovHDMFs` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `GovPHICs` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EffectiveDate` date NOT NULL,
    `MinSalaryBase` decimal(65,30) NOT NULL,
    `MaxSalaryBase` decimal(65,30) NOT NULL,
    `PremiumRate` decimal(65,30) NOT NULL,
    `EmployeeShare` decimal(65,30) NOT NULL,
    `EmployerShare` decimal(65,30) NOT NULL,
    `TotalContribution` decimal(65,30) NOT NULL,
    `Remarks` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_GovPHICs` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `GovSSSes` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EffectiveDate` date NOT NULL,
    `RangeFrom` decimal(65,30) NOT NULL,
    `RangeTo` decimal(65,30) NOT NULL,
    `MSC` decimal(65,30) NOT NULL,
    `EE` decimal(65,30) NOT NULL,
    `ER` decimal(65,30) NOT NULL,
    `EC` decimal(65,30) NOT NULL,
    `TotalContibution` decimal(65,30) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_GovSSSes` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `GovTaxes` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EffectiveDate` date NOT NULL,
    `PayrollType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `RangeFrom` decimal(65,30) NOT NULL,
    `RangeTo` decimal(65,30) NOT NULL,
    `PercentageInAmountOf` decimal(65,30) NOT NULL,
    `BaseTaxDue` decimal(65,30) NOT NULL,
    `AddOnPercentage` decimal(65,30) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_GovTaxes` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `HDMFContributions` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `PayrollFrom` date NOT NULL,
    `PayrollTo` date NOT NULL,
    `PayrollDate` date NOT NULL,
    `EmployeeShare` decimal(65,30) NOT NULL,
    `EmployerShare` decimal(65,30) NOT NULL,
    `TotalContribution` decimal(65,30) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_HDMFContributions` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `InboxState` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `MessageId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ConsumerId` char(36) COLLATE ascii_general_ci NOT NULL,
    `LockId` char(36) COLLATE ascii_general_ci NOT NULL,
    `RowVersion` timestamp(6) NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    `Received` datetime(6) NOT NULL,
    `ReceiveCount` int NOT NULL,
    `ExpirationTime` datetime(6) NULL,
    `Consumed` datetime(6) NULL,
    `Delivered` datetime(6) NULL,
    `LastSequenceNumber` bigint NULL,
    CONSTRAINT `PK_InboxState` PRIMARY KEY (`Id`),
    CONSTRAINT `AK_InboxState_MessageId_ConsumerId` UNIQUE (`MessageId`, `ConsumerId`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `IncomePayments` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `IncomeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `PayrollDate` date NOT NULL,
    `Amount` decimal(65,30) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_IncomePayments` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `leaveApplications` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `LeaveId` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `LeaveDateFrom` date NOT NULL,
    `LeaveDateTo` date NOT NULL,
    `DayType` int NOT NULL,
    `PayType` int NOT NULL,
    `ApprovalStatus` int NOT NULL,
    `ReviewedBy` int NULL,
    `ReviewedOn` datetime(6) NULL,
    `ApplicationRemarks` longtext CHARACTER SET utf8mb4 NULL,
    `AuditTrailId` int NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_leaveApplications` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Leaves` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Category` longtext CHARACTER SET utf8mb4 NULL,
    `Code` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Credits` double NOT NULL,
    `PaySource` longtext CHARACTER SET utf8mb4 NOT NULL,
    `LeaveReset` int NOT NULL,
    `Remarks` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Leaves` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `ManualAttendance` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `BatchCode` longtext CHARACTER SET utf8mb4 NOT NULL,
    `User` longtext CHARACTER SET utf8mb4 NOT NULL,
    `FromDate` date NOT NULL,
    `ToDate` date NOT NULL,
    `Time1` time(6) NULL,
    `Time2` time(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_ManualAttendance` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `OtherIncomeApplications` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `EncodeDate` date NOT NULL,
    `StartDate` date NOT NULL,
    `EndDate` date NOT NULL,
    `FrequencyOfPayment` longtext CHARACTER SET utf8mb4 NOT NULL,
    `IncomeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Amount` decimal(65,30) NOT NULL,
    `ProcessBy` longtext CHARACTER SET utf8mb4 NOT NULL,
    `IsProrated` tinyint(1) NOT NULL,
    `IsTaxable` tinyint(1) NOT NULL,
    `Remarks` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_OtherIncomeApplications` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `OutboxMessage` (
    `SequenceNumber` bigint NOT NULL AUTO_INCREMENT,
    `EnqueueTime` datetime(6) NULL,
    `SentTime` datetime(6) NOT NULL,
    `Headers` longtext CHARACTER SET utf8mb4 NULL,
    `Properties` longtext CHARACTER SET utf8mb4 NULL,
    `InboxMessageId` char(36) COLLATE ascii_general_ci NULL,
    `InboxConsumerId` char(36) COLLATE ascii_general_ci NULL,
    `OutboxId` char(36) COLLATE ascii_general_ci NULL,
    `MessageId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ContentType` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
    `MessageType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Body` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ConversationId` char(36) COLLATE ascii_general_ci NULL,
    `CorrelationId` char(36) COLLATE ascii_general_ci NULL,
    `InitiatorId` char(36) COLLATE ascii_general_ci NULL,
    `RequestId` char(36) COLLATE ascii_general_ci NULL,
    `SourceAddress` varchar(256) CHARACTER SET utf8mb4 NULL,
    `DestinationAddress` varchar(256) CHARACTER SET utf8mb4 NULL,
    `ResponseAddress` varchar(256) CHARACTER SET utf8mb4 NULL,
    `FaultAddress` varchar(256) CHARACTER SET utf8mb4 NULL,
    `ExpirationTime` datetime(6) NULL,
    CONSTRAINT `PK_OutboxMessage` PRIMARY KEY (`SequenceNumber`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `OutboxState` (
    `OutboxId` char(36) COLLATE ascii_general_ci NOT NULL,
    `LockId` char(36) COLLATE ascii_general_ci NOT NULL,
    `RowVersion` timestamp(6) NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    `Created` datetime(6) NOT NULL,
    `Delivered` datetime(6) NULL,
    `LastSequenceNumber` bigint NULL,
    CONSTRAINT `PK_OutboxState` PRIMARY KEY (`OutboxId`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `PayrollGroups` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Code` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `PayrollFrequency` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_PayrollGroups` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Payrolls` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `PayPeriodStart` date NOT NULL,
    `PayPeriodEnd` date NOT NULL,
    `PayrollDate` date NOT NULL,
    `BatchCode` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `FullName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `PayrollPeriod` longtext CHARACTER SET utf8mb4 NOT NULL,
    `BasicSalary` decimal(65,30) NOT NULL,
    `OvertimeHR` decimal(65,30) NOT NULL,
    `OvertimePay` decimal(65,30) NOT NULL,
    `NightDifferentialHR` decimal(65,30) NOT NULL,
    `NightDifferentialPay` decimal(65,30) NOT NULL,
    `HolidayPay` decimal(65,30) NOT NULL,
    `Cola` decimal(65,30) NOT NULL,
    `TotalRegularAllowances` decimal(65,30) NOT NULL,
    `TotalBonuses` decimal(65,30) NOT NULL,
    `TotalOtherIncome` decimal(65,30) NOT NULL,
    `TotalDeminimises` decimal(65,30) NOT NULL,
    `TotalCommissions` decimal(65,30) NOT NULL,
    `GrossIncome` decimal(65,30) NOT NULL,
    `WithholdingTax` decimal(65,30) NOT NULL,
    `SSSContribution` decimal(65,30) NOT NULL,
    `PhilHealthContribution` decimal(65,30) NOT NULL,
    `PagIbigContribution` decimal(65,30) NOT NULL,
    `OtherDeductions` decimal(65,30) NOT NULL,
    `TotalDeductions` decimal(65,30) NOT NULL,
    `UnpaidLeaves` decimal(65,30) NOT NULL,
    `PaidLeaves` decimal(65,30) NOT NULL,
    `Absences` decimal(65,30) NOT NULL,
    `AbsentCount` decimal(65,30) NOT NULL,
    `LateAmount` decimal(65,30) NOT NULL,
    `LateHours` decimal(65,30) NOT NULL,
    `UnderTimeAmount` decimal(65,30) NOT NULL,
    `UnderTimeHours` decimal(65,30) NOT NULL,
    `NetPay` decimal(65,30) NOT NULL,
    `EmployerSSSContribution` decimal(65,30) NOT NULL,
    `EmployerPhilHealthContribution` decimal(65,30) NOT NULL,
    `EmployerPagIbigContribution` decimal(65,30) NOT NULL,
    `EmployerECContribution` decimal(65,30) NOT NULL,
    `PayrollGroupId` char(36) COLLATE ascii_general_ci NULL,
    `AreaId` char(36) COLLATE ascii_general_ci NULL,
    `ClientId` char(36) COLLATE ascii_general_ci NULL,
    `NonTaxableBenefits` decimal(65,30) NOT NULL,
    `TaxableBenefits` decimal(65,30) NOT NULL,
    `IsPosted` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Payrolls` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `PHICContributions` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `PayrollFrom` date NOT NULL,
    `PayrollTo` date NOT NULL,
    `PayrollDate` date NOT NULL,
    `EmployeeShare` decimal(65,30) NOT NULL,
    `EmployerShare` decimal(65,30) NOT NULL,
    `TotalContribution` decimal(65,30) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_PHICContributions` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Positions` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Code` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Rate` double NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Positions` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `PremiumRates` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Type` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ShortDescription` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Rate` decimal(65,30) NOT NULL,
    `Remarks` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_PremiumRates` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `ProratedAllowances` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `PayrollFrom` date NOT NULL,
    `PayrollTo` date NOT NULL,
    `PayrollDate` date NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Amount` decimal(65,30) NOT NULL,
    `IsPosted` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_ProratedAllowances` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `SSSContributions` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `PayrollFrom` date NOT NULL,
    `PayrollTo` date NOT NULL,
    `PayrollDate` date NOT NULL,
    `EE` decimal(65,30) NOT NULL,
    `ER` decimal(65,30) NOT NULL,
    `EC` decimal(65,30) NOT NULL,
    `TotalContibution` decimal(65,30) NOT NULL,
    `Remarks` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_SSSContributions` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `TaxContributions` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `PayrollFrom` date NOT NULL,
    `PayrollTo` date NOT NULL,
    `PayrollDate` date NOT NULL,
    `Date` date NOT NULL,
    `Amount` decimal(65,30) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_TaxContributions` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `TimeShifts` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `ShiftName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `ShiftType` int NOT NULL,
    `StartTime` time(6) NOT NULL,
    `EndTime` time(6) NOT NULL,
    `WithAMBreak` int NOT NULL,
    `AMStartTime` time(6) NULL,
    `AMEndTime` time(6) NULL,
    `WithLunchBreak` int NOT NULL,
    `LunchStartTime` time(6) NULL,
    `LunchEndTime` time(6) NULL,
    `WithPMBreak` int NOT NULL,
    `PMStartTime` time(6) NULL,
    `PMEndTime` time(6) NULL,
    `GracePeriodMinutes` double NOT NULL,
    `BreakDurationMinutes` double NOT NULL,
    `WithOT` tinyint(1) NOT NULL,
    `OTRequireTimeIn` tinyint(1) NOT NULL,
    `OTStart` time(6) NOT NULL,
    `OverTimeThreshold` double NOT NULL,
    `MinimumWorkMinutes` double NOT NULL,
    `MaxWorkingMinutes` double NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_TimeShifts` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Allowances` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `IncomeClass` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Code` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `IncomeTypeId` char(36) COLLATE ascii_general_ci NULL,
    `IsTaxable` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Allowances` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Allowances_AllowanceTypes_IncomeTypeId` FOREIGN KEY (`IncomeTypeId`) REFERENCES `AllowanceTypes` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Holidays` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `HolType` int NOT NULL,
    `WorkType` int NOT NULL,
    `HolYear` int NOT NULL,
    `HolDate` date NOT NULL,
    `IsRecuring` tinyint(1) NOT NULL,
    `IsPaid` tinyint(1) NOT NULL,
    `AreaId` char(36) COLLATE ascii_general_ci NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Holidays` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Holidays_Areas_AreaId` FOREIGN KEY (`AreaId`) REFERENCES `Areas` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `ClientHolidays` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `ClientId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `HolType` int NOT NULL,
    `WorkType` int NOT NULL,
    `HolYear` int NOT NULL,
    `HolDate` date NOT NULL,
    `IsRecuring` tinyint(1) NOT NULL,
    `IsPaid` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_ClientHolidays` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ClientHolidays_Clients_ClientId` FOREIGN KEY (`ClientId`) REFERENCES `Clients` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `DeductionApplicationDetails` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `DeductionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ApplicationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Date` date NOT NULL,
    `Principal` decimal(65,30) NOT NULL,
    `Interest` decimal(65,30) NOT NULL,
    `RecordOrder` int NOT NULL,
    `Amount` decimal(65,30) NOT NULL,
    `Balance` decimal(65,30) NOT NULL,
    `Notes` longtext CHARACTER SET utf8mb4 NOT NULL,
    `DeductionApplicationId` char(36) COLLATE ascii_general_ci NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_DeductionApplicationDetails` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DeductionApplicationDetails_DeductionApplications_DeductionA~` FOREIGN KEY (`DeductionApplicationId`) REFERENCES `DeductionApplications` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Deductions` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `CategoryId` char(36) COLLATE ascii_general_ci NULL,
    `Code` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `PriorityLevel` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Deductions` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Deductions_DeductionTypes_CategoryId` FOREIGN KEY (`CategoryId`) REFERENCES `DeductionTypes` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `LeaveApplicationDetails` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `ApplicationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `LeaveDate` date NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_LeaveApplicationDetails` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LeaveApplicationDetails_leaveApplications_ApplicationId` FOREIGN KEY (`ApplicationId`) REFERENCES `leaveApplications` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `OtherIncomeApplicationDetails` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ApplicationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `IncomeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Date` date NOT NULL,
    `Amount` decimal(65,30) NOT NULL,
    `IsTaxable` tinyint(1) NOT NULL,
    `IsProrated` tinyint(1) NOT NULL,
    `Notes` longtext CHARACTER SET utf8mb4 NOT NULL,
    `OtherIncomeApplicationId` char(36) COLLATE ascii_general_ci NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_OtherIncomeApplicationDetails` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OtherIncomeApplicationDetails_OtherIncomeApplications_OtherI~` FOREIGN KEY (`OtherIncomeApplicationId`) REFERENCES `OtherIncomeApplications` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `CutoffDay` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `PayrollGroupId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Day` int NOT NULL,
    `IsEndOfMonth` tinyint(1) NOT NULL,
    `Label` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_CutoffDay` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_CutoffDay_PayrollGroups_PayrollGroupId` FOREIGN KEY (`PayrollGroupId`) REFERENCES `PayrollGroups` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `AssignAssets` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NULL,
    `AssetType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `AssetDescription` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Model` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Brand` longtext CHARACTER SET utf8mb4 NOT NULL,
    `SerialNo` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Qty` int NOT NULL,
    `IssuanceDate` datetime(6) NOT NULL,
    `ReturnedDate` datetime(6) NOT NULL,
    `Remarks` longtext CHARACTER SET utf8mb4 NOT NULL,
    `File` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_AssignAssets` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Attendances` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `BioId` int NULL,
    `WorkDateTime` datetime(6) NOT NULL,
    `IP` longtext CHARACTER SET utf8mb4 NOT NULL,
    `DeviceName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NULL,
    `DepartmentId` char(36) COLLATE ascii_general_ci NULL,
    `ClientId` char(36) COLLATE ascii_general_ci NULL,
    `BranchId` char(36) COLLATE ascii_general_ci NULL,
    `OperationAreaId` char(36) COLLATE ascii_general_ci NULL,
    `UserId` char(36) COLLATE ascii_general_ci NULL,
    `UserName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Workstate` int NOT NULL,
    `Verifycode` longtext CHARACTER SET utf8mb4 NOT NULL,
    `LogRemarks` longtext CHARACTER SET utf8mb4 NOT NULL,
    `BatchCode` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `EditRemarks` longtext CHARACTER SET utf8mb4 NOT NULL,
    `LogSource` int NOT NULL,
    `Boundary` geometry NULL /*!80003 SRID 4326 */,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Attendances` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `ChangeHolidays` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `BatchCode` longtext CHARACTER SET utf8mb4 NOT NULL,
    `HolidayId` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `PayrollDate` date NOT NULL,
    `State` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_ChangeHolidays` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ChangeHolidays_Holidays_HolidayId` FOREIGN KEY (`HolidayId`) REFERENCES `Holidays` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `ChangeRestDays` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `DayName` int NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `PayrollDate` date NOT NULL,
    `State` int NOT NULL,
    `BatchCode` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_ChangeRestDays` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `DailyTimeRecords` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `BatchCode` longtext CHARACTER SET utf8mb4 NULL,
    `WorkType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `FullName` longtext CHARACTER SET utf8mb4 NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `WorkDate` date NOT NULL,
    `ShiftName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ShiftStartTime` datetime(6) NOT NULL,
    `ShiftEndTime` datetime(6) NOT NULL,
    `StartTime` datetime(6) NULL,
    `EndTime` datetime(6) NULL,
    `LateHours` double NOT NULL,
    `UTHours` double NOT NULL,
    `OverBreakHours` double NOT NULL,
    `LateForOTHours` double NOT NULL,
    `RegularNetHours` double NOT NULL,
    `RegularOTHours` double NOT NULL,
    `RegularNDHours` double NOT NULL,
    `RegularNDOTHours` double NOT NULL,
    `RestDayHours` double NOT NULL,
    `RestDayOTHours` double NOT NULL,
    `RestDayNDHours` double NOT NULL,
    `RestDayNDOTHours` double NOT NULL,
    `LegalHolHours` double NOT NULL,
    `LegalHolOTHours` double NOT NULL,
    `LegalHolNightDiffHours` double NOT NULL,
    `LegalHolNightDiffOTHours` double NOT NULL,
    `SpecialHolHours` double NOT NULL,
    `SpecialHolOTHours` double NOT NULL,
    `SpecialHolNightDiffHours` double NOT NULL,
    `SpecialHolNightDiffOTHours` double NOT NULL,
    `RestLegalDayHours` double NOT NULL,
    `RestLegalDayOTHours` double NOT NULL,
    `RestLegalDayNDHours` double NOT NULL,
    `RestLegalDayNDOTHours` double NOT NULL,
    `RestSpecialDayHours` double NOT NULL,
    `RestSpecialDayOTHours` double NOT NULL,
    `RestSpecialDayNDHours` double NOT NULL,
    `RestSpecialDayNDOTHours` double NOT NULL,
    `LeaveHours` double NOT NULL,
    `OB` double NOT NULL,
    `Absent` int NOT NULL,
    `Note` longtext CHARACTER SET utf8mb4 NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NULL,
    `BranchId` char(36) COLLATE ascii_general_ci NULL,
    `DepartmentId` char(36) COLLATE ascii_general_ci NULL,
    `PayrollGroupId` char(36) COLLATE ascii_general_ci NULL,
    `ClientId` char(36) COLLATE ascii_general_ci NULL,
    `AreaId` char(36) COLLATE ascii_general_ci NULL,
    `HolCount` int NOT NULL,
    `SPCount` int NOT NULL,
    `ShiftWorkingHour` double NOT NULL,
    `Posted` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_DailyTimeRecords` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Departments` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Code` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `HeadId` char(36) COLLATE ascii_general_ci NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Departments` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Employees` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `BioId` int NULL,
    `EmployeeNo` longtext CHARACTER SET utf8mb4 NOT NULL,
    `DepartmentId` char(36) COLLATE ascii_general_ci NULL,
    `PayrollGroupId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ClientId` char(36) COLLATE ascii_general_ci NULL,
    `AreaId` char(36) COLLATE ascii_general_ci NULL,
    `BranchId` char(36) COLLATE ascii_general_ci NULL,
    `SectionId` char(36) COLLATE ascii_general_ci NULL,
    `PositionId` char(36) COLLATE ascii_general_ci NULL,
    `JobLevel` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TimeShiftId` char(36) COLLATE ascii_general_ci NULL,
    `DateRegistered` datetime(6) NOT NULL,
    `HireDate` date NOT NULL,
    `ContractStart` datetime(6) NULL,
    `ContractEnd` datetime(6) NULL,
    `CivilStatus` longtext CHARACTER SET utf8mb4 NOT NULL,
    `DateResigned` datetime(6) NULL,
    `HiringEntity` longtext CHARACTER SET utf8mb4 NOT NULL,
    `FirstName` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `LastName` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `MiddleName` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Suffix` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Gender` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Age` int NULL,
    `MonthlyRate` decimal(65,30) NOT NULL,
    `DailyRate` decimal(65,30) NOT NULL,
    `Cola` decimal(65,30) NOT NULL,
    `DOB` datetime(6) NULL,
    `BloodType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ModeOfPayment` int NOT NULL,
    `SalaryType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `EmploymentStatus` int NOT NULL,
    `BankName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `BankNo` longtext CHARACTER SET utf8mb4 NOT NULL,
    `SSSNo` longtext CHARACTER SET utf8mb4 NOT NULL,
    `PHICNo` longtext CHARACTER SET utf8mb4 NOT NULL,
    `HDMFNo` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TIN` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Contact` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Address1` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Address2` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ProfileImg` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Employees` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Employees_Areas_AreaId` FOREIGN KEY (`AreaId`) REFERENCES `Areas` (`Id`),
    CONSTRAINT `FK_Employees_Branches_BranchId` FOREIGN KEY (`BranchId`) REFERENCES `Branches` (`Id`),
    CONSTRAINT `FK_Employees_Clients_ClientId` FOREIGN KEY (`ClientId`) REFERENCES `Clients` (`Id`),
    CONSTRAINT `FK_Employees_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`),
    CONSTRAINT `FK_Employees_PayrollGroups_PayrollGroupId` FOREIGN KEY (`PayrollGroupId`) REFERENCES `PayrollGroups` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Employees_Positions_PositionId` FOREIGN KEY (`PositionId`) REFERENCES `Positions` (`Id`),
    CONSTRAINT `FK_Employees_TimeShifts_TimeShiftId` FOREIGN KEY (`TimeShiftId`) REFERENCES `TimeShifts` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Sections` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `DepartmentId` char(36) COLLATE ascii_general_ci NULL,
    `SectionType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Code` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Sections` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Sections_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Dependents` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NULL,
    `FullName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Relationship` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Gender` longtext CHARACTER SET utf8mb4 NOT NULL,
    `DOB` date NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Dependents` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Dependents_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Educations` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NULL,
    `SchoolName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `YearGraduated` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Educations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Educations_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `EmployeeRecords` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NULL,
    `RecordType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `File` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_EmployeeRecords` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmployeeRecords_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `EmployeeSettings` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `IsEligibleForOvertime` tinyint(1) NOT NULL,
    `IsEligibleForHolidayPay` tinyint(1) NOT NULL,
    `IsEligibleForNightDifferential` tinyint(1) NOT NULL,
    `IsEligibleForLeaveCredits` tinyint(1) NOT NULL,
    `IsEligibleFor13thMonth` tinyint(1) NOT NULL,
    `IsNoDTRNotRequired` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_EmployeeSettings` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmployeeSettings_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Employments` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NULL,
    `CompanyName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Position` longtext CHARACTER SET utf8mb4 NOT NULL,
    `FromDate` date NOT NULL,
    `ToDate` date NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Employments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Employments_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `HDMFRates` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ComputationType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `EE` decimal(65,30) NOT NULL,
    `ER` decimal(65,30) NOT NULL,
    `AddOns` decimal(65,30) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_HDMFRates` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_HDMFRates_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `LeaveLedgers` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `LeaveId` char(36) COLLATE ascii_general_ci NOT NULL,
    `EntryDate` date NOT NULL,
    `Add` decimal(65,30) NOT NULL,
    `Less` decimal(65,30) NOT NULL,
    `Balance` decimal(65,30) NOT NULL,
    `Particulars` longtext CHARACTER SET utf8mb4 NOT NULL,
    `LeaveCreditsId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ReferenceApplicationId` char(36) COLLATE ascii_general_ci NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_LeaveLedgers` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LeaveLedgers_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `OTApplications` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `OTDate` date NOT NULL,
    `StartTime` datetime(6) NOT NULL,
    `EndTime` datetime(6) NOT NULL,
    `FlexiEndTime` tinyint(1) NOT NULL,
    `PaidByNetDutyTime` tinyint(1) NOT NULL,
    `OverTimeThreshold` double NOT NULL,
    `Remarks` longtext CHARACTER SET utf8mb4 NOT NULL,
    `OTStatus` int NOT NULL,
    `OTMinutes` double NOT NULL,
    `OTBeforeOverride` double NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_OTApplications` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OTApplications_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `PHICRates` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ComputationType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `EE` decimal(65,30) NOT NULL,
    `ER` decimal(65,30) NOT NULL,
    `AddOns` decimal(65,30) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_PHICRates` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PHICRates_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `RestDayDates` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `PayrollDate` date NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_RestDayDates` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_RestDayDates_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `RestDays` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `DayName` int NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_RestDays` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_RestDays_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Skills` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Level` double NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Skills` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Skills_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `SSSRates` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ComputationType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `EE` decimal(65,30) NOT NULL,
    `ER` decimal(65,30) NOT NULL,
    `EC` decimal(65,30) NOT NULL,
    `AddOns` decimal(65,30) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_SSSRates` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SSSRates_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `TaxRates` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ComputationType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `EE` decimal(65,30) NOT NULL,
    `AddOns` decimal(65,30) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_TaxRates` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_TaxRates_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `ThirteenthMonthLedgers` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `PayrollDate` date NOT NULL,
    `Amount` decimal(65,30) NOT NULL,
    `Remarks` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_ThirteenthMonthLedgers` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ThirteenthMonthLedgers_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `UTApplications` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `PayrollDate` date NOT NULL,
    `UTMinutes` double NOT NULL,
    `Remarks` longtext CHARACTER SET utf8mb4 NOT NULL,
    `OTStatus` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_UTApplications` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_UTApplications_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `WorkSchedulePlans` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `PayrollDate` date NOT NULL,
    `EmployeeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `TimeShiftId` char(36) COLLATE ascii_general_ci NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_WorkSchedulePlans` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_WorkSchedulePlans_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_WorkSchedulePlans_TimeShifts_TimeShiftId` FOREIGN KEY (`TimeShiftId`) REFERENCES `TimeShifts` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

INSERT INTO `GeneralSettings` (`Id`, `CreatedAt`, `DeletedAt`, `Description`, `IdentityType`, `IdentityTypeId`, `Metadata`, `Status`, `UpdatedAt`, `Value`)
VALUES ('0123f5e6-d7c8-4234-bcda-6789012345fa', TIMESTAMP '2026-01-01 00:00:00', NULL, 'NightDiffThreshold', 'Company', NULL, NULL, 'Active', TIMESTAMP '2026-01-01 00:00:00', '0'),
('1234a5b6-c7d8-4345-cdab-7890123456ab', TIMESTAMP '2026-01-01 00:00:00', NULL, 'AttFillLimit', 'Company', NULL, NULL, 'Active', TIMESTAMP '2026-01-01 00:00:00', 'NOLIMIT'),
('2345b6c7-d8e9-4456-dabc-8901234567bc', TIMESTAMP '2026-01-01 00:00:00', NULL, 'HolidayTimeBasis', 'Company', NULL, NULL, 'Active', TIMESTAMP '2026-01-01 00:00:00', 'BasedOnTimeInDayType'),
('3456c7d8-e9f0-4567-abcd-9012345678cd', TIMESTAMP '2026-01-01 00:00:00', NULL, 'IsHolPlusReg', 'Company', NULL, NULL, 'Active', TIMESTAMP '2026-01-01 00:00:00', 'True'),
('4567d8e9-f012-4678-bcda-0123456789de', TIMESTAMP '2026-01-01 00:00:00', NULL, 'HolidayColumnPresentation', 'Company', NULL, NULL, 'Active', TIMESTAMP '2026-01-01 00:00:00', 'AutoCredit'),
('a2618e39-1a02-4055-989a-7b3bcc61f7b3', TIMESTAMP '2026-01-01 00:00:00', NULL, 'OTEligibility', 'Company', NULL, NULL, 'Active', TIMESTAMP '2026-01-01 00:00:00', 'IndependentOfAttendanceIssues'),
('b1a2c3d4-e5f6-4789-abcd-1234567890ab', TIMESTAMP '2026-01-01 00:00:00', NULL, 'OTInclusion', 'Company', NULL, NULL, 'Active', TIMESTAMP '2026-01-01 00:00:00', 'UsePostShiftWork'),
('c2b3a4d5-f6e7-4890-bcda-2345678901bc', TIMESTAMP '2026-01-01 00:00:00', NULL, 'IsHalfDayLateOn', 'Company', NULL, NULL, 'Active', TIMESTAMP '2026-01-01 00:00:00', 'False'),
('d3c4b5a6-e7f8-4901-cdab-3456789012cd', TIMESTAMP '2026-01-01 00:00:00', NULL, 'HalfDayLateThresholdMinutes', 'Company', NULL, NULL, 'Active', TIMESTAMP '2026-01-01 00:00:00', '0'),
('e4d5c6b7-f8e9-4012-dabc-4567890123de', TIMESTAMP '2026-01-01 00:00:00', NULL, 'IsWholeDayLateOn', 'Company', NULL, NULL, 'Active', TIMESTAMP '2026-01-01 00:00:00', 'False'),
('f5e6d7c8-9012-4123-abcd-5678901234ef', TIMESTAMP '2026-01-01 00:00:00', NULL, 'WholeDayLateThresholdMinutes', 'Company', NULL, NULL, 'Active', TIMESTAMP '2026-01-01 00:00:00', '0');

CREATE INDEX `IX_Allowances_IncomeTypeId` ON `Allowances` (`IncomeTypeId`);

CREATE INDEX `IX_AssignAssets_EmployeeId` ON `AssignAssets` (`EmployeeId`);

CREATE INDEX `IX_Att_BRId_DepId_Area_ClId_LS` ON `Attendances` (`BranchId`, `DepartmentId`, `OperationAreaId`, `ClientId`);

CREATE INDEX `IX_Attendance_LogSource_BatchCode` ON `Attendances` (`LogSource`, `BatchCode`);

CREATE INDEX `IX_Attendance_ls_wt` ON `Attendances` (`LogSource`, `WorkDateTime`);

CREATE INDEX `IX_Attendances_EmployeeId` ON `Attendances` (`EmployeeId`);

CREATE INDEX `IX_ChangeHolidays_EmployeeId` ON `ChangeHolidays` (`EmployeeId`);

CREATE INDEX `IX_ChangeHolidays_HolidayId` ON `ChangeHolidays` (`HolidayId`);

CREATE INDEX `IX_ChangeRestDays_EmployeeId` ON `ChangeRestDays` (`EmployeeId`);

CREATE INDEX `IX_ClientHolidays_ClientId` ON `ClientHolidays` (`ClientId`);

CREATE INDEX `IX_CutoffDay_PayrollGroupId` ON `CutoffDay` (`PayrollGroupId`);

CREATE INDEX `IX_DailyTimeRecords_EmployeeId` ON `DailyTimeRecords` (`EmployeeId`);

CREATE INDEX `IX_DeductionApplicationDetails_DeductionApplicationId` ON `DeductionApplicationDetails` (`DeductionApplicationId`);

CREATE INDEX `IX_Deductions_CategoryId` ON `Deductions` (`CategoryId`);

CREATE UNIQUE INDEX `IX_Departments_HeadId` ON `Departments` (`HeadId`);

CREATE INDEX `IX_Dependents_EmployeeId` ON `Dependents` (`EmployeeId`);

CREATE INDEX `IX_Educations_EmployeeId` ON `Educations` (`EmployeeId`);

CREATE INDEX `IX_EmployeeRecords_EmployeeId` ON `EmployeeRecords` (`EmployeeId`);

CREATE INDEX `IX_Employees_AreaId` ON `Employees` (`AreaId`);

CREATE INDEX `IX_Employees_BranchId` ON `Employees` (`BranchId`);

CREATE INDEX `IX_Employees_ClientId` ON `Employees` (`ClientId`);

CREATE INDEX `IX_Employees_DepartmentId` ON `Employees` (`DepartmentId`);

CREATE INDEX `IX_Employees_FirstName` ON `Employees` (`FirstName`);

CREATE INDEX `IX_Employees_LastName` ON `Employees` (`LastName`);

CREATE INDEX `IX_Employees_MiddleName` ON `Employees` (`MiddleName`);

CREATE INDEX `IX_Employees_PayrollGroupId` ON `Employees` (`PayrollGroupId`);

CREATE INDEX `IX_Employees_PositionId` ON `Employees` (`PositionId`);

CREATE INDEX `IX_Employees_Suffix` ON `Employees` (`Suffix`);

CREATE INDEX `IX_Employees_TimeShiftId` ON `Employees` (`TimeShiftId`);

CREATE UNIQUE INDEX `IX_EmployeeSettings_EmployeeId` ON `EmployeeSettings` (`EmployeeId`);

CREATE INDEX `IX_Employments_EmployeeId` ON `Employments` (`EmployeeId`);

CREATE UNIQUE INDEX `IX_HDMFRates_EmployeeId` ON `HDMFRates` (`EmployeeId`);

CREATE INDEX `IX_Holidays_AreaId` ON `Holidays` (`AreaId`);

CREATE INDEX `IX_InboxState_Delivered` ON `InboxState` (`Delivered`);

CREATE INDEX `IX_LeaveApplicationDetails_ApplicationId` ON `LeaveApplicationDetails` (`ApplicationId`);

CREATE INDEX `IX_LeaveLedgers_EmployeeId` ON `LeaveLedgers` (`EmployeeId`);

CREATE INDEX `IX_OTApplications_EmployeeId` ON `OTApplications` (`EmployeeId`);

CREATE INDEX `IX_OtherIncomeApplicationDetails_OtherIncomeApplicationId` ON `OtherIncomeApplicationDetails` (`OtherIncomeApplicationId`);

CREATE INDEX `IX_OutboxMessage_EnqueueTime` ON `OutboxMessage` (`EnqueueTime`);

CREATE INDEX `IX_OutboxMessage_ExpirationTime` ON `OutboxMessage` (`ExpirationTime`);

CREATE UNIQUE INDEX `IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber` ON `OutboxMessage` (`InboxMessageId`, `InboxConsumerId`, `SequenceNumber`);

CREATE UNIQUE INDEX `IX_OutboxMessage_OutboxId_SequenceNumber` ON `OutboxMessage` (`OutboxId`, `SequenceNumber`);

CREATE INDEX `IX_OutboxState_Created` ON `OutboxState` (`Created`);

CREATE UNIQUE INDEX `IX_PHICRates_EmployeeId` ON `PHICRates` (`EmployeeId`);

CREATE INDEX `IX_RestDayDates_EmployeeId` ON `RestDayDates` (`EmployeeId`);

CREATE INDEX `IX_RestDays_EmployeeId` ON `RestDays` (`EmployeeId`);

CREATE INDEX `IX_Sections_DepartmentId` ON `Sections` (`DepartmentId`);

CREATE INDEX `IX_Skills_EmployeeId` ON `Skills` (`EmployeeId`);

CREATE UNIQUE INDEX `IX_SSSRates_EmployeeId` ON `SSSRates` (`EmployeeId`);

CREATE UNIQUE INDEX `IX_TaxRates_EmployeeId` ON `TaxRates` (`EmployeeId`);

CREATE INDEX `IX_ThirteenthMonthLedgers_EmployeeId` ON `ThirteenthMonthLedgers` (`EmployeeId`);

CREATE INDEX `IX_UTApplications_EmployeeId` ON `UTApplications` (`EmployeeId`);

CREATE INDEX `IX_WorkSchedulePlans_EmployeeId` ON `WorkSchedulePlans` (`EmployeeId`);

CREATE INDEX `IX_WorkSchedulePlans_TimeShiftId` ON `WorkSchedulePlans` (`TimeShiftId`);

ALTER TABLE `AssignAssets` ADD CONSTRAINT `FK_AssignAssets_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`);

ALTER TABLE `Attendances` ADD CONSTRAINT `FK_Attendances_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`);

ALTER TABLE `ChangeHolidays` ADD CONSTRAINT `FK_ChangeHolidays_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE;

ALTER TABLE `ChangeRestDays` ADD CONSTRAINT `FK_ChangeRestDays_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE CASCADE;

ALTER TABLE `DailyTimeRecords` ADD CONSTRAINT `FK_DailyTimeRecords_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`);

ALTER TABLE `Departments` ADD CONSTRAINT `FK_Departments_Employees_HeadId` FOREIGN KEY (`HeadId`) REFERENCES `Employees` (`Id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260723153850_initial_create', '9.0.2');


