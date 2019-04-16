using System;
using System.Collections.Generic;
using System.Linq;
using Gdk;
using GLib;
using Gtk;
using Application = Gtk.Application;
using Thread = System.Threading.Thread;

namespace ImageProcessor2
{
    public class Channel<Pixel> where Pixel: struct
    {
        public Pixel[,] Data { get; }
        public ChannelType Tag { get; }
        public int Width => Data.GetLength(1);
        public int Height => Data.GetLength(2);

        public Channel(ChannelType tag, int width, int height)
        {
            Tag = tag;
            Data = new Pixel[height, width];
        }
    }

    public enum ChannelType
    {
        Red, Green, Blue, Alpha, Gray
    }
    
    public class Image<TPixel> where TPixel : struct
    {
        public Channel<TPixel>[] Channels { get; }
        public int Width { get; }
        public int Height { get; }

        public Image(int width, int height, params ChannelType[] channels)
        {
            Channels = new Channel<TPixel>[channels.Length];
            for (int i = 0; i < channels.Length; i++)
            {
                Channels[i] = new Channel<TPixel>(channels[i], width, height);
            }

            Width = width;
            Height = height;
        }

        public Channel<TPixel> this[ChannelType type] => Channels.FirstOrDefault(channel => channel.Tag == type);

        public static Image<TPixel> Rgb(int width, int height)
        {
            return new Image<TPixel>(width, height, ChannelType.Red, ChannelType.Green, ChannelType.Blue);
        }

        public Image<TPixel> SubImage(int x, int y, int width, int height)
        {
            Image<TPixel> subImage = new Image<TPixel>(width, height, Channels.Select(channel => channel.Tag).ToArray());
            foreach (var channel in Channels)
            {
                TPixel[,] newData = subImage[channel.Tag].Data;
                TPixel[,] oldData = channel.Data;
                for (int i = 0; i < height; i++)
                {
                    for (int j = 0; j < width; j++)
                    {
                        try
                        {
                            newData[i, j] 
                                = oldData[y + i, x + j];
                        }
                        catch (IndexOutOfRangeException e)
                        {
                            Console.WriteLine(e);
                            throw;
                        }
                    }
                }
            }

            return subImage;
        }

        public void Paste(int x, int y, Image<TPixel> toPaste)
        {
            foreach (Channel<TPixel> channel in toPaste.Channels)
            {
                TPixel[,] sourceData = channel.Data;
                TPixel[,] targetData = this[channel.Tag]?.Data;

                if (targetData != null)
                {
                    for (int i = 0; i < toPaste.Height && i + y < Height; i++)
                    {
                        for (int j = 0; j < toPaste.Width && j + x < Width; j++)
                        {
                            try
                            {
                                targetData[i + y, j + x] = sourceData[i, j];
                            }
                            catch (IndexOutOfRangeException e)
                            {
                                Console.WriteLine(e);
                                throw;
                            }
                        }
                    }
                }
            }
        }

        public Image<TPixel> Copy(int width, int height)
        {
            Image<TPixel> newImage = new Image<TPixel>(width, height, Channels.Select(channel => channel.Tag).ToArray());
            foreach (Channel<TPixel> channel in Channels)
            {
                TPixel[,] oldData = channel.Data;
                TPixel[,] newData = newImage[channel.Tag].Data;
                for (int i = 0; i < Height && i < height; i++)
                {
                    for (int j = 0; j < Width && i < width; j++)
                    {
                        newData[i, j] = oldData[i, j];
                    }
                }
            }

            return newImage;
        }
    }

    public static class Images
    {
        public static Image<byte> Rgb2Gray(this Image<byte> image)
        {
            byte[,] red = image[ChannelType.Red].Data;
            byte[,] green = image[ChannelType.Green].Data;
            byte[,] blue = image[ChannelType.Blue].Data;
            
            Image<byte> grayImage = new Image<byte>(image.Width, image.Height, ChannelType.Gray);
            byte[,] grayChannel = grayImage[ChannelType.Gray].Data;
            for (int i = 0; i < image.Height;i++)
            {
                for (int j = 0; j < image.Width; j++)
                {
                    int gray = (red[i, j] + green[i, j] + blue[i, j]) / 3;
                    grayChannel[i, j] = (byte)gray;
                }
            }

            return grayImage;
        }

        public static Image<byte> Threshold(this Image<byte> image)
        {
            Image<byte> newImage = new Image<byte>(image.Width, image.Height, image.Channels.Select(channel => channel.Tag).ToArray());
            foreach (Channel<byte> channel in image.Channels)
            {
                byte[,] oldData = channel.Data;
                long sum = 0;
                for (int i = 0; i < image.Height; i++)
                {
                    for (int j = 0; j < image.Width; j++)
                    {
                        sum += oldData[i, j];
                    }
                }

                byte average = (byte) (sum / (image.Height * image.Width));
                byte[,] newData = newImage[channel.Tag].Data;
                for (int i = 0; i < image.Height; i++)
                {
                    for (int j = 0; j < image.Width; j++)
                    {
                        newData[i, j] = oldData[i, j] >= average ? (byte)0xFF : (byte)0x0;
                    }
                }
            }

            return newImage;
        }

        public static Image<byte> FromFile(string filename)
        {
            Pixbuf pixbuf = new Pixbuf(filename);
            return FromPixbuf(pixbuf);
        }

        public static Image<byte> FromPixbuf(Pixbuf pixbuf)
        {
            if (pixbuf.Colorspace == Colorspace.Rgb)
            {
                if (!pixbuf.HasAlpha)
                {
                    Image<byte> image = new Image<byte>(pixbuf.Width, pixbuf.Height, 
                        ChannelType.Red, ChannelType.Green, ChannelType.Blue);
                    byte[] bytes = pixbuf.PixelBytes.Data;
                    int rowStride = pixbuf.Rowstride;
                    var red = image[ChannelType.Red].Data;
                    var green = image[ChannelType.Green].Data;
                    var blue = image[ChannelType.Blue].Data;
                    for (int i = 0; i < pixbuf.Height; i++)
                    {
                        for (int j = 0; j < pixbuf.Width; j++)
                        {
                            red[i, j] = bytes[i * rowStride + j * 3];
                            green[i, j] = bytes[i * rowStride + j * 3 + 1];
                            blue[i, j] = bytes[i * rowStride + j * 3 + 2];
                        }
                    }

                    return image;
                }
            }

            return null;
        }

        public static void ShowRgb(this Image<byte> image, string title)
        {
            byte[,] red = image[ChannelType.Red].Data;
            byte[,] green = image[ChannelType.Green].Data;
            byte[,] blue = image[ChannelType.Blue].Data;
            byte[] bytes = new byte[3 * image.Width * image.Height];
            int ptr = 0;
            for (int i = 0; i < image.Height; i++)
            {
                for (int j = 0; j < image.Width; j++)
                {
                    bytes[ptr++] = red[i, j];
                    bytes[ptr++] = green[i, j];
                    bytes[ptr++] = blue[i, j];
                }
            }
            Application.Init();
            Pixbuf pixbuf = new Pixbuf(bytes, Colorspace.Rgb, false, 8, 
                image.Width, image.Height, 3 * image.Width);
            Gtk.Image gtkImage = new Gtk.Image(pixbuf);
            Gtk.Window window = new Gtk.Window(title);
            ScrolledWindow scrolledWindow = new ScrolledWindow();
            scrolledWindow.Add(gtkImage);
            window.Add(scrolledWindow);
            window.ShowAll();
            window.Destroyed += (sender, args) =>
            {
                Application.Quit();
            };
            Application.Run();
        }

        public static Image<float> Transform(this Image<float> image, Func<float, float, float, float, float> core, (int Width, int Height) size)
        {
            Image<float>[,] broken = image.Break(size.Width, size.Height);
            Image<float>[,] transformed = new Image<float>[broken.GetLength(0), broken.GetLength(1)];
            for (int i = 0; i < transformed.GetLength(0); i++)
            {
                for (int j = 0; j < transformed.GetLength(1); j++)
                {
                    transformed[i, j] = broken[i, j].Transform(core);
                }
            }

            return transformed.Merge();
        }

        public static Image<float> ToFloat(this Image<byte> image)
        {
            Image<float> newImage = new Image<float>(image.Width, image.Height, 
                image.Channels.Select(channel => channel.Tag).ToArray());
            foreach (ChannelType channel in image.Channels.Select(channel => channel.Tag))
            {
                byte[,] data = image[channel].Data;
                float[,] newData = newImage[channel].Data;
                for (int i = 0; i < image.Height; i++)
                {
                    for (int j = 0; j < image.Width; j++)
                    {
                        newData[i, j] = data[i, j];
                    }
                }
            }

            return newImage;
        }
        
        public static Image<byte> ToByte(this Image<float> image)
        {
            Image<byte> newImage = new Image<byte>(image.Width, image.Height, 
                image.Channels.Select(channel => channel.Tag).ToArray());
            foreach (ChannelType channel in image.Channels.Select(channel => channel.Tag))
            {
                float[,] data = image[channel].Data;
                byte[,] newData = newImage[channel].Data;
                for (int i = 0; i < image.Height; i++)
                {
                    for (int j = 0; j < image.Width; j++)
                    {
                        newData[i, j] = (byte)data[i, j];
                    }
                }
            }

            return newImage;
        }

        public static Image<float> Transform(this Image<float> image, Func<float, float, float, float, float> core)
        {
            Image<float> newImage = new Image<float>(image.Width, image.Height, image.Channels.Select(channel => channel.Tag).ToArray());
            foreach (ChannelType channel in image.Channels.Select(channel => channel.Tag))
            {
                float[,] data = image[channel].Data;
                float[,] newData = newImage[channel].Data;

                {
                    for (int v = 0; v < image.Height; v++)
                    {
                        for (int u = 0; u < image.Width; u++)
                        {
                            float sum = 0;
                            for (int y = 0; y < image.Height; y++)
                            {
                                for (int x = 0; x < image.Width; x++)
                                {
                                    sum += core(x, y, u, v) * data[y, x];
                                }
                            }

                            newData[v, u] = sum;
//                            Console.WriteLine(sum);
                        }
                    }
                }
            }

            return newImage;
        }

        public static Image<T>[,] Break<T>(this Image<T> image, int width, int height) where T : struct
        {
            Image<T>[,] brokenImages = new Image<T>[(image.Height-1)/height+1, (image.Width-1)/width+1];
            for (int i = 0, y = 0; i < brokenImages.GetLength(0); i++)
            {
                int thisHeight = (i+1) * height <= image.Height ? height : image.Height % height;
                for (int j = 0, x = 0; j < brokenImages.GetLength(1); j++)
                {
                    int thisWidth = (j+1) * width <= image.Width ? width : image.Width % width;
                    brokenImages[i, j] = image.SubImage(x, y, thisWidth, thisHeight);
                    x += thisWidth;
                }

                y += thisHeight;
            }

            return brokenImages;
        }

        public static Image<T> Merge<T>(this Image<T>[,] brokenImages) where T : struct
        {
            int width = 0;
            int height = 0;
            for (int i = 0; i < brokenImages.GetLength(0); i++)
            {
                height += brokenImages[i, 0].Height;
            }

            for (int i = 0; i < brokenImages.GetLength(1); i++)
            {
                width += brokenImages[0, i].Width;
            }
            
            Image<T> merged = new Image<T>(width, height, brokenImages[0, 0].Channels.Select(channel => channel.Tag).ToArray());

            for (int i = 0, y = 0; i < brokenImages.GetLength(0); i++)
            {
                for (int j = 0, x = 0; j < brokenImages.GetLength(1); j++)
                {
                    merged.Paste(x, y, brokenImages[i, j]);
                    x += brokenImages[i, j].Width;
                }
                
                y += brokenImages[i, 0].Height;
            }

            return merged;
        }
    }
}