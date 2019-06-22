using System;
using System.Linq;
using System.Net;
using ITunesLibraryParser;

namespace itunesextractor
{
    class Program
    {
        static void Main(string[] args)
        {
            var folderName = "C:\\Users\\Eric\\Music\\iTunes\\iTunes Music Library.xml";
            if (args.Count() > 0)
            {
                folderName = args[0];
            }

            var library = new ITunesLibrary(folderName);

            foreach (var itunesTrack in library.Tracks.Where(t => !t.Location.Contains("http://")))
            {
                var clean = WebUtility.UrlDecode(itunesTrack.Location.Replace("file://localhost/", "").Replace("/", "\\"));
                try
                {

                    using (var mp3File = TagLib.File.Create(clean))
                    {

                        TagLib.Id3v2.Tag tag = (TagLib.Id3v2.Tag)mp3File.GetTag(TagLib.TagTypes.Id3v2, true);

                        // strip old popularity frames
                        foreach (var oldPopFrame in tag.GetFrames<TagLib.Id3v2.PopularimeterFrame>().ToArray())
                        {
                            tag.RemoveFrame(oldPopFrame);
                        }

                        var frame = TagLib.Id3v2.PopularimeterFrame.Get(tag, "Windows Media Player 9 Series", true);
                        if (itunesTrack.Rating.HasValue)
                        {
                            var stars = itunesTrack.Rating.Value / 20; // itunes sends 20 x star value
                            frame.Rating = TransformRating(stars);
                        }
                        if (itunesTrack.PlayCount.HasValue)
                        {
                            frame.PlayCount = (ulong)itunesTrack.PlayCount.Value;
                        }

                        mp3File.Save();

                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating the playcount: {ex.Message}");
                }
            }



        }

        private static byte TransformRating(int rating)
        {
            switch (rating)
            {
                case 1:
                    return 0x1;
                case 2:
                    return 0x40;// 64
                case 3:
                    return 0x80;// 128
                case 4:
                    return 0xC0;// 192
                case 5:
                    return 0xFF;// 255
                default:
                    return 0x0;// unrated/unknown
            }
        }
    }
}
