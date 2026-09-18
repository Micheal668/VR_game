using UnityEngine;

namespace LunarEscape
{
    public readonly struct OrbitalSample
    {
        public readonly float Altitude,Downrange,Pitch,SpeedKmS;
        public OrbitalSample(float altitude,float downrange,float pitch,float speed) {Altitude=altitude;Downrange=downrange;Pitch=pitch;SpeedKmS=speed;}
    }
    // 各段共享边界速度；前三秒只垂直上升，之后连续转弯，不在分段处刹停。
    // 圆轨道速度使用月球二体近似；这里不把压缩的播放时间当作真实发动机动力学。
    public static class OrbitalTrajectory
    {
        public const float MoonRadiusKm=1737.4f;
        public const float MoonGravityParameter=4900f;
        public static float Smooth(float t){t=Mathf.Clamp01(t);return t*t*t*(t*(t*6-15)+10);}
        public static float CircularSpeed(float altitudeKm)=>Mathf.Sqrt(MoonGravityParameter/(MoonRadiusKm+altitudeKm));
        // 高空展示缓慢侧倾，将弧形地平线带入侧窗；不旋转头显追踪或舱内参照物。
        public static float ViewBank(float altitude)=>50*Smooth((altitude-3000)/97000);
        public static OrbitalSample Sample(AscentMission mission)
            =>Sample(mission.AirborneSeconds,mission.LowFlightSeconds,mission.OrbitAscentSeconds,mission.Config.OrbitAltitudeKm,mission.OrbitBurnProgress);
        public static OrbitalSample Sample(float seconds,float lowSeconds,float totalSeconds,float targetKm,float burn)
        {
            seconds=Mathf.Max(0,seconds);
            lowSeconds=Mathf.Max(18,lowSeconds);totalSeconds=Mathf.Max(lowSeconds+10,totalSeconds);
            float vertical=Mathf.Clamp01(seconds/3);
            // 速度从 0 平滑增加到 2 m/s；第 3 秒仍在上升，而不是减速到零。
            float altitude=6*(Mathf.Pow(vertical,6)-3*Mathf.Pow(vertical,5)+2.5f*Mathf.Pow(vertical,4));
            float lowDuration=lowSeconds-3, highDuration=totalSeconds-lowSeconds;
            float lowT=Mathf.Clamp01((seconds-3)/lowDuration),highT=Mathf.Clamp01((seconds-lowSeconds)/highDuration);
            float climbAtJoin=1.1f*177/lowDuration;
            if(seconds>3)altitude=Hermite(3,180,2,climbAtJoin,lowDuration,lowT);
            float lowAltitude=Hermite(3,180,2,climbAtJoin,lowDuration,lowT);
            float lowHeight=Mathf.Clamp01((lowAltitude-3)/177);
            float downrange=24*lowHeight*lowHeight*lowHeight;
            float lowSlope=72f/177; // 水平位移对高度的导数；低空段从完全垂直开始。
            float pitch=Mathf.Atan(lowSlope*lowHeight*lowHeight)*Mathf.Rad2Deg;
            if(seconds>lowSeconds)
            {
                float height=targetKm*1000;
                float x1=24+lowSlope*(height-180),x2=115000;
                // Bézier 控制点接续低空段切线，最后两个控制点同高，
                // 让轨迹逐渐转平，垂直速度和转弯速度平滑归零。
                float u=Hermite(0,1,climbAtJoin/(3*(height-180)),4000/(3*(180000-x2)),highDuration,highT);
                float v=1-u;
                altitude=height-(height-180)*v*v*v;
                downrange=24*v*v*v+3*x1*v*v*u+3*x2*v*u*u+180000*u*u*u;
                float dx=3*(x1-24)*v*v+6*(x2-x1)*v*u+3*(180000-x2)*u*u;
                float dy=3*(height-180)*v*v;
                pitch=Mathf.Atan2(dx,dy)*Mathf.Rad2Deg;
            }
            float speed=CircularSpeed(targetKm)*Mathf.Lerp(.08f*Smooth(lowT),.94f,Smooth(highT));
            if(seconds>=totalSeconds)speed=CircularSpeed(targetKm)*Mathf.Lerp(.94f,1,Mathf.Clamp01(burn));
            return new OrbitalSample(altitude,downrange,pitch,speed);
        }
        // 五次 Hermite：给定两端位置、速度；两端加速度均为零。
        private static float Hermite(float start,float end,float speedStart,float speedEnd,float duration,float t)
        {
            if(t<=0)return start;if(t>=1)return end;
            double u=t,d=(double)end-start,m0=(double)speedStart*duration,m1=(double)speedEnd*duration;
            return (float)(start+m0*u+(10*d-6*m0-4*m1)*u*u*u+(-15*d+8*m0+7*m1)*u*u*u*u+(6*d-3*m0-3*m1)*u*u*u*u*u);
        }
    }
}
