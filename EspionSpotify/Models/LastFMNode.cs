﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using EspionSpotify.Enums;
using EspionSpotify.Extensions;

namespace EspionSpotify.Models
{
    [XmlRoot(ElementName = "streamable")]
    public class Streamable
    {
        [XmlAttribute(AttributeName = "fulltrack")]
        public string Fulltrack { get; set; }

        [XmlText] public string Value { get; set; }
    }

    [XmlRoot(ElementName = "artist")]
    public class Artist
    {
        [XmlElement(ElementName = "name")] public string Name { get; set; }

        [XmlElement(ElementName = "mbid")] public string Mbid { get; set; }

        [XmlElement(ElementName = "url")] public string Url { get; set; }
    }

    [XmlRoot(ElementName = "image")]
    public class Image
    {
        [XmlAttribute(AttributeName = "size")] public string Size { get; set; }

        [XmlText] public string Url { get; set; }

        public AlbumCoverSize? CoverSize => Size.ToAlbumCoverSize();
    }

    [XmlRoot(ElementName = "album")]
    public class Album
    {
        [XmlElement(ElementName = "artist")] public string Artist { get; set; }

        [XmlElement(ElementName = "title")] public string Title { get; set; }

        [XmlElement(ElementName = "mbid")] public string Mbid { get; set; }

        [XmlElement(ElementName = "url")] public string Url { get; set; }

        [XmlElement(ElementName = "image")] public List<Image> Image { get; set; }

        [XmlAttribute(AttributeName = "position")]
        public string Position { get; set; }

        public string AlbumTitle => Title;
        public int? TrackPosition => Position == null ? (int?) null : Convert.ToInt32(Position);

        public string ExtraLargeCoverUrl => Image.FirstOrDefault(x => x?.CoverSize == AlbumCoverSize.extralarge)?.Url;
        public string LargeCoverUrl => Image.FirstOrDefault(x => x?.CoverSize == AlbumCoverSize.large)?.Url;
        public string MediumCoverUrl => Image.FirstOrDefault(x => x?.CoverSize == AlbumCoverSize.medium)?.Url;
        public string SmallCoverUrl => Image.FirstOrDefault(x => x?.CoverSize == AlbumCoverSize.small)?.Url;

        public void FromSingleAlbum(LastFMAlbum album)
        {
            Image = album.Image;
            Title = album.Name;
            Position = "1";
            Artist = album.Artist;
            Mbid = album.Mbid;
            Url = album.Url;
        }
    }

    [XmlRoot(ElementName = "tag")]
    public class Tag
    {
        [XmlElement(ElementName = "name")] public string Name { get; set; }

        [XmlElement(ElementName = "url")] public string Url { get; set; }
    }

    [XmlRoot(ElementName = "toptags")]
    public class Toptags
    {
        [XmlElement(ElementName = "tag")] public List<Tag> Tag { get; set; }
    }

    [XmlRoot(ElementName = "track")]
    public class LastFMTrack
    {
        [XmlElement(ElementName = "name")]
        // song title
        public string Name { get; set; }

        [XmlElement(ElementName = "mbid")] public string Mbid { get; set; }

        [XmlElement(ElementName = "url")] public string Url { get; set; }

        [XmlElement(ElementName = "duration")] public int? Duration { get; set; }

        [XmlElement(ElementName = "streamable")]
        public Streamable Streamable { get; set; }

        [XmlElement(ElementName = "listeners")]
        public string Listeners { get; set; }

        [XmlElement(ElementName = "playcount")]
        public string Playcount { get; set; }

        [XmlElement(ElementName = "artist")] public Artist Artist { get; set; }

        [XmlElement(ElementName = "album")] public Album Album { get; set; }

        [XmlElement(ElementName = "toptags")] public Toptags Toptags { get; set; }
        
        [XmlElement(ElementName = "image")] public List<Image> Image { get; set; }
    }

    [XmlRoot(ElementName = "album")]
    public class LastFMAlbum
    {
        [XmlElement(ElementName = "name")]
        // album title
        public string Name { get; set; }
        
        [XmlElement(ElementName = "artist")]
        public string Artist { get; set; }
        
        [XmlElement(ElementName = "url")] public string Url { get; set; }
        
        [XmlElement(ElementName = "mbid")] public string Mbid { get; set; }

        [XmlElement(ElementName = "listeners")]
        public string Listeners { get; set; }

        [XmlElement(ElementName = "playcount")]
        public string Playcount { get; set; }
        
        [XmlElement(ElementName = "tags")] public List<Tag> Tags { get; set; }
        
        [XmlElement(ElementName = "image")] public List<Image> Image { get; set; }
        
        public string AlbumTitle => Name;
        public string ExtraLargeCoverUrl => Image.FirstOrDefault(x => x?.CoverSize == AlbumCoverSize.extralarge)?.Url;
        public string LargeCoverUrl => Image.FirstOrDefault(x => x?.CoverSize == AlbumCoverSize.large)?.Url;
        public string MediumCoverUrl => Image.FirstOrDefault(x => x?.CoverSize == AlbumCoverSize.medium)?.Url;
        public string SmallCoverUrl => Image.FirstOrDefault(x => x?.CoverSize == AlbumCoverSize.small)?.Url;
    }

    [XmlRoot(ElementName = "error")]
    public class Error
    {
        [XmlText] public string Message { get; set; }

        [XmlAttribute(AttributeName = "code")] public string Code { get; set; }
    }

    [Serializable]
    [XmlRoot(ElementName = "lfm")]
    public class LastFMNode
    {
        [XmlAttribute(AttributeName = "status")]
        public string StatusMessage { get; set; }

        [XmlElement(ElementName = "track")] public LastFMTrack Track { get; set; }
        
        [XmlElement(ElementName = "album")] public LastFMAlbum Album { get; set; }

        [XmlElement(ElementName = "error")] public Error Error { get; set; }

        public LastFMNodeStatus? Status => StatusMessage.ToLastFMNodeStatus();
    }
}