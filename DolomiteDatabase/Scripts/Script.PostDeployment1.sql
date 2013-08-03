/*
Post-Deployment Script
*/

-- Add some supported metadata fields to the database
INSERT INTO MetadataFields (TagName, DisplayName) 
VALUES 
	('Title', 'Title'), 
	('Performer', 'Artist'), 
	('AlbumArtist', 'Album Artist'),
	('Composer', 'Composer'),
	('Album', 'Album'),
	('Genre', 'Genre'),
	('Year', 'Year'),
	('Track', 'Track Number'),
	('TrackCount', 'Track Count'),
	('Disc', 'Disc Number'),
	('Lyrics', 'Lyrics'),
	('BeatsPerMinute', 'BPM'),
	('Conductor', 'Conductor'),
	('Copyright', 'Copyright');

-- Add some supported audio qualities for transcoding
INSERT INTO Qualities (Name, Codec, Bitrate, Extension, Directory) 
VALUES
	--('AAC 64Kbps', 'libfdk_aac -aprofile aac_he', 64, 'aac', 'aac_64'),
	('MP3 128Kbps', 'mp3', 128, 'mp3', 'mp3_128'),
	('MP3 160Kbps', 'mp3', 160, 'mp3', 'mp3_160'),
	('MP3 192Kbps', 'mp3', 192, 'mp3', 'mp3_192'),
	('MP3 256Kbps', 'mp3', 256, 'mp3', 'mp3_256'),
	('MP3 320Kbps', 'mp3', 320, 'mp3', 'mp3_320'),
	('Original', NULL, NULL, NULL, 'original');