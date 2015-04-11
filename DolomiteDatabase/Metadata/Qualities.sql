-- Add some supported audio qualities for transcoding
MERGE INTO Qualities AS [target]
USING (
	VALUES
	(2, 'MP3 128Kbps', 'mp3', 128, 'mp3', 'mp3_128', 'audio/mpeg'),
	(3, 'MP3 192Kbps', 'mp3', 192, 'mp3', 'mp3_192', 'audio/mpeg'),
	(4, 'MP3 320Kbps', 'mp3', 320, 'mp3', 'mp3_320', 'audio/mpeg'),
	(1, 'Original', NULL, NULL, NULL, 'original', NULL)
	) AS [source] ([Id], [Name], [Codec], [Bitrate], [Extension], [Directory], [Mimetype])
ON [source].[Id] = [target].[Id]
WHEN MATCHED THEN
	UPDATE SET 
		[target].[Name] = [source].[Name],
		[target].[Codec] = [source].[Codec],
		[target].[Bitrate] = [source].[Bitrate],
		[target].[Extension] = [source].[Extension],
		[target].[Directory] = [source].[Directory],
		[target].[Mimetype] = [source].[Mimetype]
WHEN NOT MATCHED BY TARGET THEN
	INSERT (Id, Name, Codec, Bitrate, Extension, Directory, Mimetype) 
	VALUES (Id, Name, Codec, Bitrate, Extension, Directory, Mimetype)
WHEN NOT MATCHED BY SOURCE THEN
	DELETE;

GO