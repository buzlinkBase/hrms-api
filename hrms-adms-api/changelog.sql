CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;
ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `BiometricDevices` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `SN` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    `BranchId` char(36) COLLATE ascii_general_ci NULL,
    `ClientId` char(36) COLLATE ascii_general_ci NULL,
    `DepartmentId` char(36) COLLATE ascii_general_ci NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_BiometricDevices` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `BiometricTemplates` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `BioId` int NOT NULL,
    `BioType` int NOT NULL,
    `BioIndex` int NOT NULL,
    `TemplateSize` int NOT NULL,
    `TemplateData` text CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_BiometricTemplates` PRIMARY KEY (`Id`)
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

CREATE INDEX `IX_InboxState_Delivered` ON `InboxState` (`Delivered`);

CREATE INDEX `IX_OutboxMessage_EnqueueTime` ON `OutboxMessage` (`EnqueueTime`);

CREATE INDEX `IX_OutboxMessage_ExpirationTime` ON `OutboxMessage` (`ExpirationTime`);

CREATE UNIQUE INDEX `IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber` ON `OutboxMessage` (`InboxMessageId`, `InboxConsumerId`, `SequenceNumber`);

CREATE UNIQUE INDEX `IX_OutboxMessage_OutboxId_SequenceNumber` ON `OutboxMessage` (`OutboxId`, `SequenceNumber`);

CREATE INDEX `IX_OutboxState_Created` ON `OutboxState` (`Created`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260318024631_initialcreate', '9.0.2');

CREATE TABLE `Attendances` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `BioId` int NOT NULL,
    `WorkDateTime` datetime(6) NOT NULL,
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    `BranchId` char(36) COLLATE ascii_general_ci NULL,
    `ClientId` char(36) COLLATE ascii_general_ci NULL,
    `DepartmentId` char(36) COLLATE ascii_general_ci NULL,
    `DeviceName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Attendances` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260322122802_attendance', '9.0.2');

ALTER TABLE `BiometricTemplates` DROP COLUMN `Status`;

ALTER TABLE `Attendances` DROP COLUMN `Status`;

ALTER TABLE `BiometricTemplates` ADD `TenantId` char(36) COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE `Attendances` ADD `BatchId` char(36) COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE `Attendances` ADD `Synced` tinyint(1) NOT NULL DEFAULT FALSE;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260404041325_att', '9.0.2');

ALTER TABLE `BiometricDevices` ADD `DeviceName` longtext CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `BiometricDevices` ADD `FwVersion` longtext CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `BiometricDevices` ADD `IpAddress` longtext CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `BiometricDevices` ADD `LanguageCode` int NOT NULL DEFAULT 0;

ALTER TABLE `BiometricDevices` ADD `MacAddress` longtext CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `BiometricDevices` ADD `OemVendor` longtext CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `BiometricDevices` ADD `Platform` longtext CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `BiometricDevices` ADD `PushVersion` longtext CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `BiometricDevices` ADD `RegDeviceType` longtext CHARACTER SET utf8mb4 NULL;

CREATE TABLE `BiometricDetails` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `SN` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Type` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Enabled` tinyint(1) NOT NULL,
    `Version` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Count` int NOT NULL,
    `MaxCount` int NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_BiometricDetails` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Biometrics` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `SN` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_Biometrics` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `MultiBioSupports` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `SN` longtext CHARACTER SET utf8mb4 NOT NULL,
    `DataSupport` longtext CHARACTER SET utf8mb4 NOT NULL,
    `PhotoSupport` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_MultiBioSupports` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `PhotosAndMedias` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `SN` longtext CHARACTER SET utf8mb4 NOT NULL,
    `PhotoFunctionEnabled` tinyint(1) NOT NULL,
    `UserPicUrlFunctionEnabled` tinyint(1) NOT NULL,
    `MaxUserPhotoCount` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_PhotosAndMedias` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `QrCodeConfigs` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `SN` longtext CHARACTER SET utf8mb4 NOT NULL,
    `IsSupported` tinyint(1) NULL,
    `Enabled` tinyint(1) NULL,
    `DecryptFunList` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_QrCodeConfigs` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `SystemCounters` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `SN` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TransactionCount` int NOT NULL,
    `MaxAttLogCount` int NOT NULL,
    `UserCount` int NOT NULL,
    `MaxUserCount` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_SystemCounters` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `ThermalAndMaskConfigs` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `SN` longtext CHARACTER SET utf8mb4 NOT NULL,
    `IrTempDetectionFunOn` tinyint(1) NULL,
    `MaskDetectionFunOn` tinyint(1) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_ThermalAndMaskConfigs` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `FeaturesAndProtocols` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `SN` longtext CHARACTER SET utf8mb4 NOT NULL,
    `VisilightFun` tinyint(1) NOT NULL,
    `VisualIntercomFunOn` tinyint(1) NULL,
    `VideoTid` longtext CHARACTER SET utf8mb4 NULL,
    `VideoProtocol` longtext CHARACTER SET utf8mb4 NULL,
    `SubcontractingUpgradeFunOn` tinyint(1) NOT NULL,
    `QrCodeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ThermalAndMaskId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ConfigSupportId` char(36) COLLATE ascii_general_ci NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_FeaturesAndProtocols` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_FeaturesAndProtocols_MultiBioSupports_ConfigSupportId` FOREIGN KEY (`ConfigSupportId`) REFERENCES `MultiBioSupports` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_FeaturesAndProtocols_QrCodeConfigs_QrCodeId` FOREIGN KEY (`QrCodeId`) REFERENCES `QrCodeConfigs` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_FeaturesAndProtocols_ThermalAndMaskConfigs_ThermalAndMaskId` FOREIGN KEY (`ThermalAndMaskId`) REFERENCES `ThermalAndMaskConfigs` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_FeaturesAndProtocols_ConfigSupportId` ON `FeaturesAndProtocols` (`ConfigSupportId`);

CREATE INDEX `IX_FeaturesAndProtocols_QrCodeId` ON `FeaturesAndProtocols` (`QrCodeId`);

CREATE INDEX `IX_FeaturesAndProtocols_ThermalAndMaskId` ON `FeaturesAndProtocols` (`ThermalAndMaskId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260517043654_deviceinfos', '9.0.2');

ALTER TABLE `BiometricTemplates` ADD `SN` longtext CHARACTER SET utf8mb4 NOT NULL;

CREATE TABLE `DeviceCommands` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `SN` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CommandType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Commands` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_DeviceCommands` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260523023557_commandtable', '9.0.2');

CREATE TABLE `DeviceUsers` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `SN` longtext CHARACTER SET utf8mb4 NOT NULL,
    `UserPin` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Priority` int NOT NULL,
    `Password` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Card` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `DeletedAt` datetime(6) NULL,
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_DeviceUsers` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260604114157_deviceuser', '9.0.2');

ALTER TABLE `BiometricTemplates` MODIFY COLUMN `TemplateData` longtext CHARACTER SET utf8mb4 NOT NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260607040023_changetoLongtext', '9.0.2');

COMMIT;

