CREATE TABLE [dbo].[AvailableQualities]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY(1,1), 
    [Track] INT NOT NULL, 
    [Quality] INT NOT NULL, 
    [Location] UNIQUEIDENTIFIER NOT NULL
)
