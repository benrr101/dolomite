CREATE TABLE [dbo].[UserSettings]
(
	[User] INT NOT NULL PRIMARY KEY,
	[SettingsSerialized] NVARCHAR(MAX)
)
