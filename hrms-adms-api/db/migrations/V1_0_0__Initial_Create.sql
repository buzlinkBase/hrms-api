CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;


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
VALUES ('20260318024631_initialcreate', '8.0.24');

COMMIT;

START TRANSACTION;

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
VALUES ('20260322122802_attendance', '8.0.24');


