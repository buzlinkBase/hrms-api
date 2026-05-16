-- ALTER TABLE `Branches` DROP COLUMN IF EXISTS `Coordinates`;
-- ALTER TABLE `Attendances` DROP COLUMN IF EXISTS  `Coordinates`;
-- ALTER TABLE `Areas` DROP COLUMN IF EXISTS  `Coordinates`;

ALTER TABLE `Branches` ADD `Boundary` geometry NULL /*!80003 SRID 4326 */;
ALTER TABLE `Attendances` ADD `Boundary` geometry NULL /*!80003 SRID 4326 */;
ALTER TABLE `Areas` ADD `Boundary` geometry NULL /*!80003 SRID 4326 */;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260516044629_changetopolygon', '9.0.2');


