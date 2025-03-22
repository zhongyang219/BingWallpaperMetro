using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources.Core;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace BingWallpaper.utilities
{
    class Common
    {
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
                    StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync("DownloadedImage.jpg", CreationCollisionOption.ReplaceExisting);
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

        static public async Task<StorageFile> ResizeImageAsync(StorageFile file, uint width, uint height)
        {
            try
            {
                // 打开文件流
                using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read))
                {
                    // 创建 BitmapDecoder
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(fileStream);

                    // 创建 InMemoryRandomAccessStream
                    InMemoryRandomAccessStream resizedStream = new InMemoryRandomAccessStream();

                    // 创建 BitmapEncoder
                    BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(resizedStream, decoder);

                    // 调整图片分辨率
                    encoder.BitmapTransform.ScaledWidth = width;
                    encoder.BitmapTransform.ScaledHeight = height;

                    // 提交更改
                    await encoder.FlushAsync();

                    // 保存调整后的图片
                    StorageFile resizedFile = await ApplicationData.Current.LocalFolder.CreateFileAsync("ResizedImage.jpg", CreationCollisionOption.ReplaceExisting);
                    using (IRandomAccessStream resizedFileStream = await resizedFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await RandomAccessStream.CopyAsync(resizedStream, resizedFileStream);
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
    }
}
