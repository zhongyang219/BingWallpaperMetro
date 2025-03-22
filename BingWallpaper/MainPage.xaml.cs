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

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace BingWallpaper
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
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
            // 获取壁纸信息
            WallpaperInfo wallpaperInfo = await GetCurrentBingWallpaper();

            // 异步加载壁纸
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.UriSource = new Uri(wallpaperInfo.url);
            curWallpaper.Source = bitmapImage;

            // 设置标题和版权信息
            titleTextBlock.Text = wallpaperInfo.title;
            copyrightTextBlock.Text = wallpaperInfo.copyright;

            // 更新磁贴
            UpdateTileWithWallpaper(wallpaperInfo.url); // 显示壁纸的磁贴
            UpdateTileWithText(wallpaperInfo.title, wallpaperInfo.copyright); // 显示文本的磁贴
        }

        private async Task<WallpaperInfo> GetCurrentBingWallpaper()
        {
            WallpaperInfo wallpaperInfo = new WallpaperInfo();

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

        private void UpdateTileWithWallpaper(string imageUrl)
        {
            try
            {
                // 转义特殊字符
                string escapedImageUrl = utilities.Common.EscapeXml(imageUrl);

                // 获取磁贴模板
                XmlDocument tileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileWide310x150Image);
                IXmlNode imageNode = tileXml.ChildNodes[0].ChildNodes[0].ChildNodes[0].ChildNodes[0];
                // 设置图片url
                imageNode.Attributes[1].NodeValue = escapedImageUrl;

                // 打印 XML
                string tileXmlString = tileXml.GetXml();
                System.Diagnostics.Debug.WriteLine("磁贴 XML: " + tileXmlString);

                // 创建磁贴通知
                TileNotification tileNotification = new TileNotification(tileXml);

                // 更新磁贴
                TileUpdateManager.CreateTileUpdaterForApplication().Update(tileNotification);
                System.Diagnostics.Debug.WriteLine("磁贴更新成功！");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("磁贴更新失败: " + ex.Message);
            }
        }

        private void UpdateTileWithText(string title, string copyright)
        {
            try
            {
                // 转义特殊字符
                string escapedTitle = utilities.Common.EscapeXml(title);
                string escapedCopyright = utilities.Common.EscapeXml(copyright);

                // 获取磁贴模板
                XmlDocument tileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileWide310x150Text09);
                IXmlNode bindingNode = tileXml.ChildNodes[0].ChildNodes[0].ChildNodes[0];
                IXmlNode textNode1 = bindingNode.ChildNodes[0];
                IXmlNode textNode2 = bindingNode.ChildNodes[1];
                // 设置text节点
                textNode1.InnerText = escapedTitle;
                textNode2.InnerText = escapedCopyright;

                // 打印 XML
                string tileXmlString = tileXml.GetXml();
                System.Diagnostics.Debug.WriteLine("磁贴 XML: " + tileXmlString);

                // 创建磁贴通知
                TileNotification tileNotification = new TileNotification(tileXml);

                // 更新磁贴
                TileUpdateManager.CreateTileUpdaterForApplication().Update(tileNotification);
                System.Diagnostics.Debug.WriteLine("磁贴更新成功！");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("磁贴更新失败: " + ex.Message);
            }
        }
    }

    public struct WallpaperInfo
    {
        public string url;
        public string title;
        public string copyright;
    }
}
