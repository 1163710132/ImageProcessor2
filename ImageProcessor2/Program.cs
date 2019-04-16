using System;

namespace ImageProcessor2
{
    class Program
    {
        static void Main(string[] args)
        {
            const int n = 4;
            double alpha1 = (float)Math.Sqrt((double) 1 / n);
            double alpha2 = (float)Math.Sqrt((double) 2 / n);
            Image<byte> image = Images.FromFile("/home/chenjs/Pictures/3fc0891fa8d3fd1fd7b7b2183d4e251f95ca5f26.jpg");//.Copy(512, 512);
//            Image[,] broken = image.Break(200, 200);
            Func<float, float, float, float, float> r = (x, y, u, v) =>
            {
                double alphaU = u == 0 ? alpha1 : alpha2;
                double alphaV = v == 0 ? alpha1 : alpha2;
                return (float)(alphaU * alphaV
                              * Math.Cos(((float)2 * x + 1) * Math.PI * u / (2 * n))
                              * Math.Cos(((float)2 * y + 1) * Math.PI * v / (2 * n)));
            };
            Func<float, float, float, float, float> s = (x, y, u, v) =>
            {
                double alphaX = x == 0 ? alpha1 : alpha2;
                double alphaY = y == 0 ? alpha1 : alpha2;
                return (float)(alphaX * alphaY
                               * Math.Cos((u + 0.5) * Math.PI * x / n)
                               * Math.Cos((v + 0.5) * Math.PI * y / n));
            };
            image.ToFloat().Transform(r, (n, n)).Transform(s, (n, n)).ToByte();//.ShowRgb("Rgb");
        }
    }
}