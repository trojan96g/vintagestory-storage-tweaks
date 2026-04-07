using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace StorageTweaks;

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
    private readonly int _shadowColor = ColorUtil.ColorFromRgba(0, 0, 0, 64);
    private LoadedTexture _activeHoverTexture = new(capi);
    private LoadedTexture _activeTexture = new(capi);
    private LoadedTexture _hoverTexture = new(capi);
    private LoadedTexture _normalTexture = new(capi);
    private LoadedTexture _shadowTexture = new(capi);

    // ReSharper disable once MemberCanBePrivate.Global
    public bool IsActive { get; set; }

    public override void ComposeElements(Context ctxStatic, ImageSurface surface)
    {
        base.ComposeElements(ctxStatic, surface);

        var iconSize = (int)Bounds.InnerWidth;

        var shadowSurface = DrawShadow(iconSize);
        api.Gui.LoadOrUpdateCairoTexture(shadowSurface, true, ref _shadowTexture);
        shadowSurface.Dispose();

        var normalSurface = DrawNormalTexture(iconSize, false);
        api.Gui.LoadOrUpdateCairoTexture(normalSurface, true, ref _normalTexture);
        normalSurface.Dispose();

        var hoverSurface = DrawHoverTexture(iconSize, false);
        api.Gui.LoadOrUpdateCairoTexture(hoverSurface, true, ref _hoverTexture);
        hoverSurface.Dispose();

        var activeSurface = DrawNormalTexture(iconSize, true);
        api.Gui.LoadOrUpdateCairoTexture(activeSurface, true, ref _activeTexture);
        activeSurface.Dispose();

        var activeHoverSurface = DrawHoverTexture(iconSize, true);
        api.Gui.LoadOrUpdateCairoTexture(activeHoverSurface, true, ref _activeHoverTexture);
        activeHoverSurface.Dispose();
    }

    private ImageSurface DrawShadow(int size)
    {
        var surface = new ImageSurface(Format.Argb32, size, size);
        var ctx = new Context(surface);

        api.Gui.DrawSvg(svgAsset, surface, 4, 3, size - 4, size - 4, _shadowColor);

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

        api.Render.Render2DTexture(_shadowTexture.TextureId, (float)Bounds.absX, (float)Bounds.absY,
            (float)Bounds.InnerWidth, (float)Bounds.InnerHeight);

        LoadedTexture mainTexture;
        LoadedTexture hoverTexture;

        if (IsActive)
        {
            mainTexture = _activeTexture;
            hoverTexture = _activeHoverTexture;
        }
        else
        {
            mainTexture = _normalTexture;
            hoverTexture = _hoverTexture;
        }

        api.Render.Render2DTexture(mainTexture.TextureId, (float)Bounds.absX, (float)Bounds.absY,
            (float)Bounds.InnerWidth, (float)Bounds.InnerHeight);

        if (mouseOver)
            api.Render.Render2DTexture(hoverTexture.TextureId, (float)Bounds.absX, (float)Bounds.absY,
                (float)Bounds.InnerWidth, (float)Bounds.InnerHeight);
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
        _shadowTexture.Dispose();
        _normalTexture.Dispose();
        _hoverTexture.Dispose();
        _activeTexture.Dispose();
        _activeHoverTexture.Dispose();
        GC.SuppressFinalize(this);
    }
}