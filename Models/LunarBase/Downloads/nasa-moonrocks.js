/**
 * Astromaterials 3D Explorer
 * Developed by: Ben Feist
 * https://ares.jsc.nasa.gov/people/bios/benjamin-f-feist/
 */

//mobile detect and redirect
if (/Android|webOS|iPhone|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent)) {
  var url = "./mobile/mobile.html" + document.location.search;
  $(location).attr("href", url);
}

var autoR = true;

var camera, box, scene, dist;
var mDown = false;

var speed = 0.05;
var cameraFov = 15;

var hasTouch = false;

// these variables are set in the initApp() function
var objectURL;
var objectBoundsURL;
var textureURL;

var rockContainer, renderer, controls;
var anaglyphEffect;
var enableAnaglyph = false;

var controlsXTarget = 0;
var rockTexture;
// var aoMapTexture;
var rockMaterial;
var samplesMeshGroup, sampleMesh, sampleMeshInside, sceneGroup, sampleBoundingBox;
var boundsMesh3DWidth, boundsMesh3DHeight, boundsMesh3DDepth;
var scaleBox, helperGrid, orientationCube;
var samplesBoundsMeshGroup;

var oriRenderer, oriScene, oriCamera;
var navCube;

var directionalLight;
var ambientLight;

var ori = ["XY", "XZ", "YZ"];
var slicerNames = ["XY0", "XY1", "XZ0", "XZ1", "YZ0", "YZ1"];

var slicerMesh = {};

var sliceSpriteRaster = {};
var sliceHighresRaster = {};
var slicerTexture = {};
var slicerMaterial = {};
var clippingPlane = {};
var stencilGroup = {};

var paperCanvasScope = {};

// quaternion rotation variables
var rotateStartPoint = new THREE.Vector3(0, 0, 1);
var rotateEndPoint = new THREE.Vector3(0, 0, 1);
var rotationSpeed = 2;
var lastMoveTimestamp;
var moveReleaseTimeDelta = 50;
var startPoint = {
  x: 0,
  y: 0,
};

var deltaX = 0,
  deltaY = 0;

var loadingCounter = 0;
var gApplicationReadyIntervalID;

var getImageDataForDownload;
var downloadImgData = "";

var mouseCoords = new THREE.Vector2();
var raycaster = new THREE.Raycaster();
var displayedPins = [],
  customPins = [],
  nasaPins = [];

// var gRootDataLocation = 'https://a3d.nyc3.digitaloceanspaces.com/';
// var gRootDataLocation = 'https://astromaterials3ddata-26f5.kxcdn.com/'; //keyCDN pull zone from ares-a3d.s3.us-gov-east-1.amazonaws.com
var gRootDataLocation = "https://ares-a3d.s3.us-gov-east-1.amazonaws.com/";

var gSamplesLocation;

var gSampleJSONDict = {};
var gAllMetadataDict = {};
var gAllMetadataDictXctOnly = {};
var sampleNum = "12038-7"; //default
// var gInitialSceneRotation = {x:-10, y:-25, z:0};
var gInitialSceneRotation = { x: -45, y: -90, z: 0 };

//double slider ranges
var range = {};

var alphabet = "abcdefghijklmnopqrstuvwxyz".split("");

var menuTestMode = false;
var gXctOnly = false;
var gXctOnlyDisplayOption = 1;
var hideSlices = false;

THREE.ImageUtils.crossOrigin = "";

$(function () {
  //Handler for .ready() called.
  console.warn = function () {}; // now warnings do nothing!

  if (typeof $.getUrlVar("sample") !== "undefined") {
    sampleNum = $.getUrlVar("sample");
    sampleNum = decodeURIComponent(sampleNum);
  }

  if (typeof $.getUrlVar("xctonly") !== "undefined") {
    gSamplesLocation = gRootDataLocation + "samples_XCT_Only/";
    gXctOnly = true;
  } else {
    gSamplesLocation = gRootDataLocation + "samples/";
  }

  if (typeof $.getUrlVar("hideslices") !== "undefined") {
    hideSlices = true;
  }

  if (typeof $.getUrlVar("xctonlydisplayoption") !== "undefined") {
    gXctOnlyDisplayOption = parseInt($.getUrlVar("xctonlydisplayoption"));
  }

  // ga('send', 'event', 'A3D', 'load', sampleNum);
  gtag("event", "astromaterials3dexplorer", {
    event_category: "engagement",
    event_label: "load " + sampleNum,
  });
  showLoader();
  setLoaderProgress(0);

  createSliders();
  gApplicationReadyIntervalID = setApplicationReadyPoller();
  $.when(ajaxGetSampleJSON(sampleNum)).done(function () {
    $.when(ajaxGetAllMetadata()).done(function () {
      setDoneLoaderField("loaderJSON");
      setInterfaceStyleBySampleType(gSampleJSONDict["sample_type"]);
      loadThreeJSItems();
      if (gSampleJSONDict["CT_data_exists"]) {
        initCanvasSizes();
        initPaperJS();
      } else {
        //fake the loading of the spritesheets
        setDoneLoaderField("loaderSpritesheetXY");
        setDoneLoaderField("loaderSpritesheetXZ");
        setDoneLoaderField("loaderSpritesheetYZ");
        for (var i = 0; i < slicerNames.length; i++) $("#" + slicerNames[i] + "sliceCanvas").css("display", "none");

        //disable CT sliders
        $("#CTDisableDiv").addClass("enabled");
        $("#sliceIntroCopy").html("Interior XCT data is unavailable for this sample. This could be due to the composition of the sample or due to preservation concerns.<br><br> However, external analysis and pin labelling is possible on the sample exterior.");

        //hide CT details
        $("#CTDataDetails").css("display", "none");
      }
    });
  });
});

function setApplicationReadyPoller() {
  return window.setInterval(function () {
    setLoaderProgress((loadingCounter / 7) * 100);
    // console.log("modal active: " + $.modal.isActive());
    if (loadingCounter >= 7) {
      window.clearInterval(gApplicationReadyIntervalID);
      console.log("loadingCounter = 7! App Ready!");

      initApp();
      handleRotation();
      animate();
      onWindowResize();

      if (typeof $.getUrlVar("state") !== "undefined") {
        // ga('send', 'event', 'A3D', 'loadstate', sampleNum);
        var state = $.getUrlVar("state"); //code to detect jump-to-timecode parameter
        selectMenuItem("navItem_Pin", 0);
        loadState(state);
      }
      drawPincount();

      $.modal.close();
      document.getElementById("loaderModal").style.display = "none";

      setTimeout(function () {
        setEventHandlers();
      }, 50);

      //mousemove reminder animation and delay is handled with CSS animation
      document.getElementById("moveMouseContainer").style.display = "flex";
    }
  }, 100);
}

function initCanvasSizes() {
  for (var i in slicerNames) {
    var ori = slicerNames[i].substring(0, 2);
    var sliceCanvasSelector = $("#" + slicerNames[i] + "sliceCanvas");
    sliceCanvasSelector.css("width", gSampleJSONDict[ori]["slice_width"]);
    sliceCanvasSelector.css("height", gSampleJSONDict[ori]["slice_height"]);
  }
}

function initPaperJS() {
  paper.install(window);

  for (var i in slicerNames) {
    paperCanvasScope[slicerNames[i]] = new paper.PaperScope();
    paperCanvasScope[slicerNames[i]].setup(document.getElementById(slicerNames[i] + "sliceCanvas"));

    paperCanvasScope[slicerNames[i]].activate();
    sliceHighresRaster[slicerNames[i]] = new paperCanvasScope[slicerNames[i]].Raster();
    var sliceSpritesheetURL = gSamplesLocation + sampleNum + "/5a_" + sampleNum + "_XCT_Sprites/" + sampleNum + "_spritesheet_" + slicerNames[i].substring(0, 2) + ".jpg";
    sliceSpriteRaster[slicerNames[i]] = new paperCanvasScope[slicerNames[i]].Raster({
      crossOrigin: "anonymous",
      source: sliceSpritesheetURL,
      name: slicerNames[i],
    });
    sliceSpriteRaster[slicerNames[i]].onLoad = function () {
      var slicerName = this.name;
      var ori = this.name.substring(0, 2);

      if (slicerName.substring(2, 3) === "0") {
        setDoneLoaderField("loaderSpritesheet" + ori);
      }
      console.log("The sprite image " + slicerName + " has loaded.");
      paperCanvasScope[slicerName].activate();

      // this.canvas.getContext('2d').filter = 'brightness(' + gSampleJSONDict['slices_brightness_level'] + '%)';
      // this.drawImage(this.canvas, 0, 0);
      // this.visible = true;

      sliceSpriteRaster[slicerName].scale(gSampleJSONDict["sprite_scale_factor"]);
      sliceSpriteRaster[slicerName].position = new Point((gSampleJSONDict[ori]["spritesheet_width"] * gSampleJSONDict["sprite_scale_factor"]) / 2, (gSampleJSONDict[ori]["spritesheet_height"] * gSampleJSONDict["sprite_scale_factor"]) / 2);
      $("#" + slicerName + "sliceCanvas").css("display", "none");
    };
  }
}

function loadThreeJSItems() {
  objectURL = gSamplesLocation + sampleNum + "/" + gSampleJSONDict["mesh_foldername"] + "/" + gSampleJSONDict["mesh_filename"];
  objectBoundsURL = gSamplesLocation + sampleNum + "/2a_" + sampleNum + "_XCT_Bounding-Box-Model/" + gSampleJSONDict["boundingbox_filename"];
  textureURL = gSamplesLocation + sampleNum + "/" + gSampleJSONDict["mesh_foldername"] + "/" + gSampleJSONDict["texture_filename"];

  // load rock model
  var loader = new THREE.OBJLoader();
  loader.load(
    objectURL,
    // onLoad callback
    function (obj) {
      samplesMeshGroup = obj;
      setDoneLoaderField("loaderMesh");
    },
    // onProgress callback
    function (xhr) {
      if (xhr.lengthComputable) {
        var percentComplete = Math.round((xhr.loaded / xhr.total) * 100);
        // console.log( 'model ' + Math.round( percentComplete ) + '% downloaded' );
        $("#loaderMesh").text(percentComplete + " %");
      }
    },
    // onError callback
    function (err) {
      console.error("***An error happened loading Sample OBJ.");
    }
  );
  if (gSampleJSONDict["CT_data_exists"]) {
    var boundsLoader = new THREE.OBJLoader();
    boundsLoader.load(
      objectBoundsURL,
      // onLoad callback
      function (obj) {
        samplesBoundsMeshGroup = obj;
        setDoneLoaderField("loaderBounding");
      },
      // onProgress callback
      function (xhr) {
        if (xhr.lengthComputable) {
          var percentComplete = Math.round((xhr.loaded / xhr.total) * 100);
          // console.log( 'model ' + Math.round( percentComplete ) + '% downloaded' );
          $("#loaderMesh").text(percentComplete + " %");
        }
      },
      // onError callback
      function (err) {
        console.error("***An error happened loading Bounds OBJ.");
      }
    );
  } else {
    //fake finishing loading bounding box
    setDoneLoaderField("loaderBounding");
  }
  // load rock texture
  if (gSampleJSONDict["texture_filename"] !== "") {
    rockTexture = new THREE.TextureLoader().load(
      textureURL,
      // onLoad callback
      function (texture) {
        setDoneLoaderField("loaderTexture");
      },
      // onProgress callback currently not supported
      undefined,
      // onError callback
      function (err) {
        console.error("***An error happened loading texture.");
      }
    );
  } else {
    //fake finishing loading texture
    setDoneLoaderField("loaderTexture");
  }
}

function initApp() {
  initializeSliceNumberFields();

  //------------------ setup renderer and scene
  rockContainer = document.getElementById("rockContainer");
  var rockSelector = $("#rockContainer");

  // initialize static objects
  camera = new THREE.PerspectiveCamera(cameraFov, rockSelector.width() / rockSelector.height(), 0.01, 2000);
  camera.position.set(0, 0, gSampleJSONDict["camera_distance"]);
  camera.lookAt(new THREE.Vector3(0, 0, 0));
  camera.useFramingBehavior = true;

  scene = new THREE.Scene();
  setSceneBackgroundImage();

  sceneGroup = new THREE.Group();

  //setup lights
  ambientLight = new THREE.AmbientLight(0xffffff, 2);
  scene.add(ambientLight);

  directionalLight = new THREE.DirectionalLight(0xffffff, 2);
  directionalLight.position.set(0, 10, 0);
  directionalLight.visible = false;
  scene.add(directionalLight);

  renderer = new THREE.WebGLRenderer({
    antialias: true,
    alpha: true,
    // outputEncoding: THREE.sRGBEncoding,
    // gammaOutput: true,
    // gammaFactor: 1.0
  });
  renderer.setClearColor(0x000000, 0);
  renderer.setPixelRatio(window.devicePixelRatio);
  renderer.localClippingEnabled = true;
  renderer.setSize(rockSelector.width(), rockSelector.height());

  console.log("renderer.capabilities.getMaxAnisotropy(): " + renderer.capabilities.getMaxAnisotropy());

  anaglyphEffect = new THREE.AnaglyphEffect(renderer);
  anaglyphEffect.setSize(rockSelector.width(), rockSelector.height());

  rockContainer.appendChild(renderer.domElement);

  //------------------ model functions ----------------
  // var alphaMapTexture =  new THREE.TextureLoader().load( 'images/slicer_alpha.png' );
  // var localPlane = new THREE.Plane( new THREE.Vector3( 0, 0, 1 ), 0.5 );

  clippingPlane["XY0"] = new THREE.Plane(new THREE.Vector3(-1, 0, 0), 0);
  clippingPlane["XY1"] = new THREE.Plane(new THREE.Vector3(-1, 0, 0), 0);
  clippingPlane["XZ0"] = new THREE.Plane(new THREE.Vector3(0, -1, 0), 0);
  clippingPlane["XZ1"] = new THREE.Plane(new THREE.Vector3(0, -1, 0), 0);
  clippingPlane["YZ0"] = new THREE.Plane(new THREE.Vector3(0, 0, -1), 0);
  clippingPlane["YZ1"] = new THREE.Plane(new THREE.Vector3(0, 0, -1), 0);

  //get rid of multiple of two texture error
  // rockTexture.minFilter = THREE.LinearFilter;
  // rockTexture.generateMipmaps = false;

  if (gSampleJSONDict["texture_filename"] !== "") {
    rockTexture.anisotropy = Math.min(2, renderer.capabilities.getMaxAnisotropy());
    rockMaterial = new THREE.MeshPhysicalMaterial({
      side: THREE.FrontSide,
      roughness: 1,
      color: "#FFFFFF",
      transparent: true,
      map: rockTexture,
      clippingPlanes: [clippingPlane["XY0"], clippingPlane["XY1"], clippingPlane["XZ0"], clippingPlane["XZ1"], clippingPlane["YZ0"], clippingPlane["YZ1"]],
      clipShadows: true,
    });
    sampleMesh = new THREE.Mesh(samplesMeshGroup.children[0].geometry, rockMaterial);
  } else {
    if (gXctOnlyDisplayOption === 1) {
      // wireframe
      // black interior
      var material = new THREE.MeshPhongMaterial({
        color: 0x000000,
        specular: 0x050505,
        shininess: 100,
        polygonOffset: true,
        polygonOffsetFactor: 1, // positive value pushes polygon further away
        polygonOffsetUnits: 1,
        clippingPlanes: [clippingPlane["XY0"], clippingPlane["XY1"], clippingPlane["XZ0"], clippingPlane["XZ1"], clippingPlane["YZ0"], clippingPlane["YZ1"]],
      });
      sampleMesh = new THREE.Mesh(samplesMeshGroup.children[0].geometry, material);

      // wireframeExterior
      var wireframeSolidMaterial = new THREE.MeshBasicMaterial({
        color: 0x999999,
        polygonOffset: true,
        polygonOffsetFactor: 1, // positive value pushes polygon further away
        polygonOffsetUnits: 1,
        clippingPlanes: [clippingPlane["XY0"], clippingPlane["XY1"], clippingPlane["XZ0"], clippingPlane["XZ1"], clippingPlane["YZ0"], clippingPlane["YZ1"]],
        wireframe: true,
      });
      var wireframe = new THREE.Mesh(samplesMeshGroup.children[0].geometry, wireframeSolidMaterial);
      // expand wireframe slightly
      wireframe.scale.multiplyScalar(1.01);
      sampleMesh.add(wireframe);
    } else if (gXctOnlyDisplayOption === 2) {
      // XRAY version
      rockMaterial = new THREE.ShaderMaterial({
        uniforms: {
          p: { type: "f", value: 2 },
          glowColor: { type: "c", value: new THREE.Color(0xffffff) },
        },
        vertexShader: document.getElementById("XrayVertexShader").textContent,
        fragmentShader: document.getElementById("XrayFragmentShader").textContent,
        side: THREE.FrontSide,
        blending: THREE.AdditiveBlending,
        transparent: true,
        depthWrite: false,
        clipping: true,
        clippingPlanes: [clippingPlane["XY0"], clippingPlane["XY1"], clippingPlane["XZ0"], clippingPlane["XZ1"], clippingPlane["YZ0"], clippingPlane["YZ1"]],
      });
      sampleMesh = new THREE.Mesh(samplesMeshGroup.children[0].geometry, rockMaterial);
    } else if (gXctOnlyDisplayOption === 3) {
      var material = new THREE.MeshPhongMaterial({
        color: 0x141414,
        specular: 0x050505,
        shininess: 100,
        side: THREE.FrontSide,
        polygonOffset: true,
        polygonOffsetFactor: 1, // positive value pushes polygon further away
        polygonOffsetUnits: 1,
        clippingPlanes: [clippingPlane["XY0"], clippingPlane["XY1"], clippingPlane["XZ0"], clippingPlane["XZ1"], clippingPlane["YZ0"], clippingPlane["YZ1"]],
      });
      sampleMesh = new THREE.Mesh(samplesMeshGroup.children[0].geometry, material);

      // wireframeExterior
      var wireframeSolidMaterial = new THREE.MeshBasicMaterial({
        color: 0x999999,
        polygonOffset: true,
        polygonOffsetFactor: 1, // positive value pushes polygon further away
        polygonOffsetUnits: 1,
        side: THREE.FrontSide,
        clippingPlanes: [clippingPlane["XY0"], clippingPlane["XY1"], clippingPlane["XZ0"], clippingPlane["XZ1"], clippingPlane["YZ0"], clippingPlane["YZ1"]],
        wireframe: true,
      });
      var wireframe = new THREE.Mesh(samplesMeshGroup.children[0].geometry, wireframeSolidMaterial);
      sampleMesh.add(wireframe);
    } else if (gXctOnlyDisplayOption === 4) {
      var material = new THREE.MeshPhongMaterial({
        color: 0x333333,
        specular: 0x050505,
        shininess: 100,
        clippingPlanes: [clippingPlane["XY0"], clippingPlane["XY1"], clippingPlane["XZ0"], clippingPlane["XZ1"], clippingPlane["YZ0"], clippingPlane["YZ1"]],
      });
      sampleMesh = new THREE.Mesh(samplesMeshGroup.children[0].geometry, material);
    }
  }

  if (!gSampleJSONDict["CT_data_exists"]) {
    //remove clipping planes and center the object
    rockMaterial.clippingPlanes = [];
    sampleMesh.geometry.center();
  }

  const flipScale = { x: 1, y: 1, z: 1 };
  sampleBoundingBox = new THREE.Box3().setFromObject(sampleMesh);

  sampleMesh.scale.set(gSampleJSONDict["scale_multiplier"], gSampleJSONDict["scale_multiplier"], gSampleJSONDict["scale_multiplier"]);

  sampleMesh.rotation.z = THREE.Math.degToRad(180); //due to sign flip from Avizo's coordinate system to WebGL
  sampleMesh.scale.set(flipScale.x, flipScale.y, flipScale.z); // fip the y axis to match WebGL's coordinate system

  sampleMesh.name = "sampleMesh";
  sceneGroup.add(sampleMesh);

  if (gSampleJSONDict["CT_data_exists"] || gXctOnly) {
    //paint the inside of the rock black
    var rockMaterialInside = new THREE.MeshBasicMaterial({
      side: THREE.BackSide,
      color: 0x000000,
      ambient: 0x000000,
      clippingPlanes: [clippingPlane["XY0"], clippingPlane["XY1"], clippingPlane["XZ0"], clippingPlane["XZ1"], clippingPlane["YZ0"], clippingPlane["YZ1"]],
    });
    sampleMeshInside = new THREE.Mesh(samplesMeshGroup.children[0].geometry, rockMaterialInside);
    sampleMeshInside.name = "sampleMeshInside";
    sampleMeshInside.scale.set(gSampleJSONDict["scale_multiplier"], gSampleJSONDict["scale_multiplier"], gSampleJSONDict["scale_multiplier"]);

    sampleMeshInside.rotation.z = THREE.Math.degToRad(180); //due to sign flip from Avizo's coordinate system to WebGL
    sampleMeshInside.scale.set(flipScale.x, flipScale.y, flipScale.z); // fip the y axis to match WebGL's coordinate system

    sceneGroup.add(sampleMeshInside);

    // ------------------ bounds functions ----------------
    var boundsMesh = samplesBoundsMeshGroup.children[0];
    boundsMesh.scale.set(gSampleJSONDict["scale_multiplier"], gSampleJSONDict["scale_multiplier"], gSampleJSONDict["scale_multiplier"]);
    boundsMesh.rotation.z = THREE.Math.degToRad(180); //due to sign flip from Avizo's coordinate system to WebGL
    boundsMesh.scale.set(flipScale.x, flipScale.y, flipScale.z);

    var boundsBoundingBox = new THREE.Box3().setFromObject(boundsMesh);

    boundsMesh3DWidth = boundsBoundingBox.max.x - boundsBoundingBox.min.x;
    boundsMesh3DHeight = boundsBoundingBox.max.y - boundsBoundingBox.min.y;
    boundsMesh3DDepth = boundsBoundingBox.max.z - boundsBoundingBox.min.z;
    calculateSlicerDistances();
  } else {
    boundsBoundingBox = new THREE.Box3().setFromObject(sampleMesh);
    boundsMesh3DWidth = boundsBoundingBox.max.x - boundsBoundingBox.min.x;
    boundsMesh3DHeight = boundsBoundingBox.max.y - boundsBoundingBox.min.y;
    boundsMesh3DDepth = boundsBoundingBox.max.z - boundsBoundingBox.min.z;
  }

  // console.log("rock height: " + boundsMesh3DHeight.toFixed(2));
  // console.log("rock width: " + boundsMesh3DWidth.toFixed(2));
  // console.log("rock depth: " + boundsMesh3DDepth.toFixed(2));

  var slicerPlaneGeometry = {};
  slicerPlaneGeometry["XY0"] = new THREE.PlaneGeometry(boundsMesh3DWidth, boundsMesh3DHeight, 1, 1);
  slicerPlaneGeometry["XY1"] = new THREE.PlaneGeometry(boundsMesh3DWidth, boundsMesh3DHeight, 1, 1);
  slicerPlaneGeometry["XZ0"] = new THREE.PlaneGeometry(boundsMesh3DWidth, boundsMesh3DDepth, 1, 1);
  slicerPlaneGeometry["XZ1"] = new THREE.PlaneGeometry(boundsMesh3DWidth, boundsMesh3DDepth, 1, 1);
  slicerPlaneGeometry["YZ0"] = new THREE.PlaneGeometry(boundsMesh3DHeight, boundsMesh3DDepth, 1, 1);
  slicerPlaneGeometry["YZ1"] = new THREE.PlaneGeometry(boundsMesh3DHeight, boundsMesh3DDepth, 1, 1);

  var visibleSides1 = [THREE.BackSide, THREE.FrontSide, THREE.BackSide, THREE.FrontSide, THREE.BackSide, THREE.FrontSide];
  var visibleSides2 = [THREE.FrontSide, THREE.BackSide, THREE.FrontSide, THREE.BackSide, THREE.FrontSide, THREE.BackSide];

  for (var i in slicerNames) {
    slicerTexture[slicerNames[i]] = new THREE.CanvasTexture(document.getElementById(slicerNames[i] + "sliceCanvas"));
    // fix the “image is not power of two” bug
    // XYslicerTexture.minFilter = THREE.LinearFilter;
    // XYslicerTexture.generateMipmaps = false;
    slicerTexture[slicerNames[i]].anisotropy = Math.min(2, renderer.capabilities.getMaxAnisotropy());
    slicerTexture[slicerNames[i]].wrapS = THREE.RepeatWrapping;
    slicerTexture[slicerNames[i]].repeat.x = -1;

    var sliceClippingPlanes = [];
    // create array of clipping planes that omit the current orientation, to be applied to current slice
    for (var x in slicerNames) {
      if (x !== i) {
        sliceClippingPlanes.push(clippingPlane[slicerNames[x]]);
      }
    }
    var slicerMaterial1 = new THREE.MeshPhysicalMaterial({
      side: visibleSides1[i],
      color: 0xffffff,
      alphaMap: slicerTexture[slicerNames[i]],
      alphaTest: gSampleJSONDict["slices_alpha_test"], // if transparent is false
      transparent: false,
      map: slicerTexture[slicerNames[i]],
      clippingPlanes: sliceClippingPlanes,
      wireframe: hideSlices,
    });
    var slicerMaterial2 = new THREE.MeshBasicMaterial({
      //flat black backside of slices so you can't see them through voids
      side: visibleSides2[i],
      color: 0x000000,
      alphaMap: slicerTexture[slicerNames[i]],
      alphaTest: 0.1, // if transparent is false
      transparent: false,
      shading: THREE.FlatShading,
      clippingPlanes: sliceClippingPlanes,
      wireframe: hideSlices,
    });
    slicerMesh[slicerNames[i]] = new THREE.Group();
    slicerMesh[slicerNames[i]].name = "slicer" + slicerNames[i];
    var slicerActualMesh = new THREE.Mesh(slicerPlaneGeometry[slicerNames[i]], slicerMaterial1);
    slicerActualMesh.name = "texturedSide " + slicerNames[i];
    slicerMesh[slicerNames[i]].add(slicerActualMesh);

    var slicerOtherSideMesh = new THREE.Mesh(slicerPlaneGeometry[slicerNames[i]], slicerMaterial2);
    slicerOtherSideMesh.name = "blackSide";
    slicerMesh[slicerNames[i]].add(slicerOtherSideMesh);

    var slicerHelperWireframe = new THREE.LineSegments(new THREE.EdgesGeometry(slicerActualMesh.geometry), new THREE.LineBasicMaterial({ color: 0xffffff }));
    var slicerHelperBody = new THREE.Mesh(
      slicerPlaneGeometry[slicerNames[i]],
      new THREE.MeshBasicMaterial({
        side: THREE.DoubleSide,
        color: 0xffffff,
        opacity: 0.05,
        transparent: true,
        depthWrite: false,
      })
    );
    slicerHelperBody.name = "helperWireframe";
    slicerHelperBody.visible = false;
    slicerMesh[slicerNames[i]].add(slicerHelperBody);
    slicerHelperWireframe.name = "helperWireframe";
    slicerHelperWireframe.visible = false;
    slicerMesh[slicerNames[i]].add(slicerHelperWireframe);

    if (gSampleJSONDict["CT_data_exists"]) {
      sceneGroup.add(slicerMesh[slicerNames[i]]);
    }
  }

  initializeSlicerMeshPositionsAndRotation();

  // ------------ add other scene items -----------
  createNavCubeCutout();

  helperGrid = placeHelperGrid(boundsBoundingBox);
  helperGrid.visible = false;
  sceneGroup.add(helperGrid);

  orientationCube = createOrientationCube(boundsBoundingBox);
  orientationCube.visible = false;
  sceneGroup.add(orientationCube);

  scaleBox = createLabelledScaleBox(boundsBoundingBox);
  scaleBox.visible = false;
  sceneGroup.add(scaleBox);

  scene.add(sceneGroup);

  setInitialSceneRotation();

  updateClippingPlanes();

  controls = new THREE.OrbitControls(camera, renderer.domElement);
  resetCameraControls();
}

function setSceneBackgroundImage() {
  if (gSampleJSONDict["sample_type"] === "lunar") {
    var bgImagePath = "images/bg_lunar_black.jpg";
  } else {
    bgImagePath = "images/bg_meteorite.jpg";
  }
  // load rock texture
  var bgTexture = new THREE.TextureLoader().load(
    bgImagePath,
    // onLoad callback
    function (texture) {
      texture.minFilter = THREE.LinearFilter;

      var rockSelector = $("#rockContainer");
      var targetAspect = rockSelector.width() / rockSelector.height();
      var imageAspect = texture.image.width / texture.image.height;
      var factor = imageAspect / targetAspect;
      // When factor larger than 1, that means texture 'wilder' than target。
      // we should scale texture height to target height and then 'map' the center  of texture to target， and vice versa.
      scene.background = texture;
      scene.background.offset.x = factor > 1 ? (1 - 1 / factor) / 2 : 0;
      scene.background.repeat.x = factor > 1 ? 1 / factor : 1;
      // scene.background.offset.y = factor > 1 ? 0 : (1 - factor) / 2;
      // scene.background.repeat.y = factor > 1 ? 1 : factor;
    },
    // onProgress callback currently not supported
    undefined,
    // onError callback
    function (err) {
      console.error("***An error happened loading background image.");
    }
  );
}

function initializeSlicerMeshPositionsAndRotation() {
  slicerMesh["XY0"].position.z = -boundsMesh3DDepth / 2;

  slicerMesh["XY1"].position.z = boundsMesh3DDepth / 2;

  slicerMesh["XZ0"].position.y = boundsMesh3DHeight / 2;
  slicerMesh["XZ0"].rotation.x = THREE.Math.degToRad(90);

  slicerMesh["XZ1"].position.y = -boundsMesh3DHeight / 2;
  slicerMesh["XZ1"].rotation.x = THREE.Math.degToRad(90);

  slicerMesh["YZ0"].position.x = -boundsMesh3DWidth / 2;
  slicerMesh["YZ0"].rotation.y = THREE.Math.degToRad(90);
  slicerMesh["YZ0"].rotation.z = THREE.Math.degToRad(90);

  slicerMesh["YZ1"].position.x = boundsMesh3DWidth / 2;
  slicerMesh["YZ1"].rotation.y = THREE.Math.degToRad(90);
  slicerMesh["YZ1"].rotation.z = THREE.Math.degToRad(90);
}

function initializeSliceNumberFields() {
  if (gSampleJSONDict["CT_data_exists"]) {
    //set slice number fields
    for (var i in ori) {
      if (gSampleJSONDict[ori[i]]["reverse_image_numbers"]) {
        document.getElementById(ori[i] + "0sliceNumText").innerHTML = padZeros(gSampleJSONDict[ori[i]]["num_of_slices"] - 1, 4);
        document.getElementById(ori[i] + "1sliceNumText").innerHTML = padZeros(0, 4);
      } else {
        document.getElementById(ori[i] + "0sliceNumText").innerHTML = padZeros(0, 4);
        document.getElementById(ori[i] + "1sliceNumText").innerHTML = padZeros(gSampleJSONDict[ori[i]]["num_of_slices"] - 1, 4);
      }
      $("#" + ori[i] + "0Range").val(0);
      $("#" + ori[i] + "1Range").val(100);
    }
  }
}

function resetCameraControls() {
  // controls.autoRotate = true;
  controls.enableRotate = false;
  controls.enablePan = true;
  controls.enableZoom = true;
  controls.zoomSpeed = 0.8;
  controls.enableDamping = true;
  controls.dampingFactor = 0.7;
  controls.rotateSpeed = 0.07;
  controls.minDistance = 0.05;
  controls.maxDistance = 1;
  // controls.target = new THREE.Vector3(-0.03, -0.01, 0);
  controls.target = new THREE.Vector3(0, 0, 0);

  // controls.enableKeys = true;
  // controls.keys = {
  //     LEFT: 65, //A
  //     UP: 87, //W
  //     RIGHT: 68, //D
  //     BOTTOM: 83 //S
  // };

  // controls.keys = {
  //     LEFT: 39, //left arrow
  //     UP: 38, // up arrow
  //     RIGHT: 37, // right arrow
  //     BOTTOM: 40 // down arrow
  // }
  controls.update();

  camera.position.set(0, 0, gSampleJSONDict["camera_distance"]);
}

function placeHelperGrid(boundsBoundingBox) {
  var group = new THREE.Group();
  group.name = "helperGrid";
  var helperGrid = new THREE.GridHelper(1, 100, 0x888888, 0x333333);
  helperGrid.position.y = boundsBoundingBox.min.y;
  group.add(helperGrid);

  return group;
}

function createOrientationCube(boundsBoundingBox) {
  var group = new THREE.Group();
  group.name = "orientationCube";

  var cubeTexturePath = "textures/orientationCubeTex/";

  var cubeMaterials = [new THREE.MeshPhysicalMaterial({ map: THREE.ImageUtils.loadTexture(cubeTexturePath + "S.png"), roughness: 1 }), new THREE.MeshPhysicalMaterial({ map: THREE.ImageUtils.loadTexture(cubeTexturePath + "N.png"), roughness: 1 }), new THREE.MeshPhysicalMaterial({ map: THREE.ImageUtils.loadTexture(cubeTexturePath + "T.png"), roughness: 1 }), new THREE.MeshPhysicalMaterial({ map: THREE.ImageUtils.loadTexture(cubeTexturePath + "B.png"), roughness: 1 }), new THREE.MeshPhysicalMaterial({ map: THREE.ImageUtils.loadTexture(cubeTexturePath + "W.png"), roughness: 1 }), new THREE.MeshPhysicalMaterial({ map: THREE.ImageUtils.loadTexture(cubeTexturePath + "E.png"), roughness: 1 })];

  var orientationCube = new THREE.Mesh(new THREE.BoxGeometry(0.01, 0.01, 0.01), cubeMaterials);
  var orientationCubeBoundingBox = new THREE.Box3().setFromObject(orientationCube);
  //orientation cube initial rotation based on curation photos //TODO why is this duplicated?
  orientationCube.rotation.x = THREE.Math.degToRad(gSampleJSONDict["orientation_cube_relative_rotation_x"]);
  orientationCube.rotation.y = THREE.Math.degToRad(gSampleJSONDict["orientation_cube_relative_rotation_y"]);
  orientationCube.rotation.z = THREE.Math.degToRad(gSampleJSONDict["orientation_cube_relative_rotation_z"]);

  orientationCube.position.x = boundsBoundingBox.min.x - (orientationCubeBoundingBox.max.y - orientationCubeBoundingBox.min.y) / 2;
  orientationCube.position.y = boundsBoundingBox.min.y + (orientationCubeBoundingBox.max.y - orientationCubeBoundingBox.min.y) / 2;
  orientationCube.position.z = boundsBoundingBox.max.z;
  orientationCube.rotation.x = THREE.Math.degToRad(gSampleJSONDict["orientation_cube_relative_rotation_x"]);
  orientationCube.rotation.y = THREE.Math.degToRad(gSampleJSONDict["orientation_cube_relative_rotation_y"]);
  orientationCube.rotation.z = THREE.Math.degToRad(gSampleJSONDict["orientation_cube_relative_rotation_z"]);
  group.add(orientationCube);
  return group;
}

function createNavCubeCutout() {
  //----------- add orientation cube scene
  var oriSelector = $("#oriContainer");
  var oriContainer = document.getElementById("oriContainer");
  oriRenderer = new THREE.WebGLRenderer({
    antialias: true,
    alpha: true,
  });
  oriRenderer.setClearColor(0xffffff, 0.1);
  oriRenderer.setPixelRatio(window.devicePixelRatio);
  oriRenderer.setSize(oriSelector.width(), oriSelector.height());
  oriContainer.appendChild(oriRenderer.domElement);

  oriScene = new THREE.Scene();
  // oriScene.background = new THREE.Color(0x00ff00);
  oriCamera = new THREE.PerspectiveCamera(cameraFov, oriSelector.width() / oriSelector.height(), 0.00001, 1000);
  oriCamera.position.set(0, 0, -0.07);
  oriCamera.lookAt(0, 0, 0);

  var ambientLight = new THREE.AmbientLight(0xffffff, 1);
  oriScene.add(ambientLight);

  var directionalLight = new THREE.DirectionalLight(0xffffff, 1.5);
  directionalLight.position.set(0, 0, -10);
  directionalLight.visible = true;
  oriScene.add(directionalLight);

  // create nav cube
  var cubeTexturePath = "textures/nav-cube_faces/";

  var cubeMaterials = [
    new THREE.MeshPhysicalMaterial({
      map: THREE.ImageUtils.loadTexture(cubeTexturePath + "nav-cube_LEFT_face.png"),
      roughness: 1,
    }),
    new THREE.MeshPhysicalMaterial({
      map: THREE.ImageUtils.loadTexture(cubeTexturePath + "nav-cube_RIGHT_face.png"),
      roughness: 1,
    }),
    new THREE.MeshPhysicalMaterial({
      map: THREE.ImageUtils.loadTexture(cubeTexturePath + "nav-cube_TOP_face.png"),
      roughness: 1,
    }),
    new THREE.MeshPhysicalMaterial({
      map: THREE.ImageUtils.loadTexture(cubeTexturePath + "nav-cube_BOTTOM_face.png"),
      roughness: 1,
    }),
    new THREE.MeshPhysicalMaterial({
      map: THREE.ImageUtils.loadTexture(cubeTexturePath + "nav-cube_BACK_face.png"),
      roughness: 1,
    }),
    new THREE.MeshPhysicalMaterial({
      map: THREE.ImageUtils.loadTexture(cubeTexturePath + "nav-cube_FRONT_face.png"),
      roughness: 1,
    }),
  ];

  navCube = new THREE.Mesh(new THREE.BoxGeometry(0.01, 0.01, 0.01), cubeMaterials);

  oriScene.add(navCube);
}

function createLabelledScaleBox() {
  //---------- show bounding box mesh with labels
  var scaleBoxGroup = new THREE.Group();

  var sampleMesh3DWidth = sampleBoundingBox.max.x - sampleBoundingBox.min.x;
  var sampleMesh3DHeight = sampleBoundingBox.max.y - sampleBoundingBox.min.y;
  var sampleMesh3DDepth = sampleBoundingBox.max.z - sampleBoundingBox.min.z;

  var sampleBoundingBoxGeometry = new THREE.BoxGeometry(sampleMesh3DWidth, sampleMesh3DHeight, sampleMesh3DDepth);
  var sampleBoundingBoxMesh = new THREE.LineSegments(new THREE.EdgesGeometry(sampleBoundingBoxGeometry), new THREE.LineBasicMaterial({ color: 0xffffff }));
  // var materialWireframe = new THREE.MeshPhongMaterial({color:"white",wireframe:true});
  // var sampleBoundingBoxMesh =  new THREE.Mesh( sampleBoundingBoxGeometry, materialWireframe );
  sampleBoundingBoxMesh.name = "boundingBoxMesh";
  scaleBoxGroup.add(sampleBoundingBoxMesh);

  //LABELS
  var spriteWidth = makeTextSprite((sampleMesh3DWidth * 100).toFixed(2) + "cm", {
    fontsize: 45,
    textColor: { r: 200, g: 200, b: 200, a: 1.0 },
    borderColor: { r: 200, g: 200, b: 200, a: 1.0 },
    backgroundColor: { r: 0, g: 0, b: 0, a: 1 },
  });
  spriteWidth.position.set(sampleMesh.position.x, sampleBoundingBox.min.y, sampleBoundingBox.min.z);
  spriteWidth.name = "spriteWidth";

  var spriteHeight = makeTextSprite((sampleMesh3DHeight * 100).toFixed(2) + "cm", {
    fontsize: 45,
    textColor: { r: 200, g: 200, b: 200, a: 1.0 },
    borderColor: { r: 200, g: 200, b: 200, a: 1.0 },
    backgroundColor: { r: 0, g: 0, b: 0, a: 1 },
  });
  spriteHeight.position.set(sampleBoundingBox.min.x, sampleMesh.position.y, sampleBoundingBox.min.z);
  spriteHeight.name = "spriteHeight";

  var spriteDepth = makeTextSprite((sampleMesh3DDepth * 100).toFixed(2) + "cm", {
    fontsize: 45,
    textColor: { r: 200, g: 200, b: 200, a: 1.0 },
    borderColor: { r: 200, g: 200, b: 200, a: 1.0 },
    backgroundColor: { r: 0, g: 0, b: 0, a: 1 },
  });
  spriteDepth.position.set(sampleBoundingBox.min.x, sampleBoundingBox.max.y, sampleMesh.position.z);
  spriteDepth.name = "spriteDepth";

  scaleBoxGroup.add(spriteWidth);
  scaleBoxGroup.add(spriteHeight);
  scaleBoxGroup.add(spriteDepth);

  return scaleBoxGroup;
}

function togglePinTab(tabName) {
  updateCustomPinComments();
  if (tabName === "nasaPins" && nasaPins.length > 0) {
    displayNasaPins();
  } else {
    displayCustomPins();
  }
}

function toggleAddPinMode() {
  // ga('send', 'event', 'A3D', 'pins', 'add');
  gtag("event", "astromaterials3dexplorer", {
    event_category: "engagement",
    event_label: "pin add",
  });
  $("#addPinsButton").toggleClass("active");
}

function hoverPin() {
  sceneGroup.remove(sceneGroup.getObjectByName("hoverPin"));
  var pin = generatePin();
  if (typeof pin !== "undefined") {
    if (pin !== null) {
      pin.pinGroup.name = "hoverPin";
      sceneGroup.add(pin.pinGroup);
    }
    // customPins.push(pin);
  }
}

function addPin() {
  var pin = generatePin();
  if (typeof pin !== "undefined") {
    if (pin !== null) {
      customPins.push(pin);
      updateCustomPinComments();
      displayCustomPins(true);
      drawPincount();
    }
  }
}

function generatePin() {
  raycaster.setFromCamera(mouseCoords, camera);
  var objectsToIntersect = [sampleMesh];

  const pointsAndNormal = {
    point: new THREE.Vector3(),
    point2: null,
  };

  var normal = new THREE.Vector3();

  var intersects = raycaster.intersectObjects(objectsToIntersect);
  if (intersects.length > 0) {
    pointsAndNormal.point.copy(intersects[0].point);
    normal.copy(intersects[0].face.normal);

    var p2 = normal.clone();
    p2.transformDirection(sampleMesh.matrixWorld);
    scalarToAdd = 0.008;
    p2.multiplyScalar(scalarToAdd);
    p2.add(pointsAndNormal.point.clone());
    pointsAndNormal.point2 = p2.clone();

    pointsAndNormal.point = sceneGroup.worldToLocal(pointsAndNormal.point);
    pointsAndNormal.point2 = sceneGroup.worldToLocal(pointsAndNormal.point2);

    var pinName;
    if (customPins.length !== 0) {
      var lastIndex = alphabet.indexOf(customPins[customPins.length - 1].name);
      pinName = alphabet[lastIndex + 1];
    } else {
      pinName = "a";
    }
    var pin = makePinObject(pointsAndNormal, pinName);

    var slicePin = null;
    var slicePinConsidered = false;
    if (pin.pinGroup.children[0].geometry.vertices[0].z <= slicerMesh["XY0"].position.z) {
      //attempt to pin is on sliced hidden part of sample mesh, so pin it on the slice.
      slicePin = makePossibleSlicePin("XY0", pinName);
      slicePinConsidered = true;
    }
    if (pin.pinGroup.children[0].geometry.vertices[0].z >= slicerMesh["XY1"].position.z && slicePin === null) {
      //attempt to pin is on sliced hidden part of sample mesh, so pin it on the slice.
      slicePin = makePossibleSlicePin("XY1", pinName);
      slicePinConsidered = true;
    }
    if (pin.pinGroup.children[0].geometry.vertices[0].y >= slicerMesh["XZ0"].position.y && slicePin === null) {
      //attempt to pin is on sliced hidden part of sample mesh, so pin it on the slice.
      slicePin = makePossibleSlicePin("XZ0", pinName);
      slicePinConsidered = true;
    }
    if (pin.pinGroup.children[0].geometry.vertices[0].y <= slicerMesh["XZ1"].position.y && slicePin === null) {
      //attempt to pin is on sliced hidden part of sample mesh, so pin it on the slice.
      slicePin = makePossibleSlicePin("XZ1", pinName);
      slicePinConsidered = true;
    }
    if (pin.pinGroup.children[0].geometry.vertices[0].x <= slicerMesh["YZ0"].position.x && slicePin === null) {
      //attempt to pin is on sliced hidden part of sample mesh, so pin it on the slice.
      slicePin = makePossibleSlicePin("YZ0", pinName);
      slicePinConsidered = true;
    }
    if (pin.pinGroup.children[0].geometry.vertices[0].x >= slicerMesh["YZ1"].position.x && slicePin === null) {
      //attempt to pin is on sliced hidden part of sample mesh, so pin it on the slice.
      slicePin = makePossibleSlicePin("YZ1", pinName);
      slicePinConsidered = true;
    }
    if (slicePinConsidered === true && slicePin !== null) {
      pin = slicePin;
    }
    return pin;
  }
}

function createPinTableRow(pin, pinTableElementId) {
  var html = $("#pinRowTemplate").html();
  html = html.replace(/@pinName/g, pin.name);
  html = html.replace(/@pinComment/g, pin.commentText);

  var table = document.getElementById(pinTableElementId);
  table.innerHTML = table.innerHTML + html;
  $("#pinText" + pin.name).selectText();
}

function makePossibleSlicePin(slicerName, name) {
  // console.log('makePossibleSlicePin(): ' + slicerName);
  var ori = slicerName.substring(0, 2);

  const pointsAndNormal = {
    point: new THREE.Vector3(),
    point2: null,
  };

  var normal = new THREE.Vector3();

  var slicePin = null;
  var sliceIntersects = raycaster.intersectObjects(slicerMesh[slicerName].children);
  var sliceIntersectToPin = -1;
  //avoid trying to pin to helper meshes if they're turned on
  for (var i = 0; i < sliceIntersects.length; i++) {
    if (sliceIntersects[i].object.name.includes("texturedSide")) {
      sliceIntersectToPin = i;
      break;
    }
  }
  if (sliceIntersectToPin !== -1) {
    pointsAndNormal.point = sliceIntersects[sliceIntersectToPin].point;
    // normal = sliceIntersects[sliceIntersectToPin].face.normal;

    switch (slicerName) {
      case "XY0":
        normal = new THREE.Vector3(0, 0, -1);
        break;
      case "XY1":
        normal = new THREE.Vector3(0, 0, 1);
        break;
      case "XZ0":
        normal = new THREE.Vector3(0, 0, -1);
        break;
      case "XZ1":
        normal = new THREE.Vector3(0, 0, 1);
        break;
      case "YZ0":
        normal = new THREE.Vector3(0, 0, -1);
        break;
      case "YZ1":
        normal = new THREE.Vector3(0, 0, 1);
        break;
    }
    var drawPin = false;
    if (ori === "XY") {
      if (pointsAndNormal.point.y <= slicerMesh["XZ0"].position.y && pointsAndNormal.point.x >= slicerMesh["YZ0"].position.x && pointsAndNormal.point.y >= slicerMesh["XZ1"].position.y && pointsAndNormal.point.x <= slicerMesh["YZ1"].position.x) {
        drawPin = true;
      }
    } else if (ori === "XZ") {
      if (pointsAndNormal.point.z >= slicerMesh["XY0"].position.z && pointsAndNormal.point.x >= slicerMesh["YZ0"].position.x && pointsAndNormal.point.z <= slicerMesh["XY1"].position.z && pointsAndNormal.point.x <= slicerMesh["YZ1"].position.x) {
        drawPin = true;
      }
    } else if (ori === "YZ") {
      if (pointsAndNormal.point.z >= slicerMesh["XY0"].position.z && pointsAndNormal.point.y <= slicerMesh["XZ0"].position.y && pointsAndNormal.point.z <= slicerMesh["XY1"].position.z && pointsAndNormal.point.y >= slicerMesh["XZ1"].position.y) {
        drawPin = true;
      }
    }

    if (drawPin) {
      // console.log("pinning on " + slicerName + " pointsAndNormal.point: " + JSON.stringify(pointsAndNormal.point) + " | normal: " + JSON.stringify(normal));
      var p2 = normal.clone();
      p2.transformDirection(sliceIntersects[sliceIntersectToPin].object.matrixWorld);
      scalarToAdd = 0.005;
      p2.multiplyScalar(scalarToAdd);
      p2.add(pointsAndNormal.point.clone());
      pointsAndNormal.point2 = p2.clone();

      pointsAndNormal.point = sceneGroup.worldToLocal(pointsAndNormal.point);
      pointsAndNormal.point2 = sceneGroup.worldToLocal(pointsAndNormal.point2);

      slicePin = makePinObject(pointsAndNormal, name);
    } else {
      console.log("It looks like the pin is being clipped by one of the other clipping planes, so I'm not adding it.");
      slicePin = null;
    }
  } else {
    console.log("Didn't seem to intersect the textured side of the slicer. Not drawing pin.");
    slicePin = null;
  }
  return slicePin;
}

function makePinObject(pointsAndNormal, name, commentText, groupRotation, slicesSelected) {
  commentText = commentText || "Type your pin description here";
  groupRotation = groupRotation || {
    x: sceneGroup.rotation.x,
    y: sceneGroup.rotation.y,
    z: sceneGroup.rotation.z,
  };
  slicesSelected = slicesSelected || {
    XY0: $("#XY0sliceNumText").text(),
    XY1: $("#XY1sliceNumText").text(),
    XZ0: $("#XZ0sliceNumText").text(),
    XZ1: $("#XZ1sliceNumText").text(),
    YZ0: $("#YZ0sliceNumText").text(),
    YZ1: $("#YZ1sliceNumText").text(),
  };

  var pinGroup = new THREE.Group();
  pinGroup.name = "pinGroup_" + name;

  var v1, v2, scalarToAdd;
  if (pointsAndNormal.point2 == null) {
    // if an old URL is used that doesn't have normal data, make pins the old way
    // *** this is the breakthrough. Transform the position from world intersection to the rotated sceneGroup
    v1 = pointsAndNormal.point.clone();
    v2 = pointsAndNormal.point.clone();
    scalarToAdd = 0.005;

    //make the pin scalarToAdd in length, regardless of whether it's on the positive or negative side of the sample
    v2.x = v1.x > 0 ? v2.x + scalarToAdd : v2.x - scalarToAdd;
    v2.y = v1.y > 0 ? v2.y + scalarToAdd : v2.y - scalarToAdd;
    v2.z = v1.z > 0 ? v2.z + scalarToAdd : v2.z - scalarToAdd;
  } else {
    //make pins the new way using the normal of the face the pin sits on for the pin shaft
    v1 = pointsAndNormal.point.clone();
    v2 = pointsAndNormal.point2.clone();
  }

  //pin shaft
  var pinShaftGeom = new THREE.Geometry();
  pinShaftGeom.vertices.push(v1, v2);
  var pinShaft = new THREE.LineSegments(
    pinShaftGeom,
    new THREE.LineBasicMaterial({
      // color: 0x00ff00
      color: 0xffffff,
    })
  );
  pinGroup.add(pinShaft);

  //pin label
  var pinLabel = makeTextSprite(
    name,
    // { fontsize: 45, textColor: {r:200, g:200, b:200, a:1.0}, borderColor: {r:200, g:200, b:200, a:1.0}, backgroundColor: {r:0, g:0, b:0, a:1} } );
    {
      fontsize: 100,
      textColor: { r: 255, g: 255, b: 255, a: 1.0 },
      borderColor: { r: 255, g: 255, b: 255, a: 1.0 },
      backgroundColor: { r: 235, g: 39, b: 43, a: 1.0 },
    }
  );
  pinLabel.position.copy(v2);
  pinLabel.visible = true;
  pinGroup.add(pinLabel);

  var pinObject = {
    name: name,
    commentText: commentText,
    point: pointsAndNormal.point,
    point2: pointsAndNormal.point2,
    sliceNumbers: {
      XY0: parseInt(document.getElementById("XY0sliceNumText").innerHTML),
      XY1: parseInt(document.getElementById("XY1sliceNumText").innerHTML),
      XZ0: parseInt(document.getElementById("XZ0sliceNumText").innerHTML),
      XZ1: parseInt(document.getElementById("XZ1sliceNumText").innerHTML),
      YZ0: parseInt(document.getElementById("YZ0sliceNumText").innerHTML),
      YZ1: parseInt(document.getElementById("YZ1sliceNumText").innerHTML),
    },
    groupRotation,
    slicesSelected,
    pinGroup: pinGroup,
  };
  return pinObject;
}

function updateCustomPinComments() {
  if (document.getElementById("customPins").style.display !== "none") {
    const comments = document.getElementsByClassName("pinText");
    for (var i = 0; i < comments.length; i++) {
      customPins[i].commentText = comments[i].innerHTML;
    }
  }
}

function clearCustomPins() {
  // ga('send', 'event', 'A3D', 'pins', 'clear');
  gtag("event", "astromaterials3dexplorer", {
    event_category: "engagement",
    event_label: "pin clear",
  });
  for (var i = 0; i < customPins.length; i++) {
    for (var y = 0; y < customPins[i].pinGroup.children.length; y++) {
      customPins[i].pinGroup.children[y].geometry.dispose();
    }
    sceneGroup.remove(customPins[i].pinGroup);
  }
  customPins = [];
  clearDisplayedPins();
  displayCustomPins();
  drawPincount();
  $.modal.close();
}

function deletePin(name) {
  var newPinsList = [];
  let newListPinEnumerator = 0;
  for (var i = 0; i < customPins.length; i++) {
    if (customPins[i].name !== name) {
      customPins[i].name = alphabet[newListPinEnumerator];
      // customPins[i].pinGroup.pinLabel.name = alphabet[newListPinEnumerator];

      var currentPinLabel = customPins[i].pinGroup.getObjectByName("labelSprite");
      var newPinLabel = updatePinLabel(alphabet[newListPinEnumerator], currentPinLabel);
      customPins[i].pinGroup.remove(currentPinLabel);
      customPins[i].pinGroup.add(newPinLabel);

      newPinsList.push(customPins[i]);
      newListPinEnumerator++;
    }
  }
  customPins = [...newPinsList];
  document.getElementById("pinEntry_" + name).remove();
  displayCustomPins();
  drawPincount();
}

function updatePinLabel(labelText, currentPinLabel) {
  var pinLabel = makeTextSprite(
    labelText,
    // { fontsize: 45, textColor: {r:200, g:200, b:200, a:1.0}, borderColor: {r:200, g:200, b:200, a:1.0}, backgroundColor: {r:0, g:0, b:0, a:1} } );
    {
      fontsize: 45,
      textColor: { r: 255, g: 255, b: 255, a: 1.0 },
      borderColor: { r: 255, g: 255, b: 255, a: 1.0 },
      backgroundColor: { r: 235, g: 39, b: 43, a: 1.0 },
    }
  );
  pinLabel.position.copy(currentPinLabel.position);
  pinLabel.visible = true;
  return pinLabel;
}

function displayCustomPins(addingPin) {
  addingPin = typeof addingPin !== "undefined" ? addingPin : false;

  document.getElementById("pinsTitleText").innerHTML = "Custom Pins";
  document.getElementById("nasaPins").style.display = "none";
  document.getElementById("customPins").style.display = "block";
  document.getElementById("pinType_left").classList.remove("pinType_navigation_item_active");
  document.getElementById("pinType_right").classList.add("pinType_navigation_item_active");
  if (nasaPins.length === 0) {
    document.getElementById("pinType_left").classList.add("pinType_navigation_item_disabled");
    document.getElementById("pinType_left").title = "NASA curated pins are coming soon for this sample";
  }

  $(".pinTableRow").remove();
  clearDisplayedPins();
  for (var i = 0; i < customPins.length; i++) {
    var pin = customPins[i];
    pin.pinGroup.visible = false;
    displayedPins.push(pin);
    sceneGroup.add(pin.pinGroup);
    createPinTableRow(pin, "customPinTable");
  }
  if (addingPin) {
    showPin(displayedPins[displayedPins.length - 1].name);
  }

  $(".pinText").attr("contenteditable", "true");
}

function displayNasaPins() {
  if (nasaPins.length === 0) {
    document.getElementById("pinType_left").classList.add("pinType_navigation_item_disabled");
    document.getElementById("pinType_left").title = "NASA curated pins are coming soon for this sample";
  } else {
    document.getElementById("pinsTitleText").innerHTML = "NASA Curated Pins";
    document.getElementById("nasaPins").style.display = "block";
    document.getElementById("customPins").style.display = "none";
    document.getElementById("pinType_left").classList.add("pinType_navigation_item_active");
    document.getElementById("pinType_right").classList.remove("pinType_navigation_item_active");
    $(".pinTableRow").remove();
    clearDisplayedPins();
    for (var i = 0; i < nasaPins.length; i++) {
      var pin = nasaPins[i];
      pin.pinGroup.visible = false;
      displayedPins.push(pin);
      sceneGroup.add(pin.pinGroup);
      createPinTableRow(pin, "nasaPinTable");
      document.getSelection().removeAllRanges();
      $(".pinText").attr("contenteditable", "false");
    }
    $(".deletePinButton").hide();
  }
}

function clearDisplayedPins() {
  for (var i = 0; i < displayedPins.length; i++) {
    for (var y = 0; y < displayedPins[i].pinGroup.children.length; y++) {
      displayedPins[i].pinGroup.children[y].geometry.dispose();
    }
    sceneGroup.remove(displayedPins[i].pinGroup);
  }
  displayedPins = [];
}

function showPin(name) {
  var pinNum = 0;
  for (var i = 0; i < displayedPins.length; i++) {
    if (displayedPins[i].name === name) {
      pinNum = i;
      break;
    }
  }

  for (var i = 0; i < displayedPins.length; i++) {
    if (i !== pinNum) {
      displayedPins[i].pinGroup.visible = false;
      document.getElementById("pinButtonId_" + displayedPins[i].name).classList.remove("pinButtonSelected");
    } else {
      document.getElementById("pinButtonId_" + displayedPins[i].name).classList.add("pinButtonSelected");
      displayedPins[i].pinGroup.visible = true;
    }
  }

  //rotate navCube
  navCube.rotation.x = displayedPins[pinNum].groupRotation.x;
  navCube.rotation.y = displayedPins[pinNum].groupRotation.y;
  navCube.rotation.z = displayedPins[pinNum].groupRotation.z;

  //rotate group
  sceneGroup.rotation.x = displayedPins[pinNum].groupRotation.x;
  sceneGroup.rotation.y = displayedPins[pinNum].groupRotation.y;
  sceneGroup.rotation.z = displayedPins[pinNum].groupRotation.z;
  sceneGroup.updateMatrixWorld(true);
  updateClippingPlanes();

  //set slice numbers
  document.getElementById("XY0sliceNumText").innerHTML = displayedPins[pinNum].slicesSelected.XY0;
  document.getElementById("XY1sliceNumText").innerHTML = displayedPins[pinNum].slicesSelected.XY1;
  document.getElementById("XZ0sliceNumText").innerHTML = displayedPins[pinNum].slicesSelected.XZ0;
  document.getElementById("XZ1sliceNumText").innerHTML = displayedPins[pinNum].slicesSelected.XZ1;
  document.getElementById("YZ0sliceNumText").innerHTML = displayedPins[pinNum].slicesSelected.YZ0;
  document.getElementById("YZ1sliceNumText").innerHTML = displayedPins[pinNum].slicesSelected.YZ1;

  //position slices
  sliceAtImageNum("XY0", displayedPins[pinNum].slicesSelected.XY0);
  sliceAtImageNum("XY1", displayedPins[pinNum].slicesSelected.XY1);
  sliceAtImageNum("XZ0", displayedPins[pinNum].slicesSelected.XZ0);
  sliceAtImageNum("XZ1", displayedPins[pinNum].slicesSelected.XZ1);
  sliceAtImageNum("YZ0", displayedPins[pinNum].slicesSelected.YZ0);
  sliceAtImageNum("YZ1", displayedPins[pinNum].slicesSelected.YZ1);
}

function animate() {
  requestAnimationFrame(animate);

  //animate panning of camera

  if (autoR === true) {
    var rotationRate = -0.001;
    sceneGroup.rotation.x += 0;
    sceneGroup.rotation.y += rotationRate;
    sceneGroup.rotation.z += 0;
    navCube.rotation.x += 0;
    navCube.rotation.y += rotationRate;
    navCube.rotation.z += 0;
    updateClippingPlanes();
  }
  for (var i in slicerNames) {
    slicerTexture[slicerNames[i]].needsUpdate = true;
  }

  if (!mDown) {
    var drag = 0.95;
    var minDelta = 0.05;

    if (deltaX < -minDelta || deltaX > minDelta) {
      deltaX *= drag;
    } else {
      deltaX = 0;
    }

    if (deltaY < -minDelta || deltaY > minDelta) {
      deltaY *= drag;
    } else {
      deltaY = 0;
    }

    if (deltaX !== 0 || deltaY !== 0) {
      handleRotation();
    }
  }
  render();
}

function render() {
  if (enableAnaglyph) anaglyphEffect.render(scene, camera);
  else renderer.render(scene, camera);

  oriRenderer.render(oriScene, oriCamera);
  //if download selected
  if (getImageDataForDownload === true) {
    var download = document.getElementById("downloadSnapshot");

    downloadImgData = renderer.domElement.toDataURL("image/png").replace("image/png", "image/octet-stream");
    download.setAttribute("href", downloadImgData);
    renderer.setClearAlpha(0);
    getImageDataForDownload = false;
    download.click();
  }
}

function updateSlicerPosition(slicerName, rangeValue) {
  var ori = slicerName.substring(0, 2);
  // var rangeSelector = $('#' + slicerName + 'Range');
  paperCanvasScope[slicerName].activate();

  var relativePosition;
  if (slicerName === "XY0") {
    relativePosition = (parseFloat(rangeValue) * boundsMesh3DDepth) / 100 - boundsMesh3DDepth / 2;
    slicerMesh["XY0"].position.z = relativePosition;
  } else if (slicerName === "XY1") {
    relativePosition = (parseFloat(rangeValue) * boundsMesh3DDepth) / 100 - boundsMesh3DDepth / 2;
    slicerMesh["XY1"].position.z = relativePosition;
  } else if (slicerName === "XZ0") {
    relativePosition = ((100 - parseFloat(rangeValue)) * boundsMesh3DHeight) / 100 - boundsMesh3DHeight / 2;
    // relativePosition = ((parseFloat(rangeSelector.val()) * boundsMesh3DHeight) / 100) - boundsMesh3DHeight / 2;
    slicerMesh["XZ0"].position.y = relativePosition;
  } else if (slicerName === "XZ1") {
    relativePosition = ((100 - parseFloat(rangeValue)) * boundsMesh3DHeight) / 100 - boundsMesh3DHeight / 2;
    // relativePosition = ((parseFloat(rangeSelector.val()) * boundsMesh3DHeight) / 100) - boundsMesh3DHeight / 2;
    slicerMesh["XZ1"].position.y = relativePosition;
  } else if (slicerName === "YZ0") {
    relativePosition = (parseFloat(rangeValue) * boundsMesh3DWidth) / 100 - boundsMesh3DWidth / 2;
    slicerMesh["YZ0"].position.x = relativePosition;
  } else if (slicerName === "YZ1") {
    relativePosition = (parseFloat(rangeValue) * boundsMesh3DWidth) / 100 - boundsMesh3DWidth / 2;
    slicerMesh["YZ1"].position.x = relativePosition;
  }

  var imageNumber = Math.round((parseFloat(rangeValue) * (gSampleJSONDict[ori]["num_of_slices"] - 1)) / 100);
  var spriteImageNumber = Math.round((parseFloat(rangeValue) * (gSampleJSONDict[ori]["spritesheet_num_of_slices"] - 1)) / 100);

  if (gSampleJSONDict[ori]["reverse_image_numbers"]) {
    imageNumber = gSampleJSONDict[ori]["num_of_slices"] - 1 - imageNumber;
    spriteImageNumber = gSampleJSONDict[ori]["spritesheet_num_of_slices"] - 1 - spriteImageNumber;
  }

  // $('#' + slicerName + 'slicerPositionInfo').html("CT Slices: " + slicerName + " Range%: " + parseFloat(rangeValue).toFixed(4) + "  RelMeshPosition: " + relativePosition.toFixed(4) + "  CTImageNum: " + imageNumber + " CTSpriteImageNumber: " + spriteImageNumber);

  //move image sprite on correct paperjs canvas
  var top = gSampleJSONDict[ori]["spritesheet_tile_height"] * gSampleJSONDict["sprite_scale_factor"] * Math.floor(spriteImageNumber / gSampleJSONDict[ori]["spritesheet_frames_per_row"]);
  var left = gSampleJSONDict[ori]["spritesheet_tile_width"] * gSampleJSONDict["sprite_scale_factor"] * (spriteImageNumber % gSampleJSONDict[ori]["spritesheet_frames_per_row"]);

  sliceSpriteRaster[slicerName].position = new Point((gSampleJSONDict[ori]["spritesheet_width"] * gSampleJSONDict["sprite_scale_factor"]) / 2 - left, (gSampleJSONDict[ori]["spritesheet_height"] * gSampleJSONDict["sprite_scale_factor"]) / 2 - top);

  //update slice number text field
  document.getElementById(slicerName + "sliceNumText").innerHTML = padZeros(imageNumber, 4);

  //call update position of clipping planes
  updateClippingPlanes();

  //recalculate distances between clipping planes
  calculateSlicerDistances();

  //make clipped pins invisible
  // showHidePins();
}

function updateClippingPlanes() {
  //orient clipping planes against slicerMesh world positions
  var targetVector = {};
  var normal = {};
  for (var i in slicerNames) {
    targetVector[slicerNames[i]] = new THREE.Vector3();
    slicerMesh[slicerNames[i]].getWorldPosition(targetVector[slicerNames[i]]);
    normal[slicerNames[i]] = new THREE.Vector3();
  }
  normal["XY0"].set(0, 0, 1).applyQuaternion(sceneGroup.quaternion);
  normal["XY1"].set(0, 0, -1).applyQuaternion(sceneGroup.quaternion);
  normal["XZ0"].set(0, -1, 0).applyQuaternion(sceneGroup.quaternion);
  normal["XZ1"].set(0, 1, 0).applyQuaternion(sceneGroup.quaternion);
  normal["YZ0"].set(1, 0, 0).applyQuaternion(sceneGroup.quaternion);
  normal["YZ1"].set(-1, 0, 0).applyQuaternion(sceneGroup.quaternion);
  for (i in slicerNames) {
    clippingPlane[slicerNames[i]].setFromNormalAndCoplanarPoint(normal[slicerNames[i]], sceneGroup.position);
    clippingPlane[slicerNames[i]].translate(targetVector[slicerNames[i]]);
    slicerTexture[slicerNames[i]].needsUpdate = true;
  }
}

function loadHighresSliceImage(sliceName, rangeValue) {
  var ori = sliceName.substring(0, 2);
  var imageNumber = Math.round((rangeValue * (gSampleJSONDict[ori]["num_of_slices"] - 1)) / 100);
  if (gSampleJSONDict[ori]["reverse_image_numbers"]) {
    imageNumber = gSampleJSONDict[ori]["num_of_slices"] - 1 - imageNumber;
  }

  var sliceImgFilename = gSampleJSONDict[ori]["slices_file_format"] + padZeros(imageNumber, 4) + ".png";
  var sliceImgPath = gSamplesLocation + sampleNum + "/" + gSampleJSONDict[ori]["slices_folder_name"].replace("JPEG", "PNG");

  paperCanvasScope[sliceName].activate();

  sliceHighresRaster[sliceName].crossOrigin = "anonymous";
  sliceHighresRaster[sliceName].source = sliceImgPath + "/" + sliceImgFilename;
  sliceHighresRaster[sliceName].position = new Point(paperCanvasScope[sliceName].view.center.x, paperCanvasScope[sliceName].view.center.y);
  sliceHighresRaster[sliceName].onLoad = function () {
    console.log("The slice image " + sliceImgFilename + " has loaded for " + sliceName + ".");
    sliceHighresRaster[sliceName].bringToFront();
    sliceHighresRaster[sliceName].visible = true;
    // this.canvas.getContext('2d').filter = 'brightness(' + gSampleJSONDict['slices_brightness_level'] + '%)';
    // this.drawImage(this.canvas, 0, 0);
    // this.visible = true;
    slicerTexture[sliceName].needsUpdate = true;
  };
}

function incrementHighresSliceImage(sliceName, increment) {
  // ga('send', 'event', 'A3D', 'incrementslice', sliceName);
  gtag("event", "astromaterials3dexplorer", {
    event_category: "engagement",
    event_label: "slice increment " + sliceName,
  });
  var ori = sliceName.substring(0, 2);
  // var textSelector = $('#' + sliceName + 'sliceNumText');
  var currSliceNum = parseInt(document.getElementById(sliceName + "sliceNumText").innerHTML);
  var newSliceNum = currSliceNum + increment;
  if (newSliceNum < gSampleJSONDict[ori]["num_of_slices"]) {
    document.getElementById(sliceName + "sliceNumText").innerHTML = padZeros(newSliceNum, 4);
    sliceAtImageNum(sliceName, newSliceNum);
  }
}

function sliceAtImageNum(sliceName, imageNum) {
  var ori = sliceName.substring(0, 2);
  var data = range[ori].getInfo();
  var leftRightPosition;
  // only slice if there is XCT data for this sample
  if (ori in gSampleJSONDict) {
    if (gSampleJSONDict[ori]["reverse_image_numbers"]) {
      leftRightPosition = 100 - (imageNum * 100) / (gSampleJSONDict[ori]["num_of_slices"] - 1);
    } else {
      leftRightPosition = (imageNum * 100) / (gSampleJSONDict[ori]["num_of_slices"] - 1);
    }

    //this is a hack. The omni-slider doesn't move if you tell it to move to 0 or 100
    if (leftRightPosition === 0) {
      leftRightPosition = 0.001;
    } else if (leftRightPosition === 100) {
      leftRightPosition = 99.999;
    }

    if (sliceName.substring(2, 3) === "0") {
      data.left = leftRightPosition;
    } else {
      data.right = leftRightPosition;
    }

    range[ori].move(data);
    updateSlicerPosition(sliceName, leftRightPosition);
    loadHighresSliceImage(sliceName, leftRightPosition);
    //move slider to new position
  }
}

function rotateMatrix(rotateStart, rotateEnd) {
  var axis = new THREE.Vector3(),
    quaternion = new THREE.Quaternion();

  var angle = Math.acos(rotateStart.dot(rotateEnd) / rotateStart.length() / rotateEnd.length());

  if (angle) {
    axis.crossVectors(rotateStart, rotateEnd).normalize();
    angle *= rotationSpeed;
    quaternion.setFromAxisAngle(axis, angle);
  }
  return quaternion;
}

function handleRotation() {
  rotateEndPoint = projectOnTrackball(deltaX, deltaY);

  var rotateQuaternion = rotateMatrix(rotateStartPoint, rotateEndPoint);
  var curQuaternion = sceneGroup.quaternion;
  curQuaternion.multiplyQuaternions(rotateQuaternion, curQuaternion);
  curQuaternion.normalize();
  sceneGroup.setRotationFromQuaternion(curQuaternion);

  updateClippingPlanes();

  var navCubeQuaternion = navCube.quaternion;
  navCubeQuaternion.multiplyQuaternions(rotateQuaternion, navCubeQuaternion);
  navCubeQuaternion.normalize();
  navCube.setRotationFromQuaternion(navCubeQuaternion);

  rotateEndPoint = rotateStartPoint;
}

function projectOnTrackball(touchX, touchY) {
  var rockSelector = $("#rockContainer");
  var mouseOnBall = new THREE.Vector3();

  mouseOnBall.set(clamp((touchX / rockSelector.width()) * 2, -1, 1), clamp((touchY / rockSelector.height()) * 2, -1, 1), 0.0);

  var length = mouseOnBall.length();

  if (length > 1.0) {
    mouseOnBall.normalize();
  } else {
    mouseOnBall.z = Math.sqrt(1.0 - length * length);
  }

  return mouseOnBall;
}

function clamp(value, min, max) {
  return Math.min(Math.max(value, min), max);
}

function is_touch_device() {
  var prefixes = " -webkit- -moz- -o- -ms- ".split(" ");
  var mq = function (query) {
    return window.matchMedia(query).matches;
  };

  if ("ontouchstart" in window || (window.DocumentTouch && document instanceof DocumentTouch)) {
    return true;
  }

  // include the 'heartz' as a way to have a non matching MQ to help terminate the join
  // https://git.io/vznFH
  var query = ["(", prefixes.join("touch-enabled),("), "heartz", ")"].join("");
  return mq(query);
}

function onWindowResize() {
  var rockSelector = $("#rockContainer");

  // rockSelector.width($('#parentContainer').width() / 2);
  // rockSelector.height(rockSelector.width() / (4/3));

  rockSelector.width(window.innerWidth);
  rockSelector.height(window.innerHeight);

  camera.aspect = rockSelector.width() / rockSelector.height();
  camera.updateProjectionMatrix();
  renderer.setSize(rockSelector.width(), rockSelector.height());
  setSceneBackgroundImage();
}

function onError(error) {
  console.error(error);
}

function onTouchStart(event) {
  event.offsetX = event.changedTouches[0].clientX;
  event.offsetY = event.changedTouches[0].clientY;

  handleDownStart(event);
}

function onMouseDown(event) {
  switch (event.button) {
    case 0:
      // left mouse button
      handleDownStart(event);
      break;
    case 1:
      // middle mouse button
      break;
    default:
    // 2 === right mouse button
  }
}

function onMouseMove(event) {
  if (hasTouch) {
    return;
  }
  handleMove(event);

  //compensate for if the 3D view canvas isn't in the top left of the page
  var rect = event.target.getBoundingClientRect();
  var x = event.clientX - rect.left; //x position within the element.
  var y = event.clientY - rect.top; //y position within the element.

  mouseCoords.x = (x / document.getElementById("rockContainer").offsetWidth) * 2 - 1;
  mouseCoords.y = -(y / document.getElementById("rockContainer").offsetHeight) * 2 + 1;
}
function onTouchMove(event) {
  // console.log("touchlist: " + event.targetTouches.length);
  if (event.targetTouches.length === 1) {
    event.offsetX = event.changedTouches[0].clientX;
    event.offsetY = event.changedTouches[0].clientY;
    handleMove(event);
  }
}
function onTouchEnd(event) {
  handleMouseUp(event);
}
function onMouseUp(event) {
  if (hasTouch) {
    return;
  }
  event.preventDefault();
  handleMouseUp(event);
}
function onMouseLeave(event) {
  mDown = false;
}

function handleDownStart(event) {
  autoR = false;
  mDown = true;
  document.getElementById("moveMouseContainer").style.display = "none"; //hide the mouse move hint in case it was shown
  startPoint = {
    x: event.offsetX,
    y: event.offsetY,
  };
  if ($("#addPinsButton").hasClass("active")) {
    addPin();
    toggleAddPinMode();
  }
  sceneGroup.remove(sceneGroup.getObjectByName("hoverPin"));
}
function handleMove(event) {
  if (mDown) {
    deltaX = event.offsetX - startPoint.x;
    deltaY = event.offsetY - startPoint.y;

    handleRotation();

    startPoint.x = event.offsetX;
    startPoint.y = event.offsetY;

    lastMoveTimestamp = new Date();
  }
  if ($("#addPinsButton").hasClass("active")) {
    hoverPin();
  }
}
function handleMouseUp(event) {
  if (mDown) {
    if (typeof lastMoveTimestamp !== "undefined") {
      if (new Date().getTime() - lastMoveTimestamp.getTime() > moveReleaseTimeDelta) {
        deltaX = event.offsetX - startPoint.x;
        deltaY = event.offsetY - startPoint.y;
      }
    }
    mDown = false;
  }

  sceneGroup.remove(sceneGroup.getObjectByName("hoverPin"));
}

function setEventHandlers() {
  if (!Detector.webgl) Detector.addGetWebGLMessage();

  document.getElementById("loaderModal").style.display = "none";

  hasTouch = is_touch_device();

  window.addEventListener("resize", onWindowResize, false);

  var canvas = renderer.domElement;
  canvas.addEventListener("mousedown", onMouseDown);
  canvas.addEventListener("mouseup", onMouseUp);
  canvas.addEventListener("mouseleave", onMouseLeave);
  canvas.addEventListener("mousemove", onMouseMove);

  canvas.addEventListener("touchstart", onTouchStart);
  canvas.addEventListener("touchmove", onTouchMove);
  canvas.addEventListener("touchend", onTouchEnd);
  canvas.addEventListener("touchcancel", onTouchEnd);
}

function padZeros(num, size) {
  var s = num + "";
  while (s.length < size) s = "0" + s;
  return s;
}

function ajaxGetAllMetadata() {
  var lunarData, meteoriteData, lunarDataXctonly, meteoriteDataXctonly;
  return $.when(
    $.getJSON("lunar_sample_metadata.json", function (data) {
      lunarData = data;
    }).fail(function () {
      console.log("error pulling lunar metadata");
    }),
    $.getJSON("lunar_sample_metadata_xct_only.json", function (data) {
      lunarDataXctonly = data;
    }).fail(function () {
      console.log("error pulling lunar metadata xct only");
    }),
    $.getJSON("meteorite_sample_metadata.json", function (data) {
      meteoriteData = data;
    }).fail(function () {
      console.log("error pulling meteorite metadata");
    }),
    $.getJSON("meteorite_sample_metadata_xct_only.json", function (data) {
      meteoriteDataXctonly = data;
    }).fail(function () {
      console.log("error pulling meteorite metadata xct only");
    })
  ).then(function () {
    gAllMetadataDict = $.extend(lunarData, meteoriteData);
    gAllMetadataDictXctOnly = $.extend(lunarDataXctonly, meteoriteDataXctonly);
    populatePageWithMetadata();
  });
}

function populatePageWithMetadata() {
  var sample_num = gSampleJSONDict["sample_num"];

  const metadataDict = gXctOnly ? gAllMetadataDictXctOnly[sample_num] : gAllMetadataDict[sample_num];

  document.getElementById("turntableImage").src = "../_images/_samples/HRPP_" + sample_num + ".jpg";
  document.getElementById("turntableImageLink").href = "../_images/_samples/HRPP_" + sample_num + ".jpg";

  document.getElementById("loaderSampleNum").innerHTML = metadataDict["display_name"];
  document.getElementById("sampleNumSpan").innerHTML = metadataDict["display_name"];
  document.getElementById("sampleDetailsHref").href = "../sample-details.htm?sample=" + sample_num;
  document.getElementById("sampleDetailsHref2").href = "../sample-details.htm?sample=" + sample_num;
  if (gSampleJSONDict["sample_type"] === "lunar") {
    document.getElementById("sampleCollectionSpan").innerHTML = "Apollo Lunar Collection";
    document.getElementById("sampleCollectionHref").href = "../apollo-lunar.htm";
  } else {
    document.getElementById("sampleCollectionSpan").innerHTML = "Antarctic Meteorite Collection";
    document.getElementById("sampleCollectionHref").href = "../antarctic-meteorite.htm";
  }

  document.getElementById("processingSampleNum").innerHTML = metadataDict["display_name"];
  document.getElementById("sampleOriginSpan").innerHTML = metadataDict["origin"];
  document.getElementById("sampleCollectedSpan").innerHTML = metadataDict["collected"];
  document.getElementById("sampleTypeSpan").innerHTML = metadataDict["classification"];
  if (!gXctOnly) {
    document.getElementById("detailParagraph").innerHTML = metadataDict["story"].split("</p>")[0].substr(3) + "..";
    document.getElementById("xctOnlyStory").style.display = "none";
  } else {
    document.getElementById("storyDetails").style.display = "none";
    document.getElementById("xctOnlyStory").style.display = "block";
  }

  if (metadataDict["detailsField_camera"] !== "") {
    document.getElementById("detailsField_camera").innerHTML = metadataDict["detailsField_camera"];
    document.getElementById("detailsField_lens").innerHTML = metadataDict["detailsField_lens"];
    document.getElementById("detailsField_mPixels").innerHTML = metadataDict["detailsField_mPixels"];
    document.getElementById("detailsField_photoCount").innerHTML = metadataDict["detailsField_photoCount"];
    document.getElementById("detailsField_totalPhotosSize").innerHTML = metadataDict["detailsField_totalPhotosSize"];
  } else {
    document.getElementById("descriptionTableHRPP").style.display = "none";
  }
  if (metadataDict["detailsField_modelTime"] !== "") {
    document.getElementById("detailsField_modelTime").innerHTML = metadataDict["detailsField_modelTime"];
    document.getElementById("detailsField_faces").innerHTML = metadataDict["detailsField_faces"];
    document.getElementById("detailsField_vertices").innerHTML = metadataDict["detailsField_vertices"];
    document.getElementById("detailsField_textureRes").innerHTML = metadataDict["detailsField_textureRes"];
    document.getElementById("detailsField_meshSize").innerHTML = metadataDict["detailsField_meshSize"];
    document.getElementById("detailsField_webFaces").innerHTML = metadataDict["detailsField_webFaces"];
    document.getElementById("detailsField_webVertices").innerHTML = metadataDict["detailsField_webVertices"];
    document.getElementById("detailsField_webTextureRes").innerHTML = metadataDict["detailsField_webTextureRes"];
    document.getElementById("detailsField_webMeshSize").innerHTML = metadataDict["detailsField_webMeshSize"];
  } else {
    document.getElementById("descriptionTableSFM").style.display = "none";
  }
  if (metadataDict["detailsField_xrayPower"] !== "") {
    document.getElementById("detailsField_xrayPower").innerHTML = metadataDict["detailsField_xrayPower"];
    document.getElementById("detailsField_YZCount").innerHTML = metadataDict["detailsField_YZCount"];
    document.getElementById("detailsField_XZCount").innerHTML = metadataDict["detailsField_XZCount"];
    document.getElementById("detailsField_XYCount").innerHTML = metadataDict["detailsField_XYCount"];
    document.getElementById("detailsField_voxelSize").innerHTML = metadataDict["detailsField_voxelSize"];
    document.getElementById("detailsField_totalCTSize").innerHTML = metadataDict["detailsField_totalCTSize"];
  } else {
    document.getElementById("descriptionTableXCT").style.display = "none";
  }

  if (!gXctOnly) {
    if (gSampleJSONDict["mesh_filename_fullres"] === "") {
      document.getElementById("fullResolutionDownloadLink").style.display = "none";
    } else {
      var filename = gSampleJSONDict["mesh_foldername_fullres"].replace(/3b_/g, "") + ".zip";
      $("#sfm-fullres-url").prop("href", gSamplesLocation + sampleNum + "/" + gSampleJSONDict["mesh_foldername_fullres"] + "/" + filename);
      $("#sfm-webres-url").prop("href", gSamplesLocation + sampleNum + "/" + gSampleJSONDict["mesh_foldername"] + "/" + gSampleJSONDict["mesh_filename"].split(".")[0] + ".zip");
    }

    // $("#xct-url2").prop("href", gRootDataLocation + "original_16bit_tiffs/" + gSampleJSONDict["XY_16bit_filename"]);
    //get NASA pin data if any
    if (gAllMetadataDict[sampleNum].hasOwnProperty("nasaPinsState")) {
      var state = gAllMetadataDict[sampleNum]["nasaPinsState"];
      var lib = JsonUrl("lzma");
      lib.decompress(state).then(function (state) {
        console.log("NASA pins decoded.");
        //set pins
        for (var i = 0; i < state.pins.length; i++) {
          const pointsAndNormal = {
            point: null,
            point2: null,
          };
          pointsAndNormal.point = new THREE.Vector3(state.pins[i].point.x, state.pins[i].point.y, state.pins[i].point.z);
          if (state.pins[i].hasOwnProperty("point2") && state.pins[i].point2 !== null) {
            //old links won't have a point2 value
            pointsAndNormal.point2 = new THREE.Vector3(state.pins[i].point2.x, state.pins[i].point2.y, state.pins[i].point2.z);
          } else {
            pointsAndNormal.point2 = null;
          }
          var pin = makePinObject(pointsAndNormal, state.pins[i].name, state.pins[i].commentText, state.pins[i].groupRotation, state.pins[i].slicesSelected);
          nasaPins.push(pin);
        }
      });
    }
    $("#xct-url").prop("href", gRootDataLocation + "original_16bit_tiffs/" + gSampleJSONDict["XY_16bit_filename"]);
  } else {
    $("#xct-url").prop("href", gRootDataLocation + "original_16bit_tiffs_XCT_Only/" + gSampleJSONDict["XY_16bit_filename"]);
  }
  $("#curation-url").prop("href", metadataDict["curation_url"]);
}

function ajaxGetSampleJSON(sampleNum) {
  return $.getJSON(gSamplesLocation + sampleNum + "/" + sampleNum + "_A3D_EXPLORER_metadata.json", function (data) {
    gSampleJSONDict = data;
    if (typeof gSampleJSONDict["CT_data_exists"] === "undefined") {
      gSampleJSONDict["CT_data_exists"] = true;
    }
    if (typeof gSampleJSONDict["sample_type"] === "undefined") {
      gSampleJSONDict["sample_type"] = "lunar";
    }
  })
    .done(function () {
      // console.log( "second success" );
    })
    .fail(function (error) {
      console.log("error pulling sample metadata");
    })
    .always(function () {
      // console.log( "complete" );
    });
}

function setInterfaceStyleBySampleType(sampleType) {
  if (sampleType === "meteorite") {
    // var bodyTagSelector = $('body');
    // bodyTagSelector.removeClass('bodyLunar');
    // bodyTagSelector.addClass('bodyMeteorite');

    var rockContainerSelector = $("#rockContainer");
    rockContainerSelector.removeClass("backgroundLunar");
    rockContainerSelector.addClass("backgroundMeteorite");

    // $('#rightNavContents').addClass('meteorite');
  }
}

function setDoneLoaderField(fieldName) {
  $("#" + fieldName).text("Done.");
  loadingCounter++;
}

function toggleAmbientLight() {
  // ga('send', 'event', 'A3D', 'viewButtons', 'toggleAmbientLight');
  gtag("event", "astromaterials3dexplorer", {
    event_category: "engagement",
    event_label: "toggleAmbientLight",
  });
  ambientLight.visible = ambientLight.visible === false;
  if (ambientLight.visible) $("#viewAmbient").addClass("selected");
  else $("#viewAmbient").removeClass("selected");
}

function toggleSpotLight() {
  // ga('send', 'event', 'A3D', 'viewButtons', 'toggleSpotLight');
  gtag("event", "astromaterials3dexplorer", {
    event_category: "engagement",
    event_label: "toggleSpotLight",
  });
  directionalLight.visible = directionalLight.visible === false;
  if (directionalLight.visible) $("#viewSpotlight").addClass("selected");
  else $("#viewSpotlight").removeClass("selected");
}

function toggleAnaglyph() {
  // ga('send', 'event', 'A3D', 'viewButtons', 'toggleAnaglyph');
  gtag("event", "astromaterials3dexplorer", {
    event_category: "engagement",
    event_label: "toggleAnaglyph",
  });
  enableAnaglyph = enableAnaglyph === false;
  if (enableAnaglyph) {
    $("#viewAnaglyph").addClass("selected");
    //set zoom of camera to default
    camera.position.set(0, 0, gSampleJSONDict["camera_distance"]);
  } else {
    $("#viewAnaglyph").removeClass("selected");
  }
}

function toggleScale() {
  // ga('send', 'event', 'A3D', 'viewButtons', 'toggleScale');
  gtag("event", "astromaterials3dexplorer", {
    event_category: "engagement",
    event_label: "toggleScale",
  });
  scaleBox.visible = scaleBox.visible === false;
  if (scaleBox.visible) $("#viewScaleGuide").addClass("selected");
  else $("#viewScaleGuide").removeClass("selected");
}

function toggleHelperGrid() {
  // ga('send', 'event', 'A3D', 'viewButtons', 'toggleHelperGrid');
  gtag("event", "astromaterials3dexplorer", {
    event_category: "engagement",
    event_label: "toggleHelperGrid",
  });
  helperGrid.visible = helperGrid.visible === false;
  if (helperGrid.visible) {
    $("#viewHelperGrid").addClass("selected");
    // $('#rockContainer').removeClass('background');
    $("#rockContainer").addClass("hideBackground");
  } else {
    $("#viewHelperGrid").removeClass("selected");
    // $('#rockContainer').addClass('background');
    $("#rockContainer").removeClass("hideBackground");
  }
}

function toggleOrientationCube() {
  // ga('send', 'event', 'A3D', 'viewButtons', 'toggleOrientationCube');
  gtag("event", "astromaterials3dexplorer", {
    event_category: "engagement",
    event_label: "toggleOrientationCube",
  });
  orientationCube.visible = orientationCube.visible === false;
  if (orientationCube.visible) {
    $("#viewOrientationCube").addClass("selected");
  } else {
    $("#viewOrientationCube").removeClass("selected");
  }
}

function toggleAutoRotate() {
  autoR = autoR === false;
}

function toggleSlicerHelpers(ori) {
  // ga('send', 'event', 'A3D', 'helperstoggle', ori);
  gtag("event", "astromaterials3dexplorer", {
    event_category: "engagement",
    event_label: "planeViewToggle " + ori,
  });
  var iconSelector = $("#" + ori + "HelpersIcon");
  var turnOn = !iconSelector.hasClass("selected");
  if (turnOn) {
    iconSelector.addClass("selected");
  } else {
    iconSelector.removeClass("selected");
  }

  sceneGroup.traverse(function (child) {
    if (child.name.includes("slicer" + ori)) {
      child.traverse(function (child2) {
        if (child2.name === "helperWireframe") {
          child2.visible = turnOn;
        }
      });
    }
  });
}

function showSliceHelper(slicerName) {
  sceneGroup.traverse(function (child) {
    if (child.name.includes("slicer" + slicerName)) {
      child.traverse(function (child2) {
        if (child2.name === "helperWireframe") {
          child2.visible = true;
        }
      });
    }
  });
}

function hideSliceHelper(slicerName) {
  //if view CT planes is not selected for this orientation
  if (!$("#" + slicerName.substring(0, 2) + "HelpersIcon").hasClass("selected")) {
    sceneGroup.traverse(function (child) {
      if (child.name.includes("slicer" + slicerName)) {
        child.traverse(function (child2) {
          if (child2.name === "helperWireframe") {
            child2.visible = false;
          }
        });
      }
    });
  }
}

function rotateFaceOn(orientation) {
  var rotationRelativeToXY = {};
  if (orientation === "XY0") {
    rotationRelativeToXY = { x: 0, y: 0, z: 0 };
  } else if (orientation === "XY1") {
    rotationRelativeToXY = { x: 0, y: 180, z: 0 };
  } else if (orientation === "XZ0") {
    rotationRelativeToXY = { x: 90, y: 0, z: 0 };
  } else if (orientation === "XZ1") {
    rotationRelativeToXY = { x: -90, y: 0, z: 0 };
  } else if (orientation === "YZ0") {
    rotationRelativeToXY = { x: 0, y: -90, z: 0 };
  } else {
    //YZ !front
    rotationRelativeToXY = { x: 0, y: 90, z: 0 };
  }
  sceneGroup.rotation.x = THREE.Math.degToRad(rotationRelativeToXY.x);
  sceneGroup.rotation.y = THREE.Math.degToRad(rotationRelativeToXY.y);
  sceneGroup.rotation.z = THREE.Math.degToRad(rotationRelativeToXY.z);
  sceneGroup.updateMatrixWorld(true);

  navCube.rotation.x = THREE.Math.degToRad(rotationRelativeToXY.x);
  navCube.rotation.y = THREE.Math.degToRad(rotationRelativeToXY.y);
  navCube.rotation.z = THREE.Math.degToRad(rotationRelativeToXY.z);
  navCube.updateMatrixWorld(true);
  updateClippingPlanes();
}

function downloadSlice(slicerName) {
  // ga('send', 'event', 'A3D', 'downloadslice', slicerName);
  gtag("event", "astromaterials3dexplorer", {
    event_category: "engagement",
    event_label: "downloadslice " + slicerName,
  });
  var ori = slicerName.substring(0, 2);
  var download = document.getElementById("downloadSlice");
  var imageNumber = document.getElementById(slicerName + "sliceNumText").innerHTML;

  var sliceImgFilename = gSampleJSONDict[ori]["slices_file_format"] + padZeros(imageNumber, 4) + ".png";
  var sliceImgPath = gSamplesLocation + sampleNum + "/" + gSampleJSONDict[ori]["slices_folder_name"]; //.replace('JPEG', 'PNG');

  download.setAttribute("href", sliceImgPath + "/" + sliceImgFilename);
  download.click();
}

function setInitialSceneRotation() {
  //set initial sample angle
  sceneGroup.rotation.x = THREE.Math.degToRad(gInitialSceneRotation["x"]);
  sceneGroup.rotation.y = THREE.Math.degToRad(gInitialSceneRotation["y"]);
  sceneGroup.rotation.z = THREE.Math.degToRad(gInitialSceneRotation["z"]);
  sceneGroup.updateMatrixWorld(true);

  //set initial navcube angle
  navCube.rotation.x = THREE.Math.degToRad(gInitialSceneRotation["x"]);
  navCube.rotation.y = THREE.Math.degToRad(gInitialSceneRotation["y"]);
  navCube.rotation.z = THREE.Math.degToRad(gInitialSceneRotation["z"]);
  navCube.updateMatrixWorld(true);
}

function resetView() {
  // ga('send', 'event', 'A3D', 'viewButtons', 'resetView');
  gtag("event", "astromaterials3dexplorer", {
    event_category: "engagement",
    event_label: "resetView",
  });
  rotateFaceOn("XY0", true);
  setInitialSceneRotation();

  //move sliders to starting positions
  for (var i = 0; i < ori.length; i++) {
    var data2 = {
      left: 0.001,
      right: 99.999,
    };
    range[ori[i]].move(data2);
    var data = range[ori[i]].getInfo();
  }

  sceneGroup.updateMatrixWorld();
  resetCameraControls();
  //nudge the sample to the left because the nav panel must be open if reset was clicked.
  panCameraControls("left", 0.02);

  initializeSliceNumberFields();
  initializeSlicerMeshPositionsAndRotation();

  updateClippingPlanes();

  for (i = 0; i < slicerNames.length; i++) {
    var slicerName = slicerNames[i];
    var thisOri = slicerName.substring(0, 2);

    //reset slicer textures to 0000
    // paperCanvasScope[ori].activate();
    sliceHighresRaster[slicerName].visible = false;
    sliceSpriteRaster[slicerName].position = new Point((gSampleJSONDict[thisOri]["spritesheet_width"] * gSampleJSONDict["sprite_scale_factor"]) / 2, (gSampleJSONDict[thisOri]["spritesheet_height"] * gSampleJSONDict["sprite_scale_factor"]) / 2);
    slicerTexture[slicerName].needsUpdate = true;
  }
}

function panCameraControls(direction, nudgeAmount) {
  if (typeof nudgeAmount === "undefined")
    //only fire event if arrow clicked. if this parameter is defined then it came from the rightnav toggle
    // ga('send', 'event', 'A3D', 'view', 'panViewport');
    gtag("event", "astromaterials3dexplorer", {
      event_category: "engagement",
      event_label: "panViewport ",
    });
  nudgeAmount = typeof nudgeAmount !== "undefined" ? nudgeAmount : 0.005;
  var dir = new THREE.Vector3();
  camera.getWorldDirection(dir);

  if (direction === "left") dir.applyAxisAngle(new THREE.Vector3(0, 1, 0), (Math.PI / 2) * -1);
  else if (direction === "right") dir.applyAxisAngle(new THREE.Vector3(0, 1, 0), Math.PI / 2);
  else if (direction === "up") dir.applyAxisAngle(new THREE.Vector3(1, 0, 0), Math.PI / 2);
  else if (direction === "down") dir.applyAxisAngle(new THREE.Vector3(1, 0, 0), (Math.PI / 2) * -1);

  // camera.position.add(dir.multiplyScalar(nudgeAmount));

  controls.target.add(dir.multiplyScalar(nudgeAmount));
  controls.update();
}

function toggleFullScreen() {
  var element = document.body;

  var isFullscreen = document.webkitIsFullScreen || document.mozFullScreen || false;

  element.requestFullScreen =
    element.requestFullScreen ||
    element.webkitRequestFullScreen ||
    element.mozRequestFullScreen ||
    function () {
      return false;
    };
  document.cancelFullScreen =
    document.cancelFullScreen ||
    document.webkitCancelFullScreen ||
    document.mozCancelFullScreen ||
    function () {
      return false;
    };

  isFullscreen ? document.cancelFullScreen() : element.requestFullScreen();
}

function saveState() {
  var sliceNumbers = {
    XY0: document.getElementById("XY0sliceNumText").innerHTML,
    XY1: document.getElementById("XY1sliceNumText").innerHTML,
    XZ0: document.getElementById("XZ0sliceNumText").innerHTML,
    XZ1: document.getElementById("XZ1sliceNumText").innerHTML,
    YZ0: document.getElementById("YZ0sliceNumText").innerHTML,
    YZ1: document.getElementById("YZ1sliceNumText").innerHTML,
  };
  var groupRotation = {
    x: sceneGroup.rotation.x,
    y: sceneGroup.rotation.y,
    z: sceneGroup.rotation.z,
  };

  var pinList = [];
  for (var i = 0; i < customPins.length; i++) {
    var pin = {
      name: customPins[i].name,
      commentText: document.getElementById("pinText" + customPins[i].name).innerHTML,
      point: customPins[i].point,
      point2: customPins[i].point2,
      groupRotation: customPins[i].groupRotation,
      slicesSelected: customPins[i].slicesSelected,
    };
    pinList.push(pin);
  }

  var state = {
    sampleNum: sampleNum,
    sliceNumbers: sliceNumbers,
    groupRotation: groupRotation,
    pins: pinList,
    scaleVisible: scaleBox.visible,
    helperGridVisible: helperGrid.visible,
  };

  var lib = JsonUrl("lzma");
  lib.compress(state).then(function (output) {
    var stateURL = window.location.protocol + "//" + window.location.host + window.location.pathname + "?sample=" + sampleNum + "&state=" + output;
    // console.log(stateURL);
    // window.open(stateURL, '_blank');

    $("#shareURL").val(stateURL);
    var copyText = document.getElementById("shareURL");
    copyText.select();
    copyText.setSelectionRange(0, 99999); /*For mobile devices*/
  });
}

function loadState(state) {
  var lib = JsonUrl("lzma");
  lib.decompress(state).then(function (state) {
    console.log("State loaded.");

    //rotate group
    sceneGroup.rotation.x = state.groupRotation.x;
    sceneGroup.rotation.y = state.groupRotation.y;
    sceneGroup.rotation.z = state.groupRotation.z;
    sceneGroup.updateMatrixWorld(true);
    updateClippingPlanes();

    navCube.rotation.x = state.groupRotation.x;
    navCube.rotation.y = state.groupRotation.y;
    navCube.rotation.z = state.groupRotation.z;

    // if this rock has XCT data, then position the slices
    if ("XY" in state) {
      //set slice numbers
      document.getElementById("XY0sliceNumText").innerHTML = state.sliceNumbers.XY0;
      document.getElementById("XY1sliceNumText").innerHTML = state.sliceNumbers.XY1;
      document.getElementById("XZ0sliceNumText").innerHTML = state.sliceNumbers.XZ0;
      document.getElementById("XZ1sliceNumText").innerHTML = state.sliceNumbers.XZ1;
      document.getElementById("YZ0sliceNumText").innerHTML = state.sliceNumbers.YZ0;
      document.getElementById("YZ1sliceNumText").innerHTML = state.sliceNumbers.YZ1;

      //position slices
      sliceAtImageNum("XY0", state.sliceNumbers.XY0);
      sliceAtImageNum("XY1", state.sliceNumbers.XY1);
      sliceAtImageNum("XZ0", state.sliceNumbers.XZ0);
      sliceAtImageNum("XZ1", state.sliceNumbers.XZ1);
      sliceAtImageNum("YZ0", state.sliceNumbers.YZ0);
      sliceAtImageNum("YZ1", state.sliceNumbers.YZ1);
    }

    //set pins
    for (var i = 0; i < state.pins.length; i++) {
      const pointsAndNormal = {
        point: null,
        point2: null,
      };
      pointsAndNormal.point = new THREE.Vector3(state.pins[i].point.x, state.pins[i].point.y, state.pins[i].point.z);
      if (state.pins[i].hasOwnProperty("point2") && state.pins[i].point2 !== null) {
        //old links won't have a point2 value
        pointsAndNormal.point2 = new THREE.Vector3(state.pins[i].point2.x, state.pins[i].point2.y, state.pins[i].point2.z);
      }
      var pin = makePinObject(pointsAndNormal, state.pins[i].name, state.pins[i].commentText, state.pins[i].groupRotation, state.pins[i].slicesSelected);
      customPins.push(pin);
      togglePinTab("customPins");
    }

    //set other items
    scaleBox.visible = state.scaleVisible;
    helperGrid.visible = state.helperGridVisible;

    drawPincount();
  });
}

function calculateSlicerDistances() {
  // boundsMesh3DWidth
  var measurePerSlice = {};
  measurePerSlice["XY"] = boundsMesh3DDepth / parseInt(gSampleJSONDict["XY"]["num_of_slices"]);
  measurePerSlice["XZ"] = boundsMesh3DHeight / parseInt(gSampleJSONDict["XZ"]["num_of_slices"]);
  measurePerSlice["YZ"] = boundsMesh3DWidth / parseInt(gSampleJSONDict["YZ"]["num_of_slices"]);

  for (var i in ori) {
    var sliceCountDiff = parseInt(document.getElementById(ori[i] + "0sliceNumText").innerHTML) - parseInt(document.getElementById(ori[i] + "1sliceNumText").innerHTML);
    if (sliceCountDiff < 0) sliceCountDiff *= -1;
    document.getElementById(ori[i] + "Distance").textContent = (measurePerSlice[ori[i]] * sliceCountDiff * 100).toFixed(3) + " cm";
  }
  // $('#YZ0sliceNumText').attr('value', padZeros(0, 4));
  // $('#XYDistance').attr('value', );
}

function makeTextSprite(number, parameters) {
  number = " " + number + " ";
  if (parameters === undefined) parameters = {};
  var fontface = parameters.hasOwnProperty("fontface") ? parameters["fontface"] : "Arial";
  var fontsize = parameters.hasOwnProperty("fontsize") ? parameters["fontsize"] : 18;
  var borderThickness = parameters.hasOwnProperty("borderThickness") ? parameters["borderThickness"] : 4;
  var borderColor = parameters.hasOwnProperty("borderColor") ? parameters["borderColor"] : { r: 0, g: 0, b: 0, a: 1.0 };
  var backgroundColor = parameters.hasOwnProperty("backgroundColor") ? parameters["backgroundColor"] : { r: 255, g: 255, b: 255, a: 1.0 };
  var textColor = parameters.hasOwnProperty("textColor") ? parameters["textColor"] : { r: 0, g: 0, b: 0, a: 1.0 };

  var textCanvas = document.createElement("canvas");
  // var textCanvas = document.getElementById('textCanvas');
  var context = textCanvas.getContext("2d", { willReadFrequently: true });
  context.font = "Bold " + fontsize + "px " + fontface;
  var metrics = context.measureText(number);
  var textWidth = metrics.width;

  context.fillStyle = "rgba(" + backgroundColor.r + "," + backgroundColor.g + "," + backgroundColor.b + "," + backgroundColor.a + ")";
  context.strokeStyle = "rgba(" + borderColor.r + "," + borderColor.g + "," + borderColor.b + "," + borderColor.a + ")";

  context.lineWidth = borderThickness;
  roundRect(context, borderThickness / 2, borderThickness / 2, (textWidth + borderThickness) * 1.15, fontsize * 1.4 + borderThickness, 8);

  context.fillStyle = "rgba(" + textColor.r + ", " + textColor.g + ", " + textColor.b + ", 1.0)";
  context.fillText(number, borderThickness * 3, fontsize + borderThickness);

  cropCanvasToContents(context);
  var texture = new THREE.CanvasTexture(textCanvas);

  var spriteMaterial = new THREE.SpriteMaterial({ map: texture, useScreenCoordinates: false });
  var sprite = new THREE.Sprite(spriteMaterial);
  var spriteScaleDenominator = 40000;
  sprite.name = "labelSprite";

  // sprite.scale.set((0.5 * fontsize) / spriteScaleDenominator, (0.25 * fontsize) / spriteScaleDenominator, (0.75 * fontsize) / spriteScaleDenominator);
  sprite.scale.set(textCanvas.width / spriteScaleDenominator, textCanvas.height / spriteScaleDenominator);
  // sprite.scale.set(((textWidth + borderThickness) * 1.1) / spriteScaleDenominator, (fontsize * 1.4 + borderThickness) / spriteScaleDenominator);
  return sprite;
}

// function for drawing rounded rectangles
function roundRect(ctx, x, y, w, h, r) {
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.lineTo(x + w - r, y);
  ctx.quadraticCurveTo(x + w, y, x + w, y + r);
  ctx.lineTo(x + w, y + h - r);
  ctx.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
  ctx.lineTo(x + r, y + h);
  ctx.quadraticCurveTo(x, y + h, x, y + h - r);
  ctx.lineTo(x, y + r);
  ctx.quadraticCurveTo(x, y, x + r, y);
  ctx.closePath();
  ctx.fill();
  ctx.stroke();
}

function cropCanvasToContents(ctx) {
  var canvas = ctx.canvas,
    w = canvas.width,
    h = canvas.height,
    pix = { x: [], y: [] },
    imageData = ctx.getImageData(0, 0, canvas.width, canvas.height),
    x,
    y,
    index;

  for (y = 0; y < h; y++) {
    for (x = 0; x < w; x++) {
      index = (y * w + x) * 4;
      if (imageData.data[index + 3] > 0) {
        pix.x.push(x);
        pix.y.push(y);
      }
    }
  }
  pix.x.sort(function (a, b) {
    return a - b;
  });
  pix.y.sort(function (a, b) {
    return a - b;
  });
  var n = pix.x.length - 1;

  w = 1 + pix.x[n] - pix.x[0];
  h = 1 + pix.y[n] - pix.y[0];
  var cut = ctx.getImageData(pix.x[0], pix.y[0], w, h);

  canvas.width = w;
  canvas.height = h;
  ctx.putImageData(cut, 0, 0);
}

function titleCase(str) {
  str = str.toLowerCase().split(" ");
  for (var i = 0; i < str.length; i++) {
    str[i] = str[i].charAt(0).toUpperCase() + str[i].slice(1);
  }
  return str.join(" ");
}

function createSliders() {
  range["XY"] = new Slider(document.getElementById("XYRange"), {
    isDate: false,
    min: 0,
    max: 100,
    start: 0,
    end: 100,
    overlap: true,
  });
  range["XY"].subscribe("start", function (data) {
    sliceHighresRaster["XY0"].visible = false;
    sliceHighresRaster["XY1"].visible = false;
    if (range["XY"].dragObj.dir === "left") {
      $("#XY0sliceNumText").addClass("selected");
      // ga('send', 'event', 'A3D', 'slice', 'XY0');
      gtag("event", "astromaterials3dexplorer", {
        event_category: "engagement",
        event_label: "slice XY0",
      });
      showSliceHelper("XY0");
    } else {
      $("#XY1sliceNumText").addClass("selected");
      // ga('send', 'event', 'A3D', 'slice', 'XY1');
      gtag("event", "astromaterials3dexplorer", {
        event_category: "engagement",
        event_label: "slice XY1",
      });
      showSliceHelper("XY1");
    }
  });
  range["XY"].subscribe("moving", function (data) {
    setRangeDistanceCalloutPositon("XYRange", data);
    updateSlicerPosition("XY0", data.left);
    updateSlicerPosition("XY1", data.right);
  });
  range["XY"].subscribe("stop", function (data) {
    updateSlicerPosition("XY0", data.left);
    updateSlicerPosition("XY1", data.right);
    loadHighresSliceImage("XY0", data.left);
    loadHighresSliceImage("XY1", data.right);
    $("#XY0sliceNumText").removeClass("selected");
    $("#XY1sliceNumText").removeClass("selected");
    hideSliceHelper("XY0");
    hideSliceHelper("XY1");
  });

  range["XZ"] = new Slider(document.getElementById("XZRange"), {
    isDate: false,
    min: 0,
    max: 100,
    start: 0,
    end: 100,
    overlap: true,
  });
  range["XZ"].subscribe("start", function (data) {
    sliceHighresRaster["XZ0"].visible = false;
    sliceHighresRaster["XZ1"].visible = false;
    if (range["XZ"].dragObj.dir === "left") {
      $("#XZ0sliceNumText").addClass("selected");
      // ga('send', 'event', 'A3D', 'slice', 'XZ0');
      gtag("event", "astromaterials3dexplorer", {
        event_category: "engagement",
        event_label: "slice XZ0",
      });
      showSliceHelper("XZ0");
    } else {
      $("#XZ1sliceNumText").addClass("selected");
      // ga('send', 'event', 'A3D', 'slice', 'XZ1');
      gtag("event", "astromaterials3dexplorer", {
        event_category: "engagement",
        event_label: "slice XZ1",
      });
      showSliceHelper("XZ1");
    }
  });
  range["XZ"].subscribe("moving", function (data) {
    setRangeDistanceCalloutPositon("XZRange", data);
    updateSlicerPosition("XZ0", data.left);
    updateSlicerPosition("XZ1", data.right);
  });
  range["XZ"].subscribe("stop", function (data) {
    updateSlicerPosition("XZ0", data.left);
    updateSlicerPosition("XZ1", data.right);
    loadHighresSliceImage("XZ0", data.left);
    loadHighresSliceImage("XZ1", data.right);
    $("#XZ0sliceNumText").removeClass("selected");
    $("#XZ1sliceNumText").removeClass("selected");
    hideSliceHelper("XZ0");
    hideSliceHelper("XZ1");
  });

  range["YZ"] = new Slider(document.getElementById("YZRange"), {
    isDate: false,
    min: 0,
    max: 100,
    start: 0,
    end: 100,
    overlap: true,
  });
  range["YZ"].subscribe("start", function (data) {
    sliceHighresRaster["YZ0"].visible = false;
    sliceHighresRaster["YZ1"].visible = false;
    if (range["YZ"].dragObj.dir === "left") {
      $("#YZ0sliceNumText").addClass("selected");
      // ga('send', 'event', 'A3D', 'slice', 'YZ0');
      gtag("event", "astromaterials3dexplorer", {
        event_category: "engagement",
        event_label: "slice YZ0",
      });
      showSliceHelper("YZ0");
    } else {
      $("#YZ1sliceNumText").addClass("selected");
      // ga('send', 'event', 'A3D', 'slice', 'YZ1');
      gtag("event", "astromaterials3dexplorer", {
        event_category: "engagement",
        event_label: "slice YZ1",
      });
      showSliceHelper("YZ1");
    }
  });
  range["YZ"].subscribe("moving", function (data) {
    setRangeDistanceCalloutPositon("YZRange", data);
    updateSlicerPosition("YZ0", data.left);
    updateSlicerPosition("YZ1", data.right);
  });
  range["YZ"].subscribe("stop", function (data) {
    updateSlicerPosition("YZ0", data.left);
    updateSlicerPosition("YZ1", data.right);
    loadHighresSliceImage("YZ0", data.left);
    loadHighresSliceImage("YZ1", data.right);
    $("#YZ0sliceNumText").removeClass("selected");
    $("#YZ1sliceNumText").removeClass("selected");
    hideSliceHelper("YZ0");
    hideSliceHelper("YZ1");
  });
  // note that YZ is inverted from the rest of the planes due to a coordinate system sign difference between Avizo and OpenGL.
  // right and left are swapped and the slider is inverted with a 100 - on each slider value. This keeps the slice numbers correct.
  // range['YZ'].subscribe('moving', function(data) {
  //     setRangeDistanceCalloutPositon('YZRange', data);
  //     updateSlicerPosition('YZ0', 100 - data.right);
  //     updateSlicerPosition('YZ1', 100 - data.left);
  // });
  // range['YZ'].subscribe('stop', function(data) {
  //     updateSlicerPosition('YZ0', 100 - data.right);
  //     updateSlicerPosition('YZ1', 100 - data.left);
  //     loadHighresSliceImage('YZ0', 100 - data.right);
  //     loadHighresSliceImage('YZ1', 100 - data.left);
  //     $('#YZ0sliceNumText').removeClass('selected');
  //     $('#YZ1sliceNumText').removeClass('selected');
  //     hideSliceHelper('YZ0');
  //     hideSliceHelper('YZ1');
  // });
}

function setRangeDistanceCalloutPositon(rangeName, data) {
  var centerOffset = 40;
  var rangeSelector = $("#" + rangeName);
  var rangeWidth = rangeSelector.width();
  var pixelsPerPercentage = (1 * rangeWidth) / 100;
  var pixelsPosition = ((data.right - data.left) / 2) * pixelsPerPercentage + data.left * pixelsPerPercentage - centerOffset;
  document.getElementById(rangeName.substring(0, 2) + "DistanceCallout").style.left = pixelsPosition.toString() + "px";
}

function clearPinsModal() {
  $("#clearPinsModal").modal({
    escapeClose: false,
    clickClose: false,
    showClose: false,
    fadeDuration: 100,
  });
}

function showLoader() {
  // $("#loaderSampleNum").text(sampleNum);
  $("#loaderModal").modal({
    escapeClose: false,
    clickClose: false,
    showClose: false,
    fadeDuration: 250,
  });
}
function setLoaderProgress(percent) {
  var circle = document.getElementById("progress-ring__circle");
  var radius = circle.r.baseVal.value;
  var circumference = radius * 2 * Math.PI;

  circle.style.strokeDasharray = [circumference, circumference];
  circle.style.strokeDashoffset = circumference.toString();
  var offset = circumference - (percent / 100) * circumference;
  circle.style.strokeDashoffset = offset.toString();
}

function saveStateModal() {
  $("#saveStateModal").modal({
    escapeClose: false,
    clickClose: false,
    showClose: false,
    fadeDuration: 100,
  });
  saveState();
}
function copyShareURL() {
  /* Get the text field */
  var copyText = document.getElementById("shareURL");

  /* Select the text field */
  copyText.select();
  copyText.setSelectionRange(0, 99999); /*For mobile devices*/

  /* Copy the text inside the text field */
  document.execCommand("copy");

  /* Change buttons style and copy */
  var copyLinkButtonSelector = $("#copyLinkButton");
  copyLinkButtonSelector.removeClass("btn-primary");
  copyLinkButtonSelector.addClass("btn-secondary");
  copyLinkButtonSelector.prop("value", "Link Copied");

  var closeButtonSelector = $("#shareModalCloseButton");
  closeButtonSelector.removeClass("btn-secondary");
  closeButtonSelector.addClass("btn-primary");
}

