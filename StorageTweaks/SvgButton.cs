using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace StorageTweaks;

public class SvgButton(
    ICoreClientAPI capi,
    IAsset svgAsset,
    ActionConsumable onClick,
    ElementBounds bounds)
    : GuiElement(capi, bounds)
{
    public static readonly int NormalColor = ColorUtil.ColorFromRgba(233, 221, 206, 255);
    public static readonly int HoverColor = ColorUtil.ColorFromRgba(0, 221, 0, 127);
    private LoadedTexture _hoverTexture = new(capi);
    private LoadedTexture _normalTexture = new(capi);
    private LoadedTexture _shadowTexture = new(capi);

    public override void ComposeElements(Context ctxStatic, ImageSurface surface)
    {
        base.ComposeElements(ctxStatic, surface);

        var iconSize = (int)Bounds.InnerWidth;

        var shadowSurface = DrawShadow(iconSize);
        api.Gui.LoadOrUpdateCairoTexture(shadowSurface, true, ref _shadowTexture);
        shadowSurface.Dispose();

        var normalSurface = DrawNormalTexture(iconSize);
        api.Gui.LoadOrUpdateCairoTexture(normalSurface, true, ref _normalTexture);
        normalSurface.Dispose();

        var hoverSurface = DrawHoverTexture(iconSize);
        api.Gui.LoadOrUpdateCairoTexture(hoverSurface, true, ref _hoverTexture);
        hoverSurface.Dispose();
    }

    private ImageSurface DrawShadow(int size)
    {
        var surface = new ImageSurface(Format.Argb32, size, size);
        var ctx = new Context(surface);

        api.Gui.DrawSvg(svgAsset, surface, 4, 3, size - 4, size - 4, ColorUtil.ColorFromRgba(0, 0, 0, 64));

        ctx.Dispose();
        return surface;
    }

    private ImageSurface DrawNormalTexture(int size)
    {
        var surface = new ImageSurface(Format.Argb32, size, size);
        var ctx = new Context(surface);

        api.Gui.DrawSvg(svgAsset, surface, 2, 2, size - 4, size - 4, NormalColor);

        ctx.Dispose();
        return surface;
    }

    private ImageSurface DrawHoverTexture(int size)
    {
        var surface = new ImageSurface(Format.Argb32, size, size);
        var ctx = new Context(surface);

        api.Gui.DrawSvg(svgAsset, surface, 2, 2, size - 4, size - 4, HoverColor);

        ctx.Dispose();

        return surface;
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        base.RenderInteractiveElements(deltaTime);

        var mouseOver = Bounds.PointInside(api.Input.MouseX, api.Input.MouseY);

        api.Render.Render2DTexture(_shadowTexture.TextureId, (float)Bounds.absX, (float)Bounds.absY,
            (float)Bounds.InnerWidth, (float)Bounds.InnerHeight);
        api.Render.Render2DTexture(_normalTexture.TextureId, (float)Bounds.absX, (float)Bounds.absY,
            (float)Bounds.InnerWidth, (float)Bounds.InnerHeight);

        if (mouseOver)
            api.Render.Render2DTexture(_hoverTexture.TextureId, (float)Bounds.absX, (float)Bounds.absY,
                (float)Bounds.InnerWidth, (float)Bounds.InnerHeight);
    }

    public override void OnMouseDownOnElement(ICoreClientAPI capi, MouseEvent args)
    {
        base.OnMouseDownOnElement(capi, args);
        if (!onClick()) return;

        capi.Gui.PlaySound("tick");
    }

    public override void Dispose()
    {
        base.Dispose();
        _shadowTexture.Dispose();
        _normalTexture.Dispose();
        _hoverTexture.Dispose();
        GC.SuppressFinalize(this);
    }
}