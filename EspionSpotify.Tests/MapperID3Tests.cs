﻿using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using EspionSpotify.API;
using EspionSpotify.Enums;
using EspionSpotify.Models;
using TagLib;
using Xunit;

namespace EspionSpotify.Tests
{
    public class MapperID3Tests
    {
        private const string ART_LINK = "https://raw.githubusercontent.com/jwallet/spy-spotify/master/psd/logo-en.png";
        private readonly OutputFile _currentFile;
        private readonly IFileSystem _fileSystem;
        private readonly Track _track;

        public MapperID3Tests()
        {
            _track = new Track
            {
                Artist = "Artist",
                Title = "Song"
            };

            _currentFile = new OutputFile
            {
                Extension = MediaFormat.Mp3.ToString(),
                MediaFile = _track.ToString(),
                FoldersPath = @"C:\path",
                Separator = "_"
            };

            _fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                {@"C:\path\Artist", new MockDirectoryData()},
                {_currentFile.ToMediaFilePath(), new MockFileData(new byte[] {0x12, 0x34, 0x56, 0xd2})}
            });
        }

        [Fact]
        internal async Task DefaultTrack_ReturnsNoTags()
        {
            var userSettings = new UserSettings {OrderNumberInMediaTagEnabled = false};
            var mapper = new MapperID3(_fileSystem, _currentFile.ToMediaFilePath(), _track, userSettings);

            var tags = new TagLibTab();
            await mapper.MapTags(tags);

            Assert.Equal(_track.ToString(), mapper.Track.ToString());

            Assert.Equal(_track.Title, tags.Title);
            Assert.Equal(_track.TitleExtended, tags.Subtitle);
            Assert.Equal(_track.Artist, tags.AlbumArtists.FirstOrDefault());
            Assert.Equal(_track.Artist, tags.Performers.FirstOrDefault());
            Assert.Null(tags.Album);
            Assert.Null(tags.Genres);
            Assert.Equal(0u, tags.Disc);
            Assert.Equal(0u, tags.Year);
            Assert.Null(tags.Pictures);
        }


        [Fact]
        internal async Task TrackNumber_ReturnsTrackNumberTag()
        {
            var userSettings = new UserSettings {OrderNumberInMediaTagEnabled = true, InternalOrderNumber = 2};
            var mapper = new MapperID3(_fileSystem, _currentFile.ToMediaFilePath(), _track, userSettings);

            var tags = new TagLibTab();
            await mapper.MapTags(tags);

            Assert.Equal(_track.Title, tags.Title);
            Assert.Equal(2u, tags.Track);
        }

        [Fact]
        internal async Task APITrack_WithParenthesisExtendedTitle_ReturnsPartTags()
        {
            var track = new Track
            {
                Artist = "Artist",
                Title = "Song",
                TitleExtended = "feat. Other",
                TitleExtendedSeparatorType = TitleSeparatorType.Parenthesis
            };

            var userSettings = new UserSettings {OrderNumberInMediaTagEnabled = false};
            var mapper = new MapperID3(_fileSystem, _currentFile.ToMediaFilePath(), track, userSettings);

            var tags = new TagLibTab();
            await mapper.MapTags(tags);

            Assert.Equal("Artist - Song (feat. Other)", mapper.Track.ToString());
            Assert.Equal(track.ToString(), mapper.Track.ToString());
            Assert.Equal(track.ToTitleString(), mapper.Track.ToTitleString());
        }

        [Fact]
        internal async Task APITrack_WithDashedExtendedTitle_ReturnsPartTags()
        {
            var track = new Track
            {
                Artist = "Artist",
                Title = "Song",
                TitleExtended = "Live",
                TitleExtendedSeparatorType = TitleSeparatorType.Dash
            };

            var userSettings = new UserSettings {OrderNumberInMediaTagEnabled = false};
            var mapper = new MapperID3(_fileSystem, _currentFile.ToMediaFilePath(), track, userSettings);

            var tags = new TagLibTab();
            await mapper.MapTags(tags);

            Assert.Equal("Artist - Song - Live", mapper.Track.ToString());
            Assert.Equal(track.ToString(), mapper.Track.ToString());
            Assert.Equal(track.ToTitleString(), mapper.Track.ToTitleString());
        }

        [Fact]
        internal async Task APITrack_ReturnsPartTags()
        {
            var track = new Track
            {
                Artist = "Artist",
                Title = "Song",
                TitleExtendedSeparatorType = TitleSeparatorType.None,
                AlbumPosition = 1,
                AlbumArtists = new[] {"Alpha", "Bravo", "Charlie"},
                Performers = new[] {"Delta", "Echo", "Foxtrot"},
                Album = "Golf",
                Genres = new[] {"Hotel", "India", "Juliet"},
                Disc = 1,
                Year = 2020,
                AlbumArtUrl = ART_LINK,
            };

            var userSettings = new UserSettings {OrderNumberInMediaTagEnabled = false};
            var mapper = new MapperID3(_fileSystem, _currentFile.ToMediaFilePath(), track, userSettings);

            var tags = new TagLibTab();
            await mapper.MapTags(tags);

            Assert.Equal("Alpha, Bravo, Charlie - Song", mapper.Track.ToString());
            Assert.Equal(track.ToString(), mapper.Track.ToString());
            Assert.Equal(track.ToTitleString(), mapper.Track.ToTitleString());

            Assert.Equal(track.AlbumPosition, (int?) tags.Track);
            Assert.Equal(track.Title, tags.Title);
            Assert.Equal(track.TitleExtended, tags.Subtitle);
            Assert.Equal(track.AlbumArtists, tags.AlbumArtists);
            Assert.Equal(track.Performers, tags.Performers);
            Assert.Equal(track.Album, tags.Album);
            Assert.Equal(track.Genres, tags.Genres);
            Assert.Equal((uint) track.Disc, tags.Disc);
            Assert.Equal((uint) track.Year, tags.Year);

            Assert.Single(tags.Pictures);
            Assert.Equal(track.AlbumArtImage.Length, tags.Pictures[0].Data.Count);
            Assert.Equal(PictureType.FrontCover, tags.Pictures[0].Type);
        }
    }
}