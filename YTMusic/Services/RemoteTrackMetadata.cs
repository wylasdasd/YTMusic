namespace YTMusic.Services
{
    public class RemoteTrackMetadata
    {
        public const string FileName = "metadata.json";

        public string Title { get; set; } = string.Empty;

        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Cover file name relative to the track directory, e.g. cover.jpg
        /// </summary>
        public string? CoverPath { get; set; }
    }
}
