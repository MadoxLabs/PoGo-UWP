using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Buffer = SharpDX.Direct3D11.Buffer;
using Ionic.Zlib;

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

    public class fbxBB
    {
        public Vector3 min;
        public Vector3 max;
    }

    public class fbxObject
    {
        public string type = "none";
        public string id = "";
    }

    public class fbxMesh : fbxObject
    {
        public fbxMesh() { type = "mesh"; }

        public List<float> vertexs = new List<float>();
        public List<float> uv = new List<float>();
        public List<float> normals = new List<float>();
        public List<int> indexes = new List<int>();
        public List<int> uvindex = new List<int>();
        public int mapping;
    }

    public class fbxModel : fbxObject
    {
        public fbxModel() { type = "model"; }

        public string name;
        public fbxMesh mesh;
        public fbxBB boundingbox;
        public Vector3 translation = new Vector3(0, 0, 0);
        public Vector3 rotation = new Vector3(0, 0, 0);
        public Vector3 scale = new Vector3(1, 1, 1);
    }

    public class fbxMaterial : fbxObject
    {
        public fbxMaterial() { type = "material"; }

        public List<fbxModel> models = new List<fbxModel>();
        public Dictionary<string, Vector3> traits = new Dictionary<string, Vector3>();
        public fbxTexture texture;
        public string name;
    }

    public class fbxTexture : fbxObject
    {
        public fbxTexture() { type = "texture"; }
        public string file;
        public float alpha;
    }

    public class fbxLayeredTexture : fbxObject
    {
        public fbxLayeredTexture() { type = "layeredtexture"; }
        public float alpha;
        public fbxTexture texture;
    }

    public class fbxFile
    {
        public Dictionary<string, int> attributes = new Dictionary<string, int>();
        public List<fbxMaterial> groups = new List<fbxMaterial>();
        public fbxBB boundingbox;
    }

    public class mxFBXLoaderA
    {
        private enum States { FindObjects, FindTag, ParseGeometry, ParseModel, ParseMaterial, ParseConnections, ParseTexture, ParseLayeredTexture };
        private List<fbxMaterial> groups = new List<fbxMaterial>();
        private Dictionary<string, fbxObject> objects = new Dictionary<string, fbxObject>();
        private fbxMesh curmesh = new fbxMesh();
        private fbxModel curmodel = new fbxModel();
        private fbxMaterial curmaterial = new fbxMaterial();
        private fbxTexture curTexture = new fbxTexture();
        private fbxLayeredTexture curLayeredTexture = new fbxLayeredTexture();

        public fbxFile file = null;

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

            file = new fbxFile();
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

            log("baking mesh: " + mesh.id + " - " + vertexstring.Count + " down to " + uniq.Count);

            // convert the uniq string list back to an actual vertex list of ints
            foreach (string s in uniq)
                foreach (string f in s.Trim().Split(' '))
                    vertexs.Add(toFloat(f));

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
            if (curLayeredTexture.id.Length != 0)
            {
                objects[curLayeredTexture.id] = curLayeredTexture;
                curLayeredTexture = new fbxLayeredTexture();
            }
            if (curmaterial.id.Length != 0)
            {
                objects[curmaterial.id] = curmaterial;
                curmaterial = new fbxMaterial();
            }
        }

        private int lastmapping = 0;

        private string logtext = "";
        private void log(string text)
        {
            logtext += text + "\n";
        }

        private void dumplog()
        {
            System.Diagnostics.Debug.WriteLine(logtext);
            logtext = "";
        }

        private float toFloat(string v)
        {
            try { return (float)Convert.ToDouble(v); }
            catch { return 0; }
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
                    log("found geometry: " + curmesh.id);

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
                else if (lines[i].IndexOf("LayeredTexture: ") != -1)
                {
                    if (lines[i].IndexOf("{") > -1)
                    {
                        save();
                        curLayeredTexture.id = lines[i].Split(':')[1].Split(',')[0].Trim();
                        state = States.ParseLayeredTexture;
                        log("found layered texture: " + curLayeredTexture.id);
                    }
                }
                else if (lines[i].IndexOf("Texture: ") != -1)
                {
                    save();
                    curTexture.id = lines[i].Split(':')[1].Split(',')[0].Trim();
                    state = States.ParseTexture;
                    log("found texture: " + curTexture.id);
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

                            //                            log("connection from " + obj1.id + " ("+obj1.type+") to " + obj2.id + " ("+ obj2.type + ")");

                            if (obj1.type == "mesh" && obj2.type == "model")
                            {
                                log(" Model " + ((fbxModel)obj2).name + " has geometry");
                                ((fbxModel)obj2).mesh = (fbxMesh)obj1;
                            }
                            else if (obj1.type == "material" && obj2.type == "model")
                            {
                                log(" Material " + ((fbxMaterial)obj1).name + " has model " + ((fbxModel)obj2).name);
                                ((fbxMaterial)obj1).models.Add((fbxModel)obj2);
                            }
                            else if (obj1.type == "texture" && obj2.type == "material")
                            {
                                log(" Material " + ((fbxMaterial)obj2).name + " has texture " + ((fbxTexture)obj1).file);
                                ((fbxMaterial)obj2).texture = ((fbxTexture)obj1);
                            }
                            else if (obj1.type == "layeredtexture" && obj2.type == "material")
                            {
                                log(" Material " + ((fbxMaterial)obj2).name + " has layeredtexture " + ((fbxLayeredTexture)obj1).id);
                                log("  this reslves to texture " + ((fbxLayeredTexture)obj1).texture.file);
                                ((fbxMaterial)obj2).texture = ((fbxLayeredTexture)obj1).texture;
                            }
                            else if (obj1.type == "texture" && obj2.type == "layeredtexture")
                            {
                                log(" Texture " + ((fbxTexture)obj1).file + " has layer " + obj2.id);
                                ((fbxLayeredTexture)obj2).texture = ((fbxTexture)obj1);
                                ((fbxTexture)obj1).alpha = ((fbxLayeredTexture)obj2).alpha;
                            }
                        }
                        catch { }
                    }
                }

                else if (state == States.ParseTexture)
                {
                    if (lines[i].IndexOf("RelativeFilename") != -1)
                    {
                        var values = lines[i].Trim().Split('"');
                        var parts = values[1].Trim().Split('\\');
                        curTexture.file = parts[parts.Length - 1];
                        log(" texture file: " + curTexture.file);
                    }
                }
                else if (state == States.ParseLayeredTexture)
                {
                    if (lines[i].IndexOf("Alphas") != -1)
                    {
                        var values = lines[i].Trim().Split(':');
                        curLayeredTexture.alpha = toFloat(values[1].Trim());
                        log(" layer texture alpha: " + curLayeredTexture.alpha);
                    }
                }
                else if (state == States.ParseModel)
                {
                    if (lines[i].IndexOf("Lcl Rotation") != -1)
                    {
                        var values = lines[i].Trim().Split(',');
                        curmodel.rotation.X = toFloat(values[4]);
                        curmodel.rotation.Y = toFloat(values[5]);
                        curmodel.rotation.Z = toFloat(values[6]);
                    }
                    if (lines[i].IndexOf("Lcl Translation") != -1)
                    {
                        var values = lines[i].Trim().Split(',');
                        curmodel.translation.X = toFloat(values[4]);
                        curmodel.translation.Y = toFloat(values[5]);
                        curmodel.translation.Z = toFloat(values[6]);
                    }
                    else if (lines[i].IndexOf("Lcl Scaling") != -1)
                    {
                        var values = lines[i].Trim().Split(',');
                        curmodel.scale.X = toFloat(values[4]);
                        curmodel.scale.Y = toFloat(values[5]);
                        curmodel.scale.Z = toFloat(values[6]);
                    }
                }

                else if (state == States.ParseMaterial)
                {
                    if (lines[i].IndexOf("P: ") != -1)
                    {
                        var values = lines[i].Split(':')[1].Trim().Split(',');
                        Vector3 v = new Vector3(0, 0, 0);
                        string name = values[0].Replace("\"", "");
                        if (values.Length > 4) v.X = toFloat(values[4]);
                        if (values.Length > 5) v.Y = toFloat(values[5]);
                        if (values.Length > 6) v.Z = toFloat(values[6]);
                        curmaterial.traits[name] = v;
                        log(" material " + name + ": " + v.X + " " + v.Y + " " + v.Z);
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
                            foreach (string v in lines[i].Split(',')) if (v.Length > 0) values.Add(toFloat(v));
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
                            foreach (string v in lines[i].Split(',')) if (v.Length > 0) values.Add(toFloat(v));
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
                            foreach (string v in lines[i].Split(',')) if (v.Length > 0) uvvalues.Add(toFloat(v));
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
            dumplog();
            done();
            dumplog();
        }
    }

    public class mxFBXLoaderB
    {
        private string logtext = "";
        private string indent = "";
        private void log(string text)
        {
            logtext += "\n"+indent + text;
        }

        private void dumplog()
        {
            System.Diagnostics.Debug.WriteLine(logtext);
            logtext = "";
        }

        private int getInteger(fbxCursor cursor)
        {
            int val = BitConverter.ToInt32(cursor.data, cursor.offset);
            cursor.offset += 4;
            return val;
        }

        private Int16 getSmallInteger(fbxCursor cursor)
        {
            Int16 val = BitConverter.ToInt16(cursor.data, cursor.offset);
            cursor.offset += 2;
            return val;
        }

        private Int64 getWideInteger(fbxCursor cursor)
        {
            Int64 val = BitConverter.ToInt64(cursor.data, cursor.offset);
            cursor.offset += 8;
            return val;
        }

        private float getFloat(fbxCursor cursor)
        {
            float val = BitConverter.ToSingle(cursor.data, cursor.offset);
            cursor.offset += 4;
            return val;
        }

        private double getWideFloat(fbxCursor cursor)
        {
            double val = BitConverter.ToDouble(cursor.data, cursor.offset);
            cursor.offset += 8;
            return val;
        }

        private byte getShort(fbxCursor cursor)
        {
            byte ret = cursor.data[cursor.offset]; // BitConverter.ToInt8(cursor.data, cursor.offset);
            cursor.offset += 1;
            return ret;
        }

        private char getChar(fbxCursor cursor)
        {
            char ret = (char)cursor.data[cursor.offset]; //BitConverter.ToChar(cursor.data, cursor.offset);
            cursor.offset += 1;
            return ret;
        }

        private bool getBool(fbxCursor cursor)
        {
            char ret = (char)cursor.data[cursor.offset]; //BitConverter.ToChar(cursor.data, cursor.offset);
            cursor.offset += 1;
            return ret == 0 ? true : false;
        }

        private string getString(fbxCursor cursor)
        {
            var len = getShort(cursor);
            var result = "";
            for (var i = 0; i < len; i++)
                result += getChar(cursor);
            return result;
        }

        private string getLongString(fbxCursor cursor)
        {
            var len = getInteger(cursor);
            var result = "";
            for (var i = 0; i < len; i++)
                result += getChar(cursor);
            return result;
        }

        private string getData(fbxCursor cursor)
        {
            var len = getInteger(cursor);
            cursor.offset += len;
            return "(" + len + "bytes)";
        }

        private float[] getArrayFloat(fbxCursor cursor)
        {
            var len = getInteger(cursor);
            var encoding = getInteger(cursor);
            var encodinglen = getInteger(cursor);

            if (encoding > 0)
            {
                Stream stream = new MemoryStream(cursor.data, cursor.offset, encodinglen);
                ZlibStream unzip = new ZlibStream(stream, Ionic.Zlib.CompressionMode.Decompress);
                byte[] unzipdata = new byte[len * 4];
                fbxCursor unzipped = new fbxCursor(ref unzipdata, 0);
                unzip.Read(unzipped.data, 0, len * 4);
                cursor.offset += encodinglen;
                cursor = unzipped;
            }

            float[] ret = new float[len];
            for (int i = 0; i < len; ++i)
                ret[i] = getFloat(cursor);

            return ret;
        }

        private double[] getArrayWideFloat(fbxCursor cursor)
        {
            var len = getInteger(cursor);
            var encoding = getInteger(cursor);
            var encodinglen = getInteger(cursor);

            if (encoding > 0)
            {
                Stream stream = new MemoryStream(cursor.data, cursor.offset, encodinglen);
                ZlibStream unzip = new ZlibStream(stream, Ionic.Zlib.CompressionMode.Decompress);
                byte[] unzipdata = new byte[len*8];
                fbxCursor unzipped = new fbxCursor(ref unzipdata, 0);
                unzip.Read(unzipped.data, 0, len*8);
                cursor.offset += encodinglen;
                cursor = unzipped;
            }

            double[] ret = new double[len];
            for (int i = 0; i < len; ++i)
                ret[i] = getWideFloat(cursor);

            return ret;
        }

        private int[] getArrayInteger(fbxCursor cursor)
        {
            var len = getInteger(cursor);
            var encoding = getInteger(cursor);
            var encodinglen = getInteger(cursor);

            if (encoding > 0)
            {
                Stream stream = new MemoryStream(cursor.data, cursor.offset, encodinglen);
                ZlibStream unzip = new ZlibStream(stream, Ionic.Zlib.CompressionMode.Decompress);
                byte[] unzipdata = new byte[len * 4];
                fbxCursor unzipped = new fbxCursor(ref unzipdata, 0);
                unzip.Read(unzipped.data, 0, len * 4);
                cursor.offset += encodinglen;
                cursor = unzipped;
            }

            int[] ret = new int[len];
            for (int i = 0; i < len; ++i)
                ret[i] = getInteger(cursor);

            return ret;
        }

        private Int64[] getArrayWideInteger(fbxCursor cursor)
        {
            var len = getInteger(cursor);
            var encoding = getInteger(cursor);
            var encodinglen = getInteger(cursor);

            if (encoding > 0)
            {
                Stream stream = new MemoryStream(cursor.data, cursor.offset, encodinglen);
                ZlibStream unzip = new ZlibStream(stream, Ionic.Zlib.CompressionMode.Decompress);
                byte[] unzipdata = new byte[len * 8];
                fbxCursor unzipped = new fbxCursor(ref unzipdata, 0);
                unzip.Read(unzipped.data, 0, len * 8);
                cursor.offset += encodinglen;
                cursor = unzipped;
            }

            Int64[] ret = new Int64[len];
            for (int i = 0; i < len; ++i)
                ret[i] = getWideInteger(cursor);

            return ret;
        }

        private bool[] getArrayBool(fbxCursor cursor)
        {
            var len = getInteger(cursor);
            var encoding = getInteger(cursor);
            var encodinglen = getInteger(cursor);

            if (encoding > 0)
            {
                Stream stream = new MemoryStream(cursor.data, cursor.offset, encodinglen);
                ZlibStream unzip = new ZlibStream(stream, Ionic.Zlib.CompressionMode.Decompress);
                byte[] unzipdata = new byte[len * 1];
                fbxCursor unzipped = new fbxCursor(ref unzipdata, 0);
                unzip.Read(unzipped.data, 0, len * 1);
                cursor.offset += encodinglen;
                cursor = unzipped;
            }

            bool[] ret = new bool[len];
            for (int i = 0; i < len; ++i)
                ret[i] = getBool(cursor);

            return ret;
        }

        // make classes for each type:
        // fbxData { enum type, byte[] data, function Value() { return real type } }
        // obj thing that holds { Dictionary<string, List<obj> subobj, Dictionary<int, fbxData> values }
        internal class fbxCursor
        {
            public fbxCursor(ref byte[] d, int o)
            {
                data = d;
                offset = o;
            }

            public byte[] data;
            public int offset;
        }

        private class fbxData
        {
            public fbxData(char t) { type = t; }
            public char type;
            public string _val = "";

            public string toString() { return _val;  }
        }

        private class fbxData<Type> : fbxData
        {
            public fbxData(Type v, char t) : base(t) { value = v; _val = value.ToString();  }
            public Type value;
        }

        private class fbxRoot
        {
            public string name;
            public Dictionary<string, List<fbxRoot>> subobjects = new Dictionary<string, List<fbxRoot>>();
            public List<fbxData> values = new List<fbxData>();
        }

        private fbxData parsePropertyRecord(fbxCursor cursor)
        {
            var type = getChar(cursor);
            fbxData ret = null;
            if (type == 'Y')      ret = new fbxData<Int16>(getSmallInteger(cursor), type);
            else if (type == 'C') ret = new fbxData<bool>(getBool(cursor), type);
            else if (type == 'I') ret = new fbxData<int>(getInteger(cursor), type);
            else if (type == 'F') ret = new fbxData<float>(getFloat(cursor), type);
            else if (type == 'D') ret = new fbxData<double>(getWideFloat(cursor), type); 
            else if (type == 'L') ret = new fbxData<Int64>(getWideInteger(cursor), type);
            else if (type == 'f') ret = new fbxData<float[]>(getArrayFloat(cursor), type);
            else if (type == 'd') ret = new fbxData<double[]>(getArrayWideFloat(cursor), type);
            else if (type == 'l') ret = new fbxData<Int64[]>(getArrayWideInteger(cursor), type);
            else if (type == 'i') ret = new fbxData<int[]>(getArrayInteger(cursor), type);
            else if (type == 'b') ret = new fbxData<bool[]>(getArrayBool(cursor), type);
            else if (type == 'S') ret = new fbxData<string>(getLongString(cursor), type);
            else if (type == 'R') ret = new fbxData<string>(getData(cursor), type);
            else ret = new fbxData<string>("(missing type)", type);
            return ret;
        } 

        private bool parseObjectRecord(fbxRoot parent, fbxCursor cursor)
        {
            string buf = "";

            var end = getInteger(cursor);    // end offset
            var len = getInteger(cursor);    // num properties
            var bytelen = getInteger(cursor); // properties byte length

             var name = getString(cursor);
            fbxRoot obj = new fbxRoot();
            obj.name = name;
            buf += name;
            // add to parent
            if (parent.subobjects.ContainsKey(name) == false) parent.subobjects[name] = new List<fbxRoot>();
            parent.subobjects[name].Add(obj);

            // obj is now Dictionary<int, propertyrecord>
            for (var i = 0; i < len; ++i)
            {
                if (buf.Length > 1000) { log(buf); buf = ""; }
                if (i == 0) buf += ": ";
                if (i > 0) buf += ", ";
                var val = parsePropertyRecord(cursor);
                buf += val.toString();
                obj.values.Add(val);
            }
            log(buf);

            if (end == 0) return false;

            if (cursor.offset != end)
            {
                log("{");
                indent += "  ";
                while (cursor.offset != end)
                {
                    parseObjectRecord(obj, cursor);
                    if (end - cursor.offset == 13)
                    {
                        for (var i = 0; i < 13; ++i)
                            if (cursor.data[cursor.offset + i] != 0) log("< expected null >");
                        cursor.offset += 13;
                    }
                }

                indent = indent.Substring(0, indent.Length - 2);
                log("}");
            }
           dumplog();
            return true;
        }

        public void process(ref byte[] bytes)
        {
            // header check
            string header = "";
            for (var i = 0; i < 20; i++)
                header += Convert.ToChar(bytes[i]);

            if (header != "Kaydara FBX Binary  ") log("Missing valid header");
            if (bytes[20] != 0x0) log("Missing magic number");
            if (bytes[21] != 0x1A) log("Missing magic number");
            if (bytes[22] != 0x0) log("Missing magic number");

            // extract objects
            fbxCursor cursor = new fbxCursor(ref bytes, 23);
            log("File version:" + getInteger(cursor));

            fbxRoot root = new fbxRoot();
            while (cursor.offset != bytes.Length)
                if (!parseObjectRecord(root, cursor)) break;

            log("Decoded");
            dumplog();
        }
    }
}