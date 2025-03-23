using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources.Core;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace BingWallpaper.utilities
{

    class Common
    {
        public const string DOWNLOAD_FILE_NAME = "DownloadedImage.jpg";

        static public string EscapeXml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        static public async Task<StorageFile> DownloadImageAsync(string imageUrl)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // 发送 HTTP 请求
                    HttpResponseMessage response = await client.GetAsync(new Uri(imageUrl));
                    response.EnsureSuccessStatusCode(); // 确保请求成功

                    // 读取响应数据
                    IBuffer buffer = await response.Content.ReadAsBufferAsync();

                    // 保存图片到本地存储
                    StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(DOWNLOAD_FILE_NAME, CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteBufferAsync(file, buffer);

                    return file;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("图片下载失败: " + ex.Message);
                return null;
            }
        }

        static public async Task<StorageFile> ResizeImageAsync(StorageFile file, uint targetWidth, uint targetHeight)
        {
            try
            {
                // 打开文件流
                using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read))
                {
                    // 创建 BitmapDecoder
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(fileStream);

                    // 获取原始图片的宽度和高度
                    uint originalWidth = decoder.PixelWidth;
                    uint originalHeight = decoder.PixelHeight;

                    // 计算目标尺寸的比例
                    double widthRatio = (double)targetWidth / originalWidth;
                    double heightRatio = (double)targetHeight / originalHeight;

                    // 选择较大的比例，确保图片不会被拉伸
                    double scaleRatio = Math.Max(widthRatio, heightRatio);

                    // 计算调整后的尺寸
                    uint scaledWidth = (uint)(originalWidth * scaleRatio);
                    uint scaledHeight = (uint)(originalHeight * scaleRatio);

                    // 创建 InMemoryRandomAccessStream
                    InMemoryRandomAccessStream resizedStream = new InMemoryRandomAccessStream();

                    // 创建 BitmapEncoder
                    BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(resizedStream, decoder);

                    // 调整图片尺寸
                    encoder.BitmapTransform.ScaledWidth = scaledWidth;
                    encoder.BitmapTransform.ScaledHeight = scaledHeight;

                    // 提交更改
                    await encoder.FlushAsync();

                    // 创建裁切后的图片
                    InMemoryRandomAccessStream croppedStream = new InMemoryRandomAccessStream();
                    BitmapEncoder croppedEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, croppedStream);

                    // 从调整后的图片中裁切出目标尺寸的部分
                    BitmapTransform transform = new BitmapTransform
                    {
                        ScaledWidth = scaledWidth,
                        ScaledHeight = scaledHeight,
                        Bounds = new BitmapBounds
                        {
                            X = (scaledWidth - targetWidth) / 2,
                            Y = (scaledHeight - targetHeight) / 2,
                            Width = targetWidth,
                            Height = targetHeight
                        }
                    };

                    // 设置裁切参数
                    croppedEncoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
                    croppedEncoder.BitmapTransform.ScaledWidth = targetWidth;
                    croppedEncoder.BitmapTransform.ScaledHeight = targetHeight;

                    // 从调整后的图片中读取像素数据
                    PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Straight,
                        transform,
                        ExifOrientationMode.IgnoreExifOrientation,
                        ColorManagementMode.DoNotColorManage
                    );

                    // 将像素数据写入裁切后的图片
                    croppedEncoder.SetPixelData(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Straight,
                        targetWidth,
                        targetHeight,
                        96,
                        96,
                        pixelData.DetachPixelData()
                    );

                    // 提交裁切后的图片
                    await croppedEncoder.FlushAsync();

                    // 保存裁切后的图片
                    string resizedFileName = string.Format("ResizedImage{0}x{1}.jpg", targetWidth, targetHeight);
                    StorageFile resizedFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(resizedFileName, CreationCollisionOption.ReplaceExisting);
                    using (IRandomAccessStream resizedFileStream = await resizedFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await RandomAccessStream.CopyAsync(croppedStream, resizedFileStream);
                    }

                    return resizedFile;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("图片调整失败: " + ex.Message);
                return null;
            }
        }
        static public string GenerateFileName(string copyright)
        {
            // 获取当前日期
            string currentDate = DateTime.Now.ToString("yyyyMMdd");

            // 替换非法字符为下划线
            string safeCopyright = ReplaceInvalidFileNameChars(copyright);

            // 生成文件名
            return string.Format("Bing_{0}_{1}.jpg", currentDate, safeCopyright);
        }

        static public string ReplaceInvalidFileNameChars(string input)
        {
            // 非法字符列表
            char[] invalidChars = Path.GetInvalidFileNameChars();

            // 替换非法字符为下划线
            foreach (char invalidChar in invalidChars)
            {
                input = input.Replace(invalidChar, '_');
            }

            return input;
        }

        static public async void OpenUrl(string url)
        {
            try
            {
                // 使用系统浏览器打开 URL
                await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("打开URL失败: " + ex.Message);
            }
        }

        static public async void OpenBingSearch(string keyword)
        {
            try
            {
                // 构造必应搜索的 URL
                string searchUrl = "https://www.bing.com/search?q=" + Uri.EscapeDataString(keyword) + "&form=hpcapt&mkt=zh-cn";

                // 使用系统浏览器打开 URL
                await Windows.System.Launcher.LaunchUriAsync(new Uri(searchUrl));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("打开必应搜索失败: " + ex.Message);
            }
        }

        static public Object GetConfigValue(IPropertySet values, string key, Object default_value)
        {
            if (values.ContainsKey(key))
                return values[key];
            else
                return default_value;
        }
    }
}
