using System.Drawing;
using Xunit;

namespace GalCompanion.Tests
{
    public class UniformColorTests
    {
        [Fact]
        public void Solid_bitmap_is_uniform()
        {
            using (var bmp = Filled(100, 50, Color.Black))
            {
                Assert.True(CaptureService.IsUniformColor(bmp));
            }
        }

        [Fact]
        public void Single_pixel_bitmap_is_uniform()
        {
            using (var bmp = Filled(1, 1, Color.White))
            {
                Assert.True(CaptureService.IsUniformColor(bmp));
            }
        }

        [Fact]
        public void Two_tone_bitmap_is_not_uniform()
        {
            using (var bmp = Filled(100, 50, Color.Black))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.FillRectangle(Brushes.White, 50, 0, 50, 50);
                }
                Assert.False(CaptureService.IsUniformColor(bmp));
            }
        }

        [Fact]
        public void Small_mixed_bitmap_is_not_uniform()
        {
            using (var bmp = Filled(3, 2, Color.Red))
            {
                bmp.SetPixel(1, 1, Color.Blue);
                Assert.False(CaptureService.IsUniformColor(bmp));
            }
        }

        private static Bitmap Filled(int width, int height, Color color)
        {
            var bmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(color);
            }
            return bmp;
        }
    }
}
