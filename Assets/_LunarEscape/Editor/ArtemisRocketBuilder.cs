using System;
using UnityEditor;
using UnityEngine;
using static LunarEscape.Editor.LunarToolBuilder;

namespace LunarEscape.Editor
{
    public static class ArtemisRocketBuilder
    {
        private const string Model = "Assets/_LunarEscape/Art/Artemis/Block1.slsmesh";

        public static ArtemisRocketVisual Create(Transform parent)
        {
            var root = UnityEngine.Object.Instantiate(Source(), parent, false).transform;
            root.name = "Artemis SLS Block 1";
            var orion = root.Find("Orion");
            BuildCabin(orion);
            var hatch = Frame(orion, "Crew hatch threshold", new Vector3(2.35f, 0, 0),
                Quaternion.LookRotation(Vector3.left));
            var seat = Frame(orion, "Commander eye", new Vector3(0, 1.18f, .05f));
            var external = Frame(orion, "Exterior flight camera", new Vector3(8, 2.5f, -10),
                Quaternion.LookRotation(new Vector3(-.53f, -.14f, 1)));
            var rig = root.gameObject.AddComponent<ArtemisRocketVisual>();
            rig.Configure(root, hatch, seat, external);
            var hardware = BuildOrbitalHardware(orion, out var hinges);
            rig.ConfigureSolarArrays(hardware.gameObject, hinges);
            rig.HatchDoor.SetActive(false);
            return rig;
        }

        internal static Transform CreateOrbitingOrion(Transform parent)
        {
            var orion = UnityEngine.Object.Instantiate(Source().transform.Find("Orion").gameObject, parent, false).transform;
            orion.name = "Orion spacecraft";
            orion.localPosition = Vector3.zero;
            BuildCabin(orion);
            BuildOrbitalHardware(orion, out _);
            return orion;
        }

        private static Transform BuildOrbitalHardware(Transform orion, out Transform[] hinges)
        {
            var hardware = Frame(orion, "Orbital service hardware", Vector3.zero);
            Prim(orion, "Docking collar backplate", PrimitiveType.Cylinder, new Vector3(0,3.22f,0),
                new Vector3(1.20f,.04f,1.20f), "EX_Trim");
            Prim(orion, "Docking contact face", PrimitiveType.Cylinder, new Vector3(0,3.29f,0),
                new Vector3(.95f,.05f,.95f), "EX_White");
            // The launch cover is smooth, whereas the exposed service module has
            // separate radiator bays, thermal blankets, a dark aft deck and nozzle.
            foreach (float height in new[] { -.39f, -3.96f })
                Prim(hardware,"Service module band",PrimitiveType.Cylinder,new Vector3(0,height,0),
                    new Vector3(6.01f,.065f,6.01f),"EX_Trim");
            for (int bay = 0; bay < 12; bay++)
            {
                var face = Frame(hardware,"Service module bay",Vector3.zero,Quaternion.Euler(0,15+bay*30,0));
                Box(face,"Panel gasket",new Vector3(0,-2.17f,2.93f),new Vector3(1.47f,3.40f,.06f),"EX_Trim");
                if (bay % 3 == 0)
                {
                    Box(face,"Equipment blanket",new Vector3(0,-2.17f,2.99f),new Vector3(1.31f,3.12f,.07f),"EX_Shell");
                    Box(face,"Umbilical socket",new Vector3(.17f,-1.55f,3.045f),new Vector3(.59f,.48f,.05f),"EX_Floor");
                    Box(face,"Service grip",new Vector3(-.35f,-2.71f,3.05f),new Vector3(.085f,.56f,.075f),"EX_Copper");
                }
                else
                {
                    for (int tile = 0; tile < 2; tile++)
                        Box(face,"Radiator panel",new Vector3(0,-1.30f-tile*1.72f,2.99f),
                            new Vector3(1.34f,1.61f,.06f),"EX_White");
                    Box(face,"Panel spine",new Vector3(-.51f,-2.17f,3.03f),
                        new Vector3(.048f,3.12f,.035f),"EX_Shell");
                }
            }
            Prim(hardware,"Aft thrust deck",PrimitiveType.Cylinder,new Vector3(0,-4.56f,0),
                new Vector3(5.17f,.09f,5.17f),"EX_Trim");
            for (int ring=0;ring<4;ring++)
                Prim(hardware,"Aft nozzle jacket",PrimitiveType.Cylinder,new Vector3(0,-4.74f-ring*.14f,0),
                    new Vector3(.57f+ring*.15f,.083f,.57f+ring*.15f),"EX_Shell");
            hinges = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var mount = Frame(hardware, "Solar array mount", new Vector3(0,-2.5f,0), Quaternion.Euler(0,45+i*90,0));
                hinges[i] = Frame(mount,"Deployment hinge",new Vector3(0,0,2.97f));
                Box(hinges[i],"Support arm",new Vector3(0,0,.42f),new Vector3(.21f,.17f,.92f),"EX_Trim");
                var wing = Frame(hinges[i],"Solar array wing",new Vector3(0,0,.86f),Quaternion.Euler(0,0,32));
                for (int panel = 0; panel < 3; panel++)
                {
                    float z = .92f + panel * 1.86f;
                    Box(wing,"Solar panel frame",new Vector3(0,0,z),new Vector3(1.89f,.074f,1.84f),"EX_White");
                    for (int row = 0; row < 2; row++)
                        Box(wing,"Photovoltaic cells",new Vector3((row-.5f)*.89f,0,z),
                            new Vector3(.85f,.094f,1.74f),"EX_Solar");
                    for (int grid = -1; grid <= 1; grid++)
                        Box(wing,"Cell bus",new Vector3(0,0,z+grid*.52f),new Vector3(1.76f,.104f,.021f),"EX_Blue");
                }
            }
            return hardware;
        }

        private static GameObject Source()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(Model)
                ?? throw new InvalidOperationException("Artemis exterior mesh has not been imported: " + Model);
        }

        private static void BuildCabin(Transform orion)
        {
            var cabin = Frame(orion, "Orion crew compartment", Vector3.zero);
            Prim(cabin, "Nonslip deck", PrimitiveType.Cylinder, new Vector3(0,-.065f,0),
                new Vector3(4.9f,.06f,4.9f), "EX_Floor");
            Prim(cabin, "Docking tunnel ceiling", PrimitiveType.Cylinder, new Vector3(0,2.69f,0),
                new Vector3(2.15f,.05f,2.15f),"EX_Trim");
            Box(cabin,"Hatch step",new Vector3(2.4f,-.065f,0), new Vector3(.81f,.12f,.95f),"EX_Trim");
            var door = Frame(cabin,"Hatch door",new Vector3(1.95f,1.03f,0),Quaternion.Euler(0,0,22));
            Box(door,"Pressure door rim",Vector3.zero,new Vector3(.16f,2.28f,1.76f),"EX_Trim");
            Box(door,"Door thermal skin",new Vector3(.09f,0,0),new Vector3(.05f,2.18f,1.68f),"EX_White");
            Box(door,"Door lining",new Vector3(-.09f,0,0),new Vector3(.05f,2.08f,1.58f),"EX_Shell");
            Box(door,"Latch handle",new Vector3(-.15f,-.08f,.58f),new Vector3(.065f,.29f,.055f),"Safety Amber");
            for (int i = 0; i < 3; i++)
            {
                float x = (i-1)*.70f;
                var chair = Frame(cabin, "Crew couch", new Vector3(x,0,i == 1 ? .05f : -.2f));
                Box(chair,"Couch pan",new Vector3(0,.36f,-.08f),new Vector3(.58f,.17f,.65f),"EX_Fabric");
                Box(chair,"Seat back",new Vector3(0,.75f,-.44f),new Vector3(.54f,.81f,.16f),"EX_Fabric", Quaternion.Euler(-12,0,0));
                Box(chair,"Head cushion",new Vector3(0,1.16f,-.52f),new Vector3(.42f,.28f,.15f),"EX_Fabric");
            }
            // Leave the real opening between the sill and brow clear for both eyes.
            var console = Frame(cabin,"Flight instruments",new Vector3(0,.47f,1.08f),Quaternion.Euler(-28,0,0));
            Box(console,"Instrument panel",Vector3.zero,new Vector3(1.72f,.40f,.09f),"EX_Trim");
            for (int i = -1; i <= 1; i++)
            {
                float x = i*.51f;
                Box(console,"Display bezel",new Vector3(x,0,-.052f),new Vector3(.46f,.31f,.018f),"EX_Floor");
                Box(console,"Glass display",new Vector3(x,0,-.065f),new Vector3(.40f,.25f,.009f),"EX_Screen");
                for (int bar = 0; bar < 3; bar++)
                    Box(console,"Status trace",new Vector3(x-.052f+bar*.043f,.045f-bar*.04f,-.072f),
                        new Vector3(.21f-bar*.036f,.008f,.006f),bar == 1 ? "Safety Amber" : "Guide Teal");
            }
            foreach (float x in new[] { -.98f, .98f })
                Box(cabin,"Window mullion",new Vector3(x,1.18f,1.64f),new Vector3(.075f,1.66f,.11f),"EX_Trim",
                    Quaternion.Euler(-25,0,-x*9));
            Box(cabin,"Window brow",new Vector3(0,1.99f,1.34f),new Vector3(1.8f,.085f,.13f),"EX_Trim");
            foreach (float side in new[] {-1f,1f})
            {
                Box(cabin,"Cabin task light",new Vector3(side*.83f,1.69f,.04f),new Vector3(.065f,.045f,.66f),"EX_Light");
                Box(cabin,"Equipment stowage",new Vector3(side*1.44f,.43f,-.97f),new Vector3(.42f,.84f,.46f),"EX_White");
            }
            var light = Frame(cabin,"Cabin illumination",new Vector3(0,1.94f,-.14f)).gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(.85f,.93f,1);
            light.intensity = 1.7f;
            light.range = 3.9f;
            light.shadows = LightShadows.None;
        }

        internal static GameObject Box(Transform parent, string name, Vector3 p, Vector3 size, string material, Quaternion? rotation = null)
            => Prim(parent,name,PrimitiveType.Cube,p,size,material,rotation);
    }
}
