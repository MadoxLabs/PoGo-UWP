using System;
using System.Collections.Generic;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;

namespace PokemonGo_UWP.Rendering
{
    class mxShaderManager
    {
        public mxRenderManager renderMan = null;
        public mxShader curVertex = null;
        public mxShader curPixel = null;

        public Dictionary<String, mxShader> shaders = new Dictionary<string, mxShader>();
        public Dictionary<String, mxEffect> effects = new Dictionary<string, mxEffect>();

        public bool Register(String filename, String name, InputElement[] layout)
        {
            if (renderMan == null) return false;
            if (filename.Contains(".vs.hlsl"))
            {
                mxVertexShader shader = new mxVertexShader() { name = name, filename = filename, type = mxShaderType.Vertex };
                shader.bytecode = new ShaderBytecode(ShaderBytecode.CompileFromFile(filename, "main", "vs_5_0"));
                shader.shader = new VertexShader(renderMan.dxDevice, shader.bytecode);
                shader.layout = new InputLayout(renderMan.dxDevice, shader.bytecode, layout);
                shaders[name] = shader;
                return true;
            }
            return false;
        }

        public bool Register(String filename, String name)
        {
            if (renderMan == null) return false;

            if (filename.Contains("ps.hlsl"))
            {
                mxPixelShader shader = new mxPixelShader() { name = name, filename = filename, type = mxShaderType.Pixel };
                shader.bytecode = new ShaderBytecode(ShaderBytecode.CompileFromFile(filename, "main", "ps_5_0"));
                shader.shader = new PixelShader(renderMan.dxDevice, shader.bytecode);
                shaders[name] = shader;
                return true;
            }
            return false;
        }

        public bool Register(String name, String vname, String pname)
        {
            mxShader v = GetShader(vname);
            mxShader p = GetShader(pname);
            mxEffect e;
            if (v == null || v.type != mxShaderType.Vertex) return false;
            if (p == null || p.type != mxShaderType.Pixel) return false;
            e = new mxEffect() { name = name, vertex = v, pixel = p, manager = this };
            effects[name] = e;
            return true;
        }

        public mxShader GetShader(String name)
        {
            if (shaders.ContainsKey(name)) return shaders[name];
            return null;
        }

        public mxEffect GetEffect(String name)
        {
            if (effects.ContainsKey(name)) return effects[name];
            return null;
        }

        public void BindEffect(mxEffect e)
        {
            if (renderMan == null) return;
            if (e.vertex != null && curVertex != e.vertex)
            {
                renderMan.dxContext.InputAssembler.InputLayout = ((mxVertexShader)e.vertex).layout;
                renderMan.dxContext.VertexShader.Set(((mxVertexShader)e.vertex).shader);
                curVertex = e.vertex;
            }
            if (e.pixel != null && curPixel != e.pixel)
            {
                renderMan.dxContext.PixelShader.Set(((mxPixelShader)e.pixel).shader);
                curPixel = e.pixel;
            }
        }

        public void BindData<T>(mxBuffer<T> data) where T : struct
        {
            if (curVertex.dataBuffers.ContainsKey(data.name) != false)
                renderMan.dxContext.VertexShader.SetConstantBuffer(curVertex.dataBuffers[data.name], data.buffer);
            if (curPixel.dataBuffers.ContainsKey(data.name) != false)
                renderMan.dxContext.PixelShader.SetConstantBuffer(curPixel.dataBuffers[data.name], data.buffer);
        }

        public void BindTexture(mxTexture tex)
        {
            if (curVertex.texSamplers.ContainsKey(tex.slot) != false)
            {
                int i = curVertex.texSamplers[tex.slot];
                renderMan.dxContext.VertexShader.SetShaderResource(i, tex.textureView);
                renderMan.dxContext.VertexShader.SetSampler(i, tex.sampler);
            }
            if (curPixel.texSamplers.ContainsKey(tex.slot) != false)
            {
                int i = curPixel.texSamplers[tex.slot];
                renderMan.dxContext.PixelShader.SetShaderResource(i, tex.textureView);
                renderMan.dxContext.PixelShader.SetSampler(i, tex.sampler);
            }
        }

        public void Release()
        {
            renderMan = null;
            curPixel = null;
            curVertex = null;
            foreach (var item in shaders.Values) item.Release();
            shaders = null;
            effects = null;
        }
    }

}
