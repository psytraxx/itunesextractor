﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ITunesLibraryParser;
using ShellProgressBar;
using TagLib;
using System.Threading.Tasks;

namespace itunesextractor
{
    class Program
    {
        static void Main(string[] args)
        {
            var folderName = "C:\\Users\\Eric\\Music\\iTunes\\iTunes Music Library.xml";
            if (args.Length > 0)
            {
                folderName = args[0];
            }

            var options = new ProgressBarOptions
            {
                ProgressCharacter = '─',
                ProgressBarOnBottom = true
            };

            var library = new ITunesLibrary(folderName);

            // ignore streams (http://) for tracking
            var tracks = library.Tracks.Where(t => !t.Location.Contains("http://")).ToList();

            using (var pbar = new ProgressBar(tracks.Count(), "Initial message", options))
            {
                Parallel.ForEach(tracks, (itunesTrack) =>
                {
                    // create a windows path from the itunes location
                    var fileName = WebUtility.UrlDecode(itunesTrack.Location.Replace("+", "%2b")).Replace("file://localhost/", "").Replace("/", "\\");
                    try
                    {
                        TagLib.Id3v2.Tag.DefaultVersion = 4;
                        TagLib.Id3v2.Tag.ForceDefaultVersion = true;

                        using var mp3File = TagLib.File.Create(fileName);
                        TagLib.Id3v2.Tag tag = (TagLib.Id3v2.Tag)mp3File.GetTag(TagLib.TagTypes.Id3v2, true);

                        // strip old popularity frames
                        foreach (var oldPopFrame in tag.GetFrames<TagLib.Id3v2.PopularimeterFrame>().ToArray())
                        {
                            tag.RemoveFrame(oldPopFrame);
                        }

                        //tag.AlbumArtists = new[] { itunesTrack.AlbumArtist };
                        //tag.Performers = new[] { itunesTrack.Artist };
                        //tag.Genres = new[] { itunesTrack.Genre };
                        //tag.IsCompilation = itunesTrack.PartOfCompilation;

                        //RemoveLyrics(mp3File);

                        var frame = TagLib.Id3v2.PopularimeterFrame.Get(tag, "Windows Media Player 9 Series", true);
                        if (itunesTrack.Rating.HasValue)
                        {
                            var stars = itunesTrack.Rating.Value / 20; // itunes sends 20 x star value
                            frame.Rating = TransformRating(stars);
                        }
                        if (itunesTrack.PlayCount.HasValue && (ulong)itunesTrack.PlayCount.Value > frame.PlayCount)
                        {
                            frame.PlayCount = (ulong)itunesTrack.PlayCount.Value;
                        }

                        //only keep existing tags in file
                        var removeTags = mp3File.TagTypes & ~mp3File.TagTypesOnDisk;
                        mp3File.RemoveTags(removeTags);
                        mp3File.Save();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error updating the playcount: {ex.Message}");
                    }

                    pbar.Tick($"updating {fileName}");
                });
            }
        }

        private static void RemoveLyrics(TagLib.File mp3File)
        {
            var maxLength = 512;
            ByteVector initVector = new ByteVector(Encoding.UTF8.GetBytes("LYRICSBEGIN"));
            long initOffset = mp3File.Find(initVector, startPosition: 0);

            if ((initOffset != -1))
            {

                // The Lyrics3 block can end with one of these two markups, so we need to evaluate both.
                foreach (string str in new[] { "LYRICS200", "LYRICSEND" })
                {
                    ByteVector endVector = new ByteVector(Encoding.UTF8.GetBytes(str));
                    long endOffset = mp3File.Find(endVector, startPosition: initOffset);

                    if (endOffset != -1)
                    {
                        int length = System.Convert.ToInt32(endOffset - initOffset) + (str.Length);
                        if ((length < maxLength))
                        {
                            try
                            {
                                mp3File.Seek(initOffset, SeekOrigin.Begin);
                                // Dim raw As String = Me.mp3File.ReadBlock(length).ToString()
                                mp3File.RemoveBlock(initOffset, length);
                                Console.WriteLine($"Removed lyrics in {mp3File.Name}");
                                return;
                            }
                            catch (Exception)
                            {
                                throw;
                            }

                            finally
                            {
                                mp3File.Seek(0, SeekOrigin.Begin);
                            }
                        }
                        else
                            // We can handle it or continue...
                            continue;
                    }
                }
            }
        }


        private static byte TransformRating(int rating)
        {
            return rating switch
            {
                1 => 0x1,
                2 => 0x40,// 64
                3 => 0x80,// 128
                4 => 0xC0,// 192
                5 => 0xFF,// 255
                _ => 0x0,// unrated/unknown
            };
        }
    }
}
