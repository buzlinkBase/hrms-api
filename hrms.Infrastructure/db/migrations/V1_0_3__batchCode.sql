ALTER TABLE `Attendances` MODIFY COLUMN `BatchCode` varchar(255) CHARACTER SET utf8mb4 NOT NULL;

CREATE INDEX `IX_Att_BRId_DepId_Area_ClId_LS` ON `Attendances` (`BranchId`, `DepartmentId`, `OperationAreaId`, `ClientId`);

CREATE INDEX `IX_Attendance_LogSource_BatchCode` ON `Attendances` (`LogSource`, `BatchCode`);

CREATE INDEX `IX_Attendance_ls_wt` ON `Attendances` (`LogSource`, `WorkDateTime`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260709161027_batchcode', '9.0.2');
 

