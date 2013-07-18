CREATE TABLE [dbo].[AvailableQualities]
(
	[Id] INT NOT NULL PRIMARY KEY, 
    [Track] INT NOT NULL, 
    [Quality] INT NOT NULL, 
    [Location] UNIQUEIDENTIFIER NOT NULL
)
