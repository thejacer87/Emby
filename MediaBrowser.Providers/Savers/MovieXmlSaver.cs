﻿using System.Globalization;
using System.Security;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Providers.Movies;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace MediaBrowser.Providers.Savers
{
    /// <summary>
    /// Saves movie.xml for movies, trailers and music videos
    /// </summary>
    public class MovieXmlSaver : IMetadataSaver
    {
        private readonly IServerConfigurationManager _config;

        public MovieXmlSaver(IServerConfigurationManager config)
        {
            _config = config;
        }

        /// <summary>
        /// Determines whether [is enabled for] [the specified item].
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="updateType">Type of the update.</param>
        /// <returns><c>true</c> if [is enabled for] [the specified item]; otherwise, <c>false</c>.</returns>
        public bool IsEnabledFor(BaseItem item, ItemUpdateType updateType)
        {
            var wasMetadataEdited = (updateType & ItemUpdateType.MetadataEdit) == ItemUpdateType.MetadataEdit;
            var wasMetadataDownloaded = (updateType & ItemUpdateType.MetadataDownload) == ItemUpdateType.MetadataDownload;

            // If new metadata has been downloaded and save local is on, OR metadata was manually edited, proceed
            if ((_config.Configuration.SaveLocalMeta && (wasMetadataEdited || wasMetadataDownloaded)) || wasMetadataEdited)
            {
                var trailer = item as Trailer;

                // Don't support local trailers
                if (trailer != null)
                {
                    return !trailer.IsLocalTrailer;
                }

                return item is Movie || item is MusicVideo;
            }

            return false;
        }

        private static readonly CultureInfo UsCulture = new CultureInfo("en-US");
        
        /// <summary>
        /// Saves the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public void Save(BaseItem item, CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();

            builder.Append("<Title>");

            XmlSaverHelpers.AddCommonNodes(item, builder);

            if (item.CommunityRating.HasValue)
            {
                builder.Append("<IMDBrating>" + SecurityElement.Escape(item.CommunityRating.Value.ToString(UsCulture)) + "</IMDBrating>");
            }

            if (!string.IsNullOrEmpty(item.Overview))
            {
                builder.Append("<Description><![CDATA[" + item.Overview + "]]></Description>");
            }
            
            XmlSaverHelpers.AddMediaInfo((Video)item, builder);

            builder.Append("</Title>");

            var xmlFilePath = GetSavePath(item);

            XmlSaverHelpers.Save(builder, xmlFilePath, new[]
                {
                    "IMDBrating",
                    "Description"
                });

            // Set last refreshed so that the provider doesn't trigger after the file save
            MovieProviderFromXml.Current.SetLastRefreshed(item, DateTime.UtcNow);
        }

        public string GetSavePath(BaseItem item)
        {
            var video = (Video)item;

            var directory = video.VideoType == VideoType.Iso || video.VideoType == VideoType.VideoFile ? Path.GetDirectoryName(video.Path) : video.Path;

            return Path.Combine(directory, "movie.xml");
        }
    }
}
