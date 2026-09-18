using System;
using UnityEngine;

namespace LunarEscape
{
    [CreateAssetMenu(fileName="Docking Config",menuName="Lunar Escape/Docking Config")]
    public sealed class DockingConfig : ScriptableObject
    {
        [SerializeField] private Vector3 startPosition=new(.6f,-.35f,-35);
        [SerializeField] private Vector3 startVelocity;
        [SerializeField] private Vector3 startEuler=new(-2,3,0);
        [SerializeField,Min(.01f)] private float translationAcceleration=.35f;
        [SerializeField,Min(.01f)] private float rotationAcceleration=4;
        [SerializeField,Min(.01f)] private float rcsConsumption=.55f;
        [SerializeField,Min(.01f)] private float initialRcs=100;
        [SerializeField,Min(10)] private float maximumDistance=140;
        [SerializeField,Min(1)] private float maximumLateralDistance=35;
        [SerializeField,Min(.1f)] private float assistDistance=8;
        [SerializeField,Min(.01f)] private float assistLateral=.8f;
        [SerializeField,Min(.1f)] private float assistAngle=8;
        [SerializeField,Min(.01f)] private float assistSpeed=.35f;
        [SerializeField,Min(.01f)] private float crashSpeed=.5f;
        public Vector3 StartPosition=>startPosition;
        public Vector3 StartVelocity=>startVelocity;
        public Quaternion StartAttitude=>Quaternion.Euler(startEuler);
        public float TranslationAcceleration=>translationAcceleration;
        public float RotationAcceleration=>rotationAcceleration;
        public float RcsConsumption=>rcsConsumption;
        public float InitialRcs=>initialRcs;
        public float MaximumDistance=>maximumDistance;
        public float MaximumLateralDistance=>maximumLateralDistance;
        public float AssistDistance=>assistDistance;
        public float AssistLateral=>assistLateral;
        public float AssistAngle=>assistAngle;
        public float AssistSpeed=>assistSpeed;
        public float CrashSpeed=>crashSpeed;
        public void ConfigureStart(Vector3 position,Vector3 velocity,Vector3 euler)
        {
            if(!Finite(position)||!Finite(velocity)||!Finite(euler)||position.z>=0)throw new ArgumentOutOfRangeException();
            startPosition=position;startVelocity=velocity;startEuler=euler;
        }
        private static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
    }
}
