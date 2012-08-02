using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using JabbR.Models;
using Microsoft.Security.Application;

namespace JabbR.Infrastructure
{

    public class TextTransform
    {
        private readonly IJabbrRepository _repository;
        public const string HashTagPattern = @"(?:(?<=\s)|^)#([A-Za-z0-9-_]{1,30}\w*)";

        public TextTransform(IJabbrRepository repository)
        {
            _repository = repository;
        }

        public string Parse(string message)
        {
            return ConvertTextWithNewLines(ConvertHashtagsToRoomLinks(message));
        }

        private string ConvertTextWithNewLines(string message)
        {
            // If the message contains new lines wrap all of it in a pre tag
            if (message.Contains('\n'))
            {
                return String.Format(@"
<div class=""collapsible_content"">
    <h3 class=""collapsible_title"">Paste (click to show/hide)</h3>
    <div class=""collapsible_box"">
        <pre class=""multiline"">{0}</pre>
    </div>
</div>
", message);
            }

            return message;
        }
        
        // regex from http://daringfireball.net/2010/07/improved_regex_for_matching_urls
        static Regex urlPattern = new Regex(@"(?xi)
\b
(                           # Capture 1: entire matched URL
  (?:
    [a-z][\w-]+:                # URL protocol and colon
    (?:
      /{1,3}                        # 1-3 slashes
      |                             #   or
      [a-z0-9%]                     # Single letter or digit or '%'
                                    # (Trying not to match e.g. 'URI::Escape')
    )
    |                           #   or
    www\d{0,3}[.]               # 'www.', 'www1.', 'www2.' … 'www999.'
    |                           #   or
    [a-z0-9.\-]+[.][a-z]{2,4}/  # looks like domain name followed by a slash
  )
  (?:                           # One or more:
    [^\s()<>]+                      # Run of non-space, non-()<>
    |                               #   or
    \(([^\s()<>]+|(\([^\s()<>]+\)))*\)  # balanced parens, up to 2 levels
  )+
  (?:                           # End with:
    \(([^\s()<>]+|(\([^\s()<>]+\)))*\)  # balanced parens, up to 2 levels
    |                                   #   or
    [^\s`!()\[\]{};:'"".,<>?«»“”‘’]        # not a space or one of these punct chars
  )
)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string TransformAndExtractUrls(string message, out HashSet<string> extractedUrls)
        {
            var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            message = urlPattern.Replace(message, m =>
            {
                string url = HttpUtility.HtmlDecode(m.Value);

                if (!url.Contains("://"))
                {
                    url = "http://" + url;
                }

                if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    return m.Value;
                }

                urls.Add(url);

                return String.Format(CultureInfo.InvariantCulture,
                                     "<a rel=\"nofollow external\" target=\"_blank\" href=\"{0}\" title=\"{1}\">{1}</a>",
                                     url,
                                     m.Value);
            });

            extractedUrls = urls;
            return message;
        }

        public string ConvertHashtagsToRoomLinks(string message)
        {
            message = Regex.Replace(message, HashTagPattern, m =>
            {
                //hashtag without #
                string roomName = m.Groups[1].Value;

                var room = _repository.GetRoomByName(roomName);

                if (room != null)
                {
                    return String.Format(CultureInfo.InvariantCulture,
                                         "<a href=\"#/rooms/{0}\" title=\"{1}\">{1}</a>",
                                         roomName,
                                         m.Value);
                }

                return m.Value;
            });

            return message;
        }

    }
}
