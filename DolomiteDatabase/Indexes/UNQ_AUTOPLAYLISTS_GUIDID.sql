﻿ALTER TABLE [dbo].[Autoplaylists]
	ADD CONSTRAINT [UNQ_AUTOPLAYLISTS_GUIDID]
	UNIQUE (GuidId)