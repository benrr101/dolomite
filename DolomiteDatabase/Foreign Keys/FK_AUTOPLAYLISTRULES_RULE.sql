﻿ALTER TABLE [dbo].[AutoplaylistRules]
	ADD CONSTRAINT [FK_AUTOPLAYLISTRULES_RULE]
	FOREIGN KEY ([Rule])
	REFERENCES [Rules] (Id)
