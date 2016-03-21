namespace DolomiteModel.PublicRepresentations
{
    public class Quality
    {
        public Quality(EntityFramework.Quality quality)
        {
            Id = quality.Id;
            Bitrate = quality.Bitrate;
            FfmpegArgs = quality.FfmpegArgs;
            Directory = quality.Directory;
            Extension = quality.Extension;
        }

        public int? Bitrate { get; set; }

        public string FfmpegArgs { get; set; }

        public string Directory { get; set; }

        public string Extension { get; set; }

        public int Id { get; set; }
    }
}
