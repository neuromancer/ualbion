﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using UAlbion.Api;
using Veldrid;

namespace UAlbion.Core.Textures
{
    public class MultiTexture : ITexture
    {
        readonly IPaletteManager _paletteManager;

        class SubImageComponent
        {
            public ITexture Source { get; set; }
            public uint X { get; set; }
            public uint Y { get; set; }
            public uint? W { get; set; }
            public uint? H { get; set; }
            public override string ToString() => $"({X}, {Y}) {Source}";
        }

        class LogicalSubImage
        {
            public LogicalSubImage(int id) { Id = id; }

            public int Id { get; }
            public uint W { get; set; }
            public uint H { get; set; }
            public int Frames { get; set; }
            public bool IsPaletteAnimated { get; set; }
            public bool IsAlphaTested { get; set; }
            public byte? TransparentColor { get; set; }
            public IList<SubImageComponent> Components { get; } = new List<SubImageComponent>();

            public override string ToString() => $"LSI{Id} {W}x{H}:{Frames}{(IsPaletteAnimated ? "P":" ")} {string.Join("; ",  Components.Select(x => x.ToString()))}";
        }

        struct LayerKey : IEquatable<LayerKey>
        {
            public LayerKey(int id, int frame)
            {
                Id = id;
                Frame = frame;
            }

            public int Id { get; }
            public int Frame { get; }

            public bool Equals(LayerKey other) => Id == other.Id && Frame == other.Frame;
            public override bool Equals(object obj) => obj is LayerKey other && Equals(other);
            public override int GetHashCode() { unchecked { return (Id * 397) ^ Frame; } }
            public override string ToString() => $"LK{Id}.{Frame}";
        }

        readonly IList<LogicalSubImage> _logicalSubImages = new List<LogicalSubImage>();
        readonly IDictionary<LayerKey, int> _layerLookup = new Dictionary<LayerKey, int>();
        readonly IList<Vector2> _layerSizes = new List<Vector2>();
        bool _isMetadataDirty = true;
        bool _isAnySubImagePaletteAnimated = false;
        int _lastPaletteFrame;
        int _lastPaletteId;
        bool _isDirty;

        public MultiTexture(string name, IPaletteManager paletteManager)
        {
            _paletteManager = paletteManager;
            Name = name;
            MipLevels = 1; //(uint)Math.Min(Math.Log(Width, 2.0), Math.Log(Height, 2.0));

            // Add empty texture for disabled walls/ceilings etc
            _logicalSubImages.Add(new LogicalSubImage(0) { W = 1, H = 1, Frames = 1, IsPaletteAnimated = false });
        }

        public string Name { get; }
        public PixelFormat Format => PixelFormat.R8_G8_B8_A8_UNorm;
        public TextureType Type => TextureType.Texture2D;
        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public uint Depth => 1;
        public uint MipLevels { get; }
        public uint ArrayLayers { get { if (_isMetadataDirty) RebuildLayers(); return (uint)_layerSizes.Count; } }
        public int SubImageCount => _layerSizes.Count;

        public bool IsDirty
        {
            get
            {
                var frame = _paletteManager.PaletteFrame;
                if ((_isAnySubImagePaletteAnimated && frame != _lastPaletteFrame) || _paletteManager.Palette.Id != _lastPaletteId)
                {
                    _lastPaletteFrame = frame;
                    _lastPaletteId = _paletteManager.Palette.Id;
                    return true;
                }

                return _isDirty;
            }
            private set => _isDirty = value;
        }

        public int SizeInBytes => (int)(Width * Height * _layerSizes.Count * sizeof(uint));
        public bool IsAnimated(int logicalId)
        {
            if(_isMetadataDirty)
                RebuildLayers();

            if (logicalId >= _logicalSubImages.Count)
                return false;

            return _logicalSubImages[logicalId].Frames > 1;
        }

        public int GetSubImageAtTime(int logicalId, int tick)
        {
            if(_isMetadataDirty)
                RebuildLayers();

            if (logicalId >= _logicalSubImages.Count)
                return 0;

            var logicalImage = _logicalSubImages[logicalId];
            if (_layerLookup.TryGetValue(new LayerKey(logicalId, tick % logicalImage.Frames), out var result))
                return result;
            return 0;
        }

        public void GetSubImageDetails(int subImage, out Vector2 size, out Vector2 texOffset, out Vector2 texSize, out uint layer)
        {
            if(_isMetadataDirty)
                RebuildLayers();

            size = _layerSizes[subImage];
            texOffset = Vector2.Zero;
            texSize = size / new Vector2(Width, Height);
            layer = (uint)subImage;
        }

        [DllImport("Kernel32.dll", EntryPoint = "RtlZeroMemory", SetLastError = false)]
        static extern void ZeroMemory(IntPtr dest, IntPtr size);

        unsafe void MemsetZero(byte* buffer, int size)
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ZeroMemory((IntPtr)buffer, (IntPtr)size);
            }
            else
                for (int i = 0; i < size; i++)
                    *(buffer + i) = 0;
        }

        void RebuildLayers()
        {
            _isAnySubImagePaletteAnimated = false;
            _isMetadataDirty = false;
            _layerLookup.Clear();
            _layerSizes.Clear();

            var palette = _paletteManager.Palette.GetCompletePalette();
            var animatedRange =
                palette
                    .SelectMany(x => x.Select((y, i) => (y, i)))
                    .GroupBy(x => x.i)
                    .Where(x => x.Distinct().Count() > 1)
                    .Select(x => (byte)x.Key)
                    .ToHashSet();

            foreach (var lsi in _logicalSubImages)
            {
                lsi.W = 1;
                lsi.H = 1;

                foreach (var component in lsi.Components)
                {
                    if (component.Source == null)
                        continue;
                    component.Source.GetSubImageDetails(0, out var size, out _, out _, out _);
                    if (component.W.HasValue) size.X = component.W.Value;
                    if (component.H.HasValue) size.Y = component.H.Value;

                    if (lsi.W < component.X + size.X)
                        lsi.W = component.X + (uint)size.X;
                    if (lsi.H < component.Y + size.Y)
                        lsi.H = component.Y + (uint)size.Y;

                    if (!lsi.IsPaletteAnimated && component.Source is EightBitTexture t)
                        lsi.IsPaletteAnimated = t.ContainsColors(animatedRange);

                    if (lsi.IsPaletteAnimated)
                        _isAnySubImagePaletteAnimated = true;
                }

                lsi.Frames = (int)Api.Util.LCM(lsi.Components.Select(x => (long)x.Source.SubImageCount).Append(1));
                for (int i = 0; i < lsi.Frames; i++)
                {
                    _layerLookup[new LayerKey(lsi.Id, i)] = _layerSizes.Count;
                    _layerSizes.Add(new Vector2(lsi.W, lsi.H));
                }

                if (Width < lsi.W)
                    Width = lsi.W;
                if (Height < lsi.H)
                    Height = lsi.H;
            }

            if (_layerSizes.Count > 255)
                throw new InvalidOperationException("Too many textures added to multi-texture");
        }

        public void AddTexture(int logicalId, ITexture texture, uint x, uint y, byte? transparentColor, bool isAlphaTested, uint? w = null, uint? h = null)
        {
            if(logicalId == 0)
                throw new InvalidOperationException("Logical Subimage Index 0 is reserved for a blank / transparent state");

            if (texture == null) // Will just end up using layer 0
                return;

            while(_logicalSubImages.Count <= logicalId)
                _logicalSubImages.Add(new LogicalSubImage(logicalId));

            var lsi = _logicalSubImages[logicalId];
            lsi.IsAlphaTested = isAlphaTested;
            lsi.TransparentColor = transparentColor;
            lsi.Components.Add(new SubImageComponent
            {
                Source = texture,
                X = x,
                Y = y,
                W = w,
                H = h
            });

            IsDirty = true;
            _isMetadataDirty = true;
        }

        uint GetFormatSize(PixelFormat format)
        {
            switch (format)
            {
                case PixelFormat.R8_G8_B8_A8_UNorm: return 4;
                case PixelFormat.R8_UNorm: return 1;
                case PixelFormat.R8_UInt: return 1;
                default: throw new NotImplementedException();
            }
        }

        static uint GetDimension(uint largestLevelDimension, uint mipLevel)
        {
            uint ret = largestLevelDimension;
            for (uint i = 0; i < mipLevel; i++)
                ret /= 2;

            return Math.Max(1, ret);
        }

        public Texture CreateDeviceTexture(GraphicsDevice gd, ResourceFactory rf, TextureUsage usage)
        {
            using var _ = PerfTracker.FrameEvent("6.1.2.1 Rebuild MultiTextures");
            if(_isMetadataDirty)
                RebuildLayers();

            var palette = _paletteManager.Palette.GetCompletePalette();
            using var staging = rf.CreateTexture(new TextureDescription(Width, Height, Depth, MipLevels, ArrayLayers, Format, TextureUsage.Staging, Type));
            staging.Name = "T_" + Name + "_Staging";

            unsafe
            {
                uint* toBuffer = stackalloc uint[(int)(Width * Height)];
                foreach (var lsi in _logicalSubImages)
                {
                    //if (!rebuildAll && !lsi.IsPaletteAnimated) // TODO: Requires caching a single Texture and then modifying it
                    //    continue;

                    for (int i = 0; i < lsi.Frames; i++)
                    {
                        if(lsi.IsAlphaTested)
                            MemsetZero((byte*)toBuffer, (int)(Width * Height * sizeof(uint)));
                        else
                        {
                            for (int j = 0; j < Width * Height; j++)
                                toBuffer[j] = 0xff000000;
                        }

                        BuildFrame(lsi, i, toBuffer, palette);

                        uint destinationLayer = (uint)_layerLookup[new LayerKey(lsi.Id, i)];
                        gd.UpdateTexture(
                            staging, (IntPtr)toBuffer, Width * Height * sizeof(uint),
                            0, 0, 0, Width, Height, 1,
                            0, destinationLayer);
                    }
                }
            }

            /* TODO: Mipmap
                for (uint level = 1; level < MipLevels; level++)
                {
                } //*/

            var texture = rf.CreateTexture(new TextureDescription(Width, Height, Depth, MipLevels, ArrayLayers, Format, usage, Type));
            texture.Name = "T_" + Name;

            using (CommandList cl = rf.CreateCommandList())
            {
                cl.Begin();
                cl.CopyTexture(staging, texture);
                cl.End();
                gd.SubmitCommands(cl);
            }

            IsDirty = false;
            return texture;
        }

        unsafe void BuildFrame(LogicalSubImage lsi, int frameNumber, uint* toBuffer, IList<uint[]> palette)
        {
            foreach (var component in lsi.Components)
            {
                if (component.Source == null)
                    continue;

                var eightBitTexture = (EightBitTexture)component.Source;
                int frame = frameNumber % eightBitTexture.SubImageCount;
                eightBitTexture.GetSubImageOffset(frame, out var sourceWidth, out var sourceHeight, out var sourceOffset, out var sourceStride);
                uint destWidth = component.W ?? (uint)sourceWidth;
                uint destHeight = component.H ?? (uint)sourceHeight;

                if (component.X + destWidth > Width || component.Y + destHeight > Height)
                {
                    CoreTrace.Log.Warning(
                        "MultiTexture",
                        $"Tried to write an oversize component to {Name}: {component.Source.Name}:{frame} is ({destWidth}x{destHeight}) @ ({component.X}, {component.Y}) but multitexture is only ({Width}x{Height})");
                    continue;
                }

                fixed (byte* fromBuffer = &eightBitTexture.TextureData[0])
                {
                    Util.Blit8To32(
                        (uint)sourceWidth, (uint)sourceHeight,
                        destWidth, destHeight,
                        fromBuffer + sourceOffset,
                        toBuffer + (int)(component.Y * Width + component.X),
                        sourceStride,
                        (int)Width,
                        palette[_paletteManager.PaletteFrame],
                        lsi.TransparentColor);
                }
            }
        }

        public void SavePng(int logicalId, int tick, string path)
        {
            if(_isMetadataDirty)
                RebuildLayers();

            var palette = _paletteManager.Palette.GetCompletePalette();
            var logicalImage = _logicalSubImages[logicalId];
            if (!_layerLookup.TryGetValue(new LayerKey(logicalId, tick % logicalImage.Frames), out var subImageId))
                return;

            var size = _layerSizes[subImageId];
            int width = (int)size.X;
            int height = (int)size.Y;
            Rgba32[] pixels = new Rgba32[width * height];

            unsafe
            {
                fixed (Rgba32* toBuffer = &pixels[0])
                    BuildFrame(logicalImage, tick, (uint*)toBuffer, palette);
            }

            Image<Rgba32> image = new Image<Rgba32>(width, height);
            image.Frames.AddFrame(pixels);
            image.Frames.RemoveFrame(0);
            using var stream = File.OpenWrite(path);
            image.SaveAsBmp(stream);
        }
    }
}