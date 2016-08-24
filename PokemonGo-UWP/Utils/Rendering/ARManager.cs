using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Geolocation;
using Windows.Devices.Sensors;
using Windows.Media.Capture;
using Windows.UI.Xaml.Controls;
using System.Collections.Generic;

using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using InputElement = SharpDX.Direct3D11.InputElement;

using PokemonGo_UWP.Entities;
using PokemonGo_UWP.Rendering;
using SharpDX.IO;

namespace PokemonGo_UWP.Utils
{
  [StructLayout(LayoutKind.Sequential)]
  internal struct CommonBuffer
  {
    public Matrix view;
    public Matrix projection;
  };

  [StructLayout(LayoutKind.Sequential)]
  internal struct ModelBuffer
  {
    public Matrix model;
  };

  [StructLayout(LayoutKind.Sequential)]
  internal struct VertexPositionTex
  {
    public VertexPositionTex(Vector3 pos, Vector2 tex)
    {
      this.pos = pos;
      this.tex = tex;
    }

    public Vector3 pos;
    public Vector2 tex;
  };
    
  class arPokemon
  {
    public mxInstance instance;
    public Geopoint geo;
    public Vector3 position;
    public float scale;
    public ulong id;
  }

  class arCamera
  {
    public Vector3 eye = new Vector3(0.0f, 0.0f, 0.0f); // Define camera position
    public Vector3 forward = new Vector3(0.0f, 0.0f, 0.0f);
    public Vector3 up = new Vector3(0.0f, 0.0f, 0.0f); // Define up direction.
    public Vector3 target = new Vector3(0.0f, 0.0f, 0.0f); // Define focus position.
    public Matrix lookat;
  }

  class ARManager : Windows.UI.Xaml.Media.Imaging.SurfaceImageSource
  {
    private MediaCapture mMediaCapture = null;
    private bool mIsInitialized = false;
    private bool mIsActive = false;
    private CaptureElement mElement = null;
    private readonly SimpleOrientationSensor mOrientationSensor = SimpleOrientationSensor.GetDefault();

    // Direct3D objects
    private mxRenderManager renderMan = new mxRenderManager();

    private mxBuffer<CommonBuffer> dataCommon;
    private mxModel plane = null;
    private mxModel billboard = null;
    private mxInstance floor = null;
    private Dictionary<ulong, arPokemon> pokemons = new Dictionary<ulong, arPokemon>();
    private Dictionary<string, arPokemon> pokestops = new Dictionary<string, arPokemon>();
    private List<arPokemon> testGuys = new List<arPokemon>();
    private arCamera camera = new arCamera();

    private int width = 0;
    private int height = 0;
//    private Geopoint playerPos;

//    Geolocator geo = new Geolocator();
    public CompassHelper compass = new CompassHelper();

    public ARManager(int pixelWidth, int pixelHeight, bool isOpaque)
        : base(pixelWidth, pixelHeight, isOpaque)
    {
      width = pixelWidth;
      height = pixelHeight;
    }

    public async Task Initialize(CaptureElement element)
    {
            //            NativeFileStream fileStream = new NativeFileStream(Windows.ApplicationModel.Package.Current.InstalledLocation.Path + "\\Assets\\Shaders\\test.fbx", NativeFileMode.Open, NativeFileAccess.Read);
            //            byte[] buffer = new byte[fileStream.Length];
            //            fileStream.Read(buffer, 0, (int)fileStream.Length);
            //            mxFBXLoaderA loader = new mxFBXLoaderA();
            //            loader.process(System.Text.Encoding.ASCII.GetString(buffer));
            NativeFileStream fileStream = new NativeFileStream(Windows.ApplicationModel.Package.Current.InstalledLocation.Path + "\\Assets\\Shaders\\testb.fbx", NativeFileMode.Open, NativeFileAccess.Read);
            byte[] buffer = new byte[fileStream.Length];
            fileStream.Read(buffer, 0, (int)fileStream.Length);
            mxFBXLoaderB loader = new mxFBXLoaderB();
            loader.process(ref buffer);

            await initVideo(element);
//      initGeo();
      initDX();

    }

//    #region Geo Position
//    Timer geoTimer;
//    private void initGeo()
//    {
//      geoTimer = new Timer(onGeoTimer, null, 0, Timeout.Infinite);
//    }
//
//    private void BeginGeo()
//    {
//      if (mIsActive) geoTimer.Change(1000, Timeout.Infinite);
//    }
//
//    private async void onGeoTimer(Object state)
//    {
//      try
//      {
//        Geoposition pos = await geo.GetGeopositionAsync();
//        playerPos = pos.Coordinate.Point;
//        PokemonGo.RocketAPI.Logger.Write($"player at {playerPos.Position.Latitude} {playerPos.Position.Longitude}");
//      }
//      catch { }
//      BeginGeo();
//    }
//    #endregion

    #region Camera Stream
    private async Task initVideo(CaptureElement element)
    {
      // already inited?
      if (mMediaCapture != null) return;
      // get hardware camera on the back side
      var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
      DeviceInformation cameraDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Back);
      if (cameraDevice == null) return;
      // get stream renderer
      mMediaCapture = new MediaCapture();
      if (mMediaCapture == null) return;
      try { await mMediaCapture.InitializeAsync(new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id }); }
      catch (UnauthorizedAccessException)
      {
        await Deinitialize();
        return;
      }
      // assume portrait
      mMediaCapture.SetPreviewRotation(VideoRotation.Clockwise90Degrees);
      // init state
      mElement = element;
      mIsInitialized = true;
    }

    public async Task Deinitialize()
    {
      // deinit video
      await StopVideoStream();
      if (mMediaCapture != null)
      {
        mMediaCapture.Dispose();
        mMediaCapture = null;
      }
      dxClean();
      mIsInitialized = false;
    }

    public async Task BeginVideoStream()
    {
      if (mIsInitialized == false) return;
      mElement.Source = mMediaCapture;
      await mMediaCapture.StartPreviewAsync();
      mIsActive = true;
      compass.Reset = true;
      //BeginGeo();
    }

    public async Task StopVideoStream()
    {
      if (mIsInitialized == false) return;
      if (mIsActive) await mMediaCapture.StopPreviewAsync();
      mIsActive = false;
    }
    #endregion

    #region DirectX
    public void initDX()
    {
      dxCreateDevice();
      dxCreateShaders();
      dxCreateMesh();
      dxInitScene();
    }

    public void dxClean()
    {
      renderMan.Release();
      plane = null;
      floor = null;
      // reset to initial state
      pokemons.Clear();
      camera = new arCamera();
    }

    private void dxCreateDevice()
    {
      renderMan.Init(width, height, this);
      renderMan.clearColor = new Color(0, 0, 0, 0);
    }

    private void dxCreateShaders()
    {
      var vertexDesc = new[]
      {
        new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
        new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0, InputClassification.PerVertexData, 0),
      };

      renderMan.shaderMan.Register("Assets\\Shaders\\VertexShader.vs.hlsl", "vs", vertexDesc);
      mxShader shader = renderMan.shaderMan.GetShader("vs");
      shader.DefineData("common", 0);
      shader.DefineData("model", 1);

      renderMan.shaderMan.Register("Assets\\Shaders\\PixelShader.ps.hlsl", "ps");
      shader = renderMan.shaderMan.GetShader("ps");
      shader.DefineTexture("mainTexture", 0);

      renderMan.shaderMan.Register("plainObj", "vs", "ps");
    }

    private void dxCreateMesh()
    {
      var cubeVertices = new[]
      {
        new VertexPositionTex(new Vector3(-5.0f, -0.01f, -5.0f), new Vector2(1.0f, 1.0f)),
        new VertexPositionTex(new Vector3(-5.0f, -0.01f,  5.0f), new Vector2(1.0f, 0.0f)),
        new VertexPositionTex(new Vector3(-5.0f,  0.01f, -5.0f), new Vector2(1.0f, 1.0f)),
        new VertexPositionTex(new Vector3(-5.0f,  0.01f,  5.0f), new Vector2(1.0f, 0.0f)),
        new VertexPositionTex(new Vector3( 5.0f, -0.01f, -5.0f), new Vector2(0.0f, 1.0f)),
        new VertexPositionTex(new Vector3( 5.0f, -0.01f,  5.0f), new Vector2(0.0f, 0.0f)),
        new VertexPositionTex(new Vector3( 5.0f,  0.01f, -5.0f), new Vector2(0.0f, 1.0f)),
        new VertexPositionTex(new Vector3( 5.0f,  0.01f,  5.0f), new Vector2(0.0f, 0.0f)),
      };

      var cubeVertices2 = new[]
      {
        new VertexPositionTex(new Vector3(-0.5f,  1.0f, -0.01f), new Vector2(0.0f, 0.0f)),
        new VertexPositionTex(new Vector3(-0.5f,  1.0f,  0.01f), new Vector2(0.0f, 0.0f)),
        new VertexPositionTex(new Vector3(-0.5f,  0.0f, -0.01f), new Vector2(0.0f, 1.0f)),
        new VertexPositionTex(new Vector3(-0.5f,  0.0f,  0.01f), new Vector2(0.0f, 1.0f)),
        new VertexPositionTex(new Vector3( 0.5f,  1.0f, -0.01f), new Vector2(1.0f, 0.0f)),
        new VertexPositionTex(new Vector3( 0.5f,  1.0f,  0.01f), new Vector2(1.0f, 0.0f)),
        new VertexPositionTex(new Vector3( 0.5f,  0.0f, -0.01f), new Vector2(1.0f, 1.0f)),
        new VertexPositionTex(new Vector3( 0.5f,  0.0f,  0.01f), new Vector2(1.0f, 1.0f)),
      };

      var cubeIndices = new ushort[]
      {
        0, 2, 1, // -x
        1, 2, 3,

        4, 5, 6, // +x
        5, 7, 6,

        0, 1, 5, // -y
        0, 5, 4,

        2, 6, 7, // +y
        2, 7, 3,

        0, 4, 6, // -z
        0, 6, 2,

        1, 3, 7, // +z
        1, 7, 5,
      };

      plane = renderMan.CreateModel<VertexPositionTex>("plane", cubeVertices, cubeIndices);
      billboard = renderMan.CreateModel<VertexPositionTex>("billboard", cubeVertices2, cubeIndices);

      renderMan.CreateTexture("texPokemon", "mainTexture", "\\Assets\\Pokemons\\1.png");
      renderMan.CreateTexture("texPokemon2", "mainTexture", "\\Assets\\Pokemons\\19.png");
      renderMan.CreateTexture("texPokemon3", "mainTexture", "\\Assets\\Pokemons\\10.png");
      renderMan.CreateTexture("pokestop", "mainTexture", "\\Assets\\Pokemons\\pokestop.png");
      renderMan.CreateTexture("texFloor", "mainTexture", "\\Assets\\UI\\compass.png");
    }

    private void dxInitScene()
    {
      // Calculate the aspect ratio and field of view.
      float aspectRatio = (float)width / (float)height;
      float fovAngleY = 60.0f * (float)Math.PI / 180.0f;

      // Create the constant buffer.
      dataCommon = renderMan.CreateBuffer<CommonBuffer>("common");
      dataCommon.data.projection = Matrix.Transpose(Matrix.PerspectiveFovRH(fovAngleY, aspectRatio, 0.01f, 100.0f));

      // create the world objects
      mxTexture tex = null;
      floor = new mxInstance();
      floor.model = plane;
      floor.data = renderMan.CreateBuffer<ModelBuffer>("model");
      floor.access<ModelBuffer>().data.model = Matrix.Transpose(Matrix.Translation(0.0f, 0.0f, 0.0f));
      renderMan.GetAsset<mxTexture>("texFloor", ref tex);
      floor.AttachTexture(tex);

      CreatePokemon("texPokemon",  new Vector3( 0.0f, 0.0f, -7.0f), 1.0f);
      CreatePokemon("texPokemon2", new Vector3( 0.0f, 0.0f,  7.0f), 1.0f);
      CreatePokemon("texPokemon3", new Vector3(-7.0f, 0.0f,  0.0f), 1.0f);
      CreatePokemon("pokestop", new Vector3( 7.0f, 0.0f,  0.0f), 10.0f);
    }

    public void Render()
    {
      if (mIsActive == false) return;
      if (renderMan.dxDevice == null) return;

      Update();
      renderMan.StartFrame();
      RenderFrame();
      renderMan.EndFrame();
    }
    #endregion

    #region Frame Updating

    private ulong fixedid = 1;
    public void CreatePokemon(String pname, Vector3 pos, float scale)
    {
      mxTexture tex = null;
      mxInstance guy;
      arPokemon mon;
      guy = new mxInstance();
      guy.model = billboard;
      guy.data = renderMan.CreateBuffer<ModelBuffer>("model");
      renderMan.GetAsset<mxTexture>(pname, ref tex);
      guy.AttachTexture(tex);
      mon = new arPokemon();
      mon.id = fixedid++;
      mon.instance = guy;
      mon.position = pos;
      mon.scale = scale;
      mon.geo = null;
      mon.instance.access<ModelBuffer>().data.model = Matrix.Transpose(Matrix.Multiply(Matrix.Scaling(mon.scale), Matrix.Translation(mon.position)));
      testGuys.Add(mon);
    }

    public void CreatePokemon(FortDataWrapper p)
    {
      mxTexture tex = null;
      mxInstance guy;
      arPokemon mon;
      guy = new mxInstance();
      guy.model = billboard;
      guy.data = renderMan.CreateBuffer<ModelBuffer>("model");
      renderMan.GetAsset<mxTexture>("pokestop", ref tex);
      guy.AttachTexture(tex);

      mon = new arPokemon();
      mon.geo = p.Geoposition;
      mon.id = fixedid++;
      mon.instance = guy;
      float X = GetDistanceTo(p.Geoposition, GetDistanceType.Long);
      float Z = GetDistanceTo(p.Geoposition, GetDistanceType.Lat);
      mon.position = new Vector3(X, 0, Z);
      mon.scale = 10.0f;
      pokestops[p.Id] = mon;
    }

    public void CreatePokemon(MapPokemonWrapper pokemon)
    {
      mxTexture tex = null;
      arPokemon mon;

      string name = (int)(pokemon.PokemonId) + ".png";
      if (!renderMan.GetAsset<mxTexture>(name, ref tex))
      {
        string fname = "\\Assets\\Pokemons\\" + name;
        renderMan.CreateTexture(name, "mainTexture", fname);
        renderMan.GetAsset<mxTexture>(name, ref tex);
      }

      mxInstance guy;
      guy = new mxInstance();
      guy.model = billboard;
      guy.data = renderMan.CreateBuffer<ModelBuffer>("model");
      guy.AttachTexture(tex);

      float X = GetDistanceTo(pokemon.Geoposition, GetDistanceType.Long);
      float Z = GetDistanceTo(pokemon.Geoposition, GetDistanceType.Lat);
      PokemonGo.RocketAPI.Logger.Write($"creating {pokemon.PokemonId} id {pokemon.EncounterId} at {X}, {Z}");

      mon = new arPokemon();
      mon.geo = pokemon.Geoposition;
      mon.id = pokemon.EncounterId;
      mon.instance = guy;
      mon.position = new Vector3(X, 0, Z);
      mon.scale = 1.0f;
      pokemons[pokemon.EncounterId] = mon;
    }

    private enum GetDistanceType { Long = 1, Lat };

    private float GetDistanceTo(Geopoint point, GetDistanceType type)
    {
      double lat2 = GameClient.Geoposition.Coordinate.Point.Position.Latitude;
      double lon1 = GameClient.Geoposition.Coordinate.Point.Position.Longitude;
      double lat1 = (type == GetDistanceType.Long) ? lat2 : point.Position.Latitude;
      double lon2 = (type == GetDistanceType.Lat) ? lon1 : point.Position.Longitude;
      double R = 6378.137; // Radius of earth in KM
      double dLat = (lat2 - lat1) * Math.PI / 180;
      double dLon = (lon2 - lon1) * Math.PI / 180;
      double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
      double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
      double d = R * c;
      return (float)(d * 1000); // meters
    }

    List<ulong> okids = new List<ulong>();
    List<string> okids2 = new List<String>();
    Vector3 forward = new Vector3(0.0f, 0.0f, 1.0f);

    private void Update()
    {
      // update camera
      Matrix3x3 mat = compass.Matrix;
//      Quaternion q = compass.Quat;
      camera.forward = Vector3.Transform(new Vector3(0.0f, 0.0f, 1.0f), mat);
      camera.eye = new Vector3(0.0f, 2.0f, 0.0f); // Define camera position.
      camera.up = Vector3.Transform(new Vector3(0.0f, 1.0f, 0.0f), mat); // Define up direction.
      camera.target = Vector3.Add(camera.eye, camera.forward); // Define focus position.
      camera.lookat = Matrix.LookAtRH(camera.eye, camera.target, camera.up);
      dataCommon.data.view = Matrix.Transpose(camera.lookat);

      // create new pokemon
      okids.Clear();
      foreach (var p in GameClient.CatchablePokemons)
      {
        if (pokemons.Keys.Contains(p.EncounterId) == false) 
          CreatePokemon(p);
        okids.Add(p.EncounterId);
      }
      foreach (var p in GameClient.NearbyPokestops)
      {
        if (pokestops.Keys.Contains(p.Id) == false)
          CreatePokemon(p);
        okids2.Add(p.Id);
      }
      // remove old pokemon
      foreach (var p in GameClient.CatchablePokemons)
      {
        if (okids.Contains(p.EncounterId)) continue;
        pokemons.Remove(p.EncounterId);
      }
      foreach (var p in GameClient.NearbyPokestops)
      {
        if (okids2.Contains(p.Id)) continue;
        pokestops.Remove(p.Id);
      }

      // update each pokemon
      foreach (var p in pokemons.Values)
        FixPokething(p);
      foreach (var p in pokestops.Values)
        FixPokething(p);
//      foreach (var p in testGuys)
//        FixPokething(p);
    }


    private void FixPokething(arPokemon p)
    {
      // get new offset from geo
      if (p.geo != null)
      {
        p.position.X = GetDistanceTo(p.geo, GetDistanceType.Long);
        p.position.Z = GetDistanceTo(p.geo, GetDistanceType.Lat);
      }
      // get new orientation to face origin
      Vector3 loc = p.position;
      loc.Normalize();
      float angle = (float)Math.Acos(Vector3.Dot(forward, loc));
      Matrix rot = Matrix.RotationY(angle);
      Matrix trans = Matrix.Translation(p.position);
      Matrix scale = Matrix.Scaling(p.scale);
      p.instance.access<ModelBuffer>().data.model = Matrix.Transpose(Matrix.Multiply(Matrix.Multiply(scale, rot), trans));
    }
    #endregion

    private void RenderFrame()
    {
      renderMan.shaderMan.GetEffect("plainObj").Bind(); // set the shader
      dataCommon.Bind(); // set the common data
      floor.Draw();      // draw the floor
      foreach (var p in pokemons.Values)
        p.instance.Draw();
      foreach (var p in pokestops.Values)
        p.instance.Draw();
//      foreach (var p in testGuys)
//        p.instance.Draw();
    }
  }
}
