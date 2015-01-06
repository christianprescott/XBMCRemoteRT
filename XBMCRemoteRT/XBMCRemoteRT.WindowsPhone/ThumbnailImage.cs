using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using XBMCRemoteRT.Helpers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace XBMCRemoteRT
{
    public class ThumbnailImage : BitmapSource
    {
        private static int loadingCount = 0;

        public static readonly DependencyProperty UriSourceProperty =
            DependencyProperty.Register(
            "UriSource",
            typeof(Uri),
            typeof(ThumbnailImage),
            new PropertyMetadata(null, OnUriChanged));
        public Uri UriSource
        {
            get
            {
                return (Uri)GetValue(UriSourceProperty);
            }
            set
            {
                if (value != UriSource)
                {
                    SetValue(UriSourceProperty, value);
                }
            }
        }

        private static void OnUriChanged(DependencyObject d, DependencyPropertyChangedEventArgs a){
            ThumbnailImage sender = d as ThumbnailImage;
            if(a.Property == UriSourceProperty){
                Uri newUri = (Uri)a.NewValue;

                if (newUri == null)
                {
                    // TODO: SetSource will not take a null parameter. Find another way to clear Source.
                }
                else
                {
                    // TODO: Perform authorized get only for http(s) requests and only to Kodi web server.
                    if (new string[] { "http", "https" }.Contains(newUri.Scheme))
                    {
                        // TODO: This is on the right track, but needs better thread management
                        d.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                        {
                            using (IRandomAccessStream imageStream = await GetHttpImageStream(newUri))
                            {
                                if (imageStream != null)
                                {
                                    try
                                    {
                                        await sender.SetSourceAsync(imageStream);
                                    }
                                    catch (TaskCanceledException)
                                    {
                                        // Async task was canceled, maybe app
                                        // was suspended or too many tasks.
                                    }
                                }
                            }
                        });
                    }
                    else
                    {
                        // TODO: What's the fallback for non-http and non-Kodi paths? e.g. app contents ms-appx://
                        System.Diagnostics.Debug.WriteLine("Unsupported scheme " + newUri.Scheme);
                    }
                }
            }
        }

        private static async Task<IRandomAccessStream> GetHttpImageStream(Uri imageURI)
        {
            IRandomAccessStream imageStream = null;

            if (imageURI != null)
            {
                // Download the image with HTTP Basic auth
                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(imageURI.Scheme + "://" + imageURI.Authority);
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, imageURI.AbsolutePath);
                // TODO: Test for active connection? What are possible errors of ConnectionManager?
                if (ConnectionManager.CurrentConnection.Password != String.Empty)
                {
                    req.Headers.Authorization = new AuthenticationHeaderValue(
                        "Basic",
                        System.Convert.ToBase64String(Encoding.UTF8.GetBytes(
                            String.Format("{0}:{1}",
                            ConnectionManager.CurrentConnection.Username,
                            ConnectionManager.CurrentConnection.Password)
                        ))
                    );
                }

                System.Diagnostics.Debug.WriteLine("Starting download " + ++loadingCount + " " + imageURI.OriginalString);
                HttpResponseMessage res = await client.SendAsync(req);
                // TODO: Ensure requests are cached
                System.Diagnostics.Debug.WriteLine("Complete download " + --loadingCount + " " + imageURI.OriginalString);
                if (res.IsSuccessStatusCode)
                {
                    imageStream = (await res.Content.ReadAsStreamAsync()).AsRandomAccessStream();
                }
            }

            return imageStream;
        }
    }
}
