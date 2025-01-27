﻿using Atom.Xml;
using Flurl.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Zune.Net.Catalog.Helpers;
using Zune.Xml.Catalog;

namespace Zune.Net.Catalog.Controllers.Music
{
    [Route("/v3.2/{culture}/music/artist/")]
    [Produces(Atom.Constants.ATOM_MIMETYPE)]
    public class ArtistController : Controller
    {
        [HttpGet, Route("")]
        public ActionResult<Feed<Artist>> Search()
        {
            if (!Request.Query.TryGetValue("q", out var queries) || queries.Count != 1)
                return BadRequest();

            return MusicBrainz.SearchArtists(queries[0], Request.Path);
        }

        [HttpGet, Route("{mbid}")]
        public async Task<ActionResult<Artist>> Details(Guid mbid)
        {
            (var dc_artist, var mb_artist) = await Discogs.GetDCArtistByMBID(mbid);
            Artist artist = MusicBrainz.MBArtistToArtist(mb_artist);
            artist.BackgroundImageId = mbid;
            artist.Images = new() { new() { Id = mbid.ToString() } };

            if (dc_artist != null)
            {
                artist.Biography = dc_artist.Value<string>("profile");
                artist.Links.Add(new(Request.Path.Value + "biography", relation: "zune://artist/biography"));
            }

            return artist;
        }

        [HttpGet, Route("{mbid}/tracks")]
        public ActionResult<Feed<Track>> Tracks(Guid mbid)
        {
            if (!Request.Query.TryGetValue("chunkSize", out var chunkSizeStrs) || chunkSizeStrs.Count != 1)
                return BadRequest();

            return MusicBrainz.GetArtistTracksByMBID(mbid, Request.Path, int.Parse(chunkSizeStrs[0]));
        }

        [HttpGet, Route("{mbid}/albums")]
        public ActionResult<Feed<Album>> Albums(Guid mbid)
        {
            return MusicBrainz.GetArtistAlbumsByMBID(mbid, Request.Path);
        }

        [HttpGet, Route("{mbid}/primaryImage")]
        public async Task<ActionResult> PrimaryImage(Guid mbid)
        {
            (var dc_artist, var mb_artist) = await Discogs.GetDCArtistByMBID(mbid);
            if (dc_artist == null)
                return StatusCode(404);

            string imgUrl = dc_artist["images"].First(i => i.Value<string>("type") == "primary").Value<string>("uri");
            var imgResponse = await imgUrl.GetAsync();
            if (imgResponse.StatusCode != 200)
                return StatusCode(imgResponse.StatusCode);
            return File(await imgResponse.GetStreamAsync(), "image/jpeg");
        }

        [HttpGet, Route("{mbid}/biography")]
        public async Task<ActionResult<Entry>> Biography(Guid mbid)
        {
            (var dc_artist, var mb_artist) = await Discogs.GetDCArtistByMBID(mbid);
            if (dc_artist == null)
                return StatusCode(404);
            DateTime updated = DateTime.Now;

            return new Entry
            {
                Id = $"tag:catalog.zune.net,1900-01-01:/music/artist/{mbid}/biography",
                Title = mb_artist.Name,
                Links = { new(Request.Path) },
                Content = Discogs.DCProfileToBiographyContent(dc_artist.Value<string>("profile")),
                Updated = updated,
            };
        }

        [HttpGet, Route("{mbid}/images")]
        public async Task<ActionResult<Feed<Image>>> Images(Guid mbid)
        {
            (var dc_artist, var mb_artist) = await Discogs.GetDCArtistByMBID(mbid);
            DateTime updated = DateTime.Now;
            int dcid = dc_artist.Value<int>("id");
            byte[] zero = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };

            Feed<Image> feed = new()
            {
                Id = $"tag:catalog.zune.net,1900-01-01:/music/artist/{mbid}/images",
                Title = mb_artist.Name,
                Links = { new(Request.Path) },
                Updated = updated,
            };

            var images = dc_artist.Value<JToken>("images");
            if (images != null)
            {
                feed.Entries = images.Select((j, idx) =>
                {
                    // Encode DCID and image index in ID
                    string imgId = new Guid(dcid, (short)idx, 0, zero).ToString();

                    return new Image
                    {
                        Id = imgId,
                        Instances = new()
                        {
                            new()
                            {
                                Id = imgId,
                                Url = j.Value<string>("uri"),
                                Format = "jpg",
                                Width = j.Value<int>("width"),
                                Height = j.Value<int>("height"),
                            }
                        }
                    };
                }).ToList();
            }

            return feed;
        }
    }
}
