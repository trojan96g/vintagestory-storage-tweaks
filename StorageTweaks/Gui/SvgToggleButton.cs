using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace StorageTweaks.Gui;

public class SvgToggleButton(
    ICoreClientAPI capi,
    IAsset svgAsset,
    ActionConsumable onClick,
    Action<bool> onToggle,
    ElementBounds bounds,
    int activeColor,
    int activeHoverColor)
    : GuiElement(capi, bounds)
{
    private readonly int shadowColor = ColorUtil.ColorFromRgba(0, 0, 0, 64);
    private LoadedTexture activeHoverTexture = new(capi);
    private LoadedTexture activeTexture = new(capi);
    private LoadedTexture normalHoverTexture = new(capi);
    private LoadedTexture normalTexture = new(capi);
    private LoadedTexture shadowTexture = new(capi);

    // ReSharper disable once MemberCanBePrivate.Global
    public bool IsActive { get; set; }

    public override void ComposeElements(Context ctxStatic, ImageSurface surface)
    {
        base.ComposeElements(ctxStatic, surface);

        var iconSize = (int)Bounds.InnerWidth;

        var shadowSurface = DrawShadow(iconSize);
        api.Gui.LoadOrUpdateCairoTexture(shadowSurface, true, ref shadowTexture);
        shadowSurface.Dispose();

        var normalSurface = DrawNormalTexture(iconSize, false);
        api.Gui.LoadOrUpdateCairoTexture(normalSurface, true, ref normalTexture);
        normalSurface.Dispose();

        var hoverSurface = DrawHoverTexture(iconSize, false);
        api.Gui.LoadOrUpdateCairoTexture(hoverSurface, true, ref normalHoverTexture);
        hoverSurface.Dispose();

        var activeSurface = DrawNormalTexture(iconSize, true);
        api.Gui.LoadOrUpdateCairoTexture(activeSurface, true, ref activeTexture);
        activeSurface.Dispose();

        var activeHoverSurface = DrawHoverTexture(iconSize, true);
        api.Gui.LoadOrUpdateCairoTexture(activeHoverSurface, true, ref activeHoverTexture);
        activeHoverSurface.Dispose();
    }

    private ImageSurface DrawShadow(int size)
    {
        var surface = new ImageSurface(Format.Argb32, size, size);
        var ctx = new Context(surface);

        api.Gui.DrawSvg(svgAsset, surface, 4, 3, size - 4, size - 4, shadowColor);

        ctx.Dispose();
        return surface;
    }

    private ImageSurface DrawNormalTexture(int size, bool active)
    {
        var surface = new ImageSurface(Format.Argb32, size, size);
        var ctx = new Context(surface);

        var color = active
            ? activeColor
            : SvgButton.NormalColor;

        api.Gui.DrawSvg(svgAsset, surface, 2, 2, size - 4, size - 4, color);

        ctx.Dispose();
        return surface;
    }

    private ImageSurface DrawHoverTexture(int size, bool active)
    {
        var surface = new ImageSurface(Format.Argb32, size, size);
        var ctx = new Context(surface);

        var color = active
            ? activeHoverColor
            : SvgButton.HoverColor;

        api.Gui.DrawSvg(svgAsset, surface, 2, 2, size - 4, size - 4, color);

        ctx.Dispose();

        return surface;
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        base.RenderInteractiveElements(deltaTime);

        var mouseOver = Bounds.PointInside(api.Input.MouseX, api.Input.MouseY);

        api.Render.Render2DTexture(shadowTexture.TextureId, (float)Bounds.absX, (float)Bounds.absY,
            (float)Bounds.InnerWidth, (float)Bounds.InnerHeight);

        LoadedTexture mainTexture;
        LoadedTexture hoverTexture;

        if (IsActive)
        {
            mainTexture = activeTexture;
            hoverTexture = activeHoverTexture;
        }
        else
        {
            mainTexture = normalTexture;
            hoverTexture = normalHoverTexture;
        }

        api.Render.Render2DTexture(mainTexture.TextureId, (float)Bounds.absX, (float)Bounds.absY,
            (float)Bounds.InnerWidth, (float)Bounds.InnerHeight);

        if (mouseOver)
        {
            api.Render.Render2DTexture(hoverTexture.TextureId, (float)Bounds.absX, (float)Bounds.absY,
                (float)Bounds.InnerWidth, (float)Bounds.InnerHeight);
        }
    }

    public override void OnMouseDownOnElement(ICoreClientAPI capi, MouseEvent args)
    {
        base.OnMouseDownOnElement(capi, args);

        IsActive = !IsActive;
        onToggle.Invoke(IsActive);

        if (!onClick())
        {
            IsActive = !IsActive;
            onToggle.Invoke(IsActive);
            return;
        }

        capi.Gui.PlaySound("toggleswitch");
    }

    public override void Dispose()
    {
        base.Dispose();
        shadowTexture.Dispose();
        normalTexture.Dispose();
        normalHoverTexture.Dispose();
        activeTexture.Dispose();
        activeHoverTexture.Dispose();
        GC.SuppressFinalize(this);
    }
}