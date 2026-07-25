ALTER TABLE `DailyTimeRecords` RENAME COLUMN `UTHours` TO `UTMinutes`;

ALTER TABLE `DailyTimeRecords` RENAME COLUMN `OverBreakHours` TO `OverMinutes`;

ALTER TABLE `DailyTimeRecords` RENAME COLUMN `OB` TO `OBHours`;

ALTER TABLE `DailyTimeRecords` RENAME COLUMN `LateHours` TO `LateMinutes`;

ALTER TABLE `DailyTimeRecords` RENAME COLUMN `LateForOTHours` TO `LateForOTMinutes`;

ALTER TABLE `DailyTimeRecords` RENAME COLUMN `Absent` TO `WorkTypeEnum`;

ALTER TABLE `DailyTimeRecords` ADD `AbsentCount` int NOT NULL DEFAULT 0;

UPDATE `GeneralSettings` SET `Value` = 'False'
WHERE `Id` = '3456c7d8-e9f0-4567-abcd-9012345678cd';
SELECT ROW_COUNT();


UPDATE `GeneralSettings` SET `Value` = 'NoCredit'
WHERE `Id` = '4567d8e9-f012-4678-bcda-0123456789de';
SELECT ROW_COUNT();


INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260725080654_changefields_dtr_detail', '9.0.2');


