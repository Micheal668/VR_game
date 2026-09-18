using System.Collections.Generic;
using UnityEngine;

namespace LunarEscape
{
    // 可重复生成的真实高度场：坑底、隆起坑缘和缓坡；基地、着陆区与撤离路保留平整通路。
    public static class LunarTerrainProfile
    {
        private readonly struct Crater
        {
            public readonly Vector2 Center;public readonly float Radius,Depth;
            public Crater(float x,float z,float radius,float depth){Center=new Vector2(x,z);Radius=radius;Depth=depth;}
        }
        private static readonly Crater[] Craters=CreateCraters();
        private static Crater[] CreateCraters()
        {
            var list=new List<Crater>{new Crater(25,16,8,3.2f),new Crater(44,-18,11,4),new Crater(68,15,12,4.5f),
                new Crater(10,-17,6,2.3f),new Crater(77,-24,8,2.8f),new Crater(-25,27,14,5),new Crater(18,42,19,6),new Crater(51,47,10,3)};
            var random=new System.Random(918);
            for(int i=0;i<145;i++)
            {
                float x=(float)(random.NextDouble()*1600-775),z=(float)(random.NextDouble()*1600-800);
                if(Mathf.Abs(x-25)<90 && Mathf.Abs(z)<65)continue;
                float radius=10+(float)random.NextDouble()*75;list.Add(new Crater(x,z,radius,radius*.17f));
            }
            return list.ToArray();
        }
        public static float Height(float x,float z)
        {
            float distance=new Vector2(x-25,z).magnitude;
            float hills=(Mathf.PerlinNoise(x*.003f+21,z*.003f+57)-.5f)*34*Mathf.SmoothStep(.06f,1,Mathf.Clamp01(distance/250));
            float height=hills+(Mathf.PerlinNoise(x*.038f+12,z*.038f+41)-.5f)*2.2f+(Mathf.PerlinNoise(x*.17f+73,z*.17f+9)-.5f)*.32f;
            foreach(var crater in Craters)
            {
                float q=Vector2.Distance(new Vector2(x,z),crater.Center)/crater.Radius;
                if(q>1.65f)continue;
                float bowl=q<1 ? -crater.Depth*Mathf.Pow(1-q*q,2) : 0;
                float rim=crater.Depth*.26f*Mathf.Exp(-Mathf.Pow((q-1)/.15f,2));height+=bowl+rim;
            }
            float baseDistance=RectangleDistance(new Vector2(x,z),new Vector2(-2.5f,0),new Vector2(13.5f,15));
            float padDistance=Mathf.Max(0,Vector2.Distance(new Vector2(x,z),new Vector2(48,1.7f))-7);
            float pathDistance=Mathf.Min(SegmentDistance(new Vector2(x,z),new Vector2(7,1.7f),new Vector2(38,1.7f)),
                Mathf.Min(SegmentDistance(new Vector2(x,z),new Vector2(38,1.7f),new Vector2(38,-4.3f)),SegmentDistance(new Vector2(x,z),new Vector2(38,-4.3f),new Vector2(49,-4.3f))));
            float freedom=Mathf.Min(Mathf.SmoothStep(0,1,baseDistance/4),Mathf.Min(Mathf.SmoothStep(0,1,padDistance/4),Mathf.SmoothStep(0,1,(pathDistance-1.8f)/3)));
            return height*freedom;
        }
        private static float RectangleDistance(Vector2 p,Vector2 center,Vector2 half)
        {var d=p-center;return new Vector2(Mathf.Max(0,Mathf.Abs(d.x)-half.x),Mathf.Max(0,Mathf.Abs(d.y)-half.y)).magnitude;}
        private static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {var ab=b-a;return Vector2.Distance(p,a+ab*Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude));}
    }
}
