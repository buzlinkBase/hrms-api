ALTER TABLE `Employees` DROP INDEX `IX_Employees_BioId`;

ALTER TABLE `Employees` MODIFY COLUMN `BioId` int NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260704014333_nullable_bio_id', '9.0.2');


