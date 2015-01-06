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
                // TODO: some validation on value
                SetValue(UriSourceProperty, value);
            }
        }
        
        public ThumbnailImage() : base()
        {
        }

        private static void OnUriChanged(DependencyObject d, DependencyPropertyChangedEventArgs a){
            ThumbnailImage sender = d as ThumbnailImage;
            Uri newUri = (Uri)a.NewValue;
            if(a.Property == UriSourceProperty){
                if (new string[] { "http", "https" }.Contains(newUri.Scheme))
                {
                    d.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        IRandomAccessStream imageStream = await GetHttpImageStream(newUri);
                        await sender.SetSourceAsync(imageStream);
                    });
                }
                else
                {
                    // TODO: What do II do with non-http paths?
                    System.Diagnostics.Debug.WriteLine("Unsupported scheme " + newUri.Scheme);
                }
            }
        }

        private static async Task<IRandomAccessStream> GetHttpImageStream(Uri imageURI)
        {
            IRandomAccessStream imageStream = null;
            // Download the image with HTTP Basic auth
            // TODO: What does HttpClientHandler do here?
            HttpClient client = new HttpClient(new HttpClientHandler());
            // TODO: Test for active connection? What are possible errors of ConnectionManager?
            client.BaseAddress = new Uri(imageURI.Scheme + "://" + imageURI.Authority);
            // TODO: Some validation on imageUri?
            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, imageURI.AbsolutePath);
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

            loadingCount++;
            System.Diagnostics.Debug.WriteLine("Starting download " + loadingCount + " " + imageURI.OriginalString);
            HttpResponseMessage res = await client.SendAsync(req);
            loadingCount--;
            System.Diagnostics.Debug.WriteLine("Complete download " + loadingCount + " " + imageURI.OriginalString);
            imageStream = (await res.Content.ReadAsStreamAsync()).AsRandomAccessStream();            
            return imageStream;
        }
    }
}
