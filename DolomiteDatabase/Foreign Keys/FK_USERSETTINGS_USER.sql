﻿ALTER TABLE [dbo].[UserSettings]
	ADD CONSTRAINT [FK_USERSETTINGS_USER]
	FOREIGN KEY ([User])
	REFERENCES [Users] (Id)