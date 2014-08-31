CREATE TABLE [dbo].[UserSettings]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY(1,1),
	[User] INT NOT NULL,
	[ApiKey] INT NOT NULL,
	[SettingsCollection] NVARCHAR(MAX) NOT NULL 
)
