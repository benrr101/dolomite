/*
Post-Deployment Script
*/

PRINT N'Inserting MetadataFields...';
GO
:r ..\Metadata\MetadataFields.sql

PRINT N'Inserting Rules...';
GO
:r ..\Metadata\Rules.sql

PRINT N'Inserting Qualities...';
GO
:r ..\Metadata\Qualities.sql

PRINT N'Inserting Statuses...';
GO
:r ..\Metadata\Statuses.sql