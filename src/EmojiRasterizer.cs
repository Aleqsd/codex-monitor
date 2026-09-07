using System.Runtime.InteropServices;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;

namespace CodexMonitor;

// Software rendering of Windows' own emoji font. No font or artwork is redistributed.
internal static unsafe class EmojiRasterizer
{
    internal sealed record Bitmap(int Width, int Height, byte[] Rgba);
    internal static Bitmap Render(string text)
    {
        const int size = 96;
        var initialized = CoInitializeEx(null, (uint)COINIT.COINIT_MULTITHREADED).SUCCEEDED;
        ComPtr<ID2D1Factory> d2d = default;
        ComPtr<IDWriteFactory> write = default;
        ComPtr<IWICImagingFactory> wic = default;
        ComPtr<IWICBitmap> bitmap = default;
        ComPtr<ID2D1RenderTarget> target = default;
        ComPtr<IDWriteTextFormat> format = default;
        ComPtr<IDWriteTextLayout> layout = default;
        ComPtr<ID2D1SolidColorBrush> brush = default;
        ComPtr<IWICBitmapLock> pixels = default;
        try
        {
            Check(D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof<ID2D1Factory>(), null, (void**)d2d.GetAddressOf()));
            Check(DWriteCreateFactory(DWRITE_FACTORY_TYPE.DWRITE_FACTORY_TYPE_SHARED, __uuidof<IDWriteFactory>(), (IUnknown**)write.GetAddressOf()));
            var clsid = CLSID.CLSID_WICImagingFactory;
            Check(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)wic.GetAddressOf()));
            var pixelFormat = GUID.GUID_WICPixelFormat32bppPBGRA;
            Check(wic.Get()->CreateBitmap(size, size, &pixelFormat, WICBitmapCreateCacheOption.WICBitmapCacheOnLoad, bitmap.GetAddressOf()));
            var properties = new D2D1_RENDER_TARGET_PROPERTIES
            {
                type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_SOFTWARE,
                pixelFormat = new D2D1_PIXEL_FORMAT { format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED },
                dpiX = 96, dpiY = 96,
            };
            Check(d2d.Get()->CreateWicBitmapRenderTarget(bitmap.Get(), &properties, target.GetAddressOf()));
            fixed (char* family = "Segoe UI Emoji", locale = "en-us", content = text)
            {
                Check(write.Get()->CreateTextFormat(family, null, DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL, DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL,
                    DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL, 64, locale, format.GetAddressOf()));
                Check(write.Get()->CreateTextLayout(content, (uint)text.Length, format.Get(), size, size, layout.GetAddressOf()));
            }
            var white = new DXGI_RGBA { r = 1, g = 1, b = 1, a = 1 };
            Check(target.Get()->CreateSolidColorBrush(&white, null, brush.GetAddressOf()));
            DWRITE_TEXT_METRICS metrics; Check(layout.Get()->GetMetrics(&metrics));
            target.Get()->BeginDraw();
            var transparent = new DXGI_RGBA(); target.Get()->Clear(&transparent);
            target.Get()->DrawTextLayout(new D2D_POINT_2F { x = (size - metrics.width) / 2, y = (size - metrics.height) / 2 }, layout.Get(), (ID2D1Brush*)brush.Get(),
                D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT);
            Check(target.Get()->EndDraw(null, null));
            var rect = new WICRect { X = 0, Y = 0, Width = size, Height = size };
            Check(bitmap.Get()->Lock(&rect, (uint)WICBitmapLockFlags.WICBitmapLockRead, pixels.GetAddressOf()));
            uint stride, length; byte* data;
            Check(pixels.Get()->GetStride(&stride)); Check(pixels.Get()->GetDataPointer(&length, &data));
            var rgba = new byte[size * size * 4];
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var source = data + y * stride + x * 4; var offset = (y * size + x) * 4; var a = source[3];
                rgba[offset] = a == 0 ? (byte)0 : (byte)Math.Min(255, source[2] * 255 / a);
                rgba[offset + 1] = a == 0 ? (byte)0 : (byte)Math.Min(255, source[1] * 255 / a);
                rgba[offset + 2] = a == 0 ? (byte)0 : (byte)Math.Min(255, source[0] * 255 / a);
                rgba[offset + 3] = a;
            }
            return new Bitmap(size, size, rgba);
        }
        finally
        {
            // Release COM resources before leaving their apartment.
            pixels.Dispose(); brush.Dispose(); layout.Dispose(); format.Dispose(); target.Dispose(); bitmap.Dispose(); wic.Dispose(); write.Dispose(); d2d.Dispose();
            if (initialized) CoUninitialize();
        }
    }
    private static void Check(HRESULT result) { if (result.FAILED) Marshal.ThrowExceptionForHR(result); }
}
