using SharpDX;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;

namespace PokemonGo_UWP.Rendering
{
    class mxTexture : mxAsset
    {
        public mxRenderManager parent;

        public String slot;
        public Texture2D texture;
        public ShaderResourceView textureView;
        public SamplerState sampler;

        public override void Release()
        {
            SharpDX.Utilities.Dispose(ref texture);
            SharpDX.Utilities.Dispose(ref textureView);
            SharpDX.Utilities.Dispose(ref sampler);
        }

        public void Bind()
        {
            parent.shaderMan.BindTexture(this);
        }
    }
}