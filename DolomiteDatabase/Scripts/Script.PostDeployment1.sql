/*
Post-Deployment Script
*/

-- Add some supported metadata fields to the database
INSERT INTO MetadataFields ([TagName], [DisplayName], [Type]) 
VALUES 
	('Title', 'Title', 'string'), 
	('Performer', 'Artist', 'string'), 
	('AlbumArtist', 'Album Artist', 'string'),
	('Composer', 'Composer', 'string'),
	('Album', 'Album', 'string'),
	('Genre', 'Genre', 'string'),
	('Year', 'Year', 'numeric'),
	('Track', 'Track Number', 'numeric'),
	('TrackCount', 'Track Count', 'numeric'),
	('Disc', 'Disc Number', 'numeric'),
	('Lyrics', 'Lyrics', 'string'),
	('BeatsPerMinute', 'BPM', 'numeric'),
	('Conductor', 'Conductor', 'string'),
	('Copyright', 'Copyright', 'string'),
	('Comment', 'Comment', 'string'),
	('DiscCount', 'Disc Count', 'numeric'),
	('DateAdded', 'Date Added', 'date'),
	('PlayCount', 'Play Count', 'numeric'),
	('LastPlayed', 'Last Played', 'date'),
	('Duration', 'Duration', 'numeric');

-- Add some supported audio qualities for transcoding
INSERT INTO Qualities (Name, Codec, Bitrate, Extension, Directory, Mimetype) 
VALUES
	--('AAC 64Kbps', 'libfdk_aac -aprofile aac_he', 64, 'aac', 'aac_64'),
	('MP3 128Kbps', 'mp3', 128, 'mp3', 'mp3_128', 'audio/mpeg'),
	('MP3 160Kbps', 'mp3', 160, 'mp3', 'mp3_160', 'audio/mpeg'),
	('MP3 192Kbps', 'mp3', 192, 'mp3', 'mp3_192', 'audio/mpeg'),
	('MP3 256Kbps', 'mp3', 256, 'mp3', 'mp3_256', 'audio/mpeg'),
	('MP3 320Kbps', 'mp3', 320, 'mp3', 'mp3_320', 'audio/mpeg'),
	('Original', NULL, NULL, NULL, 'original', NULL);
GO

-- Add important error messages
EXEC sp_addmessage 50010, 13, 
   N'The track that has been requested is currently locked.';
GO