using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;

using Buffer = SharpDX.Direct3D11.Buffer;

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

    internal class fbxBB
    {
        public Vector3 min;
        public Vector3 max;
    }

    internal class fbxObject
    {
        public string type = "none";
        public string id = "";
    }

    internal class fbxMesh : fbxObject
    {
        public fbxMesh() { type = "mesh"; }

        public List<float> vertexs = new List<float>();
        public List<float> uv = new List<float>();
        public List<float> normals = new List<float>();
        public List<int> indexes = new List<int>();
        public List<int> uvindex = new List<int>();
        public int mapping;
    }

    internal class fbxModel : fbxObject
    {
        public fbxModel() { type = "model"; }

        public string name;
        public fbxMesh mesh;
        public fbxBB boundingbox;
        public Vector3 translation = new Vector3(0, 0, 0);
        public Vector3 rotation = new Vector3(0, 0, 0);
        public Vector3 scale = new Vector3(1, 1, 1);
    }

    internal class fbxMaterial : fbxObject
    {
        public fbxMaterial() { type = "material"; }

        public List<fbxModel> models = new List<fbxModel>();
        public string texture;
        public string name;
    }

    internal class fbxTexture : fbxObject
    {
        public fbxTexture() { type = "texture"; }
        public string file;
    }

    internal class fbxFile
    {
        public Dictionary<string, int> attributes = new Dictionary<string, int>();
        public List<fbxMaterial> groups = new List<fbxMaterial>();
        public fbxBB boundingbox;
    }

    public class mxFBXLoaderA
    {
        private enum States { FindObjects, FindTag, ParseGeometry, ParseModel, ParseMaterial, ParseConnections, ParseTexture };
        private List<fbxMaterial> groups = new List<fbxMaterial>();
        private Dictionary<string, fbxObject> objects = new Dictionary<string, fbxObject>();
        private fbxMesh curmesh = new fbxMesh();
        private fbxModel curmodel = new fbxModel();
        private fbxMaterial curmaterial = new fbxMaterial();
        private fbxTexture curTexture = new fbxTexture();

        private void done()
        {
            foreach (fbxObject obj in objects.Values)
            {
                if (obj.type == "material") groups.Add((fbxMaterial)obj);
                else if (obj.type == "model")
                {
                    fbxModel model = (fbxModel)obj;
                    if (model.mesh == null) continue;
                    model.mesh = bake(model.mesh);
                    model.boundingbox = getBB(model);
                }
            }

            fbxFile file = new fbxFile();
            file.attributes["POS"] = 0;
            file.attributes["TEX0"] = 12;
            file.attributes["NORM"] = 20;
            file.groups = groups;

            // get full bb
            fbxBB bb = new fbxBB();
            bool hasmin = false;
            bool hasmax = false;
            foreach (fbxMaterial mat in groups)
                foreach (fbxModel model in mat.models)
                {
                    fbxBB mbb = model.boundingbox;
                    if (hasmin == false) { bb.min = mbb.min; hasmin = true; }
                    else
                    {
                        if (mbb.min.X < bb.min.X) bb.min.X = mbb.min.X;
                        if (mbb.min.Y < bb.min.Y) bb.min.Y = mbb.min.Y;
                        if (mbb.min.Z < bb.min.Z) bb.min.Z = mbb.min.Z;
                    }
                    if (hasmax == false) { bb.max = mbb.max; hasmax = true; }
                    else
                    {
                        if (mbb.max.X > bb.max.X) bb.max.X = mbb.max.X;
                        if (mbb.max.Y > bb.max.Y) bb.max.Y = mbb.max.Y;
                        if (mbb.max.Z > bb.max.Z) bb.max.Z = mbb.max.Z;
                    }
                }
            file.boundingbox = bb;
        }

        private List<string> unique(List<string> source)
        {
            List<string> unique = new List<string>();
            foreach (string s in source)
            {
                if (unique.Contains(s) == false) unique.Add(s);
            }
            return unique;
        }

        private fbxBB getBB(fbxModel model)
        {
            fbxBB bb = new fbxBB();
            bb.min.X = model.mesh.vertexs[0];
            bb.min.Y = model.mesh.vertexs[0];
            bb.min.Z = model.mesh.vertexs[0];
            bb.max.X = model.mesh.vertexs[0];
            bb.max.Y = model.mesh.vertexs[0];
            bb.max.Z = model.mesh.vertexs[0];

            for (int i = 8; i < model.mesh.vertexs.Count; i += 8)
            {
                if (model.mesh.vertexs[i + 0] < bb.min.X) bb.min.X = model.mesh.vertexs[i + 0];
                if (model.mesh.vertexs[i + 1] < bb.min.Y) bb.min.Y = model.mesh.vertexs[i + 1];
                if (model.mesh.vertexs[i + 2] < bb.min.Z) bb.min.Z = model.mesh.vertexs[i + 2];
                if (model.mesh.vertexs[i + 0] > bb.max.X) bb.max.X = model.mesh.vertexs[i + 0];
                if (model.mesh.vertexs[i + 1] > bb.max.Y) bb.max.Y = model.mesh.vertexs[i + 1];
                if (model.mesh.vertexs[i + 2] > bb.max.Z) bb.max.Z = model.mesh.vertexs[i + 2];
            }
            bb.min.X = bb.min.X * model.scale.X + model.translation.X;
            bb.min.Y = bb.min.Y * model.scale.Y + model.translation.Y;
            bb.min.Z = bb.min.Z * model.scale.Z + model.translation.Z;
            bb.max.X = bb.max.X * model.scale.X + model.translation.X;
            bb.max.Y = bb.max.Y * model.scale.Y + model.translation.Y;
            bb.max.Z = bb.max.Z * model.scale.Z + model.translation.Z;
            return bb;
        }

        // bake mesh
        // create non-indexed vertex lists using the given data
        // determine how many verts are exactly the same via hashing?
        // if it is worth it, generate index buffer to optimize
        //
        // outputs a mesh with vertex stride: POS, POS, POS, UV, UV, NORM, NORM, NORM
        private fbxMesh bake(fbxMesh mesh)
        {
            List<string> vertexstring = new List<string>();  // the stringified vertex list uniq to cull duplicate verts
            int index = 0;
            int normal = 0;
            // convert all verts into a string
            do
            {
                string vert = "";
                int v = mesh.indexes[index] * 3;
                vert += mesh.vertexs[v] + " ";
                vert += mesh.vertexs[v + 1] + " ";
                vert += mesh.vertexs[v + 2] + " ";
                var v2 = mesh.uvindex[index] * 2;
                vert += mesh.uv[v2] + " ";
                vert += mesh.uv[v2 + 1] + " ";
                if (mesh.mapping == 0)
                {
                    vert += mesh.normals[normal] + " ";
                    vert += mesh.normals[normal + 1] + " ";
                    vert += mesh.normals[normal + 2] + " ";
                }
                else
                {
                    vert += mesh.normals[v] + " ";
                    vert += mesh.normals[v + 1] + " ";
                    vert += mesh.normals[v + 2] + " ";
                }
                index += 1;
                normal += 3;
                vertexstring.Add(vert);
            } while (index != mesh.indexes.Count);

            // how many are the same? cull the list
            List<string> uniq = unique(vertexstring);
            List<float> vertexs = new List<float>();
            List<int> indexes = new List<int>();

            // convert the uniq string list back to an actual vertex list of ints
            foreach (string s in uniq)
                foreach (string f in s.Trim().Split(' '))
                    vertexs.Add((float)Convert.ToDouble(f));

            // use the vertexstring list as a key to build the index list
            //   to do this we need a convenience reverse lookup array
            Dictionary<string, int> reverse = new Dictionary<string, int>();
            for (var i = 0; i < uniq.Count; ++i) reverse[uniq[i]] = i;
            for (var i = 0; i < vertexstring.Count; ++i) indexes.Add(reverse[vertexstring[i]]);

            fbxMesh ret = new fbxMesh();
            ret.indexes = indexes;
            ret.vertexs = vertexs;
            return ret;
        }

        private void save()
        {
            if (curmesh.id.Length != 0)
            {
                objects[curmesh.id] = curmesh;
                curmesh = new fbxMesh();
            }
            if (curmodel.id.Length != 0)
            {
                objects[curmodel.id] = curmodel;
                curmodel = new fbxModel();
            }
            if (curTexture.id.Length != 0)
            {
                objects[curTexture.id] = curTexture;
                curTexture = new fbxTexture();
            }
            if (curmaterial.id.Length != 0)
            {
                objects[curmaterial.id] = curmaterial;
                curmaterial = new fbxMaterial();
            }
        }

        private int lastmapping = 0;

        private void log(string text)
        {

        }

        public void process(string data)
        {
            string[] lines = data.Replace("\r", "").Split('\n');
            // hunt for objects
            int i = 0;
            States state = States.FindObjects;

            for (; i < lines.Length; ++i) if (lines[i].IndexOf("Objects: ") != -1) { state = States.FindTag; break; }
            if (state == States.FindObjects) { log("ERROR: File is not an FBX file."); return; }

            // watch for geometry, model, material, connections
            for (; i < lines.Length; ++i)
            {
                if (lines[i].IndexOf("Geometry: ") != -1)
                {
                    save();
                    curmesh.id = lines[i].Split(':')[1].Split(',')[0].Trim();
                    lastmapping = 0;
                    state = States.ParseGeometry;
                }
                else if (lines[i].IndexOf("ShadingModel: ") != -1) { }
                else if (lines[i].IndexOf("Model: ") != -1)
                {
                    save();
                    curmodel.id = lines[i].Split(':')[1].Split(',')[0].Trim();
                    curmodel.name = lines[i].Replace("::", ",").Split(':')[1].Split(',')[2].Replace("\"", "").Trim();

                    log("Found model: " + curmodel.name);
                    state = States.ParseModel;
                }
                else if (lines[i].IndexOf("LayerElementMaterial: ") != -1) { }
                else if (lines[i].IndexOf("Material: ") != -1)
                {
                    save();
                    curmaterial.id = lines[i].Split(':')[1].Split(',')[0].Trim();
                    curmaterial.name = lines[i].Replace("::", ",").Split(':')[1].Split(',')[2].Replace("\"", "").Trim();

                    log("Found material: " + curmaterial.name);
                    state = States.ParseMaterial;
                }
                else if (lines[i].IndexOf("Connections: ") != -1)
                {
                    save();
                    state = States.ParseConnections;
                }
                else if (lines[i].IndexOf("Texture: ") != -1)
                {
                    save();
                    curTexture.id = lines[i].Split(':')[1].Split(',')[0].Trim();
                    state = States.ParseTexture;
                }

                if (state == States.ParseConnections)
                {
                    if (lines[i].IndexOf("C: ") != -1)
                    {
                        var values = lines[i].Split(',');
                        try
                        {
                            var obj1 = objects[values[1].Trim()];
                            var obj2 = objects[values[2].Trim()];

                            if (obj1.type == "mesh" && obj2.type == "model")
                            {
                                log("Model " + ((fbxModel)obj2).name + " has geometry");
                                ((fbxModel)obj2).mesh = (fbxMesh)obj1;
                            }
                            else if (obj1.type == "material" && obj2.type == "model") ((fbxMaterial)obj1).models.Add((fbxModel)obj2);
                            else if (obj2.type == "material" && obj1.type == "texture") ((fbxMaterial)obj2).texture = ((fbxTexture)obj1).file;
                        } catch { }
                    }
                }

                else if (state == States.ParseTexture)
                {
                    if (lines[i].IndexOf("RelativeFilename") != -1)
                    {
                        var values = lines[i].Trim().Split('"');
                        var parts = values[1].Trim().Split('\\');
                        curTexture.file = parts[parts.Length - 1];
                        log("Found texture: " + curTexture.file);
                    }
                }
                else if (state == States.ParseModel)
                {
                    if (lines[i].IndexOf("Lcl Rotation") != -1)
                    {
                        var values = lines[i].Trim().Split(',');
                        curmodel.rotation.X = (float)Convert.ToDouble(values[4]);
                        curmodel.rotation.Y = (float)Convert.ToDouble(values[5]);
                        curmodel.rotation.Z = (float)Convert.ToDouble(values[6]);
                    }
                    if (lines[i].IndexOf("Lcl Translation") != -1)
                    {
                        var values = lines[i].Trim().Split(',');
                        curmodel.translation.X = (float)Convert.ToDouble(values[4]);
                        curmodel.translation.Y = (float)Convert.ToDouble(values[5]);
                        curmodel.translation.Z = (float)Convert.ToDouble(values[6]);
                    }
                    else if (lines[i].IndexOf("Lcl Scaling") != -1)
                    {
                        var values = lines[i].Trim().Split(',');
                        curmodel.scale.X = (float)Convert.ToDouble(values[4]);
                        curmodel.scale.Y = (float)Convert.ToDouble(values[5]);
                        curmodel.scale.Z = (float)Convert.ToDouble(values[6]);
                    }
                }

                else if (state == States.ParseMaterial)
                {
                    if (lines[i].IndexOf("P: ") != -1)
                    {
                        var values = lines[i].Split(':')[1].Trim().Split(',');
                        //                        curmaterial[values[0].replace(/\"/g, "")] = values.slice(4, 7);
                    }
                }

                else if (state == States.ParseGeometry)
                {
                    if (lines[i].IndexOf("Vertices: ") != -1)
                    {
                        if (curmesh.vertexs.Count > 0) { log("ERROR: multiple vertexes at line " + i); continue; }

                        var num = Convert.ToInt32(lines[i].Split('*')[1].Split(' ')[0]);
                        List<float> values = new List<float>();
                        // get lines until values is num
                        for (++i; i < lines.Length; ++i)
                        {
                            if (lines[i].IndexOf('}') != -1) break;
                            var a = lines[i].IndexOf("a:");
                            if (a != -1) lines[i] = lines[i].Substring(a + 3);
                            //                            values.pop();
                            foreach (string v in lines[i].Split(',')) if (v.Length>0) values.Add((float)Convert.ToDouble(v));
                            if (values.Count == num) break;
                        }
                        curmesh.vertexs = values;
                    }
                    else if (lines[i].IndexOf("PolygonVertexIndex: ") != -1)
                    {
                        if (curmesh.indexes.Count != 0) { log("EROR: multiple indexes at line " + i); continue; }

                        var num = Convert.ToInt32(lines[i].Split('*')[1].Split(' ')[0]);
                        List<int> values = new List<int>();
                        // get lines until values is num
                        for (++i; i < lines.Length; ++i)
                        {
                            if (lines[i].IndexOf('}') != -1) break;
                            var a = lines[i].IndexOf("a:");
                            if (a != -1) lines[i] = lines[i].Substring(a + 3);
                            //                            values.pop();
                            foreach (string v in lines[i].Split(',')) if (v.Length > 0) values.Add(Convert.ToInt32(v));
                            if (values.Count == num) break;
                        }
                        // check that its trilist and flip the negative ones
                        for (var j = 2; j < values.Count; j += 3)
                        {
                            if (values[j] >= 0) { log("ERROR: mesh is not a triangle list at line " + i); break; }
                            values[j] = (values[j] * -1) - 1;
                        }
                        curmesh.indexes = values;
                    }
                    else if (lines[i].IndexOf("MappingInformationType: ") != -1)
                    {
                        if (lines[i].IndexOf("ByVertice") != -1) lastmapping = 1;
                        else lastmapping = 0;
                    }
                    else if (lines[i].IndexOf("Normals: ") != -1)
                    {
                        if (curmesh.normals.Count > 0) { log("ERROR: multiple normals at line " + i); continue; }

                        var num = Convert.ToInt32(lines[i].Split('*')[1].Split(' ')[0]);
                        List<float> values = new List<float>();
                        // get lines until values is num
                        for (++i; i < lines.Length; ++i)
                        {
                            if (lines[i].IndexOf('}') != -1) break;
                            var a = lines[i].IndexOf("a:");
                            if (a != -1) lines[i] = lines[i].Substring(a + 3);
                            //                            values.pop();
                            foreach (string v in lines[i].Split(',')) if (v.Length > 0) values.Add((float)Convert.ToDouble(v));
                            if (values.Count == num) break;
                        }
                        curmesh.normals = values;
                        curmesh.mapping = lastmapping;
                    }
                    else if (lines[i].IndexOf("LayerElementUV: ") != -1) { }
                    else if (lines[i].IndexOf("UV: ") != -1)
                    {
                        if (curmesh.uv.Count != 0) { log("ERROR: multiple UVs at line " + i); continue; }

                        var num = Convert.ToInt32(lines[i].Split('*')[1].Split(' ')[0]);
                        List<float> uvvalues = new List<float>();
                        // get lines until values is num
                        for (++i; i < lines.Length; ++i)
                        {
                            if (lines[i].IndexOf('}') != -1) break;
                            var a = lines[i].IndexOf("a:");
                            if (a != -1) lines[i] = lines[i].Substring(a + 3);
                            //                            uvvalues.pop();
                            foreach (string v in lines[i].Split(',')) if (v.Length > 0) uvvalues.Add((float)Convert.ToDouble(v));
                            if (uvvalues.Count == num) break;
                        }
                        curmesh.uv = uvvalues;
                    }
                    else if (lines[i].IndexOf("UVIndex: ") != -1)
                    {
                        var num = Convert.ToInt32(lines[i].Split('*')[1].Split(' ')[0]);
                        List<int> values = new List<int>();
                        // get lines until values is num
                        for (++i; i < lines.Length; ++i)
                        {
                            if (lines[i].IndexOf('}') != -1) break;
                            var a = lines[i].IndexOf("a:");
                            if (a != -1) lines[i] = lines[i].Substring(a + 3);
                            //                            values.pop();
                            foreach (string v in lines[i].Split(',')) if (v.Length > 0) values.Add(Convert.ToInt32(v));
                            if (values.Count == num) break;
                        }
                        curmesh.uvindex = values;
                    }
                }
            }
            save();
            done();
        }
    }
}