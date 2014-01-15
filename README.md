Dolomite
=========

Dolomite is a Windows Azure based music storage and playback service. This project runs on
Windows Azure as a worker role and exposes a RESTful HTTPS API for storing and retrieving
audio files and creating playlists. The founding philosophy of Dolomite is "You know more
about your music than anyone else. Take back control of it."

Why
---
- I got tired of Google <del>Music</del>Play having a copy of my music.
- I haven't found a music storage platform that does exactly what I want, yet.
    * Cloud storage platforms are either all-purpose (Dropbox, etc) that don't offer music library-esque
      features or hyphenated systems (Tomahawk + OwnCloud, Cloudaround) that don't process your music to provide optimal
      enjoyment across multiple devices.
    * Pure music platforms (Google Play, Spotify, Grooveshark, etc) either try manipulate your music
      to match everyone else's or only let you listen to what they have.
    * Music streaming systems (Ampache, Subsonic) will stream your music anywhere, but only from
      your home machine. What if my internet connection isn't reliable enough to stream my music?
    * Dolomite is a cloud-based music storage and management system that aims to solve these issues.
- I really like WCF and C# and want to do a project that uses it
- I'm intrigued by what all can be done with Windows Azure and wanted to explore the possibilities.
- For whatever reason, I need to prove I can write a RESTful interface. So why not use WCF? (yes, it **can** be done)

Requirements
------------
- .NET Framework v4.5
- Microsoft SQL Server
- Windows Azure SDK v2.2
- NuGet support for building solutions

License
-------
See COPYRIGHT.md file

All code here, except where otherwise indicated, is licensed under the GNU General Public License version 3.

Included with this software is a binary executable of FFMPEG built for compatibility with the GPLv3. For
more information please see http://ffmpeg.zeranoe.com/builds/
