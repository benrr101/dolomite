ALTER TABLE [dbo].[AvailableQualities]
	ADD CONSTRAINT [FK_AVAILABLEQUALITIES_QUALITY]
	FOREIGN KEY (Quality)
	REFERENCES [Qualities] (Id)
	ON UPDATE CASCADE
	ON DELETE NO ACTION		-- Prevent orphaning of tracks in Azure blob storage
