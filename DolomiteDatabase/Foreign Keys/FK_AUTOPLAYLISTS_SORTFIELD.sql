﻿ALTER TABLE [dbo].[Autoplaylists]
	ADD CONSTRAINT [FK_AUTOPLAYLISTS_SORTFIELD]
	FOREIGN KEY (SortField)
	REFERENCES [MetadataFields] (Id);
