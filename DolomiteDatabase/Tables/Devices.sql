CREATE TABLE [dbo].[Devices]
(
    [Key]				UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, 
    [Owner]				INT NOT NULL, 
    [PreferredQuality]	INT NULL
)
