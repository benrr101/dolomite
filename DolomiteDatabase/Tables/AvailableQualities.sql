CREATE TABLE [dbo].[AvailableQualities]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY(1,1), 
    [Track] UNIQUEIDENTIFIER NOT NULL, 
    [Quality] INT NOT NULL 
)
