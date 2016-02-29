-- Add some supported audio qualities for transcoding
MERGE INTO Qualities AS [target]
USING (
	VALUES
	-- (2, 'MP3 128Kbps',	128, '-c:a libmp3lame -ab 128000',	'mp3', 'mp3_128',	'audio/mpeg'),		-- Supported in IE/Safari/VLC plugin
	   (3, 'MP3 192Kbps',	192, '-c:a libmp3lame -ab 192000',	'mp3', 'mp3_192',	'audio/mpeg'),		-- Supported in IE/Safari/VLC plugin
	-- (4, 'MP3 320Kbps',	320, '-c:a libmp3lame -ab 320000',	'mp3', 'mp3_320',	'audio/mpeg'),		-- Supported in IE/Safari/VLC plugin
	   (5, 'OGG/Vorbis 2',	96,	 '-c:libvorbis -q:a 2',			'ogg', 'ogg_4',		'audio/ogg'),		-- Supported in FireFox/Chrome
	   (6, 'OGG/Vorbis 4',	128, '-c:libvorbis -q:a 4',			'ogg', 'ogg_6',		'audio/ogg'),		-- Supported in FireFox/Chrome
	   (7, 'OGG/Vorbis 6',	192, '-c:libvorbis -q:a 6',			'ogg', 'ogg_8',		'audio/ogg'),		-- Supported in FireFox/Chrome
	   (1, 'Original',		NULL, NULL,							NULL,	'original', NULL)				-- Required
	) AS [source] ([Id], [Name], [Bitrate], [FfmpegArgs], [Extension], [Directory], [Mimetype])
ON [source].[Id] = [target].[Id]
WHEN MATCHED THEN
	UPDATE SET 
		[target].[Name] = [source].[Name],
		[target].[Bitrate] = [source].[Bitrate],
		[target].[FfmpegArgs] = [source].[FfmpegArgs],
		[target].[Extension] = [source].[Extension],
		[target].[Directory] = [source].[Directory],
		[target].[Mimetype] = [source].[Mimetype]
WHEN NOT MATCHED BY TARGET THEN
	INSERT (Id, Name, Bitrate, FfmpegArgs, Extension, Directory, Mimetype) 
	VALUES (Id, Name, Bitrate, FfmpegArgs, Extension, Directory, Mimetype)
WHEN NOT MATCHED BY SOURCE THEN
	DELETE;

GO