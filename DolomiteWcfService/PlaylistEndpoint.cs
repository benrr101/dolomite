using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace DolomiteWcfService
{
    class PlaylistEndpoint : IPlaylistEndpoint
    {
        public Message CreatePlaylist(Stream body)
        {
            throw new NotImplementedException();
        }

        public Message GetAllPlaylists()
        {
            throw new NotImplementedException();
        }

        public Message GetPlaylist(string guid)
        {
            throw new NotImplementedException();
        }

        public Message AddToPlaylist(Stream body, string guid)
        {
            throw new NotImplementedException();
        }

        public Message DeleteFromPlaylist(string guid, string id)
        {
            throw new NotImplementedException();
        }
    }
}
