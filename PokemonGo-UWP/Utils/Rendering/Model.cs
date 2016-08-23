using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Collections.Generic;

namespace PokemonGo_UWP.Rendering
{
    class mxModel : mxAsset
    {
        public override void Release()
        {
            SharpDX.Utilities.Dispose(ref vertexBuffer);
            SharpDX.Utilities.Dispose(ref indexBuffer);
        }

        public Buffer vertexBuffer = null;
        public Buffer indexBuffer = null;
        public int indexCount = 0;
        public int stride = 0;

        public virtual void Bind() { }
        public virtual void Draw() { }
    };

    class mxModelFull<T> : mxModel where T : struct
    {
        public mxRenderManager renderMan;
        public ushort[] indices;
        public T[] vertices;

        public override void Bind()
        {
            renderMan.dxContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, stride, 0));
            renderMan.dxContext.InputAssembler.SetIndexBuffer(indexBuffer, Format.R16_UInt, 0);
            renderMan.dxContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        }
        public override void Draw()
        {
            renderMan.dxContext.DrawIndexed(indexCount, 0, 0);
        }
    }

    class mxInstance
    {
        public mxModel model = null;
        public mxDataBuffer data;
        public List<mxTexture> textures = new List<mxTexture>();

        public mxBuffer<T> access<T>() where T : struct { return (mxBuffer<T>)data; }

        public void AttachTexture(mxTexture tex)
        {
            textures.Add(tex);
        }

        public void Draw()
        {
            foreach (var tex in textures) tex.Bind();
            model.Bind();
            data.Bind();
            model.Draw();
        }
    }
}