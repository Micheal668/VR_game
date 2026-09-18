using UnityEngine;

namespace LunarEscape
{
    // 本阶段只有可见的同乘角色和独立舱位；救援、跟随和 NPC 生死规则以后接入。
    public sealed class CrewCabinLayout : MonoBehaviour
    {
        [SerializeField] private Transform playerStation;
        [SerializeField] private Transform companionStation;
        [SerializeField] private GameObject companion;
        [SerializeField] private Vector3 cabinSize;
        public Transform PlayerStation=>playerStation;
        public Transform CompanionStation=>companionStation;
        public GameObject Companion=>companion;
        public Vector3 CabinSize=>cabinSize;
        public void Configure(Transform player,Transform npc,GameObject model,Vector3 dimensions)
        {playerStation=player;companionStation=npc;companion=model;cabinSize=dimensions;}
    }
}
