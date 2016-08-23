using System;
using System.Collections.Generic;

using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SharpDX.IO;
using SharpDX.WIC;

using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace PokemonGo_UWP.Rendering
{
    class mxAsset
    {
        public String name;
        public virtual void Release() { }
    }

    class mxDataBuffer : mxAsset
    {
        public mxRenderManager parent;
        public BufferDescription desc;
        public Buffer buffer;

        public override void Release()
        {
            SharpDX.Utilities.Dispose(ref buffer);
        }

        public virtual void Bind() { }
    }

    class mxBuffer<T> : mxDataBuffer where T : struct
    {
        public T data;

        public override void Bind()
        {
            parent.dxContext.UpdateSubresource(ref data, buffer);
            parent.shaderMan.BindData(this);
        }
    }

    class mxRenderManager
    {
        Windows.UI.Xaml.Media.Imaging.SurfaceImageSource source = null;
        public Device dxDevice = null;
        public DeviceContext dxContext = null;
        public ISurfaceImageSourceNative dxOutput = null;

        private RenderTargetView dxRenderTarget = null;
        private ViewportF dxViewport;
        private Texture2D dxDepthBuffer = null;
        private DepthStencilView dxDepthView = null;

        private Rectangle drawArea;
        public mxShaderManager shaderMan = new mxShaderManager();

        public Color clearColor = Color.Black;
        public bool useDepthBuffer = true;

        private Dictionary<String, mxAsset> assets = new Dictionary<string, mxAsset>();

        ~mxRenderManager()
        {
            Release();
        }

        public void Init(int width, int height, Windows.UI.Xaml.Media.Imaging.SurfaceImageSource s)
        {
            Reset();
            source = s;
            drawArea = new Rectangle(0, 0, width, height);

            var creationFlags = DeviceCreationFlags.BgraSupport; //required for compatibility with Direct2D.
            FeatureLevel[] featureLevels = { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0, FeatureLevel.Level_9_3, FeatureLevel.Level_9_2, FeatureLevel.Level_9_1 };

            dxDevice = new Device(DriverType.Hardware, creationFlags, featureLevels);
            dxContext = dxDevice.ImmediateContext;

            dxOutput = ComObject.QueryInterface<ISurfaceImageSourceNative>(source);
            dxOutput.Device = dxDevice.QueryInterface<SharpDX.DXGI.Device>();

            dxViewport = new ViewportF(0, 0, drawArea.Width, drawArea.Height);

            shaderMan.renderMan = this;
        }

        public void StartFrame()
        {
            SharpDX.Utilities.Dispose(ref dxRenderTarget);

            try
            {
                RawPoint offset;
                using (var surface = dxOutput.BeginDraw(drawArea, out offset))
                {
                    using (var d3DTexture = surface.QueryInterface<Texture2D>())
                    {
                        dxRenderTarget = new RenderTargetView(dxDevice, d3DTexture);
                    }

                    if (dxViewport.X != offset.X) dxViewport.X = offset.X;
                    if (dxViewport.Y != offset.Y) dxViewport.Y = offset.Y;
                    dxContext.Rasterizer.SetViewport(dxViewport);

                    // Create depth/stencil buffer descriptor.
                    if (dxDepthView == null && useDepthBuffer == true)
                    {
                        Texture2DDescription depthStencilDesc = new Texture2DDescription()
                        {
                            Format = Format.D24_UNorm_S8_UInt,
                            Width = surface.Description.Width,
                            Height = surface.Description.Height,
                            ArraySize = 1,
                            MipLevels = 1,
                            BindFlags = BindFlags.DepthStencil,
                            SampleDescription = new SampleDescription(1, 0),
                            Usage = ResourceUsage.Default,
                        };
                        dxDepthBuffer = new Texture2D(dxDevice, depthStencilDesc);
                        dxDepthView = new DepthStencilView(dxDevice, dxDepthBuffer);
                    }
                }
            }
            catch (SharpDXException ex)
            {
                if (ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceRemoved ||
                    ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceReset)
                {
                    // If the device has been removed or reset, attempt to recreate it and continue drawing.
                    Init(drawArea.Width, drawArea.Height, source);
                    StartFrame();
                }
                else
                {
                    Reset();
                }
            }

            dxContext.ClearRenderTargetView(dxRenderTarget, clearColor);

            if (useDepthBuffer)
            {
                dxContext.ClearDepthStencilView(dxDepthView, DepthStencilClearFlags.Depth, 1.0f, 0);
                dxContext.OutputMerger.SetRenderTargets(dxDepthView, dxRenderTarget);
            }
            else
            {
                dxContext.OutputMerger.SetRenderTargets(dxRenderTarget);
            }
        }

        public void EndFrame()
        {
            try
            {
                dxOutput.EndDraw();
            }
            catch (SharpDXException ex)
            {
                Release();
            }
        }

        public void Release()
        {
            Reset();
            shaderMan.Release();
            foreach (var item in assets.Values) item.Release();
            // reset things back to initial state
            shaderMan = new mxShaderManager();
            assets = new Dictionary<string, mxAsset>();
        }

        public void Reset()
        {
            SharpDX.Utilities.Dispose(ref dxDevice);
            SharpDX.Utilities.Dispose(ref dxOutput);
            dxContext = null;
            SharpDX.Utilities.Dispose(ref dxRenderTarget);
            SharpDX.Utilities.Dispose(ref dxDepthBuffer);
            SharpDX.Utilities.Dispose(ref dxDepthView);
        }

        public mxBuffer<T> CreateBuffer<T>(String n) where T : struct
        {
            mxBuffer<T> obj = new mxBuffer<T>() { parent = this, name = n };
            obj.desc = new BufferDescription() { SizeInBytes = SharpDX.Utilities.SizeOf<T>(), BindFlags = BindFlags.ConstantBuffer };
            obj.buffer = new Buffer(dxDevice, obj.desc);

            assets[n] = obj;
            return obj;
        }

        public mxModel CreateModel<T>(String n, T[] verts, ushort[] indices) where T : struct
        {
            mxModelFull<T> model = new mxModelFull<T>() { name = n, vertices = verts, indices = indices, renderMan = this };

            var vertexBufferDesc = new BufferDescription() { SizeInBytes = SharpDX.Utilities.SizeOf<T>() * verts.Length, BindFlags = BindFlags.VertexBuffer };
            model.vertexBuffer = Buffer.Create(dxDevice, verts, vertexBufferDesc);

            var indexBufferDesc = new BufferDescription() { SizeInBytes = sizeof(ushort) * indices.Length, BindFlags = BindFlags.IndexBuffer };
            model.indexBuffer = Buffer.Create(dxDevice, indices, indexBufferDesc);

            model.indexCount = indices.Length;
            model.stride = SharpDX.Utilities.SizeOf<T>();

            assets[n] = model;
            return model;
        }

        public mxTexture CreateTexture(String n, String s, String filename)
        {
            mxTexture tex = new mxTexture() { parent = this, slot = s, name = n };

            NativeFileStream fileStream = new NativeFileStream(Windows.ApplicationModel.Package.Current.InstalledLocation.Path + filename, NativeFileMode.Open, NativeFileAccess.Read);
            ImagingFactory imagingFactory = new ImagingFactory();
            BitmapDecoder bitmapDecoder = new BitmapDecoder(imagingFactory, fileStream, DecodeOptions.CacheOnDemand);
            BitmapFrameDecode frame = bitmapDecoder.GetFrame(0);
            FormatConverter converter = new FormatConverter(imagingFactory);
            converter.Initialize(frame, PixelFormat.Format32bppPRGBA);

            int stride = converter.Size.Width * 4;
            using (var buffer = new DataStream(converter.Size.Height * stride, true, true))
            {
                converter.CopyPixels(stride, buffer);
                Texture2DDescription texdesc = new Texture2DDescription()
                {
                    Width = converter.Size.Width,
                    Height = converter.Size.Height,
                    ArraySize = 1,
                    BindFlags = BindFlags.ShaderResource,
                    Usage = ResourceUsage.Immutable,
                    CpuAccessFlags = CpuAccessFlags.None,
                    Format = Format.R8G8B8A8_UNorm,
                    MipLevels = 1,
                    OptionFlags = ResourceOptionFlags.None,
                    SampleDescription = new SampleDescription(1, 0)
                };
                tex.texture = new Texture2D(dxDevice, texdesc, new DataRectangle(buffer.DataPointer, stride));
                tex.textureView = new ShaderResourceView(dxDevice, tex.texture);
                SamplerStateDescription sampledesc = new SamplerStateDescription()
                {
                    Filter = Filter.MinMagMipLinear,
                    AddressU = TextureAddressMode.Wrap,
                    AddressV = TextureAddressMode.Wrap,
                    AddressW = TextureAddressMode.Wrap,
                    BorderColor = Color.Black,
                    ComparisonFunction = Comparison.Never,
                    MaximumAnisotropy = 16,
                    MipLodBias = 0,
                    MinimumLod = -float.MaxValue,
                    MaximumLod = float.MaxValue
                };
                tex.sampler = new SamplerState(dxDevice, sampledesc);
            }

            assets[n] = tex;
            return tex;
        }

        public bool GetAsset<T>(String name, ref T obj)
        {
            if (assets.ContainsKey(name) == false) return false;
            obj = (T)(object)assets[name];
            return true;
        }
    }
}