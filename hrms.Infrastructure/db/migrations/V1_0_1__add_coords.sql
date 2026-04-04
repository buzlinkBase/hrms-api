START TRANSACTION;

DROP TABLE `BiometricDevices`;

DROP TABLE `BiometricTemplates`;

ALTER TABLE `Branches` ADD `Coordinates` point NULL /*!80003 SRID 4326 */;

ALTER TABLE `Attendances` ADD `Coordinates` point NULL /*!80003 SRID 4326 */;

ALTER TABLE `Areas` ADD `Coordinates` point NULL /*!80003 SRID 4326 */;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260324015939_coords', '8.0.24');

COMMIT;

