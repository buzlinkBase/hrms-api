ALTER TABLE `DailyTimeRecords` ADD `ShiftId` char(36) COLLATE ascii_general_ci NULL;

CREATE INDEX `IX_Attendances_ClientId` ON `Attendances` (`ClientId`);

CREATE INDEX `IX_Attendances_OperationAreaId` ON `Attendances` (`OperationAreaId`);

ALTER TABLE `Attendances` ADD CONSTRAINT `FK_Attendances_Areas_OperationAreaId` FOREIGN KEY (`OperationAreaId`) REFERENCES `Areas` (`Id`);

ALTER TABLE `Attendances` ADD CONSTRAINT `FK_Attendances_Branches_BranchId` FOREIGN KEY (`BranchId`) REFERENCES `Branches` (`Id`);

ALTER TABLE `Attendances` ADD CONSTRAINT `FK_Attendances_Clients_ClientId` FOREIGN KEY (`ClientId`) REFERENCES `Clients` (`Id`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260727041833_add_shiftId', '9.0.2');


