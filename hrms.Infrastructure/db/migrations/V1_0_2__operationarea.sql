ALTER TABLE `Attendances` ADD `OperationAreaId` char(36) COLLATE ascii_general_ci NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260708002638_operationarea', '9.0.2');


