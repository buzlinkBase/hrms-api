ALTER TABLE `WorkSchedulePlans` ADD `BatchCode` longtext CHARACTER SET utf8mb4 NOT NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260724143110_batchcode_workrotationPlan', '9.0.2');


