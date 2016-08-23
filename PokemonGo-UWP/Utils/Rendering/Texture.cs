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


    internal class fbxBB
    {
        public Vector3 min;
        public Vector3 max;
    }

    internal class fbxObject
    {
        public string type = "none";
        public int id;
    }

    internal class fbxMesh : fbxObject
    {
        public fbxMesh() { type = "mesh";  }

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
    }

    internal class fbxTexture : fbxObject
    {
        public fbxTexture() { type = "texture"; }
    }

    internal class fbxFile
    {
        public Dictionary<string, int> attributes = new Dictionary<string, int>();
        public List<fbxMaterial> groups = new List<fbxMaterial>();
        public fbxBB boundingbox;
    }

    class mxFBXLoaderA
    {
        private enum States { FindObjects, FindTag, ParseGeometry, ParseModel, ParseMaterial, ParseConnections, ParseTexture };
        private List<fbxMaterial> groups = new List<fbxMaterial>();
        private Dictionary<int, fbxObject> objects = new Dictionary<int, fbxObject>();
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
//            if (curmesh.id)
//            {
//                objects[curmesh.id] = curmesh;
//                curmesh = { type: "mesh" };
//            }
//            if (curmodel.id)
//            {
//                objects[curmodel.id] = curmodel;
//                curmodel = { type: "model" };
//            }
//            if (curTexture.id)
//            {
//                objects[curTexture.id] = curTexture;
//                curTexture = { type: "texture" };
//            }
//            if (curmaterial.id)
//            {
//                objects[curmaterial.id] = curmaterial;
//                curmaterial = { type: "material", models: [] };
//            }        
        }

        private int lastmapping = 0;

        private void process(string data)
        {
//            var lines = data.replace(/\r / g, "").split("\n");
//            // hunt for objects
//            var i = 0;
//            var state = States.FindObjects;
//        
//            for (; i < lines.length; ++i) if (lines[i].indexOf("Objects: ") != -1) { state = States.FindTag; break; }
//            if (state == States.FindObjects) { log("ERROR: File is not an FBX file."); return; }
//        
//            // watch for geometry, model, material, connections
//            for (; i < lines.length; ++i)
//            {
//                datalog(lines[i]);
//        
//                if (lines[i].indexOf("Geometry: ") != -1)
//                {
//                    save();
//                    curmesh.id = lines[i].split(":")[1].split(",")[0].trim();
//                    lastmapping = 0;
//                    state = States.ParseGeometry;
//                }
//                else if (lines[i].indexOf("ShadingModel: ") != -1) { }
//                else if (lines[i].indexOf("Model: ") != -1)
//                {
//                    save();
//                    curmodel.id = lines[i].split(":")[1].split(",")[0].trim();
//                    curmodel.name = lines[i].replace("::", ",").split(":")[1].split(",")[2].replace(/\"/g, "").trim();
//              
//                    log("Found model: " + curmodel.name);
//                    state = States.ParseModel;
//                }
//                else if (lines[i].indexOf("LayerElementMaterial: ") != -1) { }
//                else if (lines[i].indexOf("Material: ") != -1)
//                {
//                    save();
//                    curmaterial.id = lines[i].split(":")[1].split(",")[0].trim();
//                    curmaterial.name = lines[i].replace("::", ",").split(":")[1].split(",")[2].replace(/\"/g,"").trim();
//              
//                    log("Found material: " + curmaterial.name);
//                    state = States.ParseMaterial;
//                }
//                else if (lines[i].indexOf("Connections: ") != -1)
//                {
//                    save();
//                    state = States.ParseConnections;
//                }
//                else if (lines[i].indexOf("Texture: ") != -1)
//                {
//                    save();
//                    curTexture.id = lines[i].split(":")[1].split(",")[0].trim();
//                    state = States.ParseTexture;
//                }
//        
//                if (state == States.ParseConnections)
//                {
//                    if (lines[i].indexOf("C: ") != -1)
//                    {
//                        var values = lines[i].split(",");
//                        var obj1 = objects[values[1].trim()];
//                        var obj2 = objects[values[2].trim()];
//                        if (!obj1 || !obj2) continue;
//        
//                        if (obj1.type === "mesh" && obj2.type === "model") { log("Model " + obj2.name + " has geometry"); obj2.mesh = obj1; }
//                        else if (obj1.type === "material" && obj2.type === "model") obj1.models[obj1.models.length] = obj2;
//                        else if (obj2.type === "material" && obj1.type === "texture") obj2.texture = obj1.file;
//                    }
//                }
//        
//                else if (state == States.ParseTexture)
//                {
//                    if (lines[i].indexOf("RelativeFilename") != -1)
//                    {
//                        var values = lines[i].trim().split("\"");
//                        var parts = values[1].trim().split("\\");
//                        curTexture.file = parts[parts.length - 1];
//                        log("Found texture: " + curTexture.file);
//                    }
//                }
//                else if (state == States.ParseModel)
//                {
//                    if (lines[i].indexOf("Lcl Rotation") != -1)
//                    {
//                        var values = lines[i].trim().split(",");
//                        curmodel.rotation = [parseFloat(values[4]), parseFloat(values[5]), parseFloat(values[6])];
//                    }
//                    if (lines[i].indexOf("Lcl Translation") != -1)
//                    {
//                        var values = lines[i].trim().split(",");
//                        curmodel.translation = [parseFloat(values[4]), parseFloat(values[5]), parseFloat(values[6])];
//                    }
//                    else if (lines[i].indexOf("Lcl Scaling") != -1)
//                    {
//                        var values = lines[i].trim().split(",");
//                        curmodel.scale = [parseFloat(values[4]), parseFloat(values[5]), parseFloat(values[6])];
//                    }
//                }
//        
//                else if (state == States.ParseMaterial)
//                {
//                    if (lines[i].indexOf("P: ") != -1)
//                    {
//                        var values = lines[i].split(":")[1].trim().split(",");
//                        curmaterial[values[0].replace(/\"/g, "")] = values.slice(4, 7);
//                    }
//                }
//        
//                else if (state == States.ParseGeometry)
//                {
//                    if (lines[i].indexOf("Vertices: ") != -1)
//                    {
//                        if (curmesh.vertexs) { log("ERROR: multiple vertexes at line " + i); continue; }
//        
//                        var num = lines[i].split("*")[1].split(" ")[0];
//                        var values = [];
//                        // get lines until values is num
//                        for (++i; i < lines.length; ++i)
//                        {
//                            if (lines[i].indexOf('}') != -1) break;
//                            var a = lines[i].indexOf('a:');
//                            if (a != -1) lines[i] = lines[i].substr(a + 3);
//                            values.pop();
//                            values = values.concat(lines[i].split(","));
//                            if (values.length == num) break;
//                        }
//                        curmesh.vertexs = values;
//                    }
//                    else if (lines[i].indexOf("PolygonVertexIndex: ") != -1)
//                    {
//                        if (curmesh.indexs) { log("EROR: multiple indexes at line " + i); continue; }
//        
//                        var num = lines[i].split("*")[1].split(" ")[0];
//                        var values = [];
//                        // get lines until values is num
//                        for (++i; i < lines.length; ++i)
//                        {
//                            if (lines[i].indexOf('}') != -1) break;
//                            var a = lines[i].indexOf('a:');
//                            if (a != -1) lines[i] = lines[i].substr(a + 3);
//                            values.pop();
//                            values = values.concat(lines[i].split(","));
//                            if (values.length == num) break;
//                        }
//                        // check that its trilist and flip the negative ones
//                        for (var j = 2; j < values.length; j += 3)
//                        {
//                            if (values[j] >= 0) { log("ERROR: mesh is not a triangle list at line " + i); break; }
//                            values[j] = (values[j] * -1) - 1;
//                        }
//                        curmesh.indexes = values;
//                    }
//                    else if (lines[i].indexOf("MappingInformationType: ") != -1)
//                    {
//                        if (lines[i].indexOf("ByVertice") != -1) lastmapping = 1;
//                        else lastmapping = 0;
//                    }
//                    else if (lines[i].indexOf("Normals: ") != -1)
//                    {
//                        if (curmesh.normals) { log("ERROR: multiple normals at line " + i); continue; }
//        
//                        var num = lines[i].split("*")[1].split(" ")[0];
//                        var values = [];
//                        // get lines until values is num
//                        for (++i; i < lines.length; ++i)
//                        {
//                            if (lines[i].indexOf('}') != -1) break;
//                            var a = lines[i].indexOf('a:');
//                            if (a != -1) lines[i] = lines[i].substr(a + 3);
//                            values.pop();
//                            values = values.concat(lines[i].split(","));
//                            if (values.length == num) break;
//                        }
//                        curmesh.normals = values;
//                        curmesh.mapping = lastmapping;
//                    }
//                    else if (lines[i].indexOf("LayerElementUV: ") != -1) { }
//                    else if (lines[i].indexOf("UV: ") != -1)
//                    {
//                        if (curmesh.uv) { log("ERROR: multiple UVs at line " + i); continue; }
//        
//                        var num = lines[i].split("*")[1].split(" ")[0];
//                        var uvvalues = [];
//                        var values = [];
//                        // get lines until values is num
//                        for (++i; i < lines.length; ++i)
//                        {
//                            if (lines[i].indexOf('}') != -1) break;
//                            var a = lines[i].indexOf('a:');
//                            if (a != -1) lines[i] = lines[i].substr(a + 3);
//                            uvvalues.pop();
//                            uvvalues = uvvalues.concat(lines[i].split(","));
//                            if (uvvalues.length == num) break;
//                        }
//                        curmesh.uv = uvvalues;
//                    }
//                    else if (lines[i].indexOf("UVIndex: ") != -1)
//                    {
//                        num = lines[i].split("*")[1].split(" ")[0];
//                        // get lines until values is num
//                        for (++i; i < lines.length; ++i)
//                        {
//                            if (lines[i].indexOf('}') != -1) break;
//                            var a = lines[i].indexOf('a:');
//                            if (a != -1) lines[i] = lines[i].substr(a + 3);
//                            values.pop();
//                            values = values.concat(lines[i].split(","));
//                            if (values.length == num) break;
//                        }
//                        curmesh.uvindex = values;
//                    }
//                }
//            }
//            save();
//            done();
        }
    }
}