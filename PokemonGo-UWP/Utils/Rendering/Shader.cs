using System;
using System.Collections.Generic;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;

namespace PokemonGo_UWP.Rendering
{
    enum mxShaderType { None, Pixel, Vertex }

    class mxShader
    {
        public virtual void Release()
        {
            SharpDX.Utilities.Dispose(ref bytecode);
        }

        public Dictionary<String, int> dataBuffers = new Dictionary<string, int>();
        public Dictionary<String, int> texSamplers = new Dictionary<string, int>();

        public mxShaderType type = mxShaderType.None;
        public String name;
        public String filename;
        public ShaderBytecode bytecode = null;

        public void DefineData(String name, int i)
        {
            dataBuffers[name] = i;
        }

        public void DefineTexture(String slot, int i)
        {
            texSamplers[slot] = i;
        }
    }

    class mxVertexShader : mxShader
    {
        public override void Release()
        {
            base.Release();
            SharpDX.Utilities.Dispose(ref shader);
            SharpDX.Utilities.Dispose(ref layout);
        }

        public VertexShader shader = null;
        public InputLayout layout = null;
    }

    class mxPixelShader : mxShader
    {
        public override void Release()
        {
            base.Release();
            SharpDX.Utilities.Dispose(ref shader);
        }

        public PixelShader shader = null;
    }

    class mxEffect
    {
        public mxShaderManager manager = null;
        public String name;
        public mxShader vertex = null;
        public mxShader pixel = null;

        public void Bind()
        {
            if (manager == null) return;
            manager.BindEffect(this);
        }
    }
}