using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Windows.Web.Http;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using Windows.Storage;

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace BingWallpaper
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        List<XmlDocument> tileNotifications = new List<XmlDocument>();

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SetBingWallpaper();
        }

        private async void SetBingWallpaper()
        {
            // 清空之前的磁贴模板
            tileNotifications.Clear();

            // 获取壁纸信息
            WallpaperInfo wallpaperInfo = await GetCurrentBingWallpaper();

            // 获取磁贴更新器
            TileUpdater tileUpdater = TileUpdateManager.CreateTileUpdaterForApplication();
            // 启用队列通知
            tileUpdater.EnableNotificationQueue(true);
            // 清空之前的计划通知
            tileUpdater.Clear();

            // 下载图片
            StorageFile downloadedFile = await utilities.Common.DownloadImageAsync(wallpaperInfo.url);
            if (downloadedFile != null)
            {
                // 调整图片尺寸
                StorageFile resizedFile = await utilities.Common.ResizeImageAsync(downloadedFile, 558, 270);

                if (resizedFile != null)
                {
                    // 使用调整后的图片更新磁贴
                    string localImageUrl = "ms-appdata:///local/" + resizedFile.Name;
                    UpdateTileWithWallpaper(localImageUrl);
                    UpdateTileWallpaperAndTitle(localImageUrl, wallpaperInfo.title);
                }
            }

            try
            {
                // 异步加载壁纸
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.UriSource = new Uri(wallpaperInfo.url);
                curWallpaper.Source = bitmapImage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("加载壁纸失败: " + ex.Message);
                // 设置默认壁纸
                curWallpaper.Source = new BitmapImage(new Uri("ms-appx:///Assets/Placeholder.jpg"));
            }

            // 设置标题和版权信息
            titleTextBlock.Text = wallpaperInfo.title;
            copyrightTextBlock.Text = wallpaperInfo.copyright;

            // 更新磁贴
            UpdateTileWithText(wallpaperInfo.title, wallpaperInfo.copyright); // 显示文本的磁贴

            try
            {
                // 添加磁贴通知到队列
                foreach (var tileXml in tileNotifications)
                {
                    TileNotification tileNotification = new TileNotification(tileXml);
                    tileUpdater.Update(tileNotification);
                }
                System.Diagnostics.Debug.WriteLine("队列通知已添加！");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("队列通知失败: " + ex.Message);
            }
        }

        private async Task<WallpaperInfo> GetCurrentBingWallpaper()
        {
            WallpaperInfo wallpaperInfo = new WallpaperInfo
            {
                url = "ms-appx:///Assets/Placeholder.jpg", // 默认壁纸
                title = "今日壁纸", // 默认标题
                copyright = "无网络连接" // 默认版权信息
            };

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string apiUrl = "https://cn.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=zh-CN";
                    HttpResponseMessage response = await client.GetAsync(new Uri(apiUrl));
                    response.EnsureSuccessStatusCode(); // 确保请求成功

                    string jsonString = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(jsonString);
                    JToken imageInfo = json["images"][0];

                    wallpaperInfo.url = "https://cn.bing.com" + imageInfo["url"].ToString();
                    // 截取url中从开头到".jpg"的部分
                    int jpgIndex = wallpaperInfo.url.IndexOf(".jpg");
                    if (jpgIndex != -1)
                    {
                        wallpaperInfo.url = wallpaperInfo.url.Substring(0, jpgIndex + 4);
                    }
                    wallpaperInfo.title = imageInfo["title"].ToString();
                    wallpaperInfo.copyright = imageInfo["copyright"].ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("发生异常: " + ex.Message);
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine("内部异常: " + ex.InnerException.Message);
                }
            }

            return wallpaperInfo;
        }

        private void Image_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            // 切换标题和版权信息的可见性
            if (titleTextBlock.Visibility == Visibility.Visible)
            {
                titleTextBlock.Visibility = Visibility.Collapsed;
                copyrightTextBlock.Visibility = Visibility.Collapsed;
            }
            else
            {
                titleTextBlock.Visibility = Visibility.Visible;
                copyrightTextBlock.Visibility = Visibility.Visible;
            }
        }

        private XmlDocument CreateTileTemplate(TileTemplateType templateType, string imageUrl = null, string title = null, string copyright = null)
        {
            XmlDocument tileXml = TileUpdateManager.GetTemplateContent(templateType);

            switch (templateType)
            {
                case TileTemplateType.TileWide310x150Image:
                    IXmlNode imageNode = tileXml.ChildNodes[0].ChildNodes[0].ChildNodes[0].ChildNodes[0];
                    imageNode.Attributes[1].NodeValue = imageUrl;
                    break;

                case TileTemplateType.TileWide310x150Text09:
                    IXmlNode bindingNode = tileXml.ChildNodes[0].ChildNodes[0].ChildNodes[0];
                    IXmlNode textNode1 = bindingNode.ChildNodes[0];
                    IXmlNode textNode2 = bindingNode.ChildNodes[1];
                    textNode1.InnerText = title;
                    textNode2.InnerText = copyright;
                    break;

                case TileTemplateType.TileWide310x150ImageAndText01:
                    IXmlNode bindingNode2 = tileXml.ChildNodes[0].ChildNodes[0].ChildNodes[0];
                    IXmlNode imageNode2 = bindingNode2.ChildNodes[0];
                    IXmlNode textNode = bindingNode2.ChildNodes[1];
                    imageNode2.Attributes[1].NodeValue = imageUrl;
                    textNode.InnerText = title;
                    break;
            }

            return tileXml;
        }

        private void UpdateTileWithWallpaper(string imageUrl)
        {
            try
            {
                XmlDocument tileXml = CreateTileTemplate(TileTemplateType.TileWide310x150Image, imageUrl);
                tileNotifications.Add(tileXml);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("磁贴 XML 添加失败: " + ex.Message);
            }
        }

        private void UpdateTileWithText(string title, string copyright)
        {
            try
            {
                XmlDocument tileXml = CreateTileTemplate(TileTemplateType.TileWide310x150Text09, null, title, copyright);
                tileNotifications.Add(tileXml);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("磁贴 XML 添加失败: " + ex.Message);
            }
        }

        private void UpdateTileWallpaperAndTitle(string imageUrl, string title)
        {
            try
            {
                XmlDocument tileXml = CreateTileTemplate(TileTemplateType.TileWide310x150ImageAndText01, imageUrl, title);
                tileNotifications.Add(tileXml);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("磁贴 XML 添加失败: " + ex.Message);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SetBingWallpaper();
        }
    }

    public struct WallpaperInfo
    {
        public string url;
        public string title;
        public string copyright;
    }
}
